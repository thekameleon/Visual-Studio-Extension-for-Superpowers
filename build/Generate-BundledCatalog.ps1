param(
	[string]$OutputRoot = "bundled-catalog/obra.superpowers/2026-09-21"
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$headers = @{ 'User-Agent' = 'GitHubCopilot' }
$repoApi = 'https://api.github.com/repos/obra/superpowers'
$repoUrl = 'https://github.com/obra/superpowers'
$root = Join-Path (Get-Location) $OutputRoot

if (Test-Path $root)
{
	Remove-Item $root -Recurse -Force
}

New-Item -ItemType Directory -Path $root | Out-Null
$releasesRoot = Join-Path $root 'releases'
New-Item -ItemType Directory -Path $releasesRoot | Out-Null

function Resolve-TagCommit([string]$tagName)
{
	$ref = Invoke-RestMethod -Uri "$repoApi/git/ref/tags/$tagName" -Headers $headers
	if ($ref.object.type -eq 'tag')
	{
		$tagObject = Invoke-RestMethod -Uri $ref.object.url -Headers $headers
		return $tagObject.object.sha
	}

	return $ref.object.sha
}

function Get-Sha256([string]$path)
{
	return (Get-FileHash -Path $path -Algorithm SHA256).Hash.ToLowerInvariant()
}

$cutoffCapturedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
$releaseItems = Invoke-RestMethod -Uri "$repoApi/releases" -Headers $headers
$catalogEntries = @()

foreach ($release in $releaseItems)
{
	$tag = $release.tag_name
	$releaseDir = Join-Path $releasesRoot $tag
	New-Item -ItemType Directory -Path $releaseDir | Out-Null

	$archivePath = Join-Path $releaseDir 'source.zip'
	Invoke-WebRequest -Uri $release.zipball_url -Headers $headers -OutFile $archivePath

	$licensePath = Join-Path $releaseDir 'LICENSE.txt'
	Invoke-WebRequest -Uri "https://raw.githubusercontent.com/obra/superpowers/$tag/LICENSE" -Headers $headers -OutFile $licensePath

	$adapterManifest = [ordered]@{
		schemaVersion = 1
		actions = @(
			[ordered]@{ actionId = 'plan-brainstorm'; skillPath = 'skills/brainstorming/SKILL.md'; requiresApproval = $false },
			[ordered]@{ actionId = 'plan-write'; skillPath = 'skills/writing-plans/SKILL.md'; requiresApproval = $false }
		)
	}
	$adapterManifestPath = Join-Path $releaseDir 'adapter-manifest.json'
	$adapterManifest | ConvertTo-Json -Depth 8 | Set-Content -Path $adapterManifestPath -Encoding UTF8

	$planMetadata = [ordered]@{
		schemaVersion = 1
		entryPoint = 'Plan'
		sourceRepository = $repoUrl
		releaseTag = $tag
		composition = @(
			[ordered]@{ order = 1; skillPath = 'skills/brainstorming/SKILL.md'; purpose = 'Gather requirements and explore options before drafting.' },
			[ordered]@{ order = 2; skillPath = 'skills/writing-plans/SKILL.md'; purpose = 'Produce the approved implementation plan once requirements are settled.' }
		)
		notes = @(
			'Plan is product metadata layered over unchanged upstream content.',
			'This metadata does not rewrite upstream skill prose or authorize execution.'
		)
	}
	$planMetadataPath = Join-Path $releaseDir 'plan-metadata.json'
	$planMetadata | ConvertTo-Json -Depth 8 | Set-Content -Path $planMetadataPath -Encoding UTF8

	$platformMetadata = [ordered]@{
		schemaVersion = 1
		product = 'SuperPowers for Visual Studio'
		menuLabel = 'Superpowers'
		hostModel = 'VisualStudio.Extensibility'
		supportedIdeVersions = @('17.14+', '18.x')
		defaultExecutionMode = 'Guided'
		supportedExecutionModes = @('Guided', 'ApprovalRequired', 'Full')
		compatibility = [ordered]@{
			adapterStatus = 'SupportedByMetadata'
			testStatus = 'UntestedPerRelease'
			distribution = 'Bundled'
		}
	}
	$platformMetadataPath = Join-Path $releaseDir 'platform-metadata.json'
	$platformMetadata | ConvertTo-Json -Depth 8 | Set-Content -Path $platformMetadataPath -Encoding UTF8

	$resolvedCommit = Resolve-TagCommit $tag

	$provenance = [ordered]@{
		schemaVersion = 1
		repositoryUrl = $repoUrl
		releaseId = $release.id
		releaseTag = $tag
		releaseName = $release.name
		prerelease = [bool]$release.prerelease
		publishedAtUtc = $release.published_at
		bundledAtUtc = $cutoffCapturedAtUtc
		resolvedCommit = $resolvedCommit
		sourceArchiveUrl = $release.zipball_url
		licenseSourceUrl = "https://raw.githubusercontent.com/obra/superpowers/$tag/LICENSE"
		files = @(
			[ordered]@{ path = 'source.zip'; sha256 = (Get-Sha256 $archivePath) },
			[ordered]@{ path = 'LICENSE.txt'; sha256 = (Get-Sha256 $licensePath) },
			[ordered]@{ path = 'adapter-manifest.json'; sha256 = (Get-Sha256 $adapterManifestPath) },
			[ordered]@{ path = 'plan-metadata.json'; sha256 = (Get-Sha256 $planMetadataPath) },
			[ordered]@{ path = 'platform-metadata.json'; sha256 = (Get-Sha256 $platformMetadataPath) }
		)
	}
	$provenancePath = Join-Path $releaseDir 'provenance.json'
	$provenance | ConvertTo-Json -Depth 8 | Set-Content -Path $provenancePath -Encoding UTF8

	$catalogEntries += [ordered]@{
		releaseTag = $tag
		releaseId = $release.id
		prerelease = [bool]$release.prerelease
		publishedAtUtc = $release.published_at
		resolvedCommit = $resolvedCommit
		archivePath = "releases/$tag/source.zip"
		licensePath = "releases/$tag/LICENSE.txt"
		adapterManifestPath = "releases/$tag/adapter-manifest.json"
		planMetadataPath = "releases/$tag/plan-metadata.json"
		platformMetadataPath = "releases/$tag/platform-metadata.json"
		provenancePath = "releases/$tag/provenance.json"
	}
}

$catalog = [ordered]@{
	schemaVersion = 1
	sourceRepositoryUrl = $repoUrl
	cutoffCapturedAtUtc = $cutoffCapturedAtUtc
	bundledStableReleaseCount = @($catalogEntries | Where-Object { -not $_.prerelease }).Count
	bundledPrereleaseCount = @($catalogEntries | Where-Object { $_.prerelease }).Count
	releases = $catalogEntries
}
$catalog | ConvertTo-Json -Depth 8 | Set-Content -Path (Join-Path $root 'catalog.json') -Encoding UTF8

@"
Bundled catalog snapshot for obra/superpowers.

This directory captures the published upstream release inventory at the recorded cutoff.
Each release folder contains the unchanged upstream source archive, the upstream MIT license text,
and separate Visual Studio adapter metadata for Plan and platform labeling.
"@ | Set-Content -Path (Join-Path $root 'README.md') -Encoding UTF8

Write-Output "Generated bundled catalog at $root with $($catalogEntries.Count) release(s)."
