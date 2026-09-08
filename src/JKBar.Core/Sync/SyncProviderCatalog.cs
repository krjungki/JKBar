// Ported from JKMon (packages/JKMon/src/JKMon.Core/Sync), reduced to the ids the band needs.
namespace JKBar.Core.Sync;

public static class SyncProviderCatalog
{
    public const string OneDrive = "onedrive";
    public const string Syncthing = "syncthing";
    public const string GlobalSecureAccess = "gsa";

    public static string DisplayName(string providerId) => providerId switch
    {
        OneDrive => "OneDrive",
        Syncthing => "Syncthing",
        GlobalSecureAccess => "Global Secure Access",
        _ => providerId
    };
}
