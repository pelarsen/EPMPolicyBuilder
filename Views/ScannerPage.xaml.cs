using EPMPolicyBuilder.Models;
using EPMPolicyBuilder.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage.Pickers;

namespace EPMPolicyBuilder.Views;

public sealed partial class ScannerPage : Page
{
    public ScannerViewModel ViewModel { get; private set; } = null!;

    private Action? _selectAllHandler;
    private Action? _clearSelectionHandler;

    public ScannerPage() { InitializeComponent(); }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is ScannerViewModel vm)
        {
            ViewModel = vm;

            _selectAllHandler      = () => ResultsList.SelectAll();
            _clearSelectionHandler = () => ResultsList.DeselectRange(
                new ItemIndexRange(0, (uint)ResultsList.Items.Count));

            ViewModel.SelectAllVisibleRequested += _selectAllHandler;
            ViewModel.ClearSelectionRequested   += _clearSelectionHandler;
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        if (ViewModel is null) return;

        if (_selectAllHandler      != null) ViewModel.SelectAllVisibleRequested -= _selectAllHandler;
        if (_clearSelectionHandler != null) ViewModel.ClearSelectionRequested   -= _clearSelectionHandler;
    }

    private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SetSelectedItems(ResultsList.SelectedItems.Cast<ExeFileInfo>());
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
        => ViewModel.SelectAllVisibleCommand.Execute(null);

    private void ClearSelection_Click(object sender, RoutedEventArgs e)
        => ViewModel.ClearSelectionCommand.Execute(null);

    private async void BrowseCustomFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        var hwnd   = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add("*");
        var folder = await picker.PickSingleFolderAsync();
        if (folder != null) ViewModel.AddCustomPath(folder.Path);
    }

    // ── Static helpers for x:Bind ────────────────────────────────────────────
    public static Visibility BoolToVisibility(bool v) => v ? Visibility.Visible : Visibility.Collapsed;
    public static bool       InvertBool(bool v)       => !v;
    public static bool       HasSelection(int count)  => count > 0;

    public static string SelectedText(int count)
        => count == 0 ? "None selected" : $"{count} selected";

    public static Visibility StatusToVisibility(string s)
        => string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible;

    public static string CountSummary(int found, int skipped)
        => skipped > 0
            ? $"{found} files found · {skipped} folders skipped"
            : $"{found} files found";

    public static Brush SignedBrush(bool isSigned)
    {
        if (isSigned &&
            Application.Current.Resources.TryGetValue("SystemFillColorSuccessBrush", out var res) &&
            res is Brush successBrush)
            return successBrush;

        if (Application.Current.Resources.TryGetValue("TextFillColorPrimaryBrush", out var primary) &&
            primary is Brush primaryBrush)
            return primaryBrush;

        return new SolidColorBrush(Colors.Black);
    }
}
