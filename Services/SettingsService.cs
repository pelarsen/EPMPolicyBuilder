using EPMPolicyBuilder.Models;
using Windows.Storage;

namespace EPMPolicyBuilder.Services;

public class SettingsService
{
    private const string ClientIdKey = "ClientId";
    private const string TenantIdKey = "TenantId";

    private readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

    public AppSettings Load()
    {
        return new AppSettings
        {
            ClientId = _localSettings.Values[ClientIdKey] as string ?? string.Empty,
            TenantId = _localSettings.Values[TenantIdKey] as string ?? "common"
        };
    }

    public void Save(AppSettings settings)
    {
        _localSettings.Values[ClientIdKey] = settings.ClientId;
        _localSettings.Values[TenantIdKey] = settings.TenantId;
    }
}
