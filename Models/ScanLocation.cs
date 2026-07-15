using CommunityToolkit.Mvvm.ComponentModel;

namespace EPMPolicyBuilder.Models;

public partial class ScanLocation : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}
