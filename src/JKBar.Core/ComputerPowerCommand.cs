// Defines the exact Windows command used after the user confirms a restart or shutdown.
namespace JKBar.Core;

public enum ComputerPowerAction
{
    Restart,
    ShutDown
}

public readonly record struct ComputerPowerCommand(string FileName, string Arguments)
{
    public static ComputerPowerCommand For(ComputerPowerAction action) => action switch
    {
        ComputerPowerAction.Restart => new("shutdown.exe", "/r /t 0"),
        ComputerPowerAction.ShutDown => new("shutdown.exe", "/s /t 0"),
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };
}