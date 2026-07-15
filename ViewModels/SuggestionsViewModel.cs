using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EPMPolicyBuilder.Models;
using EPMPolicyBuilder.Services;

namespace EPMPolicyBuilder.ViewModels;

public partial class SuggestionsViewModel : ObservableObject
{
    private readonly GraphService              _graphService;
    private readonly IntuneConnectionViewModel _connectionVm;

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool   _hasError;
    [ObservableProperty] private bool   _isConnected;

    public ObservableCollection<ElevationSuggestion> Suggestions { get; } = [];

    /// <summary>Fired when the user wants to create a rule from a suggestion.</summary>
    public event Action<FileMetadata?>? CreateRuleRequested;

    public SuggestionsViewModel(GraphService graphService, IntuneConnectionViewModel connectionVm)
    {
        _graphService  = graphService;
        _connectionVm  = connectionVm;
        IsConnected    = _connectionVm.IsConnected;

        _connectionVm.PropertyChanged += OnConnectionVmChanged;
    }

    private void OnConnectionVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IntuneConnectionViewModel.IsConnected)) return;
        bool wasConnected = IsConnected;
        IsConnected = _connectionVm.IsConnected;
        if (IsConnected && !wasConnected)
            LoadSuggestionsCommand.Execute(null);
    }

    [RelayCommand]
    private async Task LoadSuggestionsAsync()
    {
        if (!_graphService.IsConnected) return;
        IsLoading     = true;
        HasError      = false;
        StatusMessage = string.Empty;
        try
        {
            var items = await _graphService.GetUnmanagedElevationsAsync();
            Suggestions.Clear();
            foreach (var s in items) Suggestions.Add(s);

            StatusMessage = items.Count == 0
                ? "No unmanaged elevations found in the last 30 days."
                : $"Found {items.Count} application(s) with unmanaged elevations.";
        }
        catch (Exception ex)
        {
            HasError      = true;
            StatusMessage = $"Failed to load report: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    /// <summary>
    /// Converts the selected suggestion into a <see cref="FileMetadata"/> pre-filled from
    /// the report data and fires the CreateRuleRequested event so MainWindow can navigate.
    /// </summary>
    public void RequestCreateRule(ElevationSuggestion suggestion)
    {
        var meta = new FileMetadata
        {
            FileName      = suggestion.FileName,
            PublisherName = suggestion.Publisher,
            FileVersion   = suggestion.FileVersion,
            ProductName   = suggestion.ProductName
        };
        CreateRuleRequested?.Invoke(meta);
    }
}
