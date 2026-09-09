// Pins the destructive Windows commands behind the image menu's confirmation dialog.
using JKBar.Core;

namespace JKBar.Core.Tests;

public class ComputerPowerCommandTests
{
    [Theory]
    [InlineData(ComputerPowerAction.Restart, "/r /t 0")]
    [InlineData(ComputerPowerAction.ShutDown, "/s /t 0")]
    public void UsesTheExpectedShutdownCommand(ComputerPowerAction action, string arguments)
    {
        var command = ComputerPowerCommand.For(action);

        Assert.Equal("shutdown.exe", command.FileName);
        Assert.Equal(arguments, command.Arguments);
    }

    [Fact]
    public void RejectsAnUnknownAction() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ComputerPowerCommand.For((ComputerPowerAction)99));
}