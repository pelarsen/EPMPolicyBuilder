using Microsoft.UI.Xaml;
using EPMPolicyBuilder.Models;

namespace EPMPolicyBuilder;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    public App() { InitializeComponent(); }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }

    public static void NavigateToRuleBuilder(FileMetadata? metadata)
    {
        if (MainWindow is MainWindow mw) mw.NavigateToRuleBuilder(metadata);
    }

    public static void NavigateToPolicyUpload(ElevationRule rule)
    {
        if (MainWindow is MainWindow mw) mw.NavigateToPolicyUpload(rule);
    }

    public static void NavigateToScannerBatch(List<ElevationRule> rules)
    {
        if (MainWindow is MainWindow mw) mw.NavigateToScannerBatch(rules);
    }
}
