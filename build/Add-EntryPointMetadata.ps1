param(
	[string]$CatalogRoot = "bundled-catalog/obra.superpowers/2026-09-21"
)

# Backfills adapter/plan-style metadata for the remaining seven Visual Studio entry points
# (Execute, Debug, TDD, Review, Verify, Refactor, Finish) into every already-bundled release
# folder. This does not download anything from upstream: it only adds separate adapter
# metadata files alongside the unchanged, already-bundled upstream source archive and license,
# following docs/superpowers/specs/upstream-superpowers-review.md's entry-point mapping table.
$ErrorActionPreference = 'Stop'

function Get-Sha256([string]$path)
{
	return (Get-FileHash -Path $path -Algorithm SHA256).Hash.ToLowerInvariant()
}

$repoUrl = 'https://github.com/obra/superpowers'

# entryPointId -> (displayEntryPoint, composition steps, notes)
$entryPointDefinitions = [ordered]@{
	Execute = [ordered]@{
		actionId = 'execute-plan'
		composition = @(
			[ordered]@{ order = 1; skillPath = 'skills/executing-plans/SKILL.md'; purpose = 'Load the accepted plan and track task/evidence state while executing it.' }
		)
		notes = @(
			'Execute loads an accepted plan and tracks task/evidence state; it does not fabricate subagent dispatch.',
			'subagent-driven-development is an optional, separately capability-gated composition and is not included by default.'
		)
	}
	Debug = [ordered]@{
		actionId = 'debug-investigate'
		composition = @(
			[ordered]@{ order = 1; skillPath = 'skills/systematic-debugging/SKILL.md'; purpose = 'Attach diagnostics and scoped context, and retain diagnosis before any fix is applied.' }
		)
		notes = @(
			'Debug requires diagnosis evidence before permitted fixes, per systematic-debugging.'
		)
	}
	TDD = [ordered]@{
		actionId = 'tdd-cycle'
		composition = @(
			[ordered]@{ order = 1; skillPath = 'skills/test-driven-development/SKILL.md'; purpose = 'Track observed red/green evidence for each authorized edit.' }
		)
		notes = @(
			'TDD requires observed red/green evidence rather than an assumed pass.'
		)
	}
	Review = [ordered]@{
		actionId = 'review-request'
		composition = @(
			[ordered]@{ order = 1; skillPath = 'skills/requesting-code-review/SKILL.md'; purpose = 'Request a review with findings and disposition tracking.' },
			[ordered]@{ order = 2; skillPath = 'skills/receiving-code-review/SKILL.md'; purpose = 'Process review findings and record disposition.' }
		)
		notes = @(
			'Review capability-gates fresh reviewer/subagent requirements and labels manual review honestly rather than claiming independent review.'
		)
	}
	Verify = [ordered]@{
		actionId = 'verify-completion'
		composition = @(
			[ordered]@{ order = 1; skillPath = 'skills/verification-before-completion/SKILL.md'; purpose = 'Bind actual build/test results to source/configuration before claiming completion.' }
		)
		notes = @(
			'Verify never infers success from a prompt or marker file; it requires bound, scoped evidence.'
		)
	}
	Refactor = [ordered]@{
		actionId = 'refactor-composition'
		composition = @(
			[ordered]@{ order = 1; skillPath = 'skills/writing-plans/SKILL.md'; purpose = 'Plan the refactor scope and behavior baseline as needed.' },
			[ordered]@{ order = 2; skillPath = 'skills/test-driven-development/SKILL.md'; purpose = 'Preserve behavior using red/green evidence while refactoring.' },
			[ordered]@{ order = 3; skillPath = 'skills/requesting-code-review/SKILL.md'; purpose = 'Request review of the refactor.' },
			[ordered]@{ order = 4; skillPath = 'skills/verification-before-completion/SKILL.md'; purpose = 'Verify the behavior baseline still holds before completion.' }
		)
		notes = @(
			'Refactor is an approved product-specific composition of upstream planning/TDD/review/verification skills.',
			'There is no dedicated upstream Refactor skill in the reviewed tree; this composition is explicitly labeled as adapter-owned, not a claimed upstream skill.'
		)
	}
	Finish = [ordered]@{
		actionId = 'finish-branch'
		composition = @(
			[ordered]@{ order = 1; skillPath = 'skills/finishing-a-development-branch/SKILL.md'; purpose = 'Show readiness and explicit next actions for finishing the branch.' },
			[ordered]@{ order = 2; skillPath = 'skills/verification-before-completion/SKILL.md'; purpose = 'Verify completion before reporting the branch as ready.' }
		)
		notes = @(
			'Finish reports readiness truthfully; it never automatically commits, merges, pushes, removes worktrees or deletes branches.'
		)
	}
}

