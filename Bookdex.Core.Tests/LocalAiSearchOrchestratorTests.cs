using Bookdex.Core.Ai;
using Bookdex.Core.Catalog;

namespace Bookdex.Core.Tests;

public sealed class LocalAiSearchOrchestratorTests
{
    [Fact]
    public async Task SearchAsync_WhenAiDisabled_UsesKeywordSearchOnly()
    {
        var keywordResults = new[]
        {
            new CatalogSearchResult(1, "Keyword Book", "Author", "snippet", 0.1),
        };

        var store = new FakeLibraryStore(keywordResults);
        var aiService = new FakeBookAiService(
            probeResult: LocalAiProbeResult.Reachable(),
            semanticResults: [new CatalogSearchResult(2, "Semantic Book", "Author", "snippet", 0.0)]);

        var orchestrator = new LocalAiSearchOrchestrator(
            store,
            aiService,
            new LocalAiSettings(Enabled: false));

        var outcome = await orchestrator.SearchAsync("ship librarian", limit: 5);

        Assert.Equal(LocalAiSearchMode.Keyword, outcome.Mode);
        Assert.Null(outcome.Message);
        Assert.Equal(keywordResults, outcome.Results);
        Assert.Equal(0, aiService.ProbeCallCount);
        Assert.Equal(0, aiService.SemanticCallCount);
    }

    [Fact]
    public async Task SearchAsync_WhenProbeFails_FallsBackToKeywordWithMessage()
    {
        var keywordResults = new[]
        {
            new CatalogSearchResult(3, "Fallback Book", "Author", "snippet", 0.5),
        };

        var store = new FakeLibraryStore(keywordResults);
        var aiService = new FakeBookAiService(
            probeResult: LocalAiProbeResult.Unreachable("Endpoint unreachable at http://localhost:11434"),
            semanticResults: []);

        var orchestrator = new LocalAiSearchOrchestrator(
            store,
            aiService,
            new LocalAiSettings(Enabled: true));

        var outcome = await orchestrator.SearchAsync("describe this plot", limit: 5);

        Assert.Equal(LocalAiSearchMode.KeywordFallback, outcome.Mode);
        Assert.Equal(keywordResults, outcome.Results);
        Assert.Contains("unreachable", outcome.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, aiService.ProbeCallCount);
        Assert.Equal(0, aiService.SemanticCallCount);
    }

    [Fact]
    public async Task SearchAsync_WhenEndpointIsNotLocal_BlocksAiAndFallsBackToKeyword()
    {
        var keywordResults = new[]
        {
            new CatalogSearchResult(4, "Privacy Book", "Author", "snippet", 0.7),
        };

        var store = new FakeLibraryStore(keywordResults);
        var aiService = new FakeBookAiService(
            probeResult: LocalAiProbeResult.Reachable(),
            semanticResults: [new CatalogSearchResult(5, "Semantic Book", "Author", "snippet", 0.2)]);

        var orchestrator = new LocalAiSearchOrchestrator(
            store,
            aiService,
            new LocalAiSettings(Enabled: true, Endpoint: "https://api.example.com"));

        var outcome = await orchestrator.SearchAsync("test", limit: 5);

        Assert.Equal(LocalAiSearchMode.KeywordFallback, outcome.Mode);
        Assert.Equal(keywordResults, outcome.Results);
        Assert.Contains("localhost", outcome.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, aiService.ProbeCallCount);
        Assert.Equal(0, aiService.SemanticCallCount);
    }

    [Fact]
    public async Task SearchAsync_WhenAiReturnsSemanticMatches_UsesSemanticResults()
    {
        var keywordResults = new[]
        {
            new CatalogSearchResult(10, "Keyword Match", "Author", "snippet", 1.0),
        };
        var semanticResults = new[]
        {
            new CatalogSearchResult(11, "Semantic Match", "Author", "semantic snippet", 0.01),
        };

        var store = new FakeLibraryStore(keywordResults);
        var aiService = new FakeBookAiService(
            probeResult: LocalAiProbeResult.Reachable("Local AI endpoint is reachable."),
            semanticResults: semanticResults);

        var orchestrator = new LocalAiSearchOrchestrator(
            store,
            aiService,
            new LocalAiSettings(Enabled: true));

        var outcome = await orchestrator.SearchAsync("generation ship", limit: 5);

        Assert.Equal(LocalAiSearchMode.Semantic, outcome.Mode);
        Assert.Equal(semanticResults, outcome.Results);
        Assert.Contains("reachable", outcome.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, aiService.ProbeCallCount);
        Assert.Equal(1, aiService.SemanticCallCount);
    }

    private sealed class FakeLibraryStore(IReadOnlyList<CatalogSearchResult> keywordResults) : ILibraryStore
    {
        public void Initialize() => throw new NotSupportedException();

        public bool ShouldScan(FileSignature signature) => throw new NotSupportedException();

        public long UpsertBook(CatalogBookRecord record, FileSignature signature) => throw new NotSupportedException();

        public IReadOnlyList<CatalogSearchResult> Search(string query, int limit = 20) => keywordResults;

        public void ReplaceBookShelves(long bookId, IEnumerable<string> shelfNames) => throw new NotSupportedException();

        public void ReplaceBookTags(long bookId, IEnumerable<string> tagNames) => throw new NotSupportedException();

        public void SetReadingState(long bookId, BookReadingStatus? status, int? rating) => throw new NotSupportedException();

        public BookOrganizationSnapshot GetBookOrganization(long bookId) => throw new NotSupportedException();
    }

    private sealed class FakeBookAiService(
        LocalAiProbeResult probeResult,
        IReadOnlyList<CatalogSearchResult> semanticResults) : IBookAiService
    {
        public int ProbeCallCount { get; private set; }

        public int SemanticCallCount { get; private set; }

        public Task<LocalAiProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
        {
            ProbeCallCount++;
            return Task.FromResult(probeResult);
        }

        public Task<IReadOnlyList<CatalogSearchResult>> SemanticSearchAsync(
            string query,
            int limit = 20,
            CancellationToken cancellationToken = default)
        {
            SemanticCallCount++;
            return Task.FromResult(semanticResults);
        }
    }
}
