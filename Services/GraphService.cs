using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EPMPolicyBuilder.Models;
using Microsoft.Identity.Client;

namespace EPMPolicyBuilder.Services;

public class GraphService
{
    private static readonly string[] Scopes =
    [
        "DeviceManagementConfiguration.ReadWrite.All",
        "DeviceManagementManagedDevices.Read.All"
    ];
    private const string GraphBase = "https://graph.microsoft.com/beta";

    private IPublicClientApplication? _pca;
    private AuthenticationResult? _authResult;
    private readonly HttpClient _http = new();
    private readonly PolicyJsonBuilder _jsonBuilder;

    public bool IsConnected => _authResult != null && _authResult.ExpiresOn > DateTimeOffset.UtcNow;
    public string? ConnectedUser => _authResult?.Account?.Username;
    public string? ConnectedTenant => _authResult?.TenantId;

    public GraphService(PolicyJsonBuilder jsonBuilder)
    {
        _jsonBuilder = jsonBuilder;
    }

    public void Configure(string clientId, string tenantId)
    {
        _pca = PublicClientApplicationBuilder
            .Create(clientId)
            .WithAuthority($"https://login.microsoftonline.com/{tenantId}")
            .WithRedirectUri("http://localhost")   // loopback – matches any port MSAL picks
            .Build();
    }

    /// <summary>parentHwnd is required for MSAL to parent the system browser correctly.</summary>
    public async Task<bool> SignInAsync(IntPtr parentHwnd)
    {
        if (_pca == null) throw new InvalidOperationException("GraphService not configured. Set Client ID first.");

        var accounts = await _pca.GetAccountsAsync();
        try
        {
            _authResult = await _pca.AcquireTokenSilent(Scopes, accounts.FirstOrDefault()).ExecuteAsync();
        }
        catch (MsalUiRequiredException)
        {
            _authResult = await _pca
                .AcquireTokenInteractive(Scopes)
                .WithParentActivityOrWindow(parentHwnd)
                .WithUseEmbeddedWebView(false)   // system browser via loopback – no WebView2 needed
                .ExecuteAsync();
        }
        return IsConnected;
    }

    public void SignOut()
    {
        _authResult = null;
    }

