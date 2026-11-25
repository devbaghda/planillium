using System.Diagnostics;
using Planillium.App.Services;

namespace Planillium.App.Dialogs;

/// <summary>
/// The "Ask Claude" strip shared by the dialogs that used to be copy-prompt / paste-reply only
/// (Replace remaining, Add plan; 2026-10-13 AI layer). Lives inside the host dialog — a second
/// ContentDialog can't open on top of one — and shows one of four things: a hint to add a key
/// (manual copy/paste below still works), the Ask button, the one-time "what gets sent" notice,
/// or progress with a Cancel. While a reply is pending the progress shows a live "Waiting m:ss" counter
/// and, once Claude starts writing, how many tasks have arrived so far (G4, 2026-10-27); the counter
/// freezes into a "Waited m:ss" line when the reply ends, fails or is cancelled. A failure shows its plain-language reason and leaves the manual path
/// untouched; nothing here changes any data — the reply goes through the host's normal validation.
/// </summary>
public sealed class AiAskPanel : StackPanel
{
    private readonly Func<string> _prompt;
    private readonly Func<string, Task> _onReply;
    private readonly Func<string?>? _blocker;
    private readonly Button _ask = new() { Content = "Ask Claude" };
    private readonly Button _cancel = new() { Content = "Cancel" };
    private readonly ProgressRing _ring = new() { IsActive = true, Width = 20, Height = 20 };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private readonly TextBlock _hint = new()
    {
        TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = 0.8,
        Text = "Tip: add an Anthropic API key in Settings → AI and this dialog can ask Claude for you — " +
               "no copying and pasting. Until then, use the prompt and reply boxes below.",
    };
    private readonly Border _notice = new() { Padding = new Thickness(12), CornerRadius = new CornerRadius(6), IsVisible = false };
    private readonly StackPanel _runRow = new() { Orientation = Orientation.Horizontal, Spacing = 10, IsVisible = false };
    private readonly TextBlock _progress = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _elapsed = new() { FontSize = 12, Opacity = 0.8 };
    private readonly TextBlock _waited = new() { FontSize = 12, Opacity = 0.8, TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly Stopwatch _clock = new();
    private CancellationTokenSource? _cts;
    private ReplyProgress? _replyProgress;
    private static readonly string AskingText = $"Asking Claude… this can take a few minutes; it gives up after {AiClient.WaitLimitText}.";

    /// <param name="prompt">The prompt as it should be sent right now (empty = nothing to send yet).</param>
    /// <param name="blocker">Returns a reason not to ask right now (e.g. the reply box already holds text that a new answer would overwrite), or null.</param>
    /// <param name="onReply">Called on the UI thread with Claude's reply text.</param>
    public AiAskPanel(Func<string> prompt, Func<string, Task> onReply, Func<string?>? blocker = null)
    {
        _blocker = blocker;
        _prompt = prompt;
        _onReply = onReply;
        Spacing = 8;
        _status.Foreground = Ui.Brush("SystemFillColorCriticalBrush");
        _status.IsVisible = false;
        _notice.Background = Ui.Brush("CardBackgroundFillColorSecondaryBrush");

        var accept = new Button { Content = "Accept and ask Claude", Classes = { "accent" } };
        var decline = new Button { Content = "No thanks" };
        var noticeText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, FontSize = 12,
            Text = "Asking Claude sends this prompt to Anthropic over the internet: the plan's name, its task " +
                   "text and what you typed above. Your diary, scores, earnings, tags and settings are never sent. " +
                   "You can remove the key any time in Settings → AI.",
        };
        var noticeButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        noticeButtons.Children.Add(accept);
        noticeButtons.Children.Add(decline);
        var noticeBody = new StackPanel { Spacing = 10 };
        noticeBody.Children.Add(noticeText);
        noticeBody.Children.Add(noticeButtons);
        _notice.Child = noticeBody;

        // Ids for UI Automation; the progress line is a polite live region (announced when it changes —
        // a new task count — not every second, which is why the counter itself is not one).
        AutomationProperties.SetAutomationId(_progress, "AiProgress");
        AutomationProperties.SetLiveSetting(_progress, AutomationLiveSetting.Polite);
        AutomationProperties.SetAutomationId(_elapsed, "AiElapsed");
        AutomationProperties.SetAutomationId(_waited, "AiWaited");
        var runText = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 300 };
        runText.Children.Add(_progress);
        runText.Children.Add(_elapsed);
        _runRow.Children.Add(_ring);
        _runRow.Children.Add(runText);
        _runRow.Children.Add(_cancel);
        _tick.Tick += (_, _) => ShowProgress();

