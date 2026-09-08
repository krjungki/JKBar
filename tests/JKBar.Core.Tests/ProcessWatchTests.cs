// Regression checks for the watched-process list and the running-icon decision.
using System.Text.Json;
using JKBar.Core.Presentation;
using JKBar.Core.Settings;

namespace JKBar.Core.Tests;

public class ProcessWatchTests
{
    private static ProcessWatchSettings Watch(params WatchedProcess[] items) => new() { Items = items };

    [Fact]
    public void TakesTheNameAndPathTheUserTyped()
    {
        var settings = Watch(new WatchedProcess { Name = "  copilotapp.exe ", Path = "  C:\\Apps\\copilotapp.exe " }).Normalized();

        Assert.Equal("copilotapp.exe", settings.Items[0].Name);
        Assert.Equal("C:\\Apps\\copilotapp.exe", settings.Items[0].Path);
    }

    [Fact]
    public void FillsTheNameInFromThePathWhenOnlyAFileWasChosen()
    {
        var settings = Watch(new WatchedProcess { Path = "C:\\Apps\\copilotapp.exe" }).Normalized();

        Assert.Equal("copilotapp.exe", settings.Items[0].Name);
        Assert.Equal("copilotapp", settings.Items[0].MatchKey);
    }

    [Fact]
    public void DropsEmptyEntriesAndRepeatsAndCapsTheList()
    {
        var many = Enumerable.Range(0, ProcessWatchSettings.MaximumItems + 4)
            .Select(index => new WatchedProcess { Name = $"app{index}.exe" });
        var settings = Watch([
            new WatchedProcess(),
            new WatchedProcess { Name = "   " },
            new WatchedProcess { Name = "copilotapp.exe" },
            new WatchedProcess { Name = "COPILOTAPP" },
            .. many
        ]).Normalized();

        Assert.Equal(ProcessWatchSettings.MaximumItems, settings.Items.Length);
        Assert.Equal("copilotapp.exe", settings.Items[0].Name);
        Assert.Single(settings.Items, item => item.MatchKey.Equals("copilotapp", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ShowsOnlyTheWatchedExecutablesThatAreRunning()
    {
        var settings = Watch(
            new WatchedProcess { Name = "copilotapp.exe", Path = "C:\\Apps\\copilotapp.exe" },
            new WatchedProcess { Name = "teams.exe", Path = "C:\\Apps\\teams.exe" },
            new WatchedProcess { Name = "notrunning.exe", Path = "C:\\Apps\\notrunning.exe" });

        var running = ProcessWatchSource.Running(settings, Counts("CopilotApp", "explorer", "teams"));

        Assert.Equal(["copilotapp.exe", "teams.exe"], running.Select(item => item.Watched.Name));
    }

    [Fact]
    public void CountsEveryCopyThatIsRunning()
    {
        var settings = Watch(
            new WatchedProcess { Name = "chrome.exe" },
            new WatchedProcess { Name = "teams.exe" });

        var running = ProcessWatchSource.Running(
            settings,
            new Dictionary<string, int> { ["chrome"] = 7, ["teams"] = 1 });

        Assert.Equal(7, running.Single(item => item.Watched.Name == "chrome.exe").Count);
        Assert.Equal(1, running.Single(item => item.Watched.Name == "teams.exe").Count);
    }

    [Fact]
    public void IgnoresANameNothingIsActuallyRunningUnder()
    {
        var settings = Watch(new WatchedProcess { Name = "chrome.exe" });

        Assert.Empty(ProcessWatchSource.Running(settings, new Dictionary<string, int> { ["chrome"] = 0 }));
    }

    [Fact]
    public void RegistersAnEntryThatHasNoPathAtAll()
    {
        var settings = Watch(new WatchedProcess { Name = "chrome.exe" });
        var entry = Assert.Single(settings.Normalized().Items);

        Assert.True(entry.ByNameOnly);
        Assert.Single(ProcessWatchSource.Running(settings, Counts("chrome")));
    }

    [Fact]
    public void KnowsWhichEntriesPointAtAFile()
    {
        var entry = new WatchedProcess { Name = "chrome.exe", Path = "C:\\Apps\\chrome.exe" }.Normalized();

        Assert.False(entry.ByNameOnly);
    }

    [Fact]
    public void MatchesWhetherOrNotTheUserTypedTheExtension()
    {
        var settings = Watch(new WatchedProcess { Name = "copilotapp" });

        Assert.Single(ProcessWatchSource.Running(settings, Counts("copilotapp")));
        Assert.Empty(ProcessWatchSource.Running(settings, Counts("copilot")));
    }

    [Fact]
    public void AnEmptyListNeverAsksForIcons()
    {
        Assert.Empty(ProcessWatchSource.Running(new ProcessWatchSettings(), Counts("copilotapp")));
        Assert.Empty(ProcessWatchSource.Signature([]));
    }

    [Fact]
    public void TheSignatureChangesWhenAWatchedAppStartsOrStops()
    {
        var settings = Watch(
            new WatchedProcess { Name = "copilotapp.exe" },
            new WatchedProcess { Name = "teams.exe" });

        var before = ProcessWatchSource.Signature(ProcessWatchSource.Running(settings, Counts("copilotapp")));
        var after = ProcessWatchSource.Signature(
            ProcessWatchSource.Running(settings, Counts("copilotapp", "teams")));

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void TheSignatureChangesWhenAnotherCopyStarts()
    {
        var settings = Watch(new WatchedProcess { Name = "chrome.exe" });

        var one = ProcessWatchSource.Signature(
            ProcessWatchSource.Running(settings, new Dictionary<string, int> { ["chrome"] = 1 }));
        var two = ProcessWatchSource.Signature(
            ProcessWatchSource.Running(settings, new Dictionary<string, int> { ["chrome"] = 2 }));

        Assert.NotEqual(one, two);
    }

    [Fact]
    public void TheListSurvivesASettingsRoundTrip()
    {
        var settings = new JkBarSettings
        {
            ProcessWatch = Watch(new WatchedProcess { Name = "copilotapp.exe", Path = "C:\\Apps\\copilotapp.exe" })
        };

        var restored = JsonSerializer.Deserialize<JkBarSettings>(JsonSerializer.Serialize(settings))!.Normalized();

        Assert.Equal("copilotapp.exe", restored.ProcessWatch.Items[0].Name);
        Assert.Equal("C:\\Apps\\copilotapp.exe", restored.ProcessWatch.Items[0].Path);
    }

    [Fact]
    public void SettingsSavedBeforeThisFeatureStillLoad()
    {
        var restored = JsonSerializer.Deserialize<JkBarSettings>("{\"Notch\":{\"IdleContent\":1}}")!.Normalized();

        Assert.Empty(restored.ProcessWatch.Items);
        Assert.Equal(IdleNotchContent.DateTime, restored.Notch.IdleContent);
    }

    private static Dictionary<string, int> Counts(params string[] names) =>
        names.ToDictionary(name => name, _ => 1, StringComparer.OrdinalIgnoreCase);
}
