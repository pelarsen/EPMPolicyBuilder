namespace EPMPolicyBuilder.Models;

/// <summary>
/// Template IDs resolved from the Graph API EPM policy template endpoint.
/// Used by PolicyJsonBuilder to populate settingInstanceTemplateReference and
/// settingValueTemplateReference fields required by the first rule in a new policy.
/// </summary>
public record EpmTemplateRefs(
    string GroupInstanceTemplateId,
    string GroupValueTemplateId,
    IReadOnlyDictionary<string, (string InstanceId, string ValueId)> Settings);