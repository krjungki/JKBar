// Turns two independent reachability answers into one connectivity verdict.
namespace JKBar.Core.Network;

public enum ConnectivityState
{
    Unknown,
    Online,
    Partial,
    Offline
}

public static class ConnectivityVerdict
{
    /// <summary>Two probes disagreeing usually means a captive portal or a blocked host, not a dead link.</summary>
    public static ConnectivityState From(bool googleReachable, bool microsoftReachable) =>
        (googleReachable, microsoftReachable) switch
        {
            (true, true) => ConnectivityState.Online,
            (false, false) => ConnectivityState.Offline,
            _ => ConnectivityState.Partial
        };

    public static string Describe(ConnectivityState state) => state switch
    {
        ConnectivityState.Online => "인터넷 연결됨",
        ConnectivityState.Partial => "인터넷 일부 응답 없음",
        ConnectivityState.Offline => "인터넷 연결 끊김",
        _ => "인터넷 상태 확인 중"
    };
}
