using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Planillium.App.Services;

namespace Planillium.App.Dialogs;

/// <summary>
/// The two ways to spend the balance — ports of the Python app's sidebar
/// dialogs, writing the same ledger reasons so both apps read one economy:
///   • Buy entertainment time  → 'entertainment_purchase' + tracker paid window
///   • Log spend (no regrets)  → 'money_expenditure'
/// </summary>
public static class SpendDialog
{
    public static Task ShowBuyAsync(MainWindow window)
    {
        var (rate, _, _) = ConfigService.SpendRates();
        return ShowAsync(window,
            title: "Buy entertainment time",
            unitLabel: "Minutes",
            rateLabel: $"rate: {rate:0.##} pt/min",
            initialValue: 30,
            cost: amount => (int)Math.Round(amount * rate),
            confirmLabel: "Buy",
            onConfirm: (score, amount, cost) =>
            {
                score.AddLedger(-cost, ScoreReason.EntertainmentPurchase,
                    amount.ToString("0.#", CultureInfo.InvariantCulture) + " min");
                if (window.Tracker is { } tracker)
                    tracker.PaidUntil = DateTime.Now.AddMinutes(amount);
            });
    }

    public static Task ShowLogSpendAsync(MainWindow window)
    {
        var (_, rate, symbol) = ConfigService.SpendRates();
        return ShowAsync(window,
            title: "Log spend (no regrets)",
            unitLabel: $"Amount ({symbol})",
            rateLabel: $"rate: {rate:0.##} pt/{symbol}1",
            initialValue: double.NaN,   // NumberBox: empty until typed
            cost: amount => (int)Math.Round(amount * rate),
            confirmLabel: "Log spend",
            onConfirm: (score, amount, cost) =>
                score.AddLedger(-cost, ScoreReason.MoneyExpenditure,
                    symbol + amount.ToString("0.##", CultureInfo.InvariantCulture)));
    }

    private static async Task ShowAsync(MainWindow window, string title, string unitLabel,
        string rateLabel, double initialValue, Func<double, int> cost, string confirmLabel,
        Action<ScoreService, double, int> onConfirm)
    {
        long balance;
        try
        {
            using var db = new Database();
            balance = db.ScoreBalance();
        }
        catch (Exception ex)
        {
            Log.Error("SpendDialog (balance)", ex);
            return;
        }

        var panel = new StackPanel { Spacing = 10, MinWidth = 360 };
        panel.Children.Add(new TextBlock
        {
            Text = $"Balance: {balance} pts   ·   {rateLabel}",
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });
        var input = new NumberBox
        {
            Header = unitLabel,
            Value = initialValue,
            Minimum = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            SmallChange = unitLabel.StartsWith("Min") ? 15 : 5,
        };
        panel.Children.Add(input);
        var costText = new TextBlock
        {
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        panel.Children.Add(costText);

        var dialog = DialogControls.Build(window.Content.XamlRoot, title, panel,
            primaryButtonText: confirmLabel, closeButtonText: "Cancel", defaultButton: ContentDialogButton.Primary);

        void Update()
        {
            var amount = input.Value;
            if (double.IsNaN(amount) || amount <= 0)
            {
                costText.Text = "Cost: —";
                dialog.IsPrimaryButtonEnabled = false;
                return;
            }
            var c = cost(amount);
            var affordable = c <= balance;
            costText.Text = affordable
                ? $"Cost: {c} pts"
                : $"Cost: {c} pts — over your {balance} pts balance";
            costText.Foreground = (Brush)Application.Current.Resources[
                affordable ? "TextFillColorSecondaryBrush" : "SystemFillColorCriticalBrush"];
            dialog.IsPrimaryButtonEnabled = affordable && c > 0;
        }
        input.ValueChanged += (_, _) => Update();
        Update();

        if (await DialogGate.ShowAsync(dialog) != ContentDialogResult.Primary) return;

        try
        {
            using var db = new Database();
            using var score = new ScoreService(new List<Models.Plan>(), db);
            onConfirm(score, input.Value, cost(input.Value));
        }
        catch (Exception ex)
        {
            // The dialog has already closed by this point (ShowAsync above only returns once
            // it has), so nothing in it can show an error — previously this just logged and
            // called RefreshScore() regardless, leaving the user believing the purchase/spend
            // went through when it didn't (2026-07-24 audit finding #7). A toast is the one
            // way left to surface it after the fact.
            Log.Error("SpendDialog (confirm)", ex);
            ToastNotifier.Show(title, Log.Friendly("Couldn't save that — your balance wasn't changed", ex), tag: null);
            return;
        }
        window.RefreshScore();
    }
}
