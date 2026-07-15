namespace EPMPolicyBuilder.Models;

public class FileMetadata
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string InternalName { get; set; } = string.Empty;
    public string FileVersion { get; set; } = string.Empty;
    public string FileDescription { get; set; } = string.Empty;
    public string PublisherName { get; set; } = string.Empty;

    // Authenticode / signing certificate (auto-detected from the file)
    public string CertSubject { get; set; } = string.Empty;
    public string CertIssuer { get; set; } = string.Empty;
    public string CertThumbprint { get; set; } = string.Empty;
    public DateTime? CertValidTo { get; set; }
    /// <summary>Path to the exported .cer file in the system temp folder.</summary>
    public string DetectedCertPath { get; set; } = string.Empty;

    public bool HasDetectedCert => !string.IsNullOrWhiteSpace(CertThumbprint);
}
