using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using EPMPolicyBuilder.Models;

namespace EPMPolicyBuilder.Services;

public class PolicyJsonBuilder
{
    // EPM uses the literal string "{elevationrulename}" as the placeholder in all setting definition IDs.
    private const string P = "device_vendor_msft_policy_privilegemanagement_elevationrules_{elevationrulename}";

    // Group-level template IDs — overwritten by LoadTemplateRefs() after Graph lookup.
    private string _groupInstanceTemplateId = "ee3d2e5f-6b3d-4cb1-af9b-37b02d3dbae2";
    private string _groupValueTemplateId    = "0b1d415a-a4f1-4be4-a1bf-ae3137ec9450";

    // Per-field template IDs keyed by full settingDefinitionId.
    // Populated with safe hardcoded fallbacks; replaced by LoadTemplateRefs() when Graph lookup succeeds.
    private Dictionary<string, (string InstanceId, string ValueId)> _fieldTemplates;

    public PolicyJsonBuilder()
    {
        _fieldTemplates = BuildFallbackTemplates();
    }

    /// <summary>Called by GraphService after fetching live template IDs from the Graph template endpoint.</summary>
    public void LoadTemplateRefs(EpmTemplateRefs refs)
    {
        _groupInstanceTemplateId = refs.GroupInstanceTemplateId;
        _groupValueTemplateId    = refs.GroupValueTemplateId;
        _fieldTemplates          = new Dictionary<string, (string, string)>(refs.Settings);
    }

    /// <summary>Returns a pretty-printed JSON string for UI preview. Built as first-rule (template refs included).</summary>
    public string BuildRuleJson(ElevationRule rule) =>
        BuildSettingNode(rule, isFirstRule: true)
            .ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    /// <summary>
    /// Returns the full settings[0] object  { "@odata.type": "...Setting", "settingInstance": {...} }
    /// ready to be placed in the policy "settings" array.
    /// </summary>
    public JsonObject BuildSettingNode(ElevationRule rule, bool isFirstRule)
    {
        JsonObject? instanceTemplateRef = isFirstRule
            ? new JsonObject { ["settingInstanceTemplateId"] = _groupInstanceTemplateId }
            : null;

        return new JsonObject
        {
            ["@odata.type"] = "#microsoft.graph.deviceManagementConfigurationSetting",
            ["settingInstance"] = new JsonObject
            {
                ["@odata.type"] = "#microsoft.graph.deviceManagementConfigurationGroupSettingCollectionInstance",
                ["settingDefinitionId"] = P,
                ["settingInstanceTemplateReference"] = instanceTemplateRef,
                ["groupSettingCollectionValue"] = new JsonArray { BuildGroupValueNode(rule, isFirstRule) }
            }
        };
    }

    /// <summary>
    /// Returns a single groupSettingCollectionValue item  { "settingValueTemplateReference": ..., "children": [...] }.
    /// Use isFirstRule=true only for the very first rule in a brand-new policy.
    /// </summary>
    public JsonObject BuildGroupValueNode(ElevationRule rule, bool isFirstRule)
    {
        var children = new JsonArray();
        var fm = rule.FileMetadata;

        if (!string.IsNullOrWhiteSpace(fm?.FileHash))
            children.Add(StrChild($"{P}_filehash", fm!.FileHash, isFirstRule));

        // _ruletype with validation children nested inside choiceSettingValue.children
        string ruleTypeValue = rule.ElevationType switch
        {
            ElevationType.UserConfirmed        => $"{P}_self",
            ElevationType.Automatic            => $"{P}_automatic",
            ElevationType.Deny                 => $"{P}_deny",
            ElevationType.SupportApproved      => $"{P}_supportarbitrated",
            ElevationType.ElevateAsCurrentUser => $"{P}_userconfirmeduser",
            _                                  => $"{P}_self"
        };
        children.Add(ChoiceChild($"{P}_ruletype", ruleTypeValue, BuildValidationChildren(rule), isFirstRule));

        if (!string.IsNullOrWhiteSpace(fm?.FileName))
            children.Add(StrChild($"{P}_filename", fm!.FileName, isFirstRule));

        if (rule.SignatureSource != SignatureSource.NotConfigured)
            AddSignatureChild(children, rule, isFirstRule);

        string childProcValue = rule.ChildProcessBehavior switch
        {
            ChildProcessBehavior.RequireRule   => $"{P}_allowrunelevatedrulerequired",
            ChildProcessBehavior.DenyAll       => $"{P}_deny",
            ChildProcessBehavior.AllowElevated => $"{P}_allowrunelevated",
            _                                  => $"{P}_allowrunelevatedrulerequired"
        };
        children.Add(ChoiceChild($"{P}_childprocessbehavior", childProcValue, null, isFirstRule));

        children.Add(StrChild($"{P}_name", rule.RuleName, isFirstRule));

        if (!string.IsNullOrWhiteSpace(rule.RuleDescription))
            children.Add(StrChild($"{P}_description", rule.RuleDescription, isFirstRule));

        if (!string.IsNullOrWhiteSpace(fm?.FilePath))
            children.Add(StrChild($"{P}_filepath", fm!.FilePath, isFirstRule));

        if (!string.IsNullOrWhiteSpace(fm?.ProductName))
            children.Add(StrChild($"{P}_productname", fm!.ProductName, isFirstRule));

        if (!string.IsNullOrWhiteSpace(fm?.InternalName))
            children.Add(StrChild($"{P}_internalname", fm!.InternalName, isFirstRule));

        if (!string.IsNullOrWhiteSpace(fm?.FileVersion))
            children.Add(StrChild($"{P}_fileversion", fm!.FileVersion, isFirstRule));

        if (!string.IsNullOrWhiteSpace(fm?.FileDescription))
            children.Add(StrChild($"{P}_filedescription", fm!.FileDescription, isFirstRule));

        // _appliesto is always "all users"
        children.Add(ChoiceChild($"{P}_appliesto", $"{P}_allusers", null, isFirstRule));

        JsonObject? groupValueTemplateRef = isFirstRule
            ? new JsonObject { ["settingValueTemplateId"] = _groupValueTemplateId }
            : null;

        return new JsonObject
        {
            ["settingValueTemplateReference"] = groupValueTemplateRef,
            ["children"] = children
        };
    }

    // ── Validation children (nested inside _ruletype choiceSettingValue.children) ─────────────

    private static JsonArray BuildValidationChildren(ElevationRule rule)
    {
        var arr = new JsonArray();

        if (rule.ElevationType == ElevationType.UserConfirmed)
        {
            var values = new JsonArray();
            if (rule.ValidationBusinessJustification) values.Add(ChoiceCollectionValue($"{P}_ruletype_validation_0"));
            if (rule.ValidationWindowsAuthentication)  values.Add(ChoiceCollectionValue($"{P}_ruletype_validation_1"));
            if (values.Count > 0)
                arr.Add(ChoiceCollectionInstance($"{P}_ruletype_validation", values));
        }
        else if (rule.ElevationType == ElevationType.ElevateAsCurrentUser)
        {
            if (rule.ValidationWindowsAuthentication)
            {
                arr.Add(ChoiceCollectionInstance($"{P}_ruletype_userconfirmeduservalidation",
                    new JsonArray { ChoiceCollectionValue($"{P}_ruletype_userconfirmeduservalidation_0") }));
            }
            if (rule.ValidationBusinessJustification)
            {
                arr.Add(ChoiceCollectionInstance($"{P}_ruletype_userconfirmeduservalidation_2",
                    new JsonArray { ChoiceCollectionValue($"{P}_ruletype_userconfirmeduservalidation_2_0") }));
            }
        }

        return arr;
    }

