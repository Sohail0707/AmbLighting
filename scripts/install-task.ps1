Param(
	[string]$TaskName = "AmbLighting-ColorExtractor",
	[string]$ExePath,
	[ValidateSet("AtLogon","AtStartup")] [string]$TriggerType = "AtLogon",
	[string]$Arguments = "",
	[switch]$RunNow,
	[switch]$Remove,
	[switch]$Hidden
)

function Get-DefaultExePath {
	$repoRoot = Split-Path -Path $PSScriptRoot -Parent
	$candidates = @(
		(Join-Path $repoRoot 'ColorExtractor\bin\Release\net10.0-windows\ColorExtractor.exe'),
		(Join-Path $repoRoot 'ColorExtractor\bin\Release\net10.0\ColorExtractor.exe'),
		(Join-Path $repoRoot 'ColorExtractor\bin\Debug\net10.0-windows\ColorExtractor.exe'),
		(Join-Path $repoRoot 'ColorExtractor\bin\Debug\net10.0\ColorExtractor.exe')
	)
	foreach ($p in $candidates) { if (Test-Path $p) { return $p } }
	return $candidates[0]
}

if ($Remove) {
	try {
		Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction Stop
		Write-Host "Removed scheduled task '$TaskName'."
	} catch {
		Write-Warning "Failed to remove task '$TaskName': $($_.Exception.Message)"
	}
	return
}

if (-not $ExePath) { $ExePath = Get-DefaultExePath }
if (-not (Test-Path $ExePath)) {
	throw "Executable not found at '$ExePath'. Build the project (Release) or pass -ExePath explicitly."
}

$user = if ($env:USERDOMAIN) { "$($env:USERDOMAIN)\$($env:USERNAME)" } else { $env:USERNAME }
$exeDir = Split-Path -Path $ExePath -Parent
if ([string]::IsNullOrWhiteSpace($Arguments)) {
	$action = New-ScheduledTaskAction -Execute $ExePath -WorkingDirectory $exeDir
} else {
	$action = New-ScheduledTaskAction -Execute $ExePath -Argument $Arguments -WorkingDirectory $exeDir
}
$trigger = if ($TriggerType -eq 'AtStartup') { New-ScheduledTaskTrigger -AtStartup } else { New-ScheduledTaskTrigger -AtLogOn }
$principal = New-ScheduledTaskPrincipal -UserId $user -RunLevel Highest -LogonType Interactive
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable
if ($Hidden) {
	$settings.Hidden = $true
}

try {
	Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force -ErrorAction Stop | Out-Null
	Write-Host "Registered task '$TaskName' to run '$ExePath' on $TriggerType."
} catch {
	Write-Error "Failed to register task '$TaskName': $($_.Exception.Message)"
	return
}

if ($RunNow) {
	try {
		Start-ScheduledTask -TaskName $TaskName -ErrorAction Stop
		Write-Host "Started task '$TaskName' now."
	} catch {
		Write-Warning "Task registered but failed to start now: $($_.Exception.Message). It should still run on $TriggerType."
	}
}

