[CmdletBinding()]
param([string]$DestinationPath = (Join-Path $env:LOCALAPPDATA 'PacificClock'))
$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'Pacific Clock supports Windows only.'
}
$sourceDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\assets\app'))
$targetDirectory = [IO.Path]::GetFullPath($DestinationPath)
$executables = @('PacificClock.exe', 'FindPacificClock.exe')
foreach ($name in $executables) {
    if (-not (Test-Path -LiteralPath (Join-Path $sourceDirectory $name) -PathType Leaf)) {
        throw "Bundled executable missing: $name. Run scripts/build.ps1 first."
    }
}
[void][IO.Directory]::CreateDirectory($targetDirectory)
foreach ($name in $executables) {
    $source = Join-Path $sourceDirectory $name
    $target = Join-Path $targetDirectory $name
    if (Test-Path -LiteralPath $target -PathType Leaf) {
        if ((Get-FileHash -LiteralPath $source).Hash -eq (Get-FileHash -LiteralPath $target).Hash) { continue }
    }
    try { Copy-Item -LiteralPath $source -Destination $target -Force }
    catch { throw "Cannot update $target. Exit the existing Pacific Clock, then retry. No settings were replaced." }
}
[pscustomobject]@{
    appPath = Join-Path $targetDirectory 'PacificClock.exe'
    finderPath = Join-Path $targetDirectory 'FindPacificClock.exe'
    settingsPath = Join-Path $targetDirectory 'PacificClock.settings.xml'
}
