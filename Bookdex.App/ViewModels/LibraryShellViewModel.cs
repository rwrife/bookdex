using System.Collections.ObjectModel;
using Bookdex.App.Services;
using Bookdex.Core.Catalog;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bookdex.App.ViewModels;

public partial class LibraryShellViewModel : ViewModelBase
{
    private readonly ISearchService _searchService;
    private readonly ILibraryScanService _scanService;

    private IReadOnlyList<CatalogSearchResult> _currentResults = [];

    public LibraryShellViewModel(ISearchService searchService, ILibraryScanService scanService)
    {
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        _scanService = scanService ?? throw new ArgumentNullException(nameof(scanService));

        AddLibraryFolderCommand = new AsyncRelayCommand(AddLibraryFolderAsync, CanScan);
        SortOptions = ["Relevance", "Title (A-Z)", "Author (A-Z)"];
    }

    public ObservableCollection<LibraryBookCardViewModel> SearchResults { get; } = [];

    public IReadOnlyList<string> SortOptions { get; }

    public IAsyncRelayCommand AddLibraryFolderCommand { get; }

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string selectedSortOption = "Relevance";

    [ObservableProperty]
    private string libraryFolderPath = GetDefaultLibraryFolderPath();

    [ObservableProperty]
    private string resultSummary = "Search your local catalog to begin.";

    [ObservableProperty]
    private string statusMessage = "Ready. Add a library folder to scan local files.";

    [ObservableProperty]
    private bool isScanning;

    [ObservableProperty]
    private bool isScanningIndeterminate;

    [ObservableProperty]
    private double scanProgressPercent;

    [ObservableProperty]
    private LibraryBookCardViewModel? selectedResult;

    partial void OnSearchTextChanged(string value)
    {
        RunSearch(value);
    }

    partial void OnSelectedSortOptionChanged(string value)
    {
        ApplySortAndProjection();
    }

    partial void OnLibraryFolderPathChanged(string value)
    {
        AddLibraryFolderCommand.NotifyCanExecuteChanged();
    }

    private bool CanScan()
    {
        return !IsScanning && !string.IsNullOrWhiteSpace(LibraryFolderPath);
    }

    private void RunSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            _currentResults = [];
            SearchResults.Clear();
            SelectedResult = null;
            ResultSummary = "Search your local catalog to begin.";
            return;
        }

        try
        {
            _currentResults = _searchService.Search(query, limit: 250);
            ApplySortAndProjection();
            ResultSummary = $"{SearchResults.Count} result(s) for \"{query.Trim()}\".";
            StatusMessage = "Search complete.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Search failed: {ex.Message}";
        }
    }

    private async Task AddLibraryFolderAsync()
    {
        if (string.IsNullOrWhiteSpace(LibraryFolderPath))
        {
            StatusMessage = "Enter a folder path first.";
            return;
        }

        if (!Directory.Exists(LibraryFolderPath))
        {
            StatusMessage = $"Folder not found: {LibraryFolderPath}";
            return;
        }

        IsScanning = true;
        IsScanningIndeterminate = true;
        ScanProgressPercent = 0;
        AddLibraryFolderCommand.NotifyCanExecuteChanged();

        var progress = new Progress<LibraryScanProgress>(scanProgress =>
        {
            IsScanningIndeterminate = scanProgress.TotalFiles == 0;
            ScanProgressPercent = scanProgress.TotalFiles == 0
                ? 0
                : 100d * scanProgress.ProcessedFiles / scanProgress.TotalFiles;

            StatusMessage = $"Scanning {scanProgress.ProcessedFiles}/{scanProgress.TotalFiles} • " +
                            $"Indexed {scanProgress.IndexedFiles}, Skipped {scanProgress.SkippedFiles}, Failed {scanProgress.FailedFiles}";
        });

        try
        {
            var result = await _scanService.ScanFolderAsync(LibraryFolderPath, progress);
            StatusMessage = $"Scan complete for {result.FolderPath}. Indexed {result.IndexedFiles} of {result.TotalFiles} supported file(s). " +
                            $"Skipped {result.SkippedFiles}, Failed {result.FailedFiles}.";

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                RunSearch(SearchText);
            }
            else
            {
                ResultSummary = "Scan complete. Enter a search query to browse indexed books.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            IsScanningIndeterminate = false;
            AddLibraryFolderCommand.NotifyCanExecuteChanged();
        }
    }

    private void ApplySortAndProjection()
    {
        IEnumerable<CatalogSearchResult> sorted = SelectedSortOption switch
        {
            "Title (A-Z)" => _currentResults
                .OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Authors, StringComparer.OrdinalIgnoreCase),
            "Author (A-Z)" => _currentResults
                .OrderBy(x => x.Authors, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase),
            _ => _currentResults.OrderBy(x => x.Rank),
        };

        SearchResults.Clear();
        foreach (var result in sorted)
        {
            SearchResults.Add(LibraryBookCardViewModel.FromSearchResult(result));
        }

        if (SearchResults.Count > 0 && (SelectedResult is null || !SearchResults.Contains(SelectedResult)))
        {
            SelectedResult = SearchResults[0];
        }
        else if (SearchResults.Count == 0)
        {
            SelectedResult = null;
        }
    }

    private static string GetDefaultLibraryFolderPath()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documents, "Books");
    }
}
