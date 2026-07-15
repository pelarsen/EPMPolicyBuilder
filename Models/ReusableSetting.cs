namespace EPMPolicyBuilder.Models;

public class ReusableSetting
{
    public string Id              { get; set; } = string.Empty;
    public string DisplayName     { get; set; } = string.Empty;
    public string Description     { get; set; } = string.Empty;

    // Graph timestamps
    public DateTimeOffset? CreatedDateTime      { get; set; }
    public DateTimeOffset? LastModifiedDateTime { get; set; }

    // X.509 certificate metadata (parsed from the base64 cert payload)
    public string? CertSubject    { get; set; }
    public string? CertIssuer     { get; set; }
    public string? CertThumbprint { get; set; }
    public DateTime? CertNotBefore { get; set; }
    public DateTime? CertNotAfter  { get; set; }

    // ── Computed display helpers ─────────────────────────────────────────
    public bool HasCertInfo => !string.IsNullOrEmpty(CertThumbprint);

    public bool IsExpired   => CertNotAfter.HasValue && CertNotAfter.Value < DateTime.UtcNow;
    public bool ExpiresSoon => !IsExpired && CertNotAfter.HasValue
                               && CertNotAfter.Value < DateTime.UtcNow.AddDays(30);

    public string ExpiryLabel => CertNotAfter.HasValue
        ? CertNotAfter.Value.ToLocalTime().ToString("dd MMM yyyy")
        : "Unknown";

    public string ValidFromLabel => CertNotBefore.HasValue
        ? CertNotBefore.Value.ToLocalTime().ToString("dd MMM yyyy")
        : "Unknown";

    public string CreatedLabel => CreatedDateTime.HasValue
        ? CreatedDateTime.Value.LocalDateTime.ToString("dd MMM yyyy HH:mm")
        : string.Empty;

    public string ExpiryStatusLabel =>
        IsExpired   ? "⚠ Expired"    :
        ExpiresSort ? $"⚠ Expires soon ({ExpiryLabel})" :
                      $"Valid until {ExpiryLabel}";

    private bool ExpiresSort => ExpiresSoon;

    public string ShortThumbprint => CertThumbprint is { Length: > 8 }
        ? CertThumbprint[..8].ToUpperInvariant() + "…"
        : CertThumbprint?.ToUpperInvariant() ?? string.Empty;
}
