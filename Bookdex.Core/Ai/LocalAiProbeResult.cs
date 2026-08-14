namespace Bookdex.Core.Ai;

public sealed record LocalAiProbeResult(bool IsReachable, string Message)
{
    public static LocalAiProbeResult Reachable(string message = "Local AI endpoint is reachable.") =>
        new(true, message);

    public static LocalAiProbeResult Unreachable(string message) =>
        new(false, message);
}
