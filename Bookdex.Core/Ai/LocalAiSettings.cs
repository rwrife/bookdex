using System.Net;

namespace Bookdex.Core.Ai;

public sealed record LocalAiSettings(
    bool Enabled = false,
    string Endpoint = "http://localhost:11434")
{
    public bool TryGetValidatedEndpoint(out Uri? endpointUri, out string? validationError)
    {
        endpointUri = null;
        validationError = null;

        if (string.IsNullOrWhiteSpace(Endpoint))
        {
            validationError = "Local AI endpoint is empty.";
            return false;
        }

        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var parsed))
        {
            validationError = $"Local AI endpoint '{Endpoint}' is not a valid absolute URI.";
            return false;
        }

        if (parsed.Scheme is not ("http" or "https"))
        {
            validationError = "Local AI endpoint must use http or https.";
            return false;
        }

        if (!IsLoopbackHost(parsed.Host))
        {
            validationError =
                $"Local AI endpoint host '{parsed.Host}' is not local. Only localhost/loopback endpoints are allowed.";
            return false;
        }

        endpointUri = parsed;
        return true;
    }

    private static bool IsLoopbackHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
    }
}
