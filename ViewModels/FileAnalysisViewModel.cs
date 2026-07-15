using CommunityToolkit.Mvvm.ComponentModel;
using EPMPolicyBuilder.Models;
using EPMPolicyBuilder.Services;

namespace EPMPolicyBuilder.ViewModels;

public partial class FileAnalysisViewModel : ObservableObject
{
    private readonly FileAnalysisService _fileAnalysisService;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private FileMetadata? _fileMetadata;
    [ObservableProperty] private string _selectedFilePath = string.Empty;
    [ObservableProperty] private bool _hasFileMetadata;

    public FileAnalysisViewModel(FileAnalysisService fileAnalysisService)
    {
        _fileAnalysisService = fileAnalysisService;
    }

    public void AnalyzeFile(string filePath)
    {
        SelectedFilePath = filePath;
        HasError = false;
        StatusMessage = string.Empty;
        try
        {
            FileMetadata = _fileAnalysisService.AnalyzeFile(filePath);
            HasFileMetadata = true;
            StatusMessage = "File analyzed successfully.";
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = $"Error analyzing file: {ex.Message}";
            HasFileMetadata = false;
        }
    }
}
