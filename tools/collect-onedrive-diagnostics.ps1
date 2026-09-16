<#
.SYNOPSIS
Collects the signals JKBar uses to estimate OneDrive status without changing either application.

.DESCRIPTION
Runs as a standard user, samples the current Windows user's Cloud Files provider status and the current session's
OneDrive process I/O, then creates a ZIP report.
Raw sync-root paths, account names, device names, file names, settings, event messages, and OneDrive logs are not
collected. Samples are held in memory and written under TEMP after sampling, so report writes cannot create the
OneDrive activity being measured.

.EXAMPLE
.\collect-onedrive-diagnostics.ps1 -ObservedOneDriveStatus UpToDate -ObservedJKBarStatus Red

.EXAMPLE
.\collect-onedrive-diagnostics.ps1 -JKBarPath C:\Tools\JKBar\JKBar.exe -DurationSeconds 180
#>
[CmdletBinding()]
param(
    [ValidateRange(10, 1800)]
    [int] $DurationSeconds = 120,

    [ValidateRange(1, 60)]
    [int] $SampleIntervalSeconds = 5,

    [ValidateSet('UpToDate', 'Syncing', 'Paused', 'Error', 'Unknown')]
    [string] $ObservedOneDriveStatus = 'Unknown',

    [ValidateSet('Green', 'Red', 'Hidden', 'Unknown')]
    [string] $ObservedJKBarStatus = 'Red',

    [string] $JKBarPath,

    [string] $OutputDirectory = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$syncRootManagerKey = 'SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\SyncRootManager'
$activityThresholdBytesPerSecond = 16 * 1024
$activityHoldSeconds = 15
$activitySamplesToAssert = 3

$currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
try {
    $currentSid = if ($null -eq $currentIdentity.User) { $null } else { $currentIdentity.User.Value }
}
finally {
    $currentIdentity.Dispose()
}
if ([string]::IsNullOrWhiteSpace($currentSid)) {
    throw 'The current Windows user SID is unavailable.'
}

$currentProcess = [System.Diagnostics.Process]::GetCurrentProcess()
try {
    $currentSessionId = $currentProcess.SessionId
}
finally {
    $currentProcess.Dispose()
}

$nativeCode = @'
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace JKBar.Diagnostics.V2
{
    public sealed class SyncRootResult
    {
        public string HResultHex;
        public uint ReturnedLength;
        public string ProviderStatusHex;
        public string ProviderStatusName;
        public string MappedState;
        public string Failure;
    }

    public sealed class ProcessIoResult
    {
        public bool Success;
        public ulong TotalBytes;
        public string Failure;
    }

    public sealed class ArchitectureResult
    {
        public string ProcessArchitecture;
        public string OSArchitecture;
        public string Source;
        public string Failure;
    }

    public static class NativeProbe
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [DllImport("CldApi.dll", CharSet = CharSet.Unicode)]
        private static extern int CfGetSyncRootInfoByPath(
            string filePath,
            int infoClass,
            IntPtr infoBuffer,
            uint infoBufferLength,
            out uint returnedLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetProcessIoCounters(IntPtr process, out IoCounters counters);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWow64Process2(
            IntPtr process,
            out ushort processMachine,
            out ushort nativeMachine);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        public static ArchitectureResult ReadArchitecture()
        {
            ArchitectureResult result = new ArchitectureResult();
            try
            {
                ushort processMachine;
                ushort nativeMachine;
                if (IsWow64Process2(GetCurrentProcess(), out processMachine, out nativeMachine))
                {
                    result.ProcessArchitecture = MachineName(processMachine == 0 ? nativeMachine : processMachine);
                    result.OSArchitecture = MachineName(nativeMachine);
                    result.Source = "IsWow64Process2";
                    result.Failure = String.Empty;
                    return result;
                }

                result.Failure = "Win32Error-" + Marshal.GetLastWin32Error().ToString();
            }
            catch (Exception error)
            {
                result.Failure = error.GetType().Name;
            }

            string process = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
            string native = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432");
            result.ProcessArchitecture = EnvironmentArchitecture(process, IntPtr.Size);
            result.OSArchitecture = EnvironmentArchitecture(String.IsNullOrEmpty(native) ? process : native, IntPtr.Size);
            result.Source = "Environment";
            return result;
        }

        public static SyncRootResult ReadSyncRoot(string path)
        {
            SyncRootResult result = new SyncRootResult();
            IntPtr buffer = Marshal.AllocHGlobal(8192);
            try
            {
                uint returnedLength;
                int hr = CfGetSyncRootInfoByPath(path, 1, buffer, 8192, out returnedLength);
                result.HResultHex = "0x" + ((uint)hr).ToString("X8");
                result.ReturnedLength = returnedLength;
                if (hr != 0)
                {
                    result.Failure = "HRESULT";
                    result.MappedState = "Unreadable";
                    return result;
                }

                if (returnedLength < 28)
                {
                    result.Failure = "ShortResult";
                    result.MappedState = "Unreadable";
                    return result;
                }

                uint status = unchecked((uint)Marshal.ReadInt32(buffer, 24));
                result.ProviderStatusHex = "0x" + status.ToString("X8");
                result.ProviderStatusName = StatusName(status);
                result.MappedState = MapState(status);
                result.Failure = String.Empty;
                return result;
            }
            catch (Exception error)
            {
                result.HResultHex = String.Empty;
                result.ProviderStatusHex = String.Empty;
                result.ProviderStatusName = String.Empty;
                result.MappedState = "Unreadable";
                result.Failure = error.GetType().Name;
                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public static ProcessIoResult ReadProcessIo(int processId)
        {
            ProcessIoResult result = new ProcessIoResult();
            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    IoCounters counters;
                    if (!GetProcessIoCounters(process.Handle, out counters))
                    {
                        result.Failure = "Win32Error-" + Marshal.GetLastWin32Error().ToString();
                        return result;
                    }

                    result.TotalBytes = counters.ReadTransferCount + counters.WriteTransferCount +
                        counters.OtherTransferCount;
                    result.Success = true;
                    result.Failure = String.Empty;
                    return result;
                }
            }
            catch (Exception error)
            {
                result.Failure = error.GetType().Name;
                return result;
            }
        }

        private static string StatusName(uint status)
        {
            switch (status)
            {
                case 0x00000000: return "Disconnected";
                case 0x00000001: return "Idle";
                case 0x00000002: return "PopulateNamespace";
                case 0x00000004: return "PopulateMetadata";
                case 0x00000008: return "PopulateContent";
                case 0x00000010: return "SyncIncremental";
                case 0x00000020: return "SyncFull";
                case 0x00000040: return "ConnectivityLost";
                case 0x80000000: return "ClearFlags";
                case 0xC0000001: return "Terminated";
                case 0xC0000002: return "Error";
                default: return "Unrecognized";
            }
        }

        private static string MapState(uint status)
        {
            switch (status)
            {
                case 0x00000001: return "UpToDate";
                case 0x00000002:
                case 0x00000004:
                case 0x00000008:
                case 0x00000010:
                case 0x00000020: return "Synchronizing";
                case 0xC0000001:
                case 0xC0000002: return "Error";
                default: return "Unknown";
            }
        }

        private static string MachineName(ushort machine)
        {
            switch (machine)
            {
                case 0x014C: return "X86";
                case 0x01C0:
                case 0x01C4: return "Arm";
                case 0x8664: return "X64";
                case 0xAA64: return "Arm64";
                default: return "0x" + machine.ToString("X4");
            }
        }

        private static string EnvironmentArchitecture(string value, int pointerSize)
        {
            if (!String.IsNullOrEmpty(value))
            {
                switch (value.Trim().ToUpperInvariant())
                {
                    case "X86": return "X86";
                    case "AMD64": return "X64";
                    case "ARM": return "Arm";
                    case "ARM64": return "Arm64";
                }
            }

            return pointerSize == 4 ? "32-bit-Unknown" : "64-bit-Unknown";
        }
    }
}
'@

if (-not ([System.Management.Automation.PSTypeName]'JKBar.Diagnostics.V2.NativeProbe').Type) {
    Add-Type -TypeDefinition $nativeCode -Language CSharp
}

function Get-PeMachine {
    param([string] $Path)

    if ([string]::IsNullOrWhiteSpace($Path) -or -not [System.IO.File]::Exists($Path)) {
        return 'Unavailable'
    }

    try {
        $stream = [System.IO.File]::Open($Path, 'Open', 'Read', 'ReadWrite')
        $reader = New-Object System.IO.BinaryReader($stream)
        try {
            if ($stream.Length -lt 64) { return 'InvalidPE' }
            if ($reader.ReadUInt16() -ne 0x5A4D) { return 'NotPE' }
            $stream.Position = 0x3C
            $peOffset = $reader.ReadInt32()
            if ($peOffset -lt 64 -or $peOffset -gt $stream.Length - 6) { return 'InvalidPE' }
            $stream.Position = $peOffset
            if ($reader.ReadUInt32() -ne 0x00004550) { return 'NotPE' }
            $machine = $reader.ReadUInt16()
            switch ($machine) {
                0x014C { return 'X86' }
                0x8664 { return 'X64' }
                0xAA64 { return 'Arm64' }
                0xA641 { return 'Arm64EC' }
                0xA64E { return 'Arm64X' }
                default { return ('0x{0:X4}' -f $machine) }
            }
        }
        finally {
            $reader.Dispose()
            $stream.Dispose()
        }
    }
    catch {
        return 'Unreadable'
    }
}

function Get-InstallScope {
    param([string] $Path)

    if ([string]::IsNullOrWhiteSpace($Path)) { return 'Unavailable' }
    if ($env:LOCALAPPDATA -and $Path.StartsWith($env:LOCALAPPDATA, [StringComparison]::OrdinalIgnoreCase)) {
        return 'LocalAppData'
    }
    if ($env:ProgramFiles -and $Path.StartsWith($env:ProgramFiles, [StringComparison]::OrdinalIgnoreCase)) {
        return 'ProgramFiles'
    }
    if (${env:ProgramFiles(x86)} -and $Path.StartsWith(${env:ProgramFiles(x86)}, [StringComparison]::OrdinalIgnoreCase)) {
        return 'ProgramFilesX86'
    }
    return 'Other'
}

function Get-FileIdentity {
    param(
        [string] $Path,
        [bool] $IncludeHash
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or -not [System.IO.File]::Exists($Path)) {
        return [pscustomobject]@{
            Found = $false
            FileName = $null
            FileVersion = $null
            ProductVersion = $null
            Machine = 'Unavailable'
            InstallScope = 'Unavailable'
            SHA256 = $null
        }
    }

    $version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($Path)
    $hash = $null
    if ($IncludeHash) {
        $hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    }

    return [pscustomobject]@{
        Found = $true
        FileName = [System.IO.Path]::GetFileName($Path)
        FileVersion = $version.FileVersion
        ProductVersion = $version.ProductVersion
        Machine = Get-PeMachine -Path $Path
        InstallScope = Get-InstallScope -Path $Path
        SHA256 = $hash
    }
}

function Find-JKBarPath {
    if (-not [string]::IsNullOrWhiteSpace($JKBarPath)) {
        if (-not [System.IO.File]::Exists($JKBarPath)) {
            throw "JKBarPath does not exist."
        }
        return [System.IO.Path]::GetFullPath($JKBarPath)
    }

    foreach ($process in @(Get-Process -Name JKBar -ErrorAction SilentlyContinue)) {
        try {
            if (-not [string]::IsNullOrWhiteSpace($process.Path)) { return $process.Path }
        }
        catch {
        }
        finally {
            $process.Dispose()
        }
    }
    return $null
}

function Get-OneDriveProcesses {
    $results = New-Object System.Collections.Generic.List[object]
    foreach ($process in @(Get-Process -Name OneDrive -ErrorAction SilentlyContinue)) {
        try {
            try {
                if ($process.SessionId -ne $currentSessionId) { continue }
            }
            catch {
                continue
            }

            $path = $null
            $startedUtc = $null
            try { $path = $process.Path } catch {}
            try { $startedUtc = $process.StartTime.ToUniversalTime().ToString('O') } catch {}
            $identity = Get-FileIdentity -Path $path -IncludeHash $false
            $results.Add([pscustomobject]@{
                ProcessId = $process.Id
                StartedUtc = $startedUtc
                FileVersion = $identity.FileVersion
                ProductVersion = $identity.ProductVersion
                Machine = $identity.Machine
                InstallScope = $identity.InstallScope
            })
        }
        finally {
            $process.Dispose()
        }
    }
    return $results.ToArray()
}

function Get-SyncRootDefinitions {
    $results = New-Object System.Collections.Generic.List[object]
    $providerCount = 0
    $manager = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey($syncRootManagerKey)
    if ($null -eq $manager) {
        return [pscustomobject]@{ ProviderCount = 0; Roots = @() }
    }

    try {
        foreach ($providerName in $manager.GetSubKeyNames()) {
            $prefix = 'OneDrive!{0}!' -f $currentSid
            if (-not $providerName.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { continue }
            $providerCount++
            $userRoots = $manager.OpenSubKey($providerName + '\UserSyncRoots')
            if ($null -eq $userRoots) { continue }
            try {
                $path = $userRoots.GetValue($currentSid)
                if ($path -is [string] -and -not [string]::IsNullOrWhiteSpace($path)) {
                    $results.Add([pscustomobject]@{
                        RootId = 'root-{0}' -f ($results.Count + 1)
                        Path = $path
                        Exists = [System.IO.Directory]::Exists($path)
                    })
                }
            }
            finally {
                $userRoots.Dispose()
            }
        }
    }
    finally {
        $manager.Dispose()
    }

    return [pscustomobject]@{ ProviderCount = $providerCount; Roots = $results.ToArray() }
}

function Get-OneDriveIoSample {
    $total = [decimal]0
    $processCount = 0
    $readableCount = 0
    $failures = New-Object System.Collections.Generic.List[string]

    foreach ($process in @(Get-Process -Name OneDrive -ErrorAction SilentlyContinue)) {
        try {
            try {
                if ($process.SessionId -ne $currentSessionId) { continue }
            }
            catch {
                continue
            }

            $processCount++
            $reading = [JKBar.Diagnostics.V2.NativeProbe]::ReadProcessIo($process.Id)
            if ($reading.Success) {
                $readableCount++
                $total += [decimal]$reading.TotalBytes
            }
            else {
                $failures.Add($reading.Failure)
            }
        }
        finally {
            $process.Dispose()
        }
    }

    return [pscustomobject]@{
        TotalBytes = $total
        ProcessCount = $processCount
        ReadableProcessCount = $readableCount
        Failures = ($failures | Sort-Object -Unique) -join ';'
    }
}

function Get-AggregateRootState {
    param([object[]] $States)

    if ($States.Count -eq 0) { return 'Absent' }
    if ($States -contains 'Error') { return 'Error' }
    if ($States -contains 'Synchronizing') { return 'Synchronizing' }
    if ($States -contains 'Unknown') { return 'Unknown' }
    if (@($States | Where-Object { $_ -ne 'UpToDate' }).Count -eq 0) { return 'UpToDate' }
    return 'Unknown'
}

function Get-JKBarState {
    param(
        [object[]] $RootRows,
        [bool] $Transferring
    )

    $readableStates = @($RootRows | Where-Object { $_.MappedState -ne 'Unreadable' } |
        ForEach-Object { $_.MappedState })
    if ($readableStates.Count -eq 0) {
        if ($Transferring) { return 'Synchronizing' }
        return 'UpToDate'
    }

    $aggregate = Get-AggregateRootState -States $readableStates
    if ($aggregate -eq 'UpToDate' -and $Transferring) { return 'Synchronizing' }
    return $aggregate
}

function Get-EventMetadata {
    param(
        [DateTimeOffset] $From,
        [DateTimeOffset] $To
    )

    $rows = New-Object System.Collections.Generic.List[object]
    try {
        $logNames = @(& "$env:SystemRoot\System32\wevtutil.exe" el 2>$null |
            Where-Object { $_ -match '(?i)(OneDrive|CloudFiles)' })
        foreach ($logName in $logNames) {
            try {
                foreach ($logEntry in @(Get-WinEvent -LogName $logName -MaxEvents 200 -ErrorAction Stop)) {
                    if ($null -eq $logEntry.TimeCreated) { continue }
                    $time = [DateTimeOffset]$logEntry.TimeCreated
                    if ($time -lt $From -or $time -gt $To) { continue }
                    $rows.Add([pscustomobject]@{
                        TimeCreatedUtc = $time.ToUniversalTime().ToString('O')
                        LogName = $logName
                        ProviderName = $logEntry.ProviderName
                        EventId = $logEntry.Id
                        Level = $logEntry.LevelDisplayName
                        RecordId = $logEntry.RecordId
                    })
                }
            }
            catch {
            }
        }
    }
    catch {
    }
    return $rows.ToArray()
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    try {
        $principal = New-Object Security.Principal.WindowsPrincipal($identity)
        return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    }
    finally {
        $identity.Dispose()
    }
}

$architecture = [JKBar.Diagnostics.V2.NativeProbe]::ReadArchitecture()

Write-Host "Collecting JKBar OneDrive diagnostics for $DurationSeconds seconds."
Write-Host 'Keep JKBar and OneDrive running. No application state will be changed.'

$collectionStarted = [DateTimeOffset]::UtcNow
$syncRoots = Get-SyncRootDefinitions
$rootDefinitions = @($syncRoots.Roots)
$activityRows = New-Object System.Collections.Generic.List[object]
$rootRows = New-Object System.Collections.Generic.List[object]

$lastTotal = [decimal]-1
$lastSampled = [DateTimeOffset]::MinValue
$lastActive = [DateTimeOffset]::MinValue
$consecutive = 0
$collectionEnds = $collectionStarted.AddSeconds($DurationSeconds)

do {
    $now = [DateTimeOffset]::UtcNow
    $currentRootRows = New-Object System.Collections.Generic.List[object]
    foreach ($root in $rootDefinitions) {
        $reading = $null
        if ($root.Exists) {
            $reading = [JKBar.Diagnostics.V2.NativeProbe]::ReadSyncRoot($root.Path)
        }

        $row = [pscustomobject]@{
            TimestampUtc = $now.ToString('O')
            RootId = $root.RootId
            Exists = $root.Exists
            HResult = if ($null -eq $reading) { '' } else { $reading.HResultHex }
            ReturnedLength = if ($null -eq $reading) { 0 } else { $reading.ReturnedLength }
            ProviderStatus = if ($null -eq $reading) { '' } else { $reading.ProviderStatusHex }
            ProviderStatusName = if ($null -eq $reading) { '' } else { $reading.ProviderStatusName }
            MappedState = if ($null -eq $reading) { 'Unreadable' } else { $reading.MappedState }
            Failure = if ($null -eq $reading) { 'RootMissing' } else { $reading.Failure }
        }
        $currentRootRows.Add($row)
        $rootRows.Add($row)
    }

    $io = Get-OneDriveIoSample
    $elapsed = 0.0
    $delta = [decimal]0
    $rate = 0.0
    if ($lastTotal -ge 0 -and $now -gt $lastSampled) {
        $elapsed = ($now - $lastSampled).TotalSeconds
        if ($io.TotalBytes -ge $lastTotal) { $delta = $io.TotalBytes - $lastTotal }
        $rate = [double]$delta / [double]$elapsed
    }

    $withinHold = $lastActive -ne [DateTimeOffset]::MinValue -and
        ($now - $lastActive).TotalSeconds -le $activityHoldSeconds
    $transferring = $withinHold
    if ($lastTotal -ge 0 -and $now -gt $lastSampled) {
        if ($rate -lt $activityThresholdBytesPerSecond) {
            $consecutive = 0
        }
        else {
            $consecutive++
            if ($consecutive -ge $activitySamplesToAssert -or $withinHold) {
                $lastActive = $now
                $transferring = $true
            }
            else {
                $transferring = $false
            }
        }
    }

    $activityRows.Add([pscustomobject]@{
        TimestampUtc = $now.ToString('O')
        ProcessCount = $io.ProcessCount
        ReadableProcessCount = $io.ReadableProcessCount
        IoReadFailures = $io.Failures
        TotalBytes = $io.TotalBytes
        DeltaBytes = $delta
        ElapsedSeconds = [Math]::Round($elapsed, 3)
        RateKiBps = [Math]::Round($rate / 1024, 3)
        Above16KiBps = $rate -ge $activityThresholdBytesPerSecond
        ConsecutiveAboveThreshold = $consecutive
        JKBarActivityGate = $transferring
        JKBarComputedState = Get-JKBarState -RootRows $currentRootRows.ToArray() -Transferring $transferring
    })

    $lastTotal = $io.TotalBytes
    $lastSampled = $now
    if ($now -ge $collectionEnds) { break }

    $remainingMilliseconds = [int][Math]::Min(
        $SampleIntervalSeconds * 1000,
        [Math]::Max(1, ($collectionEnds - [DateTimeOffset]::UtcNow).TotalMilliseconds))
    Start-Sleep -Milliseconds $remainingMilliseconds
} while ([DateTimeOffset]::UtcNow -lt $collectionEnds.AddMilliseconds(100))

$collectionFinished = [DateTimeOffset]::UtcNow
$metadataRows = @(Get-EventMetadata -From $collectionStarted.AddMinutes(-5) -To $collectionFinished.AddMinutes(1))
$oneDriveProcesses = @(Get-OneDriveProcesses)
$resolvedJKBarPath = Find-JKBarPath
$jkBarIdentity = Get-FileIdentity -Path $resolvedJKBarPath -IncludeHash $true

$activityGateSamples = @($activityRows | Where-Object { $_.JKBarActivityGate }).Count
$computedRedSamples = @($activityRows | Where-Object {
    $_.JKBarComputedState -in @('Synchronizing', 'Error', 'Unknown')
}).Count
$maxRateKiBps = 0.0
if ($activityRows.Count -gt 0) {
    $maxRateKiBps = [double](($activityRows | Measure-Object -Property RateKiBps -Maximum).Maximum)
}

$summary = [ordered]@{
    SchemaVersion = 1
    Collector = 'JKBar OneDrive diagnostics'
    CollectorSHA256 = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
    CollectionStartedUtc = $collectionStarted.ToString('O')
    CollectionFinishedUtc = $collectionFinished.ToString('O')
    RequestedDurationSeconds = $DurationSeconds
    SampleIntervalSeconds = $SampleIntervalSeconds
    ObservedOneDriveStatus = $ObservedOneDriveStatus
    ObservedJKBarStatus = $ObservedJKBarStatus
    Environment = [ordered]@{
        ProcessArchitecture = $architecture.ProcessArchitecture
        OSArchitecture = $architecture.OSArchitecture
        ArchitectureSource = $architecture.Source
        ArchitectureFailure = $architecture.Failure
        OSDescription = [Environment]::OSVersion.VersionString
        PowerShellVersion = $PSVersionTable.PSVersion.ToString()
        Elevated = Test-IsAdministrator
    }
    JKBar = $jkBarIdentity
    OneDrive = [ordered]@{
        Scope = 'CurrentUserAndSession'
        Processes = $oneDriveProcesses
        RegistryProviderCount = $syncRoots.ProviderCount
        SyncRootCount = $rootDefinitions.Count
    }
    Results = [ordered]@{
        ActivitySampleCount = $activityRows.Count
        RootSampleCount = $rootRows.Count
        EventMetadataCount = $metadataRows.Count
        ActivityGateTrueSamples = $activityGateSamples
        ComputedRedSamples = $computedRedSamples
        MaximumRateKiBps = [Math]::Round($maxRateKiBps, 3)
    }
    Privacy = [ordered]@{
        Excluded = @(
            'account and device names',
            'raw registry key and value names',
            'sync-root paths and file names',
            'JKBar settings and error-log contents',
            'OneDrive logs and event messages'
        )
        NetworkRequests = 0
        ApplicationStateChanges = 0
    }
}

$reportId = '{0}-{1}' -f (Get-Date -Format 'yyyyMMdd-HHmmss'), ([Guid]::NewGuid().ToString('N').Substring(0, 8))
$workingDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "JKBar-OneDrive-Diagnostics-$reportId"
$archivePath = $null
try {
    New-Item -ItemType Directory -Path $workingDirectory | Out-Null
    $summary | ConvertTo-Json -Depth 8 |
        Set-Content -LiteralPath (Join-Path $workingDirectory 'summary.json') -Encoding UTF8
    $activityRows | Export-Csv -LiteralPath (Join-Path $workingDirectory 'activity.csv') -NoTypeInformation -Encoding UTF8
    $rootRows | Export-Csv -LiteralPath (Join-Path $workingDirectory 'sync-root-samples.csv') -NoTypeInformation -Encoding UTF8
    $metadataRows | Export-Csv -LiteralPath (Join-Path $workingDirectory 'event-metadata.csv') -NoTypeInformation -Encoding UTF8

    @'
This report contains only the inputs needed to compare OneDrive with JKBar's status estimate.

summary.json             Environment, versions, architectures, observations, and aggregate counts.
activity.csv             Current-session OneDrive I/O and JKBar's 16 KiB/s, 3-sample, 15-second activity gate.
sync-root-samples.csv    Current-user Cloud Files status for anonymous root IDs.
event-metadata.csv       Time, provider, event ID, and level only. Event messages are excluded.

No reset, restart, registry write, network request, or application configuration change was performed.
'@ | Set-Content -LiteralPath (Join-Path $workingDirectory 'README.txt') -Encoding UTF8

    $resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
    New-Item -ItemType Directory -Path $resolvedOutputDirectory -Force | Out-Null
    $archivePath = Join-Path $resolvedOutputDirectory "JKBar-OneDrive-Diagnostics-$reportId.zip"
    Compress-Archive -Path (Join-Path $workingDirectory '*') -DestinationPath $archivePath -CompressionLevel Optimal
}
finally {
    if ([System.IO.Directory]::Exists($workingDirectory)) {
        Remove-Item -LiteralPath $workingDirectory -Recurse -Force
    }
}

Write-Host ''
Write-Host "Report created: $archivePath"
Write-Host 'Review summary.json before sharing the ZIP.'