using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EPMPolicyBuilder.Models;
using EPMPolicyBuilder.Services;

namespace EPMPolicyBuilder.ViewModels;

public partial class ScannerViewModel : ObservableObject
{
    private readonly ExeScanner _scanner;
    private readonly FileAnalysisService _fileAnalysisService;
    private CancellationTokenSource? _cts;
    private List<ExeFileInfo> _allResults = [];
    private List<ExeFileInfo> _selectedItems = [];

    // ── UI-triggered events ──────────────────────────────────────────────────
    public event Action<FileMetadata?>? SendToRuleBuilderRequested;
    public event Action<List<ElevationRule>>? BatchUploadRequested;
    public event Action? SelectAllVisibleRequested;
    public event Action? ClearSelectionRequested;

    // ── Observable properties ────────────────────────────────────────────────
    [ObservableProperty] private string _customScanPath = string.Empty;
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private string _scanStatus = string.Empty;
    [ObservableProperty] private int _scannedCount;
    [ObservableProperty] private int _skippedFolderCount;
    [ObservableProperty] private bool _hasResults;
    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private int _selectedCount;
    [ObservableProperty] private bool _hasSelection;

    public ObservableCollection<ScanLocation> ScanLocations { get; } = [];
    public ObservableCollection<ExeFileInfo> FilteredResults { get; } = [];

    // ── Constructor ──────────────────────────────────────────────────────────
    public ScannerViewModel(ExeScanner scanner, FileAnalysisService fileAnalysisService)
    {
        _scanner = scanner;
        _fileAnalysisService = fileAnalysisService;
        SeedKnownFolders();
    }