    // ── Signature children ────────────────────────────────────────────────────────────────────

    private void AddSignatureChild(JsonArray children, ElevationRule rule, bool isFirstRule)
    {
        var sigChildren = new JsonArray();

        if (rule.SignatureSource == SignatureSource.ReusableCertificate && !string.IsNullOrWhiteSpace(rule.ReusableCertificateId))
        {
            sigChildren.Add(new JsonObject
            {
                ["@odata.type"] = "#microsoft.graph.deviceManagementConfigurationSimpleSettingInstance",
                ["settingDefinitionId"] = $"{P}_certificatepayloadwithreusablesetting",
                ["settingInstanceTemplateReference"] = null,
                ["simpleSettingValue"] = new JsonObject
                {
                    ["@odata.type"] = "#microsoft.graph.deviceManagementConfigurationReferenceSettingValue",
                    ["settingValueTemplateReference"] = null,
                    ["note"] = null,
                    ["value"] = rule.ReusableCertificateId
                }
            });
        }
        else if (rule.SignatureSource == SignatureSource.UploadCertificate && !string.IsNullOrWhiteSpace(rule.UploadedCertificatePath))
        {
            string certBase64;
            try
            {
                certBase64 = Convert.ToBase64String(File.ReadAllBytes(rule.UploadedCertificatePath));
            }
            catch
            {
                // If reading fails (e.g. already a base64 string), use as-is.
                certBase64 = rule.UploadedCertificatePath;
            }

            sigChildren.Add(new JsonObject
            {
                ["@odata.type"] = "#microsoft.graph.deviceManagementConfigurationSimpleSettingInstance",
                ["settingDefinitionId"] = $"{P}_certificatefileupload",
                ["settingInstanceTemplateReference"] = null,
                ["simpleSettingValue"] = new JsonObject
                {
                    ["@odata.type"] = "#microsoft.graph.deviceManagementConfigurationStringSettingValue",
                    ["settingValueTemplateReference"] = null,
                    ["value"] = certBase64
                }
            });
        }

        sigChildren.Add(ChoiceChild($"{P}_certificatetype", $"{P}_publisher", null, isFirstRule));

        string sigSourceValue = rule.SignatureSource == SignatureSource.ReusableCertificate
            ? $"{P}_signaturesource_0"
            : $"{P}_signaturesource_1";

        children.Add(ChoiceChild($"{P}_signaturesource", sigSourceValue, sigChildren, isFirstRule));
    }

    // ── Child helpers — children inside groupSettingCollectionValue are bare @odata.type objects,
    //   NOT wrapped in an outer { "settingInstance": {...} } envelope. ──────────────────────────

    private JsonObject StrChild(string defId, string value, bool isFirstRule)
    {
        var (instRef, valRef) = TemplateRefs(defId, isFirstRule);
        return new JsonObject
        {
            ["@odata.type"] = "#microsoft.graph.deviceManagementConfigurationSimpleSettingInstance",
            ["settingDefinitionId"] = defId,
            ["settingInstanceTemplateReference"] = instRef,
            ["simpleSettingValue"] = new JsonObject
            {
                ["@odata.type"] = "#microsoft.graph.deviceManagementConfigurationStringSettingValue",
                ["settingValueTemplateReference"] = valRef,
                ["value"] = value
            }
        };
    }

    private JsonObject ChoiceChild(string defId, string value, JsonArray? children, bool isFirstRule)
    {
        var (instRef, valRef) = TemplateRefs(defId, isFirstRule);
        return new JsonObject
        {
            ["@odata.type"] = "#microsoft.graph.deviceManagementConfigurationChoiceSettingInstance",
            ["settingDefinitionId"] = defId,
            ["settingInstanceTemplateReference"] = instRef,
            ["choiceSettingValue"] = new JsonObject
            {
                ["children"] = children ?? new JsonArray(),
                ["settingValueTemplateReference"] = valRef,
                ["value"] = value
            }
        };
    }

