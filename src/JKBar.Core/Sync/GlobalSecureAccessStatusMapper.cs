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
        Connected => "connected to all channels",
        Disconnected => "disconnected from all channels",
        SomeChannelsDisconnected => "some channels disconnected",
        EmptyPolicy => "forwarding policy is empty",
        ClientEnabled => "enabled, waiting for a connection result",
        ClientDisabled => "client disabled",
        PolicySchemaMismatch => "forwarding policy does not match the schema",
        PolicyMissing => "forwarding policy is missing",
        NoNetwork => "device is not connected to the network",
        Offboarded => "device is offboarded",
        BreakGlass => "break glass mode",
        NoInternet => "no internet connectivity",
        _ => "state unavailable"
    };
}
