namespace EPMPolicyBuilder.Models;

public class ElevationSuggestion
{
    public string FileName       { get; set; } = string.Empty;
    public string Publisher      { get; set; } = string.Empty;
    public string FileVersion    { get; set; } = string.Empty;
    public string ProductName    { get; set; } = string.Empty;
    public int    ElevationCount { get; set; }

    /// <summary>Friendly display name — ProductName if available, otherwise FileName.</summary>
    public string DisplayName    => !string.IsNullOrWhiteSpace(ProductName) ? ProductName : FileName;
    public string CountLabel     => ElevationCount == 1 ? "1 elevation" : $"{ElevationCount} elevations";
}
