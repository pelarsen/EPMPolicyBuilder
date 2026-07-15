using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace EPMPolicyBuilder.Models;

public partial class RuleConfiguration : ObservableObject
{
    [ObservableProperty] public partial string RuleName { get; set; } = "";
    [ObservableProperty] public partial string RuleDescription { get; set; } = "";
    [ObservableProperty] public partial string FileName { get; set; } = "";
    [ObservableProperty] public partial string FilePath { get; set; } = "";
    [ObservableProperty] public partial string FileHash { get; set; } = "";
    [ObservableProperty] public partial string FileVersion { get; set; } = "";
    [ObservableProperty] public partial string FileDescription { get; set; } = "";
    [ObservableProperty] public partial string ProductName { get; set; } = "";
    [ObservableProperty] public partial string InternalName { get; set; } = "";
    [ObservableProperty] public partial bool IsSigned { get; set; }
    [ObservableProperty] public partial string PublisherName { get; set; } = "";

    /// <summary>Elevation type: UserConfirmed | Automatic | Deny | SupportApproved | ElevateAsCurrentUser</summary>
    [ObservableProperty] public partial string ElevationType { get; set; } = "UserConfirmed";

    [ObservableProperty] public partial bool ValidationBusinessJustification { get; set; }
    [ObservableProperty] public partial bool ValidationWindowsAuthentication { get; set; }

    [ObservableProperty] public partial bool ElevateAsUserValidationWindowsAuth { get; set; }
    [ObservableProperty] public partial bool ElevateAsUserValidationBusinessJustification { get; set; }

    /// <summary>Certificate source: NotConfigured | ReusableSetting | UploadPem | UploadAndCreateReusable</summary>
    [ObservableProperty] public partial string CertificateSource { get; set; } = "NotConfigured";
    [ObservableProperty] public partial string? SelectedReusableSettingId { get; set; }
    [ObservableProperty] public partial string? CertificatePemContent { get; set; }
    [ObservableProperty] public partial string? NewReusableSettingName { get; set; }
    /// <summary>Certificate type: publisher | root</summary>
    [ObservableProperty] public partial string CertificateType { get; set; } = "publisher";

    /// <summary>Child process behavior: AllowAll | RequireRule | Deny</summary>
    [ObservableProperty] public partial string ChildProcessBehavior { get; set; } = "AllowAll";

    /// <summary>Argument restriction: NotConfigured | AllowList</summary>
    [ObservableProperty] public partial string ArgumentRestriction { get; set; } = "NotConfigured";

    public ObservableCollection<string> ArgumentList { get; } = [];
}
