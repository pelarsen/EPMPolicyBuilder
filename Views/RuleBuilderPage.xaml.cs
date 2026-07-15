using EPMPolicyBuilder.Models;
using EPMPolicyBuilder.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage.Pickers;

namespace EPMPolicyBuilder.Views;

public sealed partial class RuleBuilderPage : Page
{
    public RuleBuilderViewModel ViewModel { get; private set; } = null!;

    public RuleBuilderPage() { InitializeComponent(); }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is RuleBuilderViewModel vm) ViewModel = vm;
    }

    private async void BrowseCertButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add(".cer");
        picker.FileTypeFilter.Add(".pfx");
        var file = await picker.PickSingleFileAsync();
        if (file != null) ViewModel.UploadedCertPath = file.Path;
    }

    private void BuildRuleButton_Click(object sender, RoutedEventArgs e)
    {
        App.NavigateToPolicyUpload(ViewModel.BuildRule());
    }

    // Static helpers
    public static Visibility BoolToVisibility(bool v) => v ? Visibility.Visible : Visibility.Collapsed;
    public static bool HasMetadata(FileMetadata? m) => m != null;
    public static string FileInfoMessage(FileMetadata? m) => m != null ? $"Using file: {m.FileName} ({m.FilePath})" : string.Empty;
}
