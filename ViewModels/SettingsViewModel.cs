using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EPMPolicyBuilder.Models;
using EPMPolicyBuilder.Services;

namespace EPMPolicyBuilder.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService        _settingsService;
    private readonly AppRegistrationService _appRegService;

    // ── Manual configuration ──────────────────────────────────
    [ObservableProperty] private string _clientId     = string.Empty;
    [ObservableProperty] private string _tenantId     = "common";
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool   _isSaved;

    // ── Auto-setup state ──────────────────────────────────────
    [ObservableProperty] private bool   _isSetupRunning;
    [ObservableProperty] private bool   _isSetupComplete;
    [ObservableProperty] private bool   _hasSetupError;
    [ObservableProperty] private string _setupErrorMessage = string.Empty;
    [ObservableProperty] private string _newClientId       = string.Empty;
    [ObservableProperty] private string _newTenantId       = string.Empty;

    public ObservableCollection<string> SetupLog { get; } = [];

    /// <summary>Set by the page code-behind; needed for MSAL interactive auth.</summary>
    public IntPtr ParentWindowHandle { get; set; }

    /// <summary>Raised after successful auto-registration so MainWindow can navigate to Intune Connection.</summary>
    public event Action? SetupCompleted;

    public SettingsViewModel(SettingsService settingsService, AppRegistrationService appRegService)
    {
        _settingsService = settingsService;
        _appRegService   = appRegService;
        Reload();
    }

    public void Reload()
    {
        var s  = _settingsService.Load();
        ClientId = s.ClientId;
        TenantId = s.TenantId;
        IsSaved  = false;
    }

    // ── Manual save ───────────────────────────────────────────

    [RelayCommand]
    private void Save()
    {
        _settingsService.Save(new AppSettings { ClientId = ClientId, TenantId = TenantId });
        StatusMessage = "Settings saved.";
        IsSaved = true;
    }

    // ── Auto-register ─────────────────────────────────────────

    [RelayCommand]
    private async Task CreateAppRegistrationAsync()
    {
        IsSetupRunning  = true;
        IsSetupComplete = false;
        HasSetupError   = false;
        SetupErrorMessage = string.Empty;
        SetupLog.Clear();

        var progress = new Progress<string>(msg =>
        {
            SetupLog.Add(msg);
        });

        try
        {
            SetupLog.Add("Opening browser for admin sign-in...");
            SetupLog.Add("(A browser window will open — sign in with a Global Admin account)");
            await _appRegService.SignInAdminAsync(ParentWindowHandle);

            SetupLog.Add($"✅ Signed in as {_appRegService.AdminUser}");
            var result = await _appRegService.CreateAppRegistrationAsync(progress);

            // Persist the new Client ID and refresh the manual fields too
            NewClientId = result.ClientId;
            NewTenantId = result.TenantId;
            ClientId    = result.ClientId;
            TenantId    = result.TenantId;
            _settingsService.Save(new AppSettings { ClientId = result.ClientId, TenantId = result.TenantId });

            IsSetupComplete = true;
            SetupCompleted?.Invoke();
        }
        catch (OperationCanceledException)
        {
            HasSetupError     = true;
            SetupErrorMessage = "Sign-in was cancelled. Click the button to try again.";
            SetupLog.Add("Sign-in cancelled by user.");
        }
        catch (Microsoft.Identity.Client.MsalException msalEx)
        {
            HasSetupError     = true;
            SetupErrorMessage = $"Authentication error ({msalEx.ErrorCode}): {msalEx.Message}";
            SetupLog.Add($"❌ {SetupErrorMessage}");
        }
        catch (Exception ex)
        {
            HasSetupError     = true;
            SetupErrorMessage = ex.Message;
            SetupLog.Add($"❌ {ex.Message}");
        }
        finally
        {
            IsSetupRunning = false;
        }
    }
}
