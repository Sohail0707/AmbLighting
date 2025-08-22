<#!
One-shot bootstrap for Windows:
 - Ensures a compatible .NET SDK (via dotnet-install, no system changes)
 - Publishes ColorExtractor as self-contained single-file exe
 - Registers a Hidden scheduled task to run at user logon
#>
Param(
    [switch]$Uninstall,
    [switch]$NoHidden,
    [switch]$NoStartNow
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Ensure-Admin {
    $current = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($current)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Write-Host 'Elevating to Administrator...'
        $psi = New-Object System.Diagnostics.ProcessStartInfo 'pwsh'
        $psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" $($MyInvocation.Line.Split(' ',2)[1])"
        $psi.Verb = 'runas'
        $psi.UseShellExecute = $true
        [Diagnostics.Process]::Start($psi) | Out-Null
        exit
    }
}

function Get-RepoRoot { Split-Path -Path $PSScriptRoot -Parent }

function Get-DotNet {
    $dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue)?.Source
    if ($dotnet) { return $dotnet }
    # Install user-local SDK (preview channel for net10)
    $installDir = Join-Path $env:USERPROFILE '.dotnet'
    New-Item -ItemType Directory -Force -Path $installDir | Out-Null
    $tmp = Join-Path $env:TEMP 'dotnet-install.ps1'
    Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $tmp -UseBasicParsing
    & pwsh -NoProfile -ExecutionPolicy Bypass -File $tmp -Channel '10.0.1xx' -Quality preview -InstallDir $installDir
    return (Join-Path $installDir 'dotnet.exe')
}

function Publish-App([string]$dotnetExe) {
    $root = Get-RepoRoot
    $proj = Join-Path $root 'ColorExtractor/ColorExtractor.csproj'
    & $dotnetExe restore $proj
    # Self-contained, single file
    & $dotnetExe publish $proj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false | Out-Null
    $pub = Join-Path $root 'ColorExtractor/bin/Release/net10.0-windows/win-x64/publish/ColorExtractor.exe'
    if (-not (Test-Path $pub)) { throw "Publish failed, exe not found: $pub" }
    return $pub
}

function Register-Startup([string]$exePath) {
    $root = Get-RepoRoot
    $installer = Join-Path $root 'scripts/install-task.ps1'
    if (-not (Test-Path $installer)) { throw 'Missing scripts/install-task.ps1' }
    $hidden = if ($NoHidden) { @() } else { @('-Hidden') }
    $runNow = if ($NoStartNow) { @() } else { @('-RunNow') }
    & pwsh -NoProfile -ExecutionPolicy Bypass -File $installer -ExePath $exePath -TriggerType AtLogon @hidden @runNow
}

function Unregister-Startup {
    $root = Get-RepoRoot
    $installer = Join-Path $root 'scripts/install-task.ps1'
    if (Test-Path $installer) {
        & pwsh -NoProfile -ExecutionPolicy Bypass -File $installer -Remove
    }
}

if ($Uninstall) {
    Ensure-Admin
    Unregister-Startup
    Write-Host 'Uninstalled scheduled task.'
    exit 0
}

Ensure-Admin
$dotnet = Get-DotNet
$exe = Publish-App $dotnet
Register-Startup $exe
Write-Host "Setup complete. The app will run at logon (Hidden=${(-not $NoHidden)})."
