[CmdletBinding()]
param(
    [string]$ExecutablePath = (Join-Path $PSScriptRoot '..\assets\app\PacificClock.exe'),
    [string]$ReportPath
)
$ErrorActionPreference = 'Stop'
$temporaryReport = -not $ReportPath
if ($temporaryReport) { $ReportPath = Join-Path ([IO.Path]::GetTempPath()) ('pacific-clock-' + [guid]::NewGuid().ToString('N') + '.txt') }
$report = [IO.Path]::GetFullPath($ReportPath)
$executable = (Resolve-Path -LiteralPath $ExecutablePath).Path
try {
    $process = Start-Process -FilePath $executable -ArgumentList @('--self-test', ('"' + $report + '"')) -PassThru -WindowStyle Hidden
    if (-not $process.WaitForExit(30000)) {
        Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
        throw 'The clock self-test timed out.'
    }
    $process.Refresh()
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $report)) { throw 'The clock self-test failed.' }
    $result = Get-Content -LiteralPath $report -Raw
    Write-Output $result.Trim()
    if ($result -notmatch 'Failed: 0') { throw 'The report did not confirm success.' }
} finally {
    if ($temporaryReport -and (Test-Path -LiteralPath $report)) { Remove-Item -LiteralPath $report }
}
