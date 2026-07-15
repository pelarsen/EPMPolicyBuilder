using CommunityToolkit.Mvvm.ComponentModel;

namespace EPMPolicyBuilder.Models;

public partial class ExeFileInfo : ObservableObject
{
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string InternalName { get; set; } = "";
    public string FileDescription { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string FileVersion { get; set; } = "";
    public string ProductVersion { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string FileHash { get; set; } = "";
    public bool IsSigned { get; set; }
    public string? CertificateSubject { get; set; }
    public string? CertificateIssuer { get; set; }
    public string? CertificateThumbprint { get; set; }
    public string? CertificateSerialNumber { get; set; }
    public string? PublisherName { get; set; }

    public string SignedDisplay => IsSigned ? "✓" : "";
    public string PublisherDisplay => IsSigned
        ? (PublisherName ?? CertificateSubject ?? "Signed")
        : "Unsigned";
}
