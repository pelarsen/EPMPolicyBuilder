using System.Collections.ObjectModel;
using EPMPolicyBuilder.Models;
using EPMPolicyBuilder.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace EPMPolicyBuilder.Views;

public sealed partial class SuggestionsPage : Page
{
    public SuggestionsViewModel ViewModel { get; private set; } = null!;

    public SuggestionsPage() { InitializeComponent(); }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is SuggestionsViewModel vm)
        {
            ViewModel = vm;
            // Auto-load when navigating to the page if already connected
            if (ViewModel.IsConnected && ViewModel.Suggestions.Count == 0)
                ViewModel.LoadSuggestionsCommand.Execute(null);
        }
    }

    private void CreateRuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ElevationSuggestion suggestion)
            ViewModel.RequestCreateRule(suggestion);
    }

    // ── Static helpers for x:Bind ──────────────────────────────────────────
    public static Visibility BoolToVisibility(bool v)       => v ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility InvertBoolToVisibility(bool v) => v ? Visibility.Collapsed : Visibility.Visible;
    public static bool       InvertBool(bool v)             => !v;
    public static bool       HasStatus(string s)            => !string.IsNullOrEmpty(s);
    public static InfoBarSeverity StatusSeverity(bool hasError)
        => hasError ? InfoBarSeverity.Error : InfoBarSeverity.Informational;

    /// <summary>Shows the empty-state hint only when there are no items and no load is in progress.</summary>
    public static Visibility EmptyToVisibility(int count, bool isLoading)
        => count == 0 && !isLoading ? Visibility.Visible : Visibility.Collapsed;
}
