using EPMPolicyBuilder.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;

namespace EPMPolicyBuilder.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; private set; } = null!;

    public SettingsPage() { InitializeComponent(); }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is SettingsViewModel vm)
        {
            ViewModel = vm;
            // Give the VM the window handle so MSAL can parent the browser dialog
            ViewModel.ParentWindowHandle =
                WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow!);
        }
    }

    private void CopyNewClientId_Click(object sender, RoutedEventArgs e)
    {
        var pkg = new DataPackage();
        pkg.SetText(ViewModel?.NewClientId ?? string.Empty);
        Clipboard.SetContent(pkg);
    }

    private void GoToIntuneConnection_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is MainWindow mw)
            mw.NavigateToIntuneConnection();
    }

    // ── Static helpers for x:Bind ──────────────────────────────
    public static Visibility Show(bool v)       => v  ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility Hide(bool v)       => !v ? Visibility.Visible : Visibility.Collapsed;
    public static bool       NotBusy(bool busy) => !busy;

    // Legacy alias kept for any remaining x:Bind calls
    public static Visibility BoolToVisibility(bool v) => Show(v);
}
