using EPMPolicyBuilder.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage.Pickers;

namespace EPMPolicyBuilder.Views;

public sealed partial class FileAnalysisPage : Page
{
    public FileAnalysisViewModel ViewModel { get; private set; } = null!;

    public FileAnalysisPage() { InitializeComponent(); }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is FileAnalysisViewModel vm) ViewModel = vm;
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add(".exe");
        picker.FileTypeFilter.Add(".msi");
        picker.FileTypeFilter.Add(".ps1");
        var file = await picker.PickSingleFileAsync();
        if (file != null)
            ViewModel.AnalyzeFile(file.Path);
    }

    private void UseInRuleButton_Click(object sender, RoutedEventArgs e)
    {
        App.NavigateToRuleBuilder(ViewModel.FileMetadata);
    }

    // Static helpers for x:Bind
    public static Visibility BoolToVisibility(bool v) => v ? Visibility.Visible : Visibility.Collapsed;
    public static bool HasStatus(string s) => !string.IsNullOrEmpty(s);
    public static InfoBarSeverity StatusSeverity(bool hasError) => hasError ? InfoBarSeverity.Error : InfoBarSeverity.Success;
}
