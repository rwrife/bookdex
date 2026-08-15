using System.Net.Http.Json;
using Bookdex.Core.Catalog;

namespace Bookdex.Core.Ai;

public sealed class LocalOnlyOpenAiBookAiService : IBookAiService
{
    private readonly HttpClient _httpClient;
    private readonly LocalAiSettings _settings;

    public LocalOnlyOpenAiBookAiService(HttpClient httpClient, LocalAiSettings settings)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public async Task<LocalAiProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            return LocalAiProbeResult.Unreachable("Local AI is disabled.");
        }

        if (!_settings.TryGetValidatedEndpoint(out var endpoint, out var validationError))
        {
            return LocalAiProbeResult.Unreachable(validationError ?? "Local AI endpoint validation failed.");
        }

        var modelsUri = new Uri(endpoint!, "/v1/models");

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));

            using var response = await _httpClient.GetAsync(modelsUri, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return LocalAiProbeResult.Unreachable(
                    $"Local AI endpoint probe failed with HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
            }

            _ = await response.Content.ReadFromJsonAsync<object>(cancellationToken: timeout.Token);
            return LocalAiProbeResult.Reachable($"Local AI endpoint reachable at {endpoint}.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return LocalAiProbeResult.Unreachable("Local AI endpoint probe timed out.");
        }
        catch (Exception ex)
        {
            return LocalAiProbeResult.Unreachable($"Local AI endpoint probe failed: {ex.Message}");
        }
    }

    public Task<IReadOnlyList<CatalogSearchResult>> SemanticSearchAsync(
        string query,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        // Incremental slice: semantic ranking not wired yet.
        return Task.FromResult<IReadOnlyList<CatalogSearchResult>>([]);
    }
}