    private void SeedKnownFolders()
    {
        var folders = new (string Name, string Path)[]
        {
            ("Program Files",       Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)),
            ("Program Files (x86)", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)),
            ("Windows",             Environment.GetFolderPath(Environment.SpecialFolder.Windows)),
            ("System32",            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32")),
            ("SysWOW64",            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64")),
            ("ProgramData",         Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)),
            ("Local AppData",       Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)),
        };

        foreach (var (name, path) in folders)
        {
            if (!Directory.Exists(path)) continue;
            ScanLocations.Add(new ScanLocation
            {
                Name = name,
                Path = path,
                IsSelected = name is "Program Files" or "Program Files (x86)"
            });
        }
    }

    // ── Filter ───────────────────────────────────────────────────────────────
    partial void OnFilterTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        FilteredResults.Clear();
        var filter = FilterText?.Trim();
        var source = string.IsNullOrEmpty(filter)
            ? _allResults
            : _allResults.Where(f =>
                f.FileName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                f.CompanyName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                f.FilePath.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                (f.PublisherName?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false));

        foreach (var item in source)
            FilteredResults.Add(item);
    }

    // ── Public helpers ────────────────────────────────────────────────────────
    public void SetSelectedItems(IEnumerable<ExeFileInfo> items)
    {
        _selectedItems = [.. items];
        SelectedCount = _selectedItems.Count;
        HasSelection = _selectedItems.Count > 0;
    }

    public void AddCustomPath(string path)
    {
        CustomScanPath = path;
        if (ScanLocations.Any(l => l.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
            return;

        var leafName = Path.GetFileName(path.TrimEnd('\\', '/'));
        ScanLocations.Add(new ScanLocation
        {
            Name = string.IsNullOrEmpty(leafName) ? path : leafName,
            Path = path,
            IsSelected = true
        });
    }

    // ── Commands ──────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task ScanAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        var selectedDirs = ScanLocations
            .Where(l => l.IsSelected)
            .Select(l => l.Path)
            .ToList();

        if (!string.IsNullOrWhiteSpace(CustomScanPath) &&
            !selectedDirs.Contains(CustomScanPath, StringComparer.OrdinalIgnoreCase))
            selectedDirs.Add(CustomScanPath);

        if (selectedDirs.Count == 0)
        {
            ScanStatus = "Select at least one folder to scan.";
            return;
        }

        IsScanning = true;
        ScanStatus = "Starting scan…";
        ScannedCount = 0;
        SkippedFolderCount = 0;
        _allResults = [];
        FilteredResults.Clear();
        HasResults = false;

        int cumCount = 0;
        int cumSkipped = 0;

        var progress = new Progress<(int count, string file, int skipped)>(report =>
        {
            ScannedCount = cumCount + report.count;
            ScanStatus = $"Scanning… {ScannedCount} files found — {Path.GetFileName(report.file)}";
        });

        try
        {
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var dir in selectedDirs)
            {
                if (!Directory.Exists(dir)) continue;

                ScanStatus = $"Scanning {dir}…";
                var (files, skipped) = await _scanner.ScanDirectoryAsync(dir, progress, skipInaccessible: true, _cts.Token);

                foreach (var f in files)
                    if (seenPaths.Add(f.FilePath))
                        _allResults.Add(f);

                cumCount   += files.Count;
                cumSkipped += skipped;
                SkippedFolderCount = cumSkipped;
            }

            ScannedCount = _allResults.Count;
            ApplyFilter();
            HasResults = _allResults.Count > 0;

            ScanStatus = $"Scan complete — {_allResults.Count} executable files found.";
            if (cumSkipped > 0)
                ScanStatus += $" ({cumSkipped} folders skipped due to access restrictions)";
        }
        catch (OperationCanceledException)
        {
            ApplyFilter();
            HasResults = _allResults.Count > 0;
            ScanStatus = $"Scan cancelled — {_allResults.Count} files found before cancellation.";
        }
        catch (Exception ex)
        {
            ScanStatus = $"Scan error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private void CancelScan() => _cts?.Cancel();

    [RelayCommand]
    private void SelectAllVisible() => SelectAllVisibleRequested?.Invoke();

    [RelayCommand]
    private void ClearSelection() => ClearSelectionRequested?.Invoke();

    [RelayCommand]
    private async Task SendToRuleBuilderAsync()
    {
        var first = _selectedItems.FirstOrDefault();
        if (first == null) return;

        var metadata = await Task.Run(() => _fileAnalysisService.AnalyzeFile(first.FilePath));
        SendToRuleBuilderRequested?.Invoke(metadata);
    }

    [RelayCommand]
    private async Task BatchCreateRulesAsync()
    {
        if (_selectedItems.Count == 0) return;

        var items = _selectedItems.ToList();
        var rules = new List<ElevationRule>();

        IsScanning = true;
        ScanStatus = $"Analyzing {items.Count} selected files…";

        try
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                ScanStatus = $"Analyzing {i + 1} of {items.Count}: {item.FileName}";

                var metadata = await Task.Run(() => _fileAnalysisService.AnalyzeFile(item.FilePath));

                var ruleName = !string.IsNullOrWhiteSpace(metadata.ProductName)
                    ? metadata.ProductName
                    : Path.GetFileNameWithoutExtension(metadata.FileName);

                rules.Add(new ElevationRule
                {
                    RuleName        = ruleName,
                    RuleDescription = $"Auto-generated rule for {item.FileName}",
                    ElevationType   = ElevationType.UserConfirmed,
                    ChildProcessBehavior = ChildProcessBehavior.RequireRule,
                    SignatureSource = metadata.HasDetectedCert
                        ? SignatureSource.UploadCertificate
                        : SignatureSource.NotConfigured,
                    UploadedCertificatePath = metadata.HasDetectedCert ? metadata.DetectedCertPath : null,
                    FileMetadata    = metadata
                });
            }

            ScanStatus = $"Ready — {rules.Count} rules built for upload.";
            BatchUploadRequested?.Invoke(rules);
        }
        catch (Exception ex)
        {
            ScanStatus = $"Error building rules: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }
}
