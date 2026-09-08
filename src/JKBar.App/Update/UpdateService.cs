// Ported from JKMon (packages/JKMon/src/JKMon.App/Update/UpdateService.cs). Keep behaviour changes in sync.
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using JKBar.Core.Update;
using IoPath = System.IO.Path;

namespace JKBar.App.Update;

internal enum UpdateOutcome
{
    CheckFailed,
    UpToDate,
    Declined,
    StagingFailed,
    Applying
}

internal sealed class UpdateService : IDisposable
{
    private readonly HttpClient _http;
    private readonly UpdateChecker _checker;

    internal UpdateService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("JKBar", Current.ToString()));
        _checker = new UpdateChecker(_http);
    }

    internal static ReleaseVersion Current =>
        ReleaseVersion.TryParse(
            typeof(UpdateService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion,
            out var version)
            ? version
            : ReleaseVersion.Zero;

    internal Uri ReleasesPage => _checker.ReleasesPage;

    /// <param name="announceWhenCurrent">
    /// True when the user asked, so an already-current install still gets an answer. A scheduled check stays silent.
    /// </param>
    internal async Task<UpdateOutcome> RunAsync(bool announceWhenCurrent, CancellationToken cancellationToken)
    {
        var latest = await _checker.TryGetLatestAsync(cancellationToken).ConfigureAwait(true);
        if (latest is null)
        {
            if (announceWhenCurrent)
            {
                Say("업데이트를 확인하지 못했습니다. 네트워크를 확인한 뒤 다시 시도해 주세요.", MessageBoxIcon.Warning);
            }

            return UpdateOutcome.CheckFailed;
        }

        var info = latest.Value;
        if (info.Version <= Current)
        {
            if (announceWhenCurrent)
            {
                Say($"이미 최신 버전입니다. (현재 {Current})", MessageBoxIcon.Information);
            }

            return UpdateOutcome.UpToDate;
        }

        var answer = MessageBox.Show(
            $"새 버전 {info.Version}이 있습니다. 현재 버전은 {Current}입니다.\n\n"
            + "지금 업데이트할까요? 앱이 잠시 종료된 뒤 다시 실행됩니다.",
            "JKBar 업데이트",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (answer != DialogResult.Yes)
        {
            return UpdateOutcome.Declined;
        }

        var staged = await new UpdateDownloader(_http).TryStageAsync(info, cancellationToken).ConfigureAwait(true);
        if (staged is null)
        {
            Say("업데이트 파일을 내려받거나 검증하지 못했습니다. 설치된 버전은 그대로입니다.", MessageBoxIcon.Warning);
            return UpdateOutcome.StagingFailed;
        }

        if (!Launch(staged))
        {
            UpdateDownloader.TryDelete(staged.WorkDirectory);
            Say("업데이트 프로그램을 실행하지 못했습니다. 설치된 버전은 그대로입니다.", MessageBoxIcon.Warning);
            return UpdateOutcome.StagingFailed;
        }

        return UpdateOutcome.Applying;
    }

    public void Dispose() => _http.Dispose();

    private static bool Launch(StagedUpdate staged)
    {
        try
        {
            var info = new ProcessStartInfo(IoPath.Combine(staged.StagedDirectory, UpdateDownloader.ExecutableName))
            {
                UseShellExecute = false,
                WorkingDirectory = staged.StagedDirectory
            };

            info.ArgumentList.Add(UpdateArguments.ApplySwitch);
            info.ArgumentList.Add("--source");
            info.ArgumentList.Add(staged.StagedDirectory);
            info.ArgumentList.Add("--target");
            info.ArgumentList.Add(AppContext.BaseDirectory.TrimEnd(IoPath.DirectorySeparatorChar));
            info.ArgumentList.Add("--work");
            info.ArgumentList.Add(staged.WorkDirectory);
            info.ArgumentList.Add("--pid");
            info.ArgumentList.Add(Environment.ProcessId.ToString());

            using var started = Process.Start(info);

            return started is not null;
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception
            or InvalidOperationException or IOException)
        {
            return false;
        }
    }

    private static void Say(string message, MessageBoxIcon icon) =>
        MessageBox.Show(message, "JKBar 업데이트", MessageBoxButtons.OK, icon);
}
