using Bookdex.Core.Catalog;

namespace Bookdex.Core.Ai;

public interface IBookAiService
{
    Task<LocalAiProbeResult> ProbeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogSearchResult>> SemanticSearchAsync(
        string query,
        int limit = 20,
        CancellationToken cancellationToken = default);
}
