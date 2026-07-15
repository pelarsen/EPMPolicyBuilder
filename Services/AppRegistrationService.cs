using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Identity.Client;

namespace EPMPolicyBuilder.Services;

public record AppRegistrationResult(string ClientId, string TenantId, string AdminUser);

public class AppRegistrationService
{
    // Bootstrap: Microsoft Graph PowerShell well-known multi-tenant public client.
    // No Azure CLI required - uses MSAL interactive browser sign-in.
    private const string BootstrapClientId = "14d82eec-204b-4c2f-b7e8-296a70dab67e";

    private static readonly string[] BootstrapScopes =
    [
        "Application.ReadWrite.All",
        "DelegatedPermissionGrant.ReadWrite.All"
    ];

    private const string GraphBase     = "https://graph.microsoft.com/v1.0";
    private const string MsGraphAppId  = "00000003-0000-0000-c000-000000000000";
    private const string EpmPermission = "9241abd9-d0e6-425a-bd4f-47ba86e767a4";

    private IPublicClientApplication? _pca;
    private AuthenticationResult?     _authResult;
    private readonly HttpClient       _http = new();

    public string? AdminUser => _authResult?.Account?.Username;

    /// <summary>
    /// Opens an interactive MSAL sign-in using the system default browser.
    /// Throws on failure; throws OperationCanceledException if user cancels.
    /// </summary>
    public async Task SignInAdminAsync(IntPtr parentHwnd)
    {
        // Use loopback redirect so the system browser can hand the token back.
        // http://localhost is supported by the MS Graph PowerShell bootstrap client.
        _pca = PublicClientApplicationBuilder
            .Create(BootstrapClientId)
            .WithAuthority("https://login.microsoftonline.com/common")
            .WithRedirectUri("http://localhost")
            .Build();

        var accounts = await _pca.GetAccountsAsync();

        try
        {
            // Try silent first in case there is a cached session
            _authResult = await _pca
                .AcquireTokenSilent(BootstrapScopes, accounts.FirstOrDefault())
                .ExecuteAsync();
        }
        catch (MsalUiRequiredException)
        {
            // Interactive sign-in required – use system browser (reliable in WinUI 3 packaged apps)
            _authResult = await _pca
                .AcquireTokenInteractive(BootstrapScopes)
                .WithParentActivityOrWindow(parentHwnd)
                .WithUseEmbeddedWebView(false)
                .ExecuteAsync();
            // Throws MsalException on auth failure, OperationCanceledException if user closes browser
        }

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _authResult.AccessToken);
    }

    /// <summary>
    /// Creates the Azure AD App Registration, service principal, and grants admin consent.
    /// Must call <see cref="SignInAdminAsync"/> first.
    /// </summary>
    public async Task<AppRegistrationResult> CreateAppRegistrationAsync(IProgress<string>? progress = null)
    {
        if (_authResult == null)
            throw new InvalidOperationException("Not signed in. Call SignInAdminAsync first.");

        // 1. Create App Registration
        progress?.Report("Creating app registration 'EPM Policy Builder'...");

        var appPayload = new
        {
            displayName    = "EPM Policy Builder",
            signInAudience = "AzureADMyOrg",
            requiredResourceAccess = new[]
            {
                new
                {
                    resourceAppId  = MsGraphAppId,
                    resourceAccess = new[] { new { id = EpmPermission, type = "Scope" } }
                }
            },
            publicClient = new
            {
                redirectUris = new[]
                {
                    "http://localhost",   // loopback – required for system browser MSAL flow
                    "https://login.microsoftonline.com/common/oauth2/nativeclient"
                }
            }
        };

        var appResp = await PostJsonAsync($"{GraphBase}/applications", appPayload);
        await EnsureSuccessAsync(appResp, "create app registration");

        var appDoc   = JsonDocument.Parse(await appResp.Content.ReadAsStringAsync());
        var clientId = appDoc.RootElement.GetProperty("appId").GetString()!;
        var tenantId = _authResult.TenantId;

        // 2. Create Service Principal
        progress?.Report($"App registered (Client ID: {clientId}). Creating service principal...");

        var spResp = await PostJsonAsync($"{GraphBase}/servicePrincipals", new { appId = clientId });
        await EnsureSuccessAsync(spResp, "create service principal");

        var spDoc = JsonDocument.Parse(await spResp.Content.ReadAsStringAsync());
        var spId  = spDoc.RootElement.GetProperty("id").GetString()!;

        // 3. Find Microsoft Graph SP in this tenant
        progress?.Report("Looking up Microsoft Graph service principal...");

        var msGraphResp = await _http.GetAsync(
            $"{GraphBase}/servicePrincipals?$filter=appId eq '{MsGraphAppId}'&$select=id");
        await EnsureSuccessAsync(msGraphResp, "look up Microsoft Graph SP");

        var msGraphDoc  = JsonDocument.Parse(await msGraphResp.Content.ReadAsStringAsync());
        var msGraphSpId = msGraphDoc.RootElement.GetProperty("value")[0].GetProperty("id").GetString()!;

        // 4. Grant admin consent
        progress?.Report("Granting admin consent for required permissions...");

        var grantPayload = new
        {
            clientId    = spId,
            consentType = "AllPrincipals",
            resourceId  = msGraphSpId,
            scope       = "DeviceManagementConfiguration.ReadWrite.All DeviceManagementManagedDevices.Read.All"
        };

        var grantResp = await PostJsonAsync($"{GraphBase}/oauth2PermissionGrants", grantPayload);
        if (grantResp.IsSuccessStatusCode)
            progress?.Report("✅ Admin consent granted.");
        else
        {
            var grantErr = await grantResp.Content.ReadAsStringAsync();
            progress?.Report(
                $"⚠️ Admin consent could not be auto-granted — please grant it manually " +
                $"in Azure Portal > API Permissions > Grant admin consent. " +
                $"({(int)grantResp.StatusCode}: {TrimError(grantErr)})");
        }

        progress?.Report("✅ Setup complete!");
        return new AppRegistrationResult(clientId, tenantId, _authResult.Account.Username);
    }

    // ── Helpers ───────────────────────────────────────────────

    /// <summary>
    /// Patches an existing app registration to add http://localhost as a redirect URI.
    /// Call after SignInAdminAsync so the bearer token is available.
    /// </summary>
    public async Task FixRedirectUriAsync(string clientId, IntPtr parentHwnd, IProgress<string>? progress = null)
    {
        // Re-authenticate if the bootstrap token has expired
        if (_authResult == null || _authResult.ExpiresOn <= DateTimeOffset.UtcNow)
        {
            progress?.Report("Signing in to patch app registration...");
            await SignInAdminAsync(parentHwnd);
        }

        progress?.Report($"Looking up app registration {clientId}...");
        var searchResp = await _http.GetAsync(
            $"{GraphBase}/applications?$filter=appId eq '{clientId}'&$select=id,publicClient");
        await EnsureSuccessAsync(searchResp, "find app registration");

        var searchDoc = JsonDocument.Parse(await searchResp.Content.ReadAsStringAsync());
        var values    = searchDoc.RootElement.GetProperty("value");

        if (values.GetArrayLength() == 0)
            throw new InvalidOperationException(
                $"App registration with Client ID '{clientId}' was not found in this tenant. " +
                "Make sure you sign in with the same tenant where the app was created.");

        var objectId = values[0].GetProperty("id").GetString()!;

        // Merge existing redirect URIs with http://localhost
        var existing = values[0].TryGetProperty("publicClient", out var pc) &&
                       pc.TryGetProperty("redirectUris", out var uris)
            ? uris.EnumerateArray().Select(u => u.GetString()!).ToList()
            : [];

        if (existing.Contains("http://localhost"))
        {
            progress?.Report("✅ http://localhost is already registered – no change needed.");
            return;
        }

        existing.Add("http://localhost");

        progress?.Report("Patching app registration to add http://localhost...");
        var patch        = new { publicClient = new { redirectUris = existing } };
        var patchContent = new StringContent(JsonSerializer.Serialize(patch), Encoding.UTF8, "application/json");
        var patchReq     = new HttpRequestMessage(new HttpMethod("PATCH"),
                               $"{GraphBase}/applications/{objectId}") { Content = patchContent };
        var patchResp    = await _http.SendAsync(patchReq);
        await EnsureSuccessAsync(patchResp, "patch redirect URIs");

        progress?.Report("✅ Redirect URI fixed. You can now connect to Intune.");
    }

    private Task<HttpResponseMessage> PostJsonAsync<T>(string url, T payload)    {
        var json    = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return _http.PostAsync(url, content);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage resp, string operation)
    {
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Graph API failed to {operation} " +
                $"({(int)resp.StatusCode} {resp.StatusCode}): {TrimError(body)}");
        }
    }

    private static string TrimError(string body)
    {
        try
        {
            var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err) &&
                err.TryGetProperty("message", out var msg))
                return msg.GetString() ?? body;
        }
        catch { /* not JSON */ }
        return body.Length > 200 ? body[..200] : body;
    }
}
