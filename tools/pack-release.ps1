<#
.SYNOPSIS
Builds one release: a versioned zip, its checksums and the version manifest the updater reads.

.DESCRIPTION
Everything the updater needs comes out of this one script, so a release cannot be published with a manifest and
an archive that disagree. The version is read from Directory.Build.props rather than passed in, because that file
is the single source of truth the running app also reports.

Nothing is uploaded here. Publishing is a separate, deliberate step.
#>
[CmdletBinding()]
param(
    [string] $OutputRoot = (Join-Path $PSScriptRoot '..\releases')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
$props = Join-Path $repo 'Directory.Build.props'
$version = ([xml](Get-Content $props)).Project.PropertyGroup.Version
if (-not $version) {
    throw "Directory.Build.props에서 Version을 읽지 못했습니다."
}

Write-Host "JKBar $version 릴리스를 만듭니다."

$staging = Join-Path ([System.IO.Path]::GetTempPath()) "jkbar-release-$version"
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Path $staging | Out-Null

& dotnet publish (Join-Path $repo 'src\JKBar.App\JKBar.App.csproj') -c Release -o $staging --nologo
if ($LASTEXITCODE -ne 0) { throw "publish 실패" }

# Never ship a developer's own settings or a stray staging folder.
Get-ChildItem $staging -Filter 'settings.json' | Remove-Item -Force
Get-ChildItem $staging -Filter 'user_image_*' | Remove-Item -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $repo 'LICENSE') $staging
Copy-Item (Join-Path $repo 'THIRD-PARTY-NOTICES.txt') $staging

$release = Join-Path $OutputRoot "jkbar-$version"
if (Test-Path $release) { Remove-Item $release -Recurse -Force }
New-Item -ItemType Directory -Path $release -Force | Out-Null

$package = "JKBar-$version-win-x64.zip"
$archive = Join-Path $release $package
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $archive -CompressionLevel Optimal

# The updater reads the archive hash first and the executable hash second, so both are published.
$lines = @(
    "SHA-256 checksums for JKBar $version (win-x64).",
    '',
    ('{0}  {1}' -f (Get-FileHash $archive -Algorithm SHA256).Hash, $package),
    '',
    'Files inside the archive:',
    ''
)
foreach ($file in Get-ChildItem $staging -File | Sort-Object Name) {
    $lines += ('{0}  {1}' -f (Get-FileHash $file.FullName -Algorithm SHA256).Hash, $file.Name)
}

Set-Content -Path (Join-Path $release 'SHA256SUMS.txt') -Value $lines -Encoding ascii
[pscustomobject]@{ version = $version } | ConvertTo-Json -Compress |
    Set-Content -Path (Join-Path $release 'version.json') -Encoding ascii

Remove-Item $staging -Recurse -Force

Write-Host ''
Write-Host "만들어진 파일: $release"
Get-ChildItem $release | Select-Object Name, Length | Format-Table | Out-String | Write-Host
Write-Host "다음 단계: gh release create v$version (릴리스 체크리스트를 먼저 끝낼 것)"
