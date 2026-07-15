using EPMPolicyBuilder.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage.Pickers;

namespace EPMPolicyBuilder.Views;

public sealed partial class IntuneConnectionPage : Page
{
    public IntuneConnectionViewModel ViewModel { get; private set; } = null!;

    public IntuneConnectionPage() { InitializeComponent(); }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is IntuneConnectionViewModel vm)
        {
            ViewModel = vm;
            ViewModel.ParentWindowHandle =
                WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow!);
        }
    }

    private void SetupWizardButton_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is MainWindow mw)
            mw.NavigateToSettings();
    }

    private async void UploadCertButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Upload Certificate",
            PrimaryButtonText = "Upload",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var panel = new StackPanel { Spacing = 12 };
        var nameBox = new TextBox { Header = "Certificate Name", PlaceholderText = "e.g., My Company Code Signing Cert" };
        var descBox = new TextBox { Header = "Description", PlaceholderText = "Optional description" };
        var pathBox = new TextBox { Header = "Certificate File (.cer)", IsReadOnly = true, PlaceholderText = "Click Browse to select..." };
        var browseBtn = new Button { Content = "Browse .cer file..." };
        string selectedPath = string.Empty;

        browseBtn.Click += async (s, args) =>
        {
            var picker = new FileOpenPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeFilter.Add(".cer");
            var file = await picker.PickSingleFileAsync();
            if (file != null) { selectedPath = file.Path; pathBox.Text = file.Path; }
        };

        panel.Children.Add(nameBox);
        panel.Children.Add(descBox);
        panel.Children.Add(pathBox);
        panel.Children.Add(browseBtn);
        dialog.Content = panel;

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(selectedPath))
        {
            await ViewModel.UploadCertificateAsync(nameBox.Text, descBox.Text, selectedPath);
        }
    }

    // Static helpers
    public static Visibility BoolToVisibility(bool v) => v ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility StringToVisibility(string? s) => string.IsNullOrWhiteSpace(s) ? Visibility.Collapsed : Visibility.Visible;
    public static Visibility ValidBadgeVisibility(bool isExpired, bool expiresSoon, bool hasCertInfo)
        => hasCertInfo && !isExpired && !expiresSoon ? Visibility.Visible : Visibility.Collapsed;
    public static bool InvertBool(bool v) => !v;
    public static bool NotBusy(bool busy) => !busy;
    public static bool HasStatus(string s) => !string.IsNullOrEmpty(s);
    public static InfoBarSeverity StatusSeverity(bool hasError) => hasError ? InfoBarSeverity.Error : InfoBarSeverity.Success;
}
