using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EPMPolicyBuilder.Models;
using EPMPolicyBuilder.Services;

namespace EPMPolicyBuilder.ViewModels;

public partial class IntuneConnectionViewModel : ObservableObject
{
    private readonly GraphService _graphService;
    private readonly SettingsService _settingsService;
    private readonly AppRegistrationService _appRegService;

    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _connectedUser = string.Empty;
    [ObservableProperty] private string _connectedTenant = string.Empty;
    [ObservableProperty] private bool _hasClientId;
    [ObservableProperty] private bool _hasRedirectUriError;
    [ObservableProperty] private bool _isFixingRedirectUri;

    /// <summary>Set by IntuneConnectionPage code-behind on navigation.</summary>
    public IntPtr ParentWindowHandle { get; set; }

    public ObservableCollection<ReusableSetting> ReusableSettings { get; } = [];

    public IntuneConnectionViewModel(GraphService graphService, SettingsService settingsService, AppRegistrationService appRegService)
    {
        _graphService    = graphService;
        _settingsService = settingsService;
        _appRegService   = appRegService;
        var settings     = _settingsService.Load();
        HasClientId      = !string.IsNullOrWhiteSpace(settings.ClientId);
        if (HasClientId)
            _graphService.Configure(settings.ClientId, settings.TenantId);
    }

    public void RefreshClientIdStatus()
    {
        var settings = _settingsService.Load();
        HasClientId = !string.IsNullOrWhiteSpace(settings.ClientId);
        if (HasClientId)
            _graphService.Configure(settings.ClientId, settings.TenantId);
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        IsLoading = true;
        HasError = false;
        StatusMessage = "Connecting to Microsoft Graph...";
        try
        {
            var settings = _settingsService.Load();
            if (string.IsNullOrWhiteSpace(settings.ClientId))
            {
                HasError = true;
                StatusMessage = "Please configure your Azure AD Client ID in Settings first.";
                return;
            }
            _graphService.Configure(settings.ClientId, settings.TenantId);
            var ok = await _graphService.SignInAsync(ParentWindowHandle);
            IsConnected = ok;
            if (ok)
            {
                ConnectedUser = _graphService.ConnectedUser ?? string.Empty;
                ConnectedTenant = _graphService.ConnectedTenant ?? string.Empty;
                StatusMessage = $"Connected as {ConnectedUser} — loading templates…";
                await _graphService.RefreshTemplateRefsAsync();
                await LoadReusableSettingsAsync();
            }
            else
            {
                HasError = true;
                StatusMessage = "Authentication failed.";
            }
        }
        catch (Exception ex)
        {
            HasError    = true;
            IsConnected = false;
            // Detect redirect URI mismatch and offer one-click fix
            if (ex.Message.Contains("AADSTS50011") || ex.Message.Contains("redirect"))
            {
                HasRedirectUriError = true;
                StatusMessage = "Redirect URI mismatch (AADSTS50011). Use the 'Fix Redirect URI' button below.";
            }
            else
            {
                StatusMessage = $"Connection error: {ex.Message}";
            }
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task FixRedirectUriAsync()
    {
        IsFixingRedirectUri = true;
        HasError            = false;
        HasRedirectUriError = false;
        StatusMessage       = "Fixing redirect URI in app registration...";
        try
        {
            var settings = _settingsService.Load();
            var progress = new Progress<string>(msg => StatusMessage = msg);
            await _appRegService.FixRedirectUriAsync(settings.ClientId, ParentWindowHandle, progress);
        }
        catch (Exception ex)
        {
            HasError      = true;
            StatusMessage = $"Fix failed: {ex.Message}";
        }
        finally { IsFixingRedirectUri = false; }
    }

    [RelayCommand]
    private void Disconnect()
    {
        _graphService.SignOut();
        IsConnected = false;
        ConnectedUser = string.Empty;
        ConnectedTenant = string.Empty;
        ReusableSettings.Clear();
        StatusMessage = "Disconnected.";
    }

    [RelayCommand]
    private async Task LoadReusableSettingsAsync()
    {
        if (!IsConnected) return;
        IsLoading = true;
        HasError = false;
        try
        {
            var settings = await _graphService.GetReusableCertSettingsAsync();
            ReusableSettings.Clear();
            foreach (var s in settings) ReusableSettings.Add(s);
            StatusMessage = $"Loaded {settings.Count} reusable certificate setting(s).";
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = $"Failed to load reusable settings: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    public async Task<ReusableSetting?> UploadCertificateAsync(string name, string description, string certPath)
    {
        IsLoading = true;
        HasError = false;
        try
        {
            var result = await _graphService.UploadCertificateAsync(name, description, certPath);
            ReusableSettings.Add(result);
            StatusMessage = $"Certificate '{name}' uploaded successfully.";
            return result;
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = $"Certificate upload failed: {ex.Message}";
            return null;
        }
        finally { IsLoading = false; }
    }
}
