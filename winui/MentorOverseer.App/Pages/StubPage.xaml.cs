using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace MentorOverseer.App.Pages;

public sealed partial class StubPage : Page
{
    public StubPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        StubTitle.Text = e.Parameter as string ?? "";
    }
}
