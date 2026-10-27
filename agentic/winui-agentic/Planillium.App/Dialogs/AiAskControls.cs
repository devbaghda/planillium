using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Planillium.App.Services;

namespace Planillium.App.Dialogs;

/// <summary>
/// The "Ask Claude" strip shared by Replace remaining tasks and Add plan (ai-layer spec, D2).
/// It owns the button, the progress row (with Stop), the one-time notice and the reason line,
/// and runs notice -> call -> result once for both dialogs. The caller places the four elements
/// in its own layout and says what is sent (<c>buildPrompt</c>), which inputs lock while a call
/// runs, and what a successful reply does (<c>onSuccess</c>, after the reply box is filled).
/// </summary>
internal sealed class AiAskControls
{
    private const string FailurePrefix = "Couldn't get a reply — ";
    private const string CopyPromptSuffix = " You can still copy the prompt to claude.ai.";
    private const string NoKeyHint = "No API key — add one in Settings -> AI, or copy the prompt to claude.ai as before.";
    private const string NothingSent = "Nothing was sent.";
    private const string Stopped = "Stopped. Nothing was changed.";

    private readonly TextBox _reply;
    private readonly Func<string?> _buildPrompt;
    private readonly Action<bool> _setInputsLocked;
    private readonly Func<string, string?> _onSuccess;
    private readonly TextBlock _progressText;
    private ContentDialog? _dialog;
    private CancellationTokenSource? _cts;
    private TaskCompletionSource<bool>? _noticeAnswer;
    private bool _busy;
    private bool _closed;

    /// <param name="reply">The dialog's reply box: filled on success, locked while a call runs.</param>
    /// <param name="noticeBullet">The one "What is sent" bullet for the dialog asking.</param>
    /// <param name="buildPrompt">Returns the exact prompt the box shows, or null when its own
    /// validation refused (that validation message is the caller's to show).</param>
    /// <param name="setInputsLocked">Locks (true) or unlocks the inputs that form the prompt.</param>
    /// <param name="onSuccess">Runs after the reply box holds the reply text. Returns a neutral
    /// message for the reason line, or null for none.</param>
    public AiAskControls(TextBox reply, string noticeBullet, Func<string?> buildPrompt,
        Action<bool> setInputsLocked, Func<string, string?> onSuccess)
    {
        _reply = reply;
        _buildPrompt = buildPrompt;
        _setInputsLocked = setInputsLocked;
        _onSuccess = onSuccess;
        HasKey = AiKey.Has();

        Ask = new Button
        {
            Content = AskLabel(),
            HorizontalAlignment = HorizontalAlignment.Left,
            Visibility = HasKey ? Visibility.Visible : Visibility.Collapsed,
        };
        AutomationProperties.SetName(Ask, "Ask Claude");
        Ask.Click += async (_, _) => await AskAsync();
        _reply.TextChanged += (_, _) => Ask.Content = AskLabel();

        _progressText = new TextBlock { FontSize = 13, Foreground = ThemeBrush("TextFillColorPrimaryBrush") };
        var givesUp = new TextBlock
        {
            Text = "Gives up after 2 minutes.",
            FontSize = 12,
            Foreground = ThemeBrush("TextFillColorSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap,
        };
        var bar = new ProgressBar { IsIndeterminate = true };
        var stop = new Button { Content = "Stop", VerticalAlignment = VerticalAlignment.Bottom };
        AutomationProperties.SetName(stop, "Stop");
        stop.Click += (_, _) => _cts?.Cancel();

        var text = new StackPanel { Spacing = 4 };
        text.Children.Add(_progressText);
        text.Children.Add(givesUp);
        text.Children.Add(bar);
        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(text, 0);
        Grid.SetColumn(stop, 1);
        row.Children.Add(text);
        row.Children.Add(stop);
        Progress = new StackPanel { Visibility = Visibility.Collapsed };
        Progress.Children.Add(row);

        Notice = AiNoticeDialog.Build(noticeBullet, Accept, Decline);

        Reason = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Visibility = Visibility.Collapsed,
        };
        if (!HasKey) ShowHint();
    }

    /// <summary>The Ask Claude button ("Ask again" once the reply box is non-empty). Collapsed,
    /// not greyed, when no key is saved.</summary>
    public Button Ask { get; }

    /// <summary>Progress row: bar, "Asking Claude… N s", "Gives up after 2 minutes." and Stop.
    /// Collapsed unless a call runs.</summary>
    public StackPanel Progress { get; }

    /// <summary>The one-time notice, shown inline before the first call. Collapsed otherwise.</summary>
    public StackPanel Notice { get; }

    /// <summary>The reason line: failures (critical, prefixed), neutral messages, and the no-key hint.</summary>
    public TextBlock Reason { get; }

    /// <summary>Whether a key was saved when this strip was last checked.</summary>
    public bool HasKey { get; private set; }

