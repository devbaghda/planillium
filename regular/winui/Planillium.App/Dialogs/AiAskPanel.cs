using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Planillium.App.Services;

namespace Planillium.App.Dialogs;

/// <summary>
/// The "Ask Claude" strip shared by the dialogs that used to be copy-prompt / paste-reply only
/// (Replace remaining, Add plan; 2026-10-13 AI layer). Lives inside the host dialog — a second
/// ContentDialog can't open on top of one — and shows one of four things: a hint to add a key
/// (manual copy/paste below still works), the Ask button, the one-time "what gets sent" notice,
/// or progress with a Cancel. A failure shows its plain-language reason and leaves the manual path
/// untouched; nothing here changes any data — the reply goes through the host's normal validation.
/// </summary>
public sealed class AiAskPanel : StackPanel
{
    private readonly Func<string> _prompt;
    private readonly Func<string, Task> _onReply;
    private readonly Func<string?>? _blocker;
    private readonly Button _ask = new() { Content = "Ask Claude" };
    private readonly Button _cancel = new() { Content = "Cancel request" };
    private readonly ProgressRing _ring = new() { IsActive = true, Width = 20, Height = 20 };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private readonly TextBlock _hint = new()
    {
        TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = 0.8,
        Text = "Tip: add an Anthropic API key in Settings → AI and this dialog can ask Claude for you — " +
               "no copying and pasting. Until then, use the prompt and reply boxes below.",
    };
    private readonly Border _notice = new() { Padding = new Thickness(12), CornerRadius = new CornerRadius(6), Visibility = Visibility.Collapsed };
    private readonly StackPanel _runRow = new() { Orientation = Orientation.Horizontal, Spacing = 10, Visibility = Visibility.Collapsed };
    private CancellationTokenSource? _cts;

    /// <param name="prompt">The prompt as it should be sent right now (empty = nothing to send yet).</param>
    /// <param name="blocker">Returns a reason not to ask right now (e.g. the reply box already holds text that a new answer would overwrite), or null.</param>
    /// <param name="onReply">Called on the UI thread with Claude's reply text.</param>
    public AiAskPanel(Func<string> prompt, Func<string, Task> onReply, Func<string?>? blocker = null)
    {
        _blocker = blocker;
        _prompt = prompt;
        _onReply = onReply;
        Spacing = 8;
        _status.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
        _status.Visibility = Visibility.Collapsed;
        _notice.Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"];

        var accept = new Button { Content = "Accept and ask Claude", Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
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

        _runRow.Children.Add(_ring);
        _runRow.Children.Add(new TextBlock { Text = "Asking Claude… this can take a minute or two.", VerticalAlignment = VerticalAlignment.Center });
        _runRow.Children.Add(_cancel);

        _ask.Click += (_, _) =>
        {
            if (_prompt().Trim().Length == 0) { Fail("There is no prompt to send yet — generate it first."); return; }
            if (_blocker?.Invoke() is { } why) { Fail(why); return; }
            if (!AiSettings.NoticeAccepted) { _notice.Visibility = Visibility.Visible; _ask.Visibility = Visibility.Collapsed; return; }
            _ = RunAsync();
        };
        accept.Click += (_, _) => { AiSettings.AcceptNotice(); _notice.Visibility = Visibility.Collapsed; _ = RunAsync(); };
        decline.Click += (_, _) => { _notice.Visibility = Visibility.Collapsed; _ask.Visibility = Visibility.Visible; };
        _cancel.Click += (_, _) => _cts?.Cancel();

        Children.Add(_hint);
        Children.Add(_ask);
        Children.Add(_notice);
        Children.Add(_runRow);
        Children.Add(_status);
        Refresh();
    }

    /// <summary>Re-reads whether a key exists (cheap) — call when the dialog opens.</summary>
    public void Refresh()
    {
        var hasKey = AiSettings.HasKey;
        _hint.Visibility = hasKey ? Visibility.Collapsed : Visibility.Visible;
        _ask.Visibility = hasKey ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Stops a running request (e.g. the host dialog is closing).</summary>
    public void CancelRunning() => _cts?.Cancel();

    private void Fail(string message)
    {
        _status.Text = message + " You can still use the manual prompt and reply boxes below.";
        _status.Visibility = Visibility.Visible;
    }

    private async Task RunAsync()
    {
        _status.Visibility = Visibility.Collapsed;
        _ask.Visibility = Visibility.Collapsed;
        _runRow.Visibility = Visibility.Visible;
        _cts = new CancellationTokenSource();
        try
        {
            var client = AiSettings.CreateClient();
            var reply = await client.CompleteAsync(_prompt(), _cts.Token);
            await _onReply(reply);
        }
        catch (AiException ex) when (ex.Kind == AiFailure.Cancelled) { /* the user asked for it — say nothing */ }
        catch (AiException ex) { Fail(ex.Message); }
        catch (Exception ex)
        {
            Log.Error("AiAskPanel", ex);
            Fail("Something went wrong talking to the AI service.");
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _runRow.Visibility = Visibility.Collapsed;
            Refresh();
        }
    }
}
