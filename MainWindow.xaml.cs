using EPMPolicyBuilder.Models;
using EPMPolicyBuilder.Services;
using EPMPolicyBuilder.ViewModels;
using EPMPolicyBuilder.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace EPMPolicyBuilder;

public sealed partial class MainWindow : Window
{
    private readonly FileAnalysisViewModel     _fileAnalysisVm;
    private readonly RuleBuilderViewModel      _ruleBuilderVm;
    private readonly IntuneConnectionViewModel _intuneConnectionVm;
    private readonly PolicyUploadViewModel     _policyUploadVm;
    private readonly SettingsViewModel         _settingsVm;
    private readonly ScannerViewModel          _scannerVm;
    private readonly SuggestionsViewModel      _suggestionsVm;

    /// <summary>Exposed for x:Bind in XAML (PaneFooter connection widget).</summary>
    public IntuneConnectionViewModel ConnectionVm => _intuneConnectionVm;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;

        var settingsService  = new SettingsService();
        var jsonBuilder      = new PolicyJsonBuilder();
        var graphService     = new GraphService(jsonBuilder);
        var fileAnalysisSvc  = new FileAnalysisService();
        var appRegService    = new AppRegistrationService();

        _settingsVm          = new SettingsViewModel(settingsService, appRegService);
        _fileAnalysisVm      = new FileAnalysisViewModel(fileAnalysisSvc);
        _ruleBuilderVm       = new RuleBuilderViewModel();
        _intuneConnectionVm  = new IntuneConnectionViewModel(graphService, settingsService, appRegService);
        // Pass _intuneConnectionVm so PolicyUpload can react to connection state changes
        _policyUploadVm      = new PolicyUploadViewModel(graphService, jsonBuilder, _intuneConnectionVm);
        _scannerVm           = new ScannerViewModel(new ExeScanner(), fileAnalysisSvc);
        _suggestionsVm       = new SuggestionsViewModel(graphService, _intuneConnectionVm);

        // Wire scanner events → navigation helpers
        _scannerVm.SendToRuleBuilderRequested += NavigateToRuleBuilder;
        _scannerVm.BatchUploadRequested       += NavigateToScannerBatch;

        // Wire suggestions → rule builder navigation
        _suggestionsVm.CreateRuleRequested += NavigateToRuleBuilder;

        // After successful auto-registration navigate to Intune Connection
        _settingsVm.SetupCompleted += () =>
        {
            _intuneConnectionVm.RefreshClientIdStatus();
            NavigateToIntuneConnection();
        };
    }

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        // Set once — the window handle never changes and is needed by the connect flow
        // from anywhere in the app (PaneFooter button, PolicyUpload inline button, etc.)
        _intuneConnectionVm.ParentWindowHandle = WindowNative.GetWindowHandle(this);

        var settings = new SettingsService().Load();
        if (string.IsNullOrWhiteSpace(settings.ClientId))
        {
            // First launch – go straight to Settings so the user can set up
            NavView.SelectedItem = NavView.SettingsItem;
            ContentFrame.Navigate(typeof(SettingsPage), _settingsVm);
        }
        else
        {
            NavView.SelectedItem = FileAnalysisNavItem;
            ContentFrame.Navigate(typeof(FileAnalysisPage), _fileAnalysisVm);
        }
    }

    // ── Static helpers for x:Bind converters in XAML ──────────────────────
    public static Visibility BoolToVisibility(bool v)        => v ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility InvertBoolToVisibility(bool v)  => v ? Visibility.Collapsed : Visibility.Visible;
    public static bool       InvertBool(bool v)              => !v;

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            _settingsVm.Reload();
            ContentFrame.Navigate(typeof(SettingsPage), _settingsVm);
            return;
        }

        if (args.SelectedItem is NavigationViewItem item)
        {
            switch (item.Tag?.ToString())
            {
                case "FileAnalysis":
                    ContentFrame.Navigate(typeof(FileAnalysisPage), _fileAnalysisVm);
                    break;
                case "RuleBuilder":
                    _ruleBuilderVm.SetReusableCertificates(_intuneConnectionVm.ReusableSettings);
                    ContentFrame.Navigate(typeof(RuleBuilderPage), _ruleBuilderVm);
                    break;
                case "Scanner":
                    ContentFrame.Navigate(typeof(ScannerPage), _scannerVm);
                    break;
                case "Suggestions":
                    ContentFrame.Navigate(typeof(SuggestionsPage), _suggestionsVm);
                    break;
                case "IntuneConnection":
                    ContentFrame.Navigate(typeof(IntuneConnectionPage), _intuneConnectionVm);
                    break;
                case "PolicyUpload":
                    ContentFrame.Navigate(typeof(PolicyUploadPage), _policyUploadVm);
                    break;
            }
        }
    }

    // ── Navigation helpers ────────────────────────────────────

    public void NavigateToSettings()
    {
        NavView.SelectedItem = NavView.SettingsItem;
        _settingsVm.Reload();
        ContentFrame.Navigate(typeof(SettingsPage), _settingsVm);
    }

    public void NavigateToIntuneConnection()
    {
        NavView.SelectedItem = IntuneConnectionNavItem;
        ContentFrame.Navigate(typeof(IntuneConnectionPage), _intuneConnectionVm);
    }

    public void NavigateToRuleBuilder(FileMetadata? metadata)
    {
        _ruleBuilderVm.SetFileMetadata(metadata);
        NavView.SelectedItem = RuleBuilderNavItem;
        ContentFrame.Navigate(typeof(RuleBuilderPage), _ruleBuilderVm);
    }

    public void NavigateToPolicyUpload(ElevationRule rule)
    {
        _policyUploadVm.SetRule(rule);
        NavView.SelectedItem = PolicyUploadNavItem;
        ContentFrame.Navigate(typeof(PolicyUploadPage), _policyUploadVm);
    }

    public void NavigateToScannerBatch(List<ElevationRule> rules)
    {
        _policyUploadVm.SetRules(rules);
        NavView.SelectedItem = PolicyUploadNavItem;
        ContentFrame.Navigate(typeof(PolicyUploadPage), _policyUploadVm);
    }
}
