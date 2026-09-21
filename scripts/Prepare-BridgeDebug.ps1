[CmdletBinding(SupportsShouldProcess = $true)]
param(
	[ValidateSet('2022', '2026')]
	[string] $VisualStudioVersion,

	[ValidatePattern('^[a-zA-Z0-9_-]+$')]
	[string] $InstanceId
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $VisualStudioVersion -and -not $InstanceId) {
	throw 'Specify -InstanceId or -VisualStudioVersion. Interactive target selection is not supported.'
}

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere -PathType Leaf)) {
	throw "Visual Studio Installer discovery tool not found: $vswhere"
}

$range = if (-not $VisualStudioVersion) { '[17.14,19.0)' } elseif ($VisualStudioVersion -eq '2022') { '[17.14,18.0)' } else { '[18.0,19.0)' }
$json = & $vswhere -products * -version $range -requires Microsoft.Component.MSBuild -format json -utf8
if ($LASTEXITCODE -ne 0) {
	throw "Visual Studio discovery failed with exit code $LASTEXITCODE."
}

$instances = @($json | ConvertFrom-Json | Where-Object {
	$_.isComplete -and $_.isLaunchable -and
	(Test-Path -LiteralPath (Join-Path $_.installationPath 'Common7\IDE\devenv.exe'))
})
if ($InstanceId) {
	$instances = @($instances | Where-Object { $_.instanceId -eq $InstanceId })
}
if ($instances.Count -ne 1) {
	$choices = ($instances | ForEach-Object { "$($_.instanceId): $($_.installationPath)" }) -join '; '
	throw "Expected one VS $VisualStudioVersion IDE installation, found $($instances.Count). Use -InstanceId to select explicitly. Candidates: $choices"
}

$instance = $instances[0]
$VisualStudioVersion = if ([version]$instance.installationVersion -lt [version]'18.0') { '2022' } else { '2026' }
if ($instance.instanceId -notmatch '^[a-zA-Z0-9_-]+$') {
	throw 'Unexpected Visual Studio instance identifier; refusing deployment.'
}
$devenv = Join-Path $instance.installationPath 'Common7\IDE\devenv.exe'
$msbuild = Join-Path $instance.installationPath 'MSBuild\Current\Bin\amd64\MSBuild.exe'
$project = Join-Path $PSScriptRoot '..\TheKameleon.Superpowers.InProcess\TheKameleon.Superpowers.InProcess.csproj'
foreach ($file in @($devenv, $msbuild, $project)) {
	if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
		throw "Required file not found: $file"
	}
}

$arguments = @(
	$project,
	'/restore',
	'/t:PrepareBridgeDebug',
	'/p:Configuration=Debug',
	'/p:BridgeDebugDeployment=true',
	'/p:DeployExtension=true',
	'/p:CreateVsixContainer=true',
	'/p:VSSDKTargetPlatformRegRootSuffix=Exp',
	"/p:DeployTargetInstanceId=$($instance.instanceId)",
	'/v:minimal',
	'/nologo'
)

Write-Host "Target: $($instance.displayName) ($($instance.instanceId)) /RootSuffix Exp"
Write-Host "Installation: $($instance.installationPath)"
Write-Host "MSBuild: $msbuild"
Write-Host "Arguments: $($arguments -join ' ')"
if (-not $PSCmdlet.ShouldProcess("$($instance.instanceId) /RootSuffix Exp", 'Build and deploy the Superpowers in-process bridge')) {
	return
}

$processes = @(Get-CimInstance Win32_Process -Filter "Name = 'devenv.exe'")
foreach ($process in $processes) {
	if (-not $process.ExecutablePath -or -not $process.CommandLine) {
		throw 'Cannot verify a running devenv process target. Close experimental IDE instances and retry; no process will be terminated automatically.'
	}
	if ($process.ExecutablePath -ieq $devenv -and
		$process.CommandLine -match '(?i)[/-]rootsuffix(?:\s+|:)"?Exp"?(?=\s|$)') {
		throw "Close VS $VisualStudioVersion Exp (PID $($process.ProcessId)) before refreshing the bridge. The main IDE can remain open."
	}
}

& $msbuild @arguments
if ($LASTEXITCODE -ne 0) {
	throw "Bridge debug preparation failed with exit code $LASTEXITCODE. Review the MSBuild output; deployment/activation is not confirmed."
}

Write-Host 'Bridge build/deployment completed. Runtime activation is not yet verified.'
Write-Host 'Now use F5 on TheKameleon.Superpowers.Vsix, selecting this same IDE installation and Exp profile.'
Write-Host 'This script does not launch an IDE, attach a debugger, install the modern VSIX, or change Marketplace packaging.'
