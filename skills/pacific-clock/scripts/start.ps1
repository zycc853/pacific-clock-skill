[CmdletBinding()]
param(
    [string]$DestinationPath = (Join-Path $env:LOCALAPPDATA 'PacificClock'),
    [switch]$Find
)
$ErrorActionPreference = 'Stop'
$prepared = & (Join-Path $PSScriptRoot 'prepare.ps1') -DestinationPath $DestinationPath
if (-not ('PacificClockSkill.DesktopInfo' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
namespace PacificClockSkill {
    public static class DesktopInfo {
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] static extern IntPtr GetThreadDesktop(uint id);
        [DllImport("user32.dll")] static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
        [DllImport("user32.dll")] static extern bool CloseDesktop(IntPtr handle);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern bool GetUserObjectInformation(IntPtr handle, int index, StringBuilder value, int size, out int needed);
        static string Name(IntPtr handle) {
            if(handle == IntPtr.Zero) return "";
            var value = new StringBuilder(512); int needed;
            return GetUserObjectInformation(handle, 2, value, 1024, out needed) ? value.ToString() : "";
        }
        public static string Current() { return Name(GetThreadDesktop(GetCurrentThreadId())); }
        public static string Input() {
            IntPtr handle = OpenInputDesktop(0, false, 1);
            try { return Name(handle); }
            finally { if(handle != IntPtr.Zero) CloseDesktop(handle); }
        }
    }
}
'@
}
$currentDesktop = [PacificClockSkill.DesktopInfo]::Current()
$inputDesktop = [PacificClockSkill.DesktopInfo]::Input()
if (-not $currentDesktop -or -not $inputDesktop -or $currentDesktop -ne $inputDesktop) {
    [pscustomobject]@{
        status = 'requires-interactive-launch'
        appPath = $prepared.appPath
        finderPath = $prepared.finderPath
        executionDesktop = $currentDesktop
        inputDesktop = $inputDesktop
    }
    return
}
$launchPath = if ($Find) { $prepared.finderPath } else { $prepared.appPath }
Start-Process -FilePath $launchPath -WindowStyle Normal
[pscustomobject]@{ status = 'launch-requested'; appPath = $prepared.appPath; finderPath = $prepared.finderPath }
