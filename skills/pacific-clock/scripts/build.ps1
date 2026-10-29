[CmdletBinding()]
param([string]$OutputDirectory = (Join-Path $PSScriptRoot '..\assets\app'))
$ErrorActionPreference = 'Stop'
$compiler = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) { throw 'The Windows .NET Framework C# compiler was not found.' }
$sourceDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\assets\source'))
$output = [IO.Path]::GetFullPath($OutputDirectory)
[void][IO.Directory]::CreateDirectory($output)
$clockPath = Join-Path $output 'PacificClock.exe'
$finderPath = Join-Path $output 'FindPacificClock.exe'
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.dll "/out:$clockPath" (Join-Path $sourceDirectory 'PacificClock.cs')
if ($LASTEXITCODE -ne 0) { throw 'Clock compilation failed.' }
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /reference:System.Windows.Forms.dll "/out:$finderPath" (Join-Path $sourceDirectory 'FindPacificClock.cs')
if ($LASTEXITCODE -ne 0) { throw 'Finder compilation failed.' }
[pscustomobject]@{ appPath = $clockPath; finderPath = $finderPath }
