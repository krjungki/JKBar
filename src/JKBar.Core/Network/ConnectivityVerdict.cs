// Turns the reachability answers of every probe into one connectivity verdict.
namespace JKBar.Core.Network;

public enum ConnectivityState
{
    Unknown,
    Online,
    Partial,
    Offline
}

/// <param name="Target">What the user recognises the probe by: its host name.</param>
public sealed record ProbeResult(string Target, bool Reachable);

/// <param name="Unreachable">The targets that did not answer, in probe order; empty unless the state is Partial.</param>
public sealed record ConnectivityReport(ConnectivityState State, IReadOnlyList<string> Unreachable)
{
    public static readonly ConnectivityReport Unknown = new(ConnectivityState.Unknown, []);

    /// <summary>Two partial reports with different failing sites are different situations.</summary>
    public string Signature => State == ConnectivityState.Partial
        ? $"{State}:{string.Join(',', Unreachable)}"
        : State.ToString();
}

public static class ConnectivityVerdict
{
    private const int MaximumNamedTargets = 2;

    /// <summary>Probes disagreeing usually means a firewall or a blocked host, not a dead link.</summary>
    public static ConnectivityReport From(IReadOnlyList<ProbeResult> results)
    {
        if (results.Count == 0)
        {
            return ConnectivityReport.Unknown;
        }

        var unreachable = results.Where(result => !result.Reachable)
            .Select(result => result.Target)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (unreachable.Length == 0)
        {
            return new ConnectivityReport(ConnectivityState.Online, []);
        }

        return results.All(result => !result.Reachable)
            ? new ConnectivityReport(ConnectivityState.Offline, [])
            : new ConnectivityReport(ConnectivityState.Partial, unreachable);
    }

    /// <summary>
    /// A redirect is a failure, since captive portals and sign-in walls answer by redirecting. A generate_204 probe
    /// must return exactly 204, because a portal can also serve its page with 200.
    /// </summary>
    public static bool IsSuccess(Uri probe, int statusCode) =>
        probe.AbsolutePath.EndsWith("/generate_204", StringComparison.OrdinalIgnoreCase)
            ? statusCode == 204
            : statusCode is >= 200 and < 300;

    /// <summary>Any site answering means the network is up; the probes may well be intranet hosts.</summary>
    public static string Describe(ConnectivityState state) => state switch
    {
        ConnectivityState.Online or ConnectivityState.Partial => "네트워크 연결됨",
        ConnectivityState.Offline => "네트워크 연결 끊김",
        _ => "인터넷 상태 확인 중"
    };

    public const string AllReachable = "모든 사이트 접속 가능";

    /// <summary>Names the first failing sites and counts the rest, so the notch line stays short.</summary>
    public static string DescribeUnreachable(IReadOnlyList<string> unreachable)
    {
        var named = string.Join(", ", unreachable.Take(MaximumNamedTargets));
        var rest = unreachable.Count - MaximumNamedTargets;
        return rest > 0
            ? $"일부 사이트 접속 가능 ({named} 외 {rest}곳 안 됨)"
            : $"일부 사이트 접속 가능 ({named} 안 됨)";
    }
}
