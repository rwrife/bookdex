using Bookdex.Core.Catalog;

namespace Bookdex.Core.Ai;

public sealed class LocalAiSearchOrchestrator
{
    private readonly ILibraryStore _libraryStore;
    private readonly IBookAiService _bookAiService;
    private readonly LocalAiSettings _settings;

    public LocalAiSearchOrchestrator(
        ILibraryStore libraryStore,
        IBookAiService bookAiService,
        LocalAiSettings settings)
    {
        _libraryStore = libraryStore ?? throw new ArgumentNullException(nameof(libraryStore));
        _bookAiService = bookAiService ?? throw new ArgumentNullException(nameof(bookAiService));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public async Task<LocalAiSearchOutcome> SearchAsync(
        string query,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || limit <= 0)
        {
            return new LocalAiSearchOutcome([], LocalAiSearchMode.Keyword, null);
        }

        var normalizedQuery = query.Trim();

        LocalAiSearchOutcome Keyword(LocalAiSearchMode mode, string? message = null) =>
            new(_libraryStore.Search(normalizedQuery, limit), mode, message);

        if (!_settings.Enabled)
        {
            return Keyword(LocalAiSearchMode.Keyword);
        }

        if (!_settings.TryGetValidatedEndpoint(out _, out var endpointError))
        {
            return Keyword(LocalAiSearchMode.KeywordFallback, endpointError);
        }

        try
        {
            var probe = await _bookAiService.ProbeAsync(cancellationToken);
            if (!probe.IsReachable)
            {
                return Keyword(LocalAiSearchMode.KeywordFallback, probe.Message);
            }

            var semanticResults = await _bookAiService.SemanticSearchAsync(normalizedQuery, limit, cancellationToken);
            if (semanticResults.Count == 0)
            {
                return Keyword(
                    LocalAiSearchMode.KeywordFallback,
                    "Local AI endpoint is reachable but returned no semantic matches; used keyword search instead.");
            }

            return new LocalAiSearchOutcome(semanticResults, LocalAiSearchMode.Semantic, probe.Message);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Keyword(LocalAiSearchMode.KeywordFallback, "Local AI request timed out; used keyword search instead.");
        }
        catch (Exception ex)
        {
            return Keyword(LocalAiSearchMode.KeywordFallback, $"Local AI failed: {ex.Message}");
        }
    }
}
