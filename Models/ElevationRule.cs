namespace EPMPolicyBuilder.Models;

public class ElevationRule
{
    public string RuleName { get; set; } = string.Empty;
    public string RuleDescription { get; set; } = string.Empty;
    public ElevationType ElevationType { get; set; } = ElevationType.UserConfirmed;
    public ChildProcessBehavior ChildProcessBehavior { get; set; } = ChildProcessBehavior.RequireRule;
    public SignatureSource SignatureSource { get; set; } = SignatureSource.NotConfigured;
    public string? ReusableCertificateId { get; set; }
    public string? UploadedCertificatePath { get; set; }
    public bool ValidationBusinessJustification { get; set; }
    public bool ValidationWindowsAuthentication { get; set; }
    public FileMetadata? FileMetadata { get; set; }
}

public enum ElevationType
{
    UserConfirmed,
    Automatic,
    Deny,
    SupportApproved,
    ElevateAsCurrentUser
}

public enum ChildProcessBehavior
{
    RequireRule,
    DenyAll,
    AllowElevated
}

public enum SignatureSource
{
    NotConfigured,
    ReusableCertificate,
    UploadCertificate
}
