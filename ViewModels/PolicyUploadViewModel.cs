using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EPMPolicyBuilder.Models;
using EPMPolicyBuilder.Services;

namespace EPMPolicyBuilder.ViewModels;

public partial class PolicyUploadViewModel : ObservableObject
{
    private readonly GraphService _graphService;
    private readonly PolicyJsonBuilder _jsonBuilder;
    private readonly IntuneConnectionViewModel _connectionVm;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private bool _hasSuccess;
    [ObservableProperty] private string _ruleJson = string.Empty;
    [ObservableProperty] private bool _createNewPolicy;
    [ObservableProperty] private string _newPolicyName = string.Empty;
    [ObservableProperty] private EpmPolicy? _selectedPolicy;
    [ObservableProperty] private bool _hasRule;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _batchSummary = string.Empty;
    [ObservableProperty] private string _errorRequestJson = string.Empty;

    public bool HasErrorJson => !string.IsNullOrWhiteSpace(ErrorRequestJson);

    // Pass-through properties from the shared connection VM
    public IRelayCommand ConnectCommand => _connectionVm.ConnectCommand;
    public bool IsConnecting => _connectionVm.IsLoading;
    public string ConnectionStatusMessage => _connectionVm.HasError && !_connectionVm.IsConnected
        ? _connectionVm.StatusMessage
        : string.Empty;

    public bool IsBatchMode => _batchRules.Count > 1;

    public ObservableCollection<EpmPolicy> Policies { get; } = [];

    private ElevationRule? _currentRule;
    private List<ElevationRule> _batchRules = [];

    public PolicyUploadViewModel(GraphService graphService, PolicyJsonBuilder jsonBuilder,
        IntuneConnectionViewModel connectionVm)
    {
        _graphService  = graphService;
        _jsonBuilder   = jsonBuilder;
        _connectionVm  = connectionVm;
        IsConnected    = _connectionVm.IsConnected;

        // React to connection state changes made anywhere in the app
        _connectionVm.PropertyChanged += OnConnectionVmPropertyChanged;
    }

    private void OnConnectionVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IntuneConnectionViewModel.IsConnected):
                bool wasConnected = IsConnected;
                IsConnected = _connectionVm.IsConnected;
                // Auto-load policies the moment we become connected
                if (IsConnected && !wasConnected)
                    LoadPoliciesCommand.Execute(null);
                break;
            case nameof(IntuneConnectionViewModel.IsLoading):
                OnPropertyChanged(nameof(IsConnecting));
                break;
            case nameof(IntuneConnectionViewModel.StatusMessage):
            case nameof(IntuneConnectionViewModel.HasError):
                OnPropertyChanged(nameof(ConnectionStatusMessage));
                break;
        }
    }

    public void SetRule(ElevationRule rule)
    {
        _batchRules  = [];
        _currentRule = rule;
        RuleJson     = _jsonBuilder.BuildRuleJson(rule);
        HasRule      = !string.IsNullOrWhiteSpace(RuleJson);
        BatchSummary = string.Empty;
        OnPropertyChanged(nameof(IsBatchMode));
    }

    public void SetRules(IList<ElevationRule> rules)
    {
        _batchRules  = [.. rules];
        _currentRule = _batchRules.FirstOrDefault();
        HasRule      = _batchRules.Count > 0;
        OnPropertyChanged(nameof(IsBatchMode));

        BatchSummary = _batchRules.Count > 0
            ? $"{_batchRules.Count} rules ready for batch upload — will be added to the selected policy"
            : string.Empty;

        if (_batchRules.Count == 1 && _currentRule != null)
        {
            RuleJson = _jsonBuilder.BuildRuleJson(_currentRule);
        }
        else if (_batchRules.Count > 1)
        {
            var preview = string.Join("\n", _batchRules.Take(5).Select((r, i) => $"// {i + 1}. {r.RuleName}"));
            var more    = _batchRules.Count > 5 ? $"\n// … and {_batchRules.Count - 5} more" : string.Empty;
            RuleJson    = $"// {_batchRules.Count} rules queued for batch upload\n{preview}{more}";
        }
    }

    public void RefreshConnectionState()
    {
        IsConnected = _connectionVm.IsConnected;
    }

    [RelayCommand]
    private async Task LoadPoliciesAsync()
    {
        if (!_graphService.IsConnected) return;
        IsLoading = true;
        HasError  = false;
        try
        {
            var policies = await _graphService.GetEpmPoliciesAsync();
            Policies.Clear();
            foreach (var p in policies) Policies.Add(p);
            StatusMessage = $"Loaded {policies.Count} EPM policy(ies).";
        }
        catch (Exception ex)
        {
            HasError      = true;
            StatusMessage = $"Failed to load policies: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task UploadAsync()
    {
        if (!_graphService.IsConnected) { StatusMessage = "Not connected to Intune."; HasError = true; return; }

        if (IsBatchMode)
        {
            await DoBatchUploadAsync();
            return;
        }

        if (_currentRule == null || string.IsNullOrWhiteSpace(RuleJson)) return;

        IsLoading        = true;
        HasError         = false;
        HasSuccess       = false;
        ErrorRequestJson = string.Empty;
        OnPropertyChanged(nameof(HasErrorJson));
        try
        {
            string result;
            if (CreateNewPolicy)
            {
                if (string.IsNullOrWhiteSpace(NewPolicyName)) { StatusMessage = "Enter a policy name."; HasError = true; return; }
                result        = await _graphService.CreatePolicyWithRuleAsync(NewPolicyName, _currentRule);
                StatusMessage = $"Policy '{NewPolicyName}' created successfully.";
            }
            else
            {
                if (SelectedPolicy == null) { StatusMessage = "Select an existing policy."; HasError = true; return; }
                result        = await _graphService.UploadRuleToPolicyAsync(SelectedPolicy.Id, _currentRule);
                StatusMessage = $"Rule added to '{SelectedPolicy.Name}' successfully.";
            }
            _ = result;
            HasSuccess = true;
        }
        catch (EpmUploadException ex)
        {
            HasError         = true;
            StatusMessage    = $"Upload failed: {ex.Message}";
            ErrorRequestJson = ex.RequestJson ?? string.Empty;
            OnPropertyChanged(nameof(HasErrorJson));
        }
        catch (Exception ex)
        {
            HasError      = true;
            StatusMessage = $"Upload failed: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    private async Task DoBatchUploadAsync()
    {
        if (_batchRules.Count == 0) return;

        IsLoading        = true;
        HasError         = false;
        HasSuccess       = false;
        ErrorRequestJson = string.Empty;
        OnPropertyChanged(nameof(HasErrorJson));
        int total  = _batchRules.Count;

        try
        {
            if (CreateNewPolicy)
            {
                if (string.IsNullOrWhiteSpace(NewPolicyName))
                {
                    StatusMessage = "Enter a policy name.";
                    HasError      = true;
                    return;
                }

                StatusMessage = $"Creating policy '{NewPolicyName}' with first rule…";
                var responseJson = await _graphService.CreatePolicyWithRuleAsync(NewPolicyName, _batchRules[0]);

                // Extract the new policy ID from the response body
                string? policyId = null;
                try
                {
                    using var doc = JsonDocument.Parse(responseJson);
                    if (doc.RootElement.TryGetProperty("id", out var idEl))
                        policyId = idEl.GetString();
                }
                catch { }

                if (string.IsNullOrWhiteSpace(policyId))
                {
                    StatusMessage = $"Policy '{NewPolicyName}' created with 1 rule. " +
                                    "Could not extract policy ID to add remaining rules — upload remaining rules manually.";
                    HasSuccess = true;
                    return;
                }

                for (int i = 1; i < total; i++)
                {
                    var rule  = _batchRules[i];
                    StatusMessage = $"Uploading rule {i + 1} of {total}: {rule.RuleName}…";
                    await _graphService.UploadRuleToPolicyAsync(policyId, rule);
                }

                StatusMessage = $"Policy '{NewPolicyName}' created with {total} rules successfully.";
            }
            else
            {
                if (SelectedPolicy == null)
                {
                    StatusMessage = "Select an existing policy.";
                    HasError      = true;
                    return;
                }

                for (int i = 0; i < total; i++)
                {
                    var rule  = _batchRules[i];
                    StatusMessage = $"Uploading rule {i + 1} of {total}: {rule.RuleName}…";
                    await _graphService.UploadRuleToPolicyAsync(SelectedPolicy.Id, rule);
                }

                StatusMessage = $"{total} rules added to '{SelectedPolicy.Name}' successfully.";
            }

            HasSuccess = true;
        }
        catch (EpmUploadException ex)
        {
            HasError         = true;
            StatusMessage    = $"Batch upload failed: {ex.Message}";
            ErrorRequestJson = ex.RequestJson ?? string.Empty;
            OnPropertyChanged(nameof(HasErrorJson));
        }
        catch (Exception ex)
        {
            HasError      = true;
            StatusMessage = $"Batch upload failed: {ex.Message}";
        }
        finally { IsLoading = false; }
    }
}