        _ask.Click += (_, _) =>
        {
            if (_prompt().Trim().Length == 0) { Fail("There is no prompt to send yet — generate it first."); return; }
            if (_blocker?.Invoke() is { } why) { Fail(why); return; }
            if (!AiSettings.NoticeAccepted) { _notice.IsVisible = true; _ask.IsVisible = false; return; }
            _ = RunAsync();
        };
        accept.Click += (_, _) =>
        {
            AiSettings.AcceptNotice();
            _notice.IsVisible = false;
            if (_blocker?.Invoke() is { } why) { _ask.IsVisible = true; Fail(why); return; }
            _ = RunAsync();
        };
        decline.Click += (_, _) => { _notice.IsVisible = false; _ask.IsVisible = true; };
        _cancel.Click += (_, _) => { StopClock("Stopped after {0} — request cancelled."); _cts?.Cancel(); };

        Children.Add(_hint);
        Children.Add(_ask);
        Children.Add(_notice);
        Children.Add(_runRow);
        Children.Add(_waited);
        Children.Add(_status);
        Refresh();
    }

    /// <summary>Re-reads whether a key exists (cheap) — call when the dialog opens.</summary>
    public void Refresh()
    {
        var hasKey = AiSettings.HasKey;
        _hint.IsVisible = !(hasKey);
        _ask.IsVisible = hasKey;
    }

    /// <summary>Stops a running request (e.g. the host dialog is closing).</summary>
    public void CancelRunning()
    {
        if (_cts == null) return;
        StopClock("Stopped after {0} — request cancelled.");
        _cts.Cancel();
    }

    private static string Clock(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:00}";

    private void ShowProgress()
    {
        var tasks = _replyProgress?.Tasks ?? 0;
        var chars = _replyProgress?.Chars ?? 0;
        var line = tasks > 0 ? $"Claude is writing the reply: {tasks} task{(tasks == 1 ? "" : "s")} so far."
                 : chars > 0 ? "Claude has started writing the reply…"
                 : AskingText;
        if (_progress.Text != line)
        {
            _progress.Text = line;
        }
        var shown = "Waiting " + Clock(_clock.Elapsed);
        if (_elapsed.Text != shown) _elapsed.Text = shown;
    }

    /// <summary>Freezes the counter (first call wins: a Cancel click keeps its own moment and wording even
    /// though the request itself unwinds a moment later).</summary>
    private void StopClock(string outcome)
    {
        if (!_clock.IsRunning) return;
        _clock.Stop();
        _tick.Stop();
        _waited.Text = string.Format(outcome, Clock(_clock.Elapsed));
        _waited.IsVisible = true;
    }

    private void Fail(string message)
    {
        _status.Text = message + " You can still use the manual prompt and reply boxes below.";
        _status.IsVisible = true;
    }

    private async Task RunAsync()
    {
        _status.IsVisible = false;
        _waited.IsVisible = false;
        _ask.IsVisible = false;
        _replyProgress = new ReplyProgress();
        var progress = _replyProgress;
        _clock.Restart();
        ShowProgress();
        _tick.Start();
        _runRow.IsVisible = true;
        _cts = new CancellationTokenSource();
        try
        {
            var client = AiSettings.CreateClient();
            // G10: the exchange is compact (less for Claude to write); the reply is expanded back to the
            // full plan form below, so the host dialog sees exactly what it always did.
            var reply = await client.CompleteAsync(_prompt() + CompactReply.Instruction, _cts.Token, progress.Feed);
            // Cancel pressed just as the reply landed: the user's Cancel wins, nothing is applied.
            if (_cts.IsCancellationRequested) return;
            StopClock("Waited {0} — Claude answered.");
            // Text typed into the reply box while Claude was answering must not be overwritten.
            if (_blocker?.Invoke() is { } why) Fail(why + " Claude's answer was discarded.");
            else if (CompactReply.Expand(reply, out var full) is { } notCompact) Fail(notCompact);
            else await _onReply(full);
        }
        catch (AiException ex) when (ex.Kind == AiFailure.Cancelled) { /* the user asked for it — say nothing */ }
        catch (AiException ex) { StopClock("Waited {0} — no usable answer."); Fail(ex.Message); }
        catch (Exception ex)
        {
            StopClock("Waited {0} — no usable answer.");
            Log.Error("AiAskPanel", ex);
            Fail("Something went wrong talking to the AI service.");
        }
        finally
        {
            StopClock("Stopped after {0}.");
            _replyProgress = null;
            _cts.Dispose();
            _cts = null;
            _runRow.IsVisible = false;
            Refresh();
        }
    }
}
