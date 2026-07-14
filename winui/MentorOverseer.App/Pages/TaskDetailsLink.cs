using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MentorOverseer.App.Models;

namespace MentorOverseer.App.Pages;

/// <summary>
/// The "Details" hyperlink that opens <see cref="Dialogs.TaskDetailDialog"/>,
/// shared by Today and Schedule so the label/click-handler shape can't drift
/// out of sync between them. Padding and vertical alignment stay per call
/// site since Today lays this into a Grid column and Schedule into a
/// horizontal action StackPanel.
/// </summary>
internal static class TaskDetailsLink
{
    public static HyperlinkButton Build(XamlRoot xamlRoot, PlanTask task, Thickness padding,
        VerticalAlignment verticalAlignment)
    {
        var details = new HyperlinkButton
        {
            Content = "Details",
            FontSize = 12,
            Padding = padding,
            VerticalAlignment = verticalAlignment,
        };
        details.Click += async (_, _) => await Dialogs.TaskDetailDialog.ShowAsync(xamlRoot, task);
        return details;
    }
}
