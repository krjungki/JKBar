// Ported from JKMon (packages/JKMon/src/JKMon.Core/Sync). Keep behaviour changes in sync with the original.
namespace JKBar.Core.Sync;

/// <summary>
/// One reading of the Global Secure Access client state. The event id is the client's own tray-icon status code and
/// the description is the message it wrote, so no undocumented enum has to be interpreted.
/// </summary>
public readonly record struct GlobalSecureAccessStatus(int EventId, string Description, DateTimeOffset? Written);

/// <summary>
/// Maps the tray-icon status events the Global Secure Access client writes to its operational log. The ids come
/// from the manifest the client installs, where each one carries the message shown next to its tray icon.
/// </summary>
public static class GlobalSecureAccessStatusMapper
{
    public const int Connected = 630;
    public const int Disconnected = 631;
    public const int SomeChannelsDisconnected = 632;
    public const int EmptyPolicy = 633;
    public const int ClientEnabled = 634;
    public const int ClientDisabled = 635;
    public const int PolicySchemaMismatch = 636;
    public const int PolicyMissing = 637;
    public const int NoNetwork = 638;
    public const int Offboarded = 639;
    public const int BreakGlass = 648;
    public const int NoInternet = 649;

    /// <summary>Every status the client reports through its tray icon, newest of which decides the circle.</summary>
    public static IReadOnlyList<int> StatusEventIds { get; } =
    [
        Connected, Disconnected, SomeChannelsDisconnected, EmptyPolicy, ClientEnabled, ClientDisabled,
        PolicySchemaMismatch, PolicyMissing, NoNetwork, Offboarded, BreakGlass, NoInternet
    ];

    public static SyncState ToSyncState(int eventId) => eventId switch
    {
        Connected => SyncState.UpToDate,

        // "Enabled" only says the client was switched on; the connection result arrives as a separate event.
        ClientEnabled => SyncState.Unknown,

        Disconnected or SomeChannelsDisconnected or ClientDisabled or NoNetwork or NoInternet
            or EmptyPolicy or PolicySchemaMismatch or PolicyMissing or Offboarded or BreakGlass => SyncState.Error,

        _ => SyncState.Unknown
    };

    public static string Describe(int eventId) => eventId switch
    {
        Connected => "모든 채널에 연결됨",
        Disconnected => "모든 채널 연결 끊김",
        SomeChannelsDisconnected => "일부 채널 연결 끊김",
        EmptyPolicy => "전달 정책이 비어 있음",
        ClientEnabled => "켜짐, 연결 결과 대기 중",
        ClientDisabled => "클라이언트 꺼짐",
        PolicySchemaMismatch => "전달 정책이 스키마와 맞지 않음",
        PolicyMissing => "전달 정책 없음",
        NoNetwork => "기기가 네트워크에 연결되지 않음",
        Offboarded => "기기 등록 해제됨",
        BreakGlass => "비상 우회(break glass) 모드",
        NoInternet => "인터넷 연결 없음",
        _ => "상태를 알 수 없음"
    };
}