    private void SetAuthHeader()
    {
        if (_authResult == null) throw new InvalidOperationException("Not authenticated");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authResult.AccessToken);
    }

    /// <summary>Throws an <see cref="HttpRequestException"/> that includes the Graph API error body.</summary>
    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync();
        // Try to extract the Graph "message" field for a clean error string.
        string detail = body;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err) &&
                err.TryGetProperty("message", out var msg))
                detail = msg.GetString() ?? body;
        }
        catch { }
        throw new HttpRequestException(
            $"{(int)response.StatusCode} {response.ReasonPhrase}: {detail}",
            null, response.StatusCode);
    }

    public async Task<List<EpmPolicy>> GetEpmPoliciesAsync()
    {
        SetAuthHeader();
        string? url = $"{GraphBase}/deviceManagement/configurationPolicies?$select=id,name,templateReference&$filter=templateReference/templateFamily eq 'endpointSecurityEndpointPrivilegeManagement'";
        var policies = new List<EpmPolicy>();

        do
        {
            var response = await _http.GetFromJsonAsync<GraphListResponse<EpmPolicyRaw>>(url);
            if (response?.Value != null)
            {
                foreach (var p in response.Value)
                {
                    if (p.TemplateReference?.TemplateId?.StartsWith("cff02aad-51b1-498d-83ad-81161a393f56", StringComparison.OrdinalIgnoreCase) == true)
                        policies.Add(new EpmPolicy { Id = p.Id ?? string.Empty, Name = p.Name ?? string.Empty });
                }
            }
            url = response?.NextLink;
        } while (url != null);

        return policies;
    }

    public async Task<List<ReusableSetting>> GetReusableCertSettingsAsync()
    {
        SetAuthHeader();
        var url = $"{GraphBase}/deviceManagement/reusablePolicySettings"
                + "?$filter=settingDefinitionId eq 'device_vendor_msft_policy_privilegemanagement_reusablesettings_certificatefile'"
                + "&$expand=settingInstance";
        var response = await _http.GetFromJsonAsync<GraphListResponse<ReusableSettingRaw>>(url);
        if (response?.Value == null) return [];
        return response.Value.Select(MapReusableSetting).ToList();
    }

    public async Task<ReusableSetting> UploadCertificateAsync(string displayName, string description, string certFilePath)
    {
        SetAuthHeader();
        var certBytes = await System.IO.File.ReadAllBytesAsync(certFilePath);
        var certBase64 = Convert.ToBase64String(certBytes);

        var payload = new System.Text.Json.Nodes.JsonObject
        {
            ["displayName"] = displayName,
            ["description"] = description,
            ["settingDefinitionId"] = "device_vendor_msft_policy_privilegemanagement_reusablesettings_certificatefile",
            ["settingInstance"] = new System.Text.Json.Nodes.JsonObject
            {
                ["@odata.type"] = "#microsoft.graph.deviceManagementConfigurationSimpleSettingInstance",
                ["settingDefinitionId"] = "device_vendor_msft_policy_privilegemanagement_reusablesettings_certificatefile",
                ["simpleSettingValue"] = new System.Text.Json.Nodes.JsonObject
                {
                    ["@odata.type"] = "#microsoft.graph.deviceManagementConfigurationStringSettingValue",
                    ["value"] = certBase64
                }
            }
        };

        var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        var response = await _http.PostAsync($"{GraphBase}/deviceManagement/reusablePolicySettings", content);
        await EnsureSuccessAsync(response);
        var result = await response.Content.ReadFromJsonAsync<ReusableSettingRaw>();
        return MapReusableSetting(result ?? new ReusableSettingRaw { DisplayName = displayName, Description = description });
    }

    private static ReusableSetting MapReusableSetting(ReusableSettingRaw s)
    {
        var setting = new ReusableSetting
        {
            Id                   = s.Id          ?? string.Empty,
            DisplayName          = s.DisplayName ?? string.Empty,
            Description          = s.Description ?? string.Empty,
            CreatedDateTime      = s.CreatedDateTime,
            LastModifiedDateTime = s.LastModifiedDateTime,
        };

        // Attempt to parse the embedded X.509 certificate
        var certBase64 = s.SettingInstance?.SimpleSettingValue?.Value;
        if (!string.IsNullOrEmpty(certBase64))
        {
            try
            {
                var certBytes = Convert.FromBase64String(certBase64);
                using var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(certBytes);
                setting.CertSubject    = cert.Subject;
                setting.CertIssuer     = cert.Issuer;
                setting.CertThumbprint = cert.Thumbprint;
                setting.CertNotBefore  = cert.NotBefore.ToUniversalTime();
                setting.CertNotAfter   = cert.NotAfter.ToUniversalTime();
            }
            catch { /* cert parse failed — leave nulls, UI handles gracefully */ }
        }

        return setting;
    }

    public async Task<string> UploadRuleToPolicyAsync(string policyId, ElevationRule rule)
    {
        SetAuthHeader();

        if (string.IsNullOrWhiteSpace(rule.RuleName))
            throw new InvalidOperationException("Rule name cannot be empty. Set a name in Rule Builder before uploading.");

        // GET existing policy metadata (name, description, platforms, technologies, etc.)
        var metaUrl = $"{GraphBase}/deviceManagement/configurationPolicies/{policyId}";
        var metaResponse = await _http.GetAsync(metaUrl);
        await EnsureSuccessAsync(metaResponse);
        var metaNode = JsonNode.Parse(await metaResponse.Content.ReadAsStringAsync())!;

        // GET existing settings to extract the current groupSettingCollectionValue array (all existing rules)
        var settingsUrl = $"{GraphBase}/deviceManagement/configurationPolicies/{policyId}/settings";
        var settingsResponse = await _http.GetAsync(settingsUrl);
        await EnsureSuccessAsync(settingsResponse);
        var settingsDoc = JsonNode.Parse(await settingsResponse.Content.ReadAsStringAsync())!;

        var existingSettingInstance = settingsDoc["value"]?[0]?["settingInstance"];
        var existingRules = existingSettingInstance?["groupSettingCollectionValue"]?.AsArray()
                            ?? new JsonArray();

        // ── Pre-flight: check for duplicate rule name ─────────────────────────────────────────
        // Graph uses _name as the unique key for each rule in the collection. Sending a duplicate
        // causes a 400 "doesn't have unique replacement setting values for ..._name".
        const string nameDefId = "device_vendor_msft_policy_privilegemanagement_elevationrules_{elevationrulename}_name";
        foreach (var existingRule in existingRules)
        {
            var children = existingRule?["children"]?.AsArray();
            if (children == null) continue;
            foreach (var child in children)
            {
                if (child?["settingDefinitionId"]?.GetValue<string>() == nameDefId)
                {
                    var existingName = child["simpleSettingValue"]?["value"]?.GetValue<string>() ?? string.Empty;
                    if (string.Equals(existingName, rule.RuleName, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(
                            $"A rule named \"{rule.RuleName}\" already exists in this policy.\n\n" +
                            "Rule names must be unique within a policy. Please change the name in Rule Builder and try again.\n\n" +
                            "Note: if a previous upload attempt returned a server error (500), the rule may have been " +
                            "added to the policy despite the error — check the policy in Intune before retrying.");
                }
            }
        }

        // Copy the root template reference exactly as the policy was created — Graph needs it.
        var rootTemplateRef = existingSettingInstance?["settingInstanceTemplateReference"]?.DeepClone();

        // Append the new rule — NOT the first rule in the policy, so no template refs.
        var newRuleItem = _jsonBuilder.BuildGroupValueNode(rule, isFirstRule: false);
        var allRules = new JsonArray();
        // Preserve existing rules verbatim — Graph requires their template refs to stay intact.
        foreach (var r in existingRules) allRules.Add(r!.DeepClone());
        allRules.Add(newRuleItem);

        // PUT the full policy body with the updated rules list.
        var putPayload = new JsonObject
        {
            ["name"]              = metaNode["name"]?.GetValue<string>() ?? string.Empty,
            ["description"]       = metaNode["description"]?.GetValue<string>() ?? string.Empty,
            ["platforms"]         = metaNode["platforms"]?.GetValue<string>() ?? "windows10",
            ["technologies"]      = metaNode["technologies"]?.GetValue<string>() ?? "endpointPrivilegeManagement",
            ["roleScopeTagIds"]   = metaNode["roleScopeTagIds"]?.DeepClone() ?? new JsonArray { JsonValue.Create("0") },
            ["templateReference"] = metaNode["templateReference"]?.DeepClone(),
            ["settings"] = new JsonArray
            {
                new JsonObject
                {
                    ["@odata.type"] = "#microsoft.graph.deviceManagementConfigurationSetting",
                    ["settingInstance"] = new JsonObject
                    {
                        ["@odata.type"] = "#microsoft.graph.deviceManagementConfigurationGroupSettingCollectionInstance",
                        ["settingDefinitionId"] = "device_vendor_msft_policy_privilegemanagement_elevationrules_{elevationrulename}",
                        ["settingInstanceTemplateReference"] = rootTemplateRef,
                        ["groupSettingCollectionValue"] = allRules
                    }
                }
            }
        };

        var putJson    = putPayload.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        var putContent = new StringContent(putJson, Encoding.UTF8, "application/json");
        var putRequest = new HttpRequestMessage(HttpMethod.Put, metaUrl) { Content = putContent };
        var putResponse = await _http.SendAsync(putRequest);
        try
        {
            await EnsureSuccessAsync(putResponse);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("unique replacement setting values"))
        {
            // Graph rejects the PUT when two rules share the same _name value.
            // A previous upload that returned 500 may have silently written the rule.
            throw new EpmUploadException(
                $"A rule named \"{rule.RuleName}\" already exists in this policy.\n\n" +
                "Rule names must be unique within a policy. Please:\n" +
                "1. Open the policy in the Intune portal and check whether the rule was already added " +
                "(a previous upload may have succeeded despite showing a server error).\n" +
                "2. If the rule is already there, delete it — or change the rule name in Rule Builder and try again.",
                putJson, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new EpmUploadException(ex.Message, putJson, ex);
        }
        return await putResponse.Content.ReadAsStringAsync();
    }

    public async Task<string> CreatePolicyWithRuleAsync(string policyName, ElevationRule rule)
    {
        SetAuthHeader();

        var settingNode = _jsonBuilder.BuildSettingNode(rule, isFirstRule: true);

        var payload = new JsonObject
        {
            ["name"]            = policyName,
            ["description"]     = "",
            ["platforms"]       = "windows10",
            ["technologies"]    = "endpointPrivilegeManagement",
            ["roleScopeTagIds"] = new JsonArray { JsonValue.Create("0") },
            ["templateReference"] = new JsonObject
            {
                ["templateId"]     = "cff02aad-51b1-498d-83ad-81161a393f56_1",
                ["templateFamily"] = "endpointSecurityEndpointPrivilegeManagement"
            },
            ["settings"] = new JsonArray { settingNode }
        };

        var postJson = payload.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        var content  = new StringContent(postJson, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync($"{GraphBase}/deviceManagement/configurationPolicies", content);
        try
        {
            await EnsureSuccessAsync(response);
        }
        catch (HttpRequestException ex)
        {
            throw new EpmUploadException(ex.Message, postJson, ex);
        }
        return await response.Content.ReadAsStringAsync();
    }

    // ── Unmanaged elevation report ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fetches EPM unmanaged elevations from the privilegeManagementElevations OData endpoint,
    /// aggregates them by application (filePath + companyName + fileVersion), and returns
    /// results sorted by elevation count descending.
    /// </summary>
    public async Task<List<ElevationSuggestion>> GetUnmanagedElevationsAsync()
    {
        SetAuthHeader();

        // Page through all unmanaged elevation events (OData nextLink pagination)
        var allEvents = new List<JsonObject>();
        var url = $"{GraphBase}/deviceManagement/privilegeManagementElevations"
                + "?$filter=elevationType eq 'unmanagedElevation'"
                + "&$select=filePath,internalName,productName,companyName,fileVersion,fileDescription"
                + "&$top=999";

        while (!string.IsNullOrEmpty(url))
        {
            var response = await _http.GetAsync(url);
            await EnsureSuccessAsync(response);
            var json = await response.Content.ReadAsStringAsync();
            var doc  = JsonNode.Parse(json);

            var value = doc?["value"]?.AsArray();
            if (value != null)
                foreach (var item in value)
                    if (item is JsonObject obj) allEvents.Add(obj);

            url = doc?["@odata.nextLink"]?.GetValue<string>() ?? string.Empty;
        }

        // Aggregate by (filePath, companyName, fileVersion) — group key is filename+version+publisher
        var groups = new Dictionary<string, ElevationSuggestion>(StringComparer.OrdinalIgnoreCase);
        foreach (var ev in allEvents)
        {
            var filePath    = ev["filePath"]?.GetValue<string>()    ?? string.Empty;
            var internalName= ev["internalName"]?.GetValue<string>() ?? string.Empty;
            var productName = ev["productName"]?.GetValue<string>()  ?? string.Empty;
            var publisher   = ev["companyName"]?.GetValue<string>()  ?? string.Empty;
            var version     = ev["fileVersion"]?.GetValue<string>()  ?? string.Empty;

            // Use just the filename portion of filePath for display
            var fileName = System.IO.Path.GetFileName(filePath);
            if (string.IsNullOrEmpty(fileName)) fileName = internalName;

            var key = $"{fileName.ToLowerInvariant()}|{publisher.ToLowerInvariant()}|{version}";
            if (!groups.TryGetValue(key, out var entry))
            {
                entry = new ElevationSuggestion
                {
                    FileName    = fileName,
                    Publisher   = publisher,
                    FileVersion = version,
                    ProductName = !string.IsNullOrWhiteSpace(productName) ? productName : internalName,
                };
                groups[key] = entry;
            }
            entry.ElevationCount++;
        }

        var result = new List<ElevationSuggestion>(groups.Values);
        result.Sort((a, b) => b.ElevationCount.CompareTo(a.ElevationCount));
        return result;
    }

    // ── Template ref resolution ───────────────────────────────────────────────────────────────────

    private const string EpmTemplateId = "cff02aad-51b1-498d-83ad-81161a393f56_1";
    private const string ElevationRuleDefId =
        "device_vendor_msft_policy_privilegemanagement_elevationrules_{elevationrulename}";

    /// <summary>
    /// Fetches live template IDs from the Graph EPM policy template endpoint and passes them to
    /// PolicyJsonBuilder.LoadTemplateRefs(). Falls back silently to hardcoded IDs on any error.
    /// </summary>
    public async Task RefreshTemplateRefsAsync()
    {
        try
        {
            SetAuthHeader();
            var url = $"{GraphBase}/deviceManagement/configurationPolicyTemplates('{EpmTemplateId}')" +
                      "/settingTemplates?$expand=settingDefinitions&top=1000";
            var json = await _http.GetStringAsync(url);
            var refs = ParseTemplateRefs(json);
            _jsonBuilder.LoadTemplateRefs(refs);
        }
        catch
        {
            // Non-fatal: hardcoded fallback values remain in use.
        }
    }

    private static EpmTemplateRefs ParseTemplateRefs(string json)
    {
        const string groupFallbackInstanceId = "ee3d2e5f-6b3d-4cb1-af9b-37b02d3dbae2";
        const string groupFallbackValueId    = "0b1d415a-a4f1-4be4-a1bf-ae3137ec9450";

        string groupInstanceId = groupFallbackInstanceId;
        string groupValueId    = groupFallbackValueId;
        var settings           = new Dictionary<string, (string InstanceId, string ValueId)>();

        try
        {
            var doc   = JsonNode.Parse(json);
            var items = doc?["value"]?.AsArray();
            if (items == null) return new EpmTemplateRefs(groupInstanceId, groupValueId, settings);

            foreach (var item in items)
            {
                var sit = item?["settingInstanceTemplate"];
                if (sit == null) continue;

                var defId      = sit["settingDefinitionId"]?.GetValue<string>() ?? string.Empty;
                var instanceId = sit["settingInstanceTemplateId"]?.GetValue<string>() ?? string.Empty;
                var odataType  = sit["@odata.type"]?.GetValue<string>() ?? string.Empty;

                if (odataType.Contains("GroupSettingCollection") && defId == ElevationRuleDefId)
                {
                    // Root group collection — extract group-level template IDs and all field children.
                    if (!string.IsNullOrEmpty(instanceId)) groupInstanceId = instanceId;

                    var collTemplates = sit["groupSettingCollectionValueTemplate"]?.AsArray();
                    var first = collTemplates?.Count > 0 ? collTemplates![0] : null;
                    if (first != null)
                    {
                        var vid = first["settingValueTemplateId"]?.GetValue<string>();
                        if (!string.IsNullOrEmpty(vid)) groupValueId = vid!;
                        ExtractChildTemplates(first["children"]?.AsArray(), settings);
                    }
                }
                else if (!string.IsNullOrEmpty(defId) && !string.IsNullOrEmpty(instanceId))
                {
                    // Flat item (in case API returns field templates at top level).
                    settings[defId] = (instanceId, ExtractValueTemplateId(sit) ?? string.Empty);
                }
            }
        }
        catch { /* parsing failure — caller uses fallback */ }

        return new EpmTemplateRefs(groupInstanceId, groupValueId, settings);
    }

    private static void ExtractChildTemplates(
        JsonArray? children,
        Dictionary<string, (string InstanceId, string ValueId)> settings)
    {
        if (children == null) return;
        foreach (var child in children)
        {
            var defId      = child?["settingDefinitionId"]?.GetValue<string>() ?? string.Empty;
            var instanceId = child?["settingInstanceTemplateId"]?.GetValue<string>() ?? string.Empty;
            if (string.IsNullOrEmpty(defId) || string.IsNullOrEmpty(instanceId)) continue;
            settings[defId] = (instanceId, ExtractValueTemplateId(child) ?? string.Empty);
        }
    }

    private static string? ExtractValueTemplateId(JsonNode? node) =>
        node?["simpleSettingValueTemplate"]?["settingValueTemplateId"]?.GetValue<string>()
        ?? node?["choiceSettingValueTemplate"]?["settingValueTemplateId"]?.GetValue<string>()
        ?? node?["choiceSettingCollectionValueTemplate"]?["settingValueTemplateId"]?.GetValue<string>();

    // Internal raw types for deserialization
    private class GraphListResponse<T>
    {
        public List<T>? Value { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("@odata.nextLink")]
        public string? NextLink { get; set; }
    }
    private class EpmPolicyRaw
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public TemplateRef? TemplateReference { get; set; }
    }
    private class TemplateRef
    {
        public string? TemplateId { get; set; }
        public string? TemplateFamily { get; set; }
    }
    private class ReusableSettingRaw
    {
        public string?         Id                   { get; set; }
        public string?         DisplayName          { get; set; }
        public string?         Description          { get; set; }
        public DateTimeOffset? CreatedDateTime      { get; set; }
        public DateTimeOffset? LastModifiedDateTime { get; set; }
        public SettingInstance? SettingInstance     { get; set; }
    }
    private class SettingInstance
    {
        public SimpleSettingValue? SimpleSettingValue { get; set; }
    }
    private class SimpleSettingValue
    {
        public string? Value { get; set; }
    }
}
