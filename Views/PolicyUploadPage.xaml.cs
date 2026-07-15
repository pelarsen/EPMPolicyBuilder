using EPMPolicyBuilder.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace EPMPolicyBuilder.Views;

public sealed partial class PolicyUploadPage : Page
{
    public PolicyUploadViewModel ViewModel { get; private set; } = null!;

    public PolicyUploadPage() { InitializeComponent(); }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is PolicyUploadViewModel vm) ViewModel = vm;

        // Auto-load policies immediately if we arrive already connected
        if (ViewModel.IsConnected && ViewModel.Policies.Count == 0)
            ViewModel.LoadPoliciesCommand.Execute(null);
    }

    private void PolicyTargetRadio_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.CreateNewPolicy = NewPolicyRadio.IsChecked == true;
    }

    // Static helpers
    public static Visibility BoolToVisibility(bool v) => v ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility InvertBoolToVisibility(bool v) => v ? Visibility.Collapsed : Visibility.Visible;
    public static bool InvertBool(bool v) => !v;
    public static bool HasStatus(string s) => !string.IsNullOrEmpty(s);
    public static bool CanUpload(bool connected, bool hasRule, bool isLoading) => connected && hasRule && !isLoading;
    public static InfoBarSeverity UploadSeverity(bool hasError, bool hasSuccess)
        => hasError ? InfoBarSeverity.Error : hasSuccess ? InfoBarSeverity.Success : InfoBarSeverity.Informational;
}