    /// <summary>Hooks the dialog so that closing it during a call cancels the call silently.
    /// Call once, after the dialog is built and before it is shown.</summary>
    public void Attach(ContentDialog dialog)
    {
        _dialog = dialog;
        dialog.Closing += (_, _) =>
        {
            _cts?.Cancel();
            _noticeAnswer?.TrySetResult(false);
        };
        dialog.Closed += (_, _) => _closed = true;
    }

    /// <summary>Neutral message in the reason line (secondary text colour).</summary>
    public void ShowNeutral(string message) => SetReason(message, failure: false);

    /// <summary>Clears the reason line. Called when the inputs that form the prompt change, so a
    /// stale message never sits beside a new prompt. With no key, the hint comes back instead.</summary>
    public void ClearReason()
    {
        if (HasKey) Reason.Visibility = Visibility.Collapsed;
        else ShowHint();
    }

    private async Task AskAsync()
    {
        if (_busy || _closed) return;

        // Checked again on every click: a key removed while the dialog was open turns the
        // button into the hint, and no request is made.
        HasKey = AiKey.Has();
        if (!HasKey)
        {
            Ask.Visibility = Visibility.Collapsed;
            ShowHint();
            return;
        }

        // Never overwrite a reply the user already has (user decision 2026-10-13): nothing is sent.
        if (AiReplyBox.BlocksAsk(_reply.Text))
        {
            ShowNeutral(AiReplyBox.Blocked);
            return;
        }

        var prompt = _buildPrompt();
        if (prompt is null) return;

        _busy = true;
        ClearReason();
        Lock(true);

        if (!StateService.Load().AiNoticeAccepted)
        {
            _noticeAnswer = new TaskCompletionSource<bool>();
            Notice.Visibility = Visibility.Visible;
            var accepted = await _noticeAnswer.Task;
            _noticeAnswer = null;
            Notice.Visibility = Visibility.Collapsed;
            if (!accepted)
            {
                _busy = false;
                Lock(false);
                if (!_closed) ShowNeutral(NothingSent);
                return;
            }

            // Text may have arrived in the box while the notice was up: check again before sending.
            if (AiReplyBox.BlocksAsk(_reply.Text))
            {
                _busy = false;
                Lock(false);
                if (!_closed) ShowNeutral(AiReplyBox.Blocked);
                return;
            }
        }

        Progress.Visibility = Visibility.Visible;
        var cts = new CancellationTokenSource();
        _cts = cts;
        var started = DateTime.UtcNow;
        var call = AiKey.CreateClient(ConfigService.AiModel).AskAsync(prompt, cts.Token);
        while (!call.IsCompleted)
        {
            _progressText.Text = $"Asking Claude… {(int)(DateTime.UtcNow - started).TotalSeconds} s";
            await Task.WhenAny(call, Task.Delay(1000));
        }
        var result = await call;
        _cts = null;
        cts.Dispose();

        Progress.Visibility = Visibility.Collapsed;
        _busy = false;
        Lock(false);
        if (_closed) return;

        if (result.Failure == AiFailure.Cancelled)
        {
            ShowNeutral(Stopped);
        }
        else if (!result.Succeeded)
        {
            var suffix = result.Failure == AiFailure.NoKey ? "" : CopyPromptSuffix;
            SetReason(FailurePrefix + result.Message + suffix, failure: true);
        }
        else if (AiReplyBox.BlocksAsk(_reply.Text))
        {
            // Text was typed into the box while the call ran: the answer is dropped, the box is kept.
            ShowNeutral(AiReplyBox.DiscardedAfterAnswer);
        }
        else
        {
            _reply.Text = result.Text!;
            ClearReason();
            var note = _onSuccess(result.Text!);
            if (note is not null) ShowNeutral(note);
        }

        if (Ask.Visibility == Visibility.Visible) Ask.Focus(FocusState.Programmatic);
    }

    private void Accept()
    {
        var state = StateService.Load();
        state.AiNoticeAccepted = true;
        StateService.Save(state);
        _noticeAnswer?.TrySetResult(true);
    }

    private void Decline() => _noticeAnswer?.TrySetResult(false);

    /// <summary>Running (or waiting on the notice): reply read-only, Ask and the dialog's primary
    /// button disabled so a half-arrived reply cannot be submitted, and the caller's inputs locked.</summary>
    private void Lock(bool on)
    {
        _reply.IsReadOnly = on;
        Ask.IsEnabled = !on;
        if (_dialog is not null) _dialog.IsPrimaryButtonEnabled = !on;
        _setInputsLocked(on);
    }

    private void ShowHint() =>
        SetReason(AiSettings.ScratchBlocked ? AiSettings.ScratchBlockedMessage : NoKeyHint, failure: false);

    private void SetReason(string message, bool failure)
    {
        Reason.Text = message;
        AutomationProperties.SetName(Reason, message);
        Reason.Foreground = ThemeBrush(failure ? "SystemFillColorCriticalBrush" : "TextFillColorSecondaryBrush");
        Reason.Visibility = Visibility.Visible;
    }

    private string AskLabel() => _reply.Text.Length > 0 ? "Ask again" : "Ask Claude";

    private static Brush ThemeBrush(string key) => (Brush)Application.Current.Resources[key];
}
