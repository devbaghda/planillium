using Microsoft.UI.Xaml.Controls;

namespace MentorOverseer.App.Dialogs;

/// <summary>
/// WinUI throws if two ContentDialogs are open on the same XamlRoot, and this
/// app has three independent triggers (kickoff, idle-return, EOD review) plus
/// user-opened dialogs. Every ShowAsync in the app goes through this gate so
/// they queue instead of colliding.
/// </summary>
public static class DialogGate
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task<ContentDialogResult> ShowAsync(ContentDialog dialog)
    {
        await Gate.WaitAsync();
        try
        {
            return await dialog.ShowAsync();
        }
        finally
        {
            Gate.Release();
        }
    }
}
