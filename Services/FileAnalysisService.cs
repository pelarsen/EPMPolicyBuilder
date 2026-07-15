using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Diagnostics;
using EPMPolicyBuilder.Models;

namespace EPMPolicyBuilder.Services;

public class FileAnalysisService
{
    public FileMetadata AnalyzeFile(string filePath)
    {
        var fi = new FileInfo(filePath);
        var hash = ComputeSha256(filePath);
        var versionInfo = FileVersionInfo.GetVersionInfo(filePath);

        var metadata = new FileMetadata
        {
            FileName = fi.Name,
            FilePath = filePath,
            FileHash = hash,
            ProductName = versionInfo.ProductName ?? string.Empty,
            InternalName = versionInfo.InternalName ?? string.Empty,
            FileVersion = versionInfo.FileVersion ?? string.Empty,
            FileDescription = versionInfo.FileDescription ?? string.Empty,
        };

        ExtractAuthenticodeCert(filePath, metadata);
        return metadata;
    }

    /// <summary>
    /// Reads the Authenticode signing certificate from a PE/MSI/PS1 file,
    /// populates the cert fields on <paramref name="metadata"/> and writes
    /// a .cer file to the system temp folder for use in policy rules.
    /// </summary>
    internal static void ExtractAuthenticodeCert(string filePath, FileMetadata metadata)
    {
        try
        {
            // CreateFromSignedFile reads the Authenticode cert from a PE/MSI file.
            using var rawCert = X509Certificate.CreateFromSignedFile(filePath);
            using var cert2 = new X509Certificate2(rawCert);

            metadata.PublisherName = cert2.GetNameInfo(X509NameType.SimpleName, false);
            metadata.CertSubject = cert2.Subject;
            metadata.CertIssuer = cert2.Issuer;
            metadata.CertThumbprint = cert2.Thumbprint;
            metadata.CertValidTo = cert2.NotAfter;

            // Export the DER-encoded certificate to a temp .cer file so it can be
            // used directly as the rule's certificate without manual browsing.
            var certDir = Path.Combine(Path.GetTempPath(), "EPMPolicyBuilder_Certs");
            Directory.CreateDirectory(certDir);
            var certPath = Path.Combine(certDir, $"{cert2.Thumbprint}.cer");
            if (!File.Exists(certPath))
                File.WriteAllBytes(certPath, cert2.Export(X509ContentType.Cert));
            metadata.DetectedCertPath = certPath;
        }
        catch
        {
            // File is unsigned or cert cannot be read — leave cert fields empty.
        }
    }

    private static string ComputeSha256(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToUpperInvariant();
    }
}