    private (JsonObject? instRef, JsonObject? valRef) TemplateRefs(string defId, bool isFirstRule)
    {
        if (!isFirstRule || !_fieldTemplates.TryGetValue(defId, out var t))
            return (null, null);
        return (
            new JsonObject { ["settingInstanceTemplateId"] = t.InstanceId },
            new JsonObject { ["useTemplateDefault"] = false, ["settingValueTemplateId"] = t.ValueId }
        );
    }

    private static JsonObject ChoiceCollectionInstance(string defId, JsonArray values) => new()
    {
        ["@odata.type"] = "#microsoft.graph.deviceManagementConfigurationChoiceSettingCollectionInstance",
        ["settingDefinitionId"] = defId,
        ["settingInstanceTemplateReference"] = null,
        ["choiceSettingCollectionValue"] = values
    };

    private static JsonObject ChoiceCollectionValue(string value) => new()
    {
        ["@odata.type"] = "#microsoft.graph.deviceManagementConfigurationChoiceSettingValue",
        ["children"] = new JsonArray(),
        ["settingValueTemplateReference"] = null,
        ["value"] = value
    };

    // ── Hardcoded fallback template IDs (from official EPM template, verified against PS reference) ─

    private static Dictionary<string, (string InstanceId, string ValueId)> BuildFallbackTemplates() => new()
    {
        [$"{P}_filehash"]        = ("e4436e2c-1584-4fba-8e38-78737cbbbfdf", "1adcc6f7-9fa4-4ce3-8941-2ce22cf5e404"),
        [$"{P}_ruletype"]        = ("bc5a31ac-95b5-4ec6-be1f-50a384bb165f", "cb2ea689-ebc3-42ea-a7a4-c704bb13e3ad"),
        [$"{P}_filename"]        = ("0c1ceb2b-bbd4-46d4-9ba5-9ee7abe1f094", "a165327c-f0e5-4c7d-9af1-d856b02191f7"),
        [$"{P}_name"]            = ("fdabfcf9-afa4-4dbf-a4ef-d5c1549065e1", "03f003e5-43ef-4e7e-bf30-57f00781fdcc"),
        [$"{P}_description"]     = ("b3714f3a-ead8-4682-a16f-ffa264c9d58f", "5e82a1e9-ef4f-43ea-8031-93aace2ad14d"),
        [$"{P}_appliesto"]       = ("0cde1c42-c701-44b1-94b7-438dd4536128", "2ec26569-c08f-434c-af3d-a50ac4a1ce26"),
        [$"{P}_filepath"]        = ("c3b7fda4-db6a-421d-bf04-d485e9d0cfb1", "f011bcfc-03cd-4b28-a1f4-305278d7a030"),
        [$"{P}_signaturesource"] = ("beb74dcd-b6c8-41b1-a95d-0874e96951e0", "437fac65-8d18-433c-bd51-8edd33038082"),
        [$"{P}_certificatetype"] = ("ea3b1b52-411c-4d89-80e9-c2289e59a97f", "e36c490a-62b5-4b8c-8b2a-f099fad4441b"),
        [$"{P}_fileversion"]     = ("4a36432a-ad41-4ea6-b69f-05655afc43d2", "9d5c2849-fa98-437c-92f6-abee4e9b8a0c"),
        [$"{P}_filedescription"] = ("5e10c5a9-d3ca-4684-b425-e52238cf3c8b", "df3081ea-4ea7-4f34-ac87-49b2e84d4c4b"),
        [$"{P}_productname"]     = ("234631a1-aeb1-436f-9e05-dcd9489caf08", "e466f96d-0633-40b3-86a4-9e093b696077"),
        [$"{P}_internalname"]    = ("08511f12-25b5-4218-812c-39a2db444ef1", "ec295dd4-6bbc-4fa8-a503-960784c53f41"),
    };
}
