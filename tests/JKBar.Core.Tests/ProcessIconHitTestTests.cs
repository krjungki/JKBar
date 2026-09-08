// Regression checks for clicking a watched-process icon in the band.
using System.Drawing;
using JKBar.Core.Presentation;
using JKBar.Core.Settings;

namespace JKBar.Core.Tests;

public class ProcessIconHitTestTests
{
    private static readonly WatchedProcess Copilot = new() { Name = "copilotapp.exe", Path = "C:\\Apps\\copilotapp.exe" };
    private static readonly WatchedProcess Teams = new() { Name = "teams.exe", Path = "C:\\Apps\\teams.exe" };

    private static readonly ProcessIcon[] Icons =
    [
        new(Copilot, new Rectangle(2050, 10, 37, 37)),
        new(Teams, new Rectangle(2100, 10, 37, 37))
    ];

    [Fact]
    public void FindsTheApplicationTheClickLandedOn()
    {
        Assert.Equal(Copilot, ProcessIconHitTest.At(Icons, new Point(2068, 28)));
        Assert.Equal(Teams, ProcessIconHitTest.At(Icons, new Point(2118, 28)));
    }

    [Fact]
    public void IgnoresClicksBesideTheIcons()
    {
        Assert.Null(ProcessIconHitTest.At(Icons, new Point(2094, 28)));
        Assert.Null(ProcessIconHitTest.At(Icons, new Point(2068, 60)));
        Assert.Null(ProcessIconHitTest.At([], new Point(2068, 28)));
    }

    [Fact]
    public void TheEdgesOfAnIconStillCount()
    {
        Assert.Equal(Copilot, ProcessIconHitTest.At(Icons, new Point(2050, 10)));
        Assert.Null(ProcessIconHitTest.At(Icons, new Point(2087, 47)));
    }

    [Theory]
    [InlineData(true, true, ProcessActivation.Focus)]
    [InlineData(true, false, ProcessActivation.Focus)]
    [InlineData(false, true, ProcessActivation.Launch)]
    [InlineData(false, false, ProcessActivation.None)]
    public void RaisesAnExistingWindowBeforeStartingAnything(bool hasWindow, bool canLaunch, ProcessActivation expected)
    {
        Assert.Equal(expected, ProcessIconHitTest.Decide(hasWindow, canLaunch));
    }
}
