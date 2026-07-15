using System.Security.Cryptography.X509Certificates;

namespace EPMPolicyBuilder.Services;

public static class CertificateReader
{
    public static (bool isSigned, string? subject, string? issuer, string? thumbprint, string? serialNumber, string? publisherName)
        ReadCertificate(string filePath)
    {
        try
        {
            // CreateFromSignedFile reads the Authenticode (WinVerifyTrust) embedded cert.
            // The SYSLIB0057 warning is suppressed here — there is no modern replacement
            // for reading the Authenticode signer cert from a PE file without P/Invoke.
#pragma warning disable SYSLIB0057
            var cert = X509Certificate.CreateFromSignedFile(filePath);
#pragma warning restore SYSLIB0057
            var cert2 = new X509Certificate2(cert);
            var subject = cert2.Subject;
            var publisherName = ExtractCN(subject);
            return (true, subject, cert2.Issuer, cert2.Thumbprint, cert2.SerialNumber, publisherName);
        }
        catch
        {
            return (false, null, null, null, null, null);
        }
    }

    private static string? ExtractCN(string? subject)
    {
        if (string.IsNullOrEmpty(subject)) return null;
        foreach (var part in subject.Split(','))
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                return trimmed[3..].Trim();
        }
        return subject;
    }
}