$releasesRoot = Join-Path $CatalogRoot 'releases'
$catalogPath = Join-Path $CatalogRoot 'catalog.json'
$catalog = Get-Content -Raw -Path $catalogPath | ConvertFrom-Json

foreach ($releaseEntry in $catalog.releases)
{
	$tag = $releaseEntry.releaseTag
	$releaseDir = Join-Path $releasesRoot $tag

	$adapterManifestPath = Join-Path $releaseDir 'adapter-manifest.json'
	$adapterManifest = Get-Content -Raw -Path $adapterManifestPath | ConvertFrom-Json
	$actions = [System.Collections.Generic.List[object]]::new()
	foreach ($existingAction in $adapterManifest.actions)
	{
		$actions.Add($existingAction)
	}

	$provenancePath = Join-Path $releaseDir 'provenance.json'
	$provenance = Get-Content -Raw -Path $provenancePath | ConvertFrom-Json
	$provenanceFiles = [System.Collections.Generic.List[object]]::new()
	foreach ($existingFile in $provenance.files)
	{
		$provenanceFiles.Add($existingFile)
	}

	$entryPointMetadataPaths = [ordered]@{}
	if ($releaseEntry.planMetadataPath)
	{
		$entryPointMetadataPaths['Plan'] = $releaseEntry.planMetadataPath
	}

	foreach ($entryPointId in $entryPointDefinitions.Keys)
	{
		$definition = $entryPointDefinitions[$entryPointId]
		$fileName = "$($entryPointId.ToLowerInvariant())-metadata.json"
		$metadataPath = Join-Path $releaseDir $fileName

		$metadata = [ordered]@{
			schemaVersion = 1
			entryPoint = $entryPointId
			sourceRepository = $repoUrl
			releaseTag = $tag
			composition = $definition.composition
			notes = $definition.notes
		}
		$metadata | ConvertTo-Json -Depth 8 | Set-Content -Path $metadataPath -Encoding UTF8

		$relativeMetadataPath = "releases/$tag/$fileName"
		$entryPointMetadataPaths[$entryPointId] = $relativeMetadataPath

		if (-not ($actions | Where-Object { $_.actionId -eq $definition.actionId }))
		{
			foreach ($step in $definition.composition)
			{
				$actionId = if ($definition.composition.Count -eq 1) { $definition.actionId } else { "$($definition.actionId)-$($step.order)" }
				if (-not ($actions | Where-Object { $_.actionId -eq $actionId }))
				{
					$actions.Add([ordered]@{ actionId = $actionId; skillPath = $step.skillPath; requiresApproval = $false })
				}
			}
		}

		if (-not ($provenanceFiles | Where-Object { $_.path -eq $fileName }))
		{
			$provenanceFiles.Add([ordered]@{ path = $fileName; sha256 = (Get-Sha256 $metadataPath) })
		}
	}

	$adapterManifest = [ordered]@{ schemaVersion = $adapterManifest.schemaVersion; actions = $actions }
	$adapterManifest | ConvertTo-Json -Depth 8 | Set-Content -Path $adapterManifestPath -Encoding UTF8

	# adapter-manifest.json content changed, so refresh its own provenance hash entry too.
	$adapterManifestFileEntry = $provenanceFiles | Where-Object { $_.path -eq 'adapter-manifest.json' }
	if ($adapterManifestFileEntry)
	{
		$adapterManifestFileEntry.sha256 = Get-Sha256 $adapterManifestPath
	}

	$provenance = [ordered]@{
		schemaVersion = $provenance.schemaVersion
		repositoryUrl = $provenance.repositoryUrl
		releaseId = $provenance.releaseId
		releaseTag = $provenance.releaseTag
		releaseName = $provenance.releaseName
		prerelease = $provenance.prerelease
		publishedAtUtc = $provenance.publishedAtUtc
		bundledAtUtc = $provenance.bundledAtUtc
		resolvedCommit = $provenance.resolvedCommit
		sourceArchiveUrl = $provenance.sourceArchiveUrl
		licenseSourceUrl = $provenance.licenseSourceUrl
		files = $provenanceFiles
	}
	$provenance | ConvertTo-Json -Depth 8 | Set-Content -Path $provenancePath -Encoding UTF8

	$releaseEntry | Add-Member -Name 'entryPointMetadataPaths' -MemberType NoteProperty -Value $entryPointMetadataPaths -Force

	Write-Output "Updated release '$tag' with $($entryPointDefinitions.Count) additional entry-point metadata files."
}

$catalog | ConvertTo-Json -Depth 8 | Set-Content -Path $catalogPath -Encoding UTF8
Write-Output "Updated catalog at $catalogPath with entry-point metadata paths for $($catalog.releases.Count) release(s)."
