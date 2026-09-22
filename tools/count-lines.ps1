<#
.SYNOPSIS
    Counts hand-written lines of code and lines of configuration in 4Elements Events.

.DESCRIPTION
    Wraps cloc, but answers a question cloc on its own answers badly. A bare
    `cloc --vcs=git` over this repo reports ~111,000 lines, and roughly half of
    that is vendored Bootstrap sitting in wwwroot/lib plus EF Core migration
    scaffolding — neither of which anyone wrote. This script buckets every file
    by PATH first (authored / vendored / generated / tooling) and only then by
    language, so the headline figure is work actually done.

    Files come from git — tracked files plus untracked ones that aren't
    gitignored — so new work counts before it is committed and bin/obj never do.

    Nothing is silently dropped: every excluded bucket is reported with its own
    total, and any file cloc doesn't recognise is called out by extension.

    Also splits the authored total into main code and test code, because a
    static-analysis tool's own "Lines of Code" figure usually means main code
    ONLY — comparing this script's headline total against one without making
    that same cut compares two different things. See "Main code vs tests"
    below.

.PARAMETER RepoRoot
    Repository to count. Defaults to the parent of this script's folder.

.PARAMETER ByProject
    Also break the authored total down per project.

.PARAMETER IncludeExcluded
    List the individual files in each excluded bucket.

.PARAMETER Json
    Emit the result as JSON instead of a formatted report.

.PARAMETER Html
    Write the result as JSON plus a page that renders it, and open the page in
    the default browser.

.PARAMETER OutputPath
    Where -Html writes. A folder, or a path ending in .html. Defaults to the
    temp folder — deliberately outside the repository, since a page written
    into it would be counted as authored work on the next run.

.PARAMETER NoOpen
    Write the page but don't launch a browser.

.EXAMPLE
    .\tools\count-lines.ps1

.EXAMPLE
    .\tools\count-lines.ps1 -ByProject

.EXAMPLE
    .\tools\count-lines.ps1 -Html
#>
[CmdletBinding()]
param(
    [string]$RepoRoot,
    [switch]$ByProject,
    [switch]$IncludeExcluded,
    [switch]$Json,
    [switch]$Html,
    [string]$OutputPath,
    [switch]$NoOpen
)

$ErrorActionPreference = 'Stop'

if (-not $RepoRoot) { $RepoRoot = Split-Path -Parent $PSScriptRoot }
$RepoRoot = (Resolve-Path $RepoRoot).Path

if (-not (Get-Command cloc -ErrorAction SilentlyContinue)) {
    throw "cloc is not on PATH. Install it (winget install AlDanial.Cloc) and retry."
}
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw "git is not on PATH."
}

# --- What counts as what -----------------------------------------------------
#
# Path rules run BEFORE language rules, because a .css file is authored work in
# Components/ and somebody else's download in wwwroot/lib. Order matters within
# this list too: the first bucket that matches wins.

$pathBuckets = @(
    @{ Name = 'binary'; Why = 'images, fonts and source maps — no lines to count'; Patterns = @(
        '\.(png|jpe?g|gif|ico|bmp|webp|woff2?|ttf|eot|pdf|zip|dll|exe|pfx|snk)$'
        '\.map$'
    )},
    @{ Name = 'vendored'; Why = 'third-party assets shipped as-is'; Patterns = @(
        # Name the vendor folder, not wwwroot/lib as a whole — Web.Public keeps
        # its OWN logos and animation.js under lib/, and a blanket rule there
        # quietly writes authored code out of the total.
        '/wwwroot/lib/bootstrap/'
        '/node_modules/'
        '/package-lock\.json$'
    )},
    @{ Name = 'generated'; Why = 'scaffolded by EF Core or the build, not typed'; Patterns = @(
        '\.Designer\.cs$'
        'ModelSnapshot\.cs$'
        '/Migrations/.*\.cs$'
        '\.g\.cs$'
        '^4ElementsEvents\.AppHost/bundle/'
    )},
    @{ Name = 'tooling'; Why = 'agent and editor scaffolding, not the product'; Patterns = @(
        '^\.agents/'
        '^\.claude/'
        '^TestResults/'
        '^tester-bundle/'
        '/(bin|obj)/'
    )}
)

# Languages cloc reports, sorted into the two figures asked for. Markdown is
# neither, and there is a lot of it here, so it gets its own line rather than
# being quietly folded into "code".
$codeLanguages = @(
    'C#', 'Razor', 'JavaScript', 'TypeScript', 'CSS', 'SCSS', 'Sass', 'LESS',
    'HTML', 'SQL', 'PowerShell', 'Bourne Shell', 'Bourne Again Shell',
    'DOS Batch', 'Python', 'F#', 'Visual Basic'
)
$configLanguages = @(
    'JSON', 'JSON5', 'YAML', 'XML', 'MSBuild script', 'INI', 'TOML',
    'Dockerfile', 'Bazel', 'CMake', 'Properties', 'dotnet-tools'
)
$docsLanguages = @('Markdown', 'Text', 'reStructuredText', 'AsciiDoc')

# Extensions cloc 2.10 doesn't know about. Postgres project files use .pgsql
# and .pgproj; .slnx is the new solution format.
$forceLang = @(
    '--force-lang=SQL,pgsql'
    '--force-lang=XML,pgproj'
    '--force-lang=XML,slnx'
)

# --- Gather the files --------------------------------------------------------

Push-Location $RepoRoot
try {
    $files = @(& git ls-files --cached --others --exclude-standard) |
        Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) }
    if ($LASTEXITCODE -ne 0) { throw "git ls-files failed in $RepoRoot" }
    if ($files.Count -eq 0) { throw "No files found under $RepoRoot" }

    $classified = foreach ($file in $files) {
        $bucket = 'authored'
        foreach ($rule in $pathBuckets) {
            if ($rule.Patterns | Where-Object { $file -match $_ }) {
                $bucket = $rule.Name
                break
            }
        }
        [PSCustomObject]@{ Path = $file; Bucket = $bucket }
    }

    # --- Which projects are test projects -------------------------------------
    #
    # Read off <IsTestProject>true</IsTestProject> on each csproj rather than
    # guessed by name. 4ElementsEvents.Tests.DataSeeder starts with "Tests." and
    # is NOT one — it references no test SDK and ships as a console tool, not a
    # suite — and it is exactly the case a name-based guess (`*.Tests.*`) would
    # get wrong, folding its ~2,900 lines into "tests" and understating what
    # actually ships. This is also the signal MSBuild-based tooling itself uses
    # to tell the two apart, so it is the same cut a scanner's own "Lines of
    # Code" figure is likely built on.
    $testProjectFolders = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    foreach ($csproj in ($files | Where-Object { $_ -like '*.csproj' })) {
        if ((Get-Content -LiteralPath $csproj -Raw) -match '<IsTestProject>\s*true\s*</IsTestProject>') {
            $testProjectFolders.Add(($csproj -split '/')[0]) | Out-Null
        }
    }

    # --- Measure -------------------------------------------------------------
    # One cloc pass over everything with --by-file, so each file carries its own
    # language and the buckets can be summed afterwards without re-running.

    $listFile = New-TemporaryFile
    try {
        Set-Content -LiteralPath $listFile -Value ($classified.Path) -Encoding UTF8
        # --skip-uniqueness matters more here than it looks. By default cloc
        # hashes files and counts identical ones once, and this solution has
        # real duplicates across the front ends — ReconnectModal.razor and its
        # .css and .js are byte-identical in Web.Admin and Web.Community, as are
        # several appsettings files. Those lines exist twice and are maintained
        # twice, so they are counted twice.
        $raw = & cloc "--list-file=$listFile" --by-file --json --quiet --skip-uniqueness @forceLang
        if (-not $raw) { throw "cloc produced no output." }
        $report = ($raw -join "`n") | ConvertFrom-Json
    }
    finally {
        Remove-Item -LiteralPath $listFile -Force -ErrorAction SilentlyContinue
    }
}
finally {
    Pop-Location
}

# cloc keys results by path and adds "header"/"SUM" alongside them; it also
# normalises separators and may prefix ./ — match back on the tail of the path.
$measured = @{}
foreach ($property in $report.PSObject.Properties) {
    if ($property.Name -in @('header', 'SUM')) { continue }
    $key = $property.Name -replace '\\', '/' -replace '^\./', ''
    $measured[$key] = $property.Value
}

$rows = foreach ($item in $classified) {
    $stat = $measured[$item.Path]
    if (-not $stat) { continue }   # unrecognised by cloc; reported separately below
    [PSCustomObject]@{
        Path     = $item.Path
        Bucket   = $item.Bucket
        Language = $stat.language
        Code     = [int]$stat.code
        Comment  = [int]$stat.comment
        Blank    = [int]$stat.blank
        Project  = ($item.Path -split '/')[0]
    }
}
$rows = @($rows)

# Only worth flagging for files we meant to count. Binaries and source maps are
# expected to come back empty, and saying so every run would teach you to skip
# the line that matters.
$unrecognised = @($classified | Where-Object { $_.Bucket -eq 'authored' -and -not $measured[$_.Path] })

function Get-Kind {
    param([string]$Language)
    if ($codeLanguages   -contains $Language) { return 'code' }
    if ($configLanguages -contains $Language) { return 'config' }
    if ($docsLanguages   -contains $Language) { return 'docs' }
    return 'other'
}

function Get-ReportPageTemplate {
    # One file, no CDN, no fetch: it is opened from disk as often as not, and
    # anything the page needs from the network is a blank panel on a laptop in
    # a hotel. __DATA__ is the same JSON -Json prints, embedded verbatim.
    @'
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>__TITLE__ — hand-written lines</title>
<style>
:root{
  color-scheme:light dark;
  --bg:#f7f7f5; --panel:#fff; --ink:#1b1b19; --muted:#73736c; --line:#e5e4df;
  --track:#ecebe6; --accent:#2f6f9f;
  --code:#2f6f9f; --config:#b07a2c; --docs:#78879a; --other:#a3a39c;
}
@media (prefers-color-scheme:dark){
  :root{
    --bg:#151517; --panel:#1d1d21; --ink:#e9e8e4; --muted:#98978e; --line:#2d2d33;
    --track:#26262c; --accent:#6aa9d8;
    --code:#6aa9d8; --config:#d6a45c; --docs:#93a2b5; --other:#7c7c76;
  }
}
*{box-sizing:border-box}
body{
  margin:0; padding:2.5rem 1.25rem 4rem; background:var(--bg); color:var(--ink);
  font:15px/1.5 ui-sans-serif,system-ui,"Segoe UI",Roboto,Helvetica,Arial,sans-serif;
}
main{max-width:60rem; margin:0 auto}
h1{font-size:1.6rem; margin:0 0 .2rem; font-weight:650; letter-spacing:-.01em}
h2{font-size:.78rem; text-transform:uppercase; letter-spacing:.09em; color:var(--muted);
   font-weight:600; margin:2.4rem 0 .8rem}
.when{color:var(--muted); font-size:.86rem; margin:0 0 1.6rem}
.tiles{display:grid; grid-template-columns:repeat(auto-fit,minmax(9.5rem,1fr)); gap:.7rem}
.tile{background:var(--panel); border:1px solid var(--line); border-radius:11px; padding:.85rem 1rem}
.tile .v{font-size:1.75rem; font-weight:600; font-variant-numeric:tabular-nums; letter-spacing:-.02em}
.tile .l{font-size:.72rem; text-transform:uppercase; letter-spacing:.08em; color:var(--muted); margin-top:.15rem}
.tile.is-total{border-color:var(--accent)}
.tile.is-total .v{color:var(--accent)}
.note{color:var(--muted); font-size:.86rem; margin:.9rem 0 0}
.panel{background:var(--panel); border:1px solid var(--line); border-radius:11px; padding:.85rem 1.1rem}
.bar{display:grid; grid-template-columns:minmax(7rem,15rem) 1fr auto; gap:.85rem; align-items:center; padding:.3rem 0}
.bar .label{overflow:hidden; text-overflow:ellipsis; white-space:nowrap}
/* Not scoped to .bar: the excluded buckets use .sub too, and scoping it there
   left "binary" and "13 files" touching. */
.sub{color:var(--muted); font-size:.78rem; margin-left:.45rem}
.track{background:var(--track); border-radius:99px; height:.5rem; display:flex; overflow:hidden}
.track>span{height:100%; background:var(--accent); min-width:2px}
.k-code .track>span{background:var(--code)}
.k-config .track>span{background:var(--config)}
.k-docs .track>span{background:var(--docs)}
.k-other .track>span{background:var(--other)}
/* Qualified, both of them: a bare .s-code loses to .track>span above, and the
   whole stacked bar comes out one colour with nothing to say it went wrong. */
.track>.s-code,.dot.s-code{background:var(--code)}
.track>.s-config,.dot.s-config{background:var(--config)}
.track>.s-docs,.dot.s-docs{background:var(--docs)}
.num{font-variant-numeric:tabular-nums; font-family:ui-monospace,Consolas,Menlo,monospace; font-size:.9rem}
.cmp{display:grid; grid-template-columns:1fr auto auto; gap:1rem; padding:.3rem 0; align-items:baseline}
.cmp .num{min-width:4.5rem; text-align:right}
.cmp .num.h{font-family:inherit; font-variant-numeric:normal; text-transform:uppercase;
  letter-spacing:.06em; font-size:.72rem; color:var(--muted)}
.legend{display:flex; gap:1rem; flex-wrap:wrap; color:var(--muted); font-size:.8rem; margin:.7rem 0 0}
.dot{display:inline-block; width:.55rem; height:.55rem; border-radius:99px; margin-right:.35rem}
.ex{padding:.55rem 0; border-bottom:1px solid var(--line)}
.ex:last-child{border-bottom:0}
.ex .row{display:flex; justify-content:space-between; gap:1rem; align-items:baseline}
.ex p{margin:.15rem 0 0; color:var(--muted); font-size:.84rem}
.warn{color:var(--config)}
footer{color:var(--muted); font-size:.8rem; margin-top:2.5rem; word-break:break-all}
</style>
</head>
<body>
<main id="app"></main>
<script type="application/json" id="report">__DATA__</script>
<script>
const data = JSON.parse(document.getElementById('report').textContent);

const n = v => Number(v || 0).toLocaleString();
const esc = s => String(s).replace(/[&<>"]/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'})[c]);
const files = c => c === 1 ? '1 file' : n(c) + ' files';
const pct = (v, max) => (v / max * 100).toFixed(2) + '%';

const total = data.code + data.configuration + data.documentation + data.other;
const kinds = [['code','Code'],['config','Configuration'],['docs','Documentation'],['other','Other']];

function tile(label, value, extra) {
  return '<div class="tile ' + (extra || '') + '"><div class="v">' + n(value) +
         '</div><div class="l">' + esc(label) + '</div></div>';
}

function languageBars(kind) {
  const items = data.byLanguage.filter(l => l.kind === kind);
  if (!items.length) return '';
  const max = Math.max(...items.map(i => i.code), 1);
  return items.map(i =>
    '<div class="bar k-' + kind + '">' +
      '<div class="label">' + esc(i.language) + '<span class="sub">' + files(i.files) + '</span></div>' +
      '<div class="track"><span style="width:' + pct(i.code, max) + '"></span></div>' +
      '<div class="num">' + n(i.code) + '</div>' +
    '</div>').join('');
}

function projectBars() {
  const rows = data.byProject.filter(p => p.code + p.configuration + p.documentation > 0);
  const max = Math.max(...rows.map(p => p.code + p.configuration + p.documentation), 1);
  const seg = (v, cls) => v ? '<span class="' + cls + '" style="width:' + pct(v, max) + '"></span>' : '';
  return rows.map(p =>
    '<div class="bar">' +
      '<div class="label">' + esc(p.project) + '<span class="sub">' + files(p.files) +
        (p.comments ? ' · ' + n(p.comments) + ' comments' : '') + '</span></div>' +
      '<div class="track">' + seg(p.code,'s-code') + seg(p.configuration,'s-config') + seg(p.documentation,'s-docs') + '</div>' +
      '<div class="num">' + n(p.code + p.configuration + p.documentation) + '</div>' +
    '</div>').join('');
}

function section(title, body) {
  return body ? '<h2>' + esc(title) + '</h2><div class="panel">' + body + '</div>' : '';
}

// The same cut the console report prints, drawn from the identical rows the
// headline total is: whether that many lines' PROJECT declares
// <IsTestProject>true</IsTestProject>, not a guess from its name. Absent when
// nothing in the repository is classified as a test project.
function mainVsTestsSection() {
  const mvt = data.mainVsTests;
  if (!mvt || !mvt.testProjects || !mvt.testProjects.length) return '';

  const line = (label, m, t) =>
    '<div class="cmp"><div>' + esc(label) + '</div><div class="num">' + n(m) +
    '</div><div class="num">' + n(t) + '</div></div>';

  const rows = [
    ['Code', mvt.main.code, mvt.tests.code],
    ['Configuration', mvt.main.configuration, mvt.tests.configuration],
    ['Documentation', mvt.main.documentation, mvt.tests.documentation],
  ];
  if (mvt.main.other || mvt.tests.other) rows.push(['Other', mvt.main.other, mvt.tests.other]);
  rows.push(['Files', mvt.main.files, mvt.tests.files]);

  const body =
    '<div class="cmp"><div></div><div class="num h">main</div><div class="num h">tests</div></div>' +
    rows.map(([label, m, t]) => line(label, m, t)).join('') +
    '<p class="note">Test project(s), by &lt;IsTestProject&gt;true&lt;/IsTestProject&gt;: ' +
      esc(mvt.testProjects.join(', ')) + '</p>';

  return section('Main code vs tests', body);
}

// The timestamp is written as "yyyy-MM-dd HH:mm:ssZ"; only the T makes it a
// date every browser agrees to parse.
const stamp = new Date(String(data.generatedAt).replace(' ', 'T'));
const when = isNaN(stamp) ? esc(data.generatedAt) : stamp.toLocaleString();

// The sentence the console prints, built the same way and for the same reason:
// naming two kinds without the total invites adding them up by hand.
const commentKinds = [
  [data.comments.code, 'in the code'],
  [data.comments.configuration, 'in configuration'],
  [data.comments.documentation, 'in documentation'],
  [data.comments.other, 'in other files'],
].filter(([lines]) => lines > 0);

const commentText = (() => {
  if (!commentKinds.length) return '';
  const sum = commentKinds.reduce((running, [lines]) => running + lines, 0);
  if (commentKinds.length === 1) return ', plus ' + n(sum) + ' comment lines ' + commentKinds[0][1];
  const parts = commentKinds.map(([lines, label]) => n(lines) + ' ' + label);
  return ', plus ' + n(sum) + ' comment lines — ' +
         parts.slice(0, -1).join(', ') + ' and ' + parts[parts.length - 1];
})();

const extensions = [...new Set((data.unrecognised || []).map(p => {
  const dot = p.lastIndexOf('.'), slash = p.lastIndexOf('/');
  return dot > slash ? p.slice(dot) : p;
}))].sort();

document.getElementById('app').innerHTML =
  '<h1>' + esc(data.name) + ' — hand-written lines</h1>' +
  '<p class="when">Counted ' + when + '</p>' +
  '<div class="tiles">' +
    tile('Code', data.code) +
    tile('Configuration', data.configuration) +
    tile('Documentation', data.documentation) +
    (data.other > 0 ? tile('Other', data.other) : '') +
    tile('Total', total, 'is-total') +
  '</div>' +
  '<p class="note">Across ' + n(data.files) + ' files' + commentText + '.</p>' +
  mainVsTestsSection() +
  kinds.map(([kind, label]) => section(label + ' by language', languageBars(kind))).join('') +
  section('By project',
    projectBars() +
    '<div class="legend">' +
      '<span><i class="dot s-code"></i>code</span>' +
      '<span><i class="dot s-config"></i>configuration</span>' +
      '<span><i class="dot s-docs"></i>documentation</span>' +
    '</div>') +
  section('Left out of the figures above', data.excluded.map(b =>
    '<div class="ex"><div class="row"><span>' + esc(b.bucket) +
      '<span class="sub">' + files(b.files) + '</span></span>' +
      '<span class="num">' + n(b.code) + '</span></div>' +
      '<p>' + esc(b.why) + '</p></div>').join('')) +
  (extensions.length
    ? '<p class="note warn">' + n(data.unrecognised.length) +
      ' file(s) cloc does not recognise, counted nowhere: ' + esc(extensions.join(' ')) + '</p>'
    : '') +
  '<footer>' + esc(data.repository) + '</footer>';
</script>
</body>
</html>
'@
}

foreach ($row in $rows) {
    $row | Add-Member -NotePropertyName Kind -NotePropertyValue (Get-Kind $row.Language)
    $row | Add-Member -NotePropertyName IsTest -NotePropertyValue $testProjectFolders.Contains($row.Project)
}

$authored = @($rows | Where-Object Bucket -eq 'authored')
# Cast: Measure-Object sums as a double, and a line count that reaches the JSON
# as 49249.0 is a number every consumer then has to round.
$sum = { param($set) [int](($set | Measure-Object Code -Sum).Sum ?? 0) }
$commentsOf = { param($set) [int](($set | Measure-Object Comment -Sum).Sum ?? 0) }

$result = [ordered]@{
    repository    = $RepoRoot
    name          = Split-Path -Leaf $RepoRoot
    generatedAt   = (Get-Date).ToString('u')
    code          = & $sum @($authored | Where-Object Kind -eq 'code')
    configuration = & $sum @($authored | Where-Object Kind -eq 'config')
    documentation = & $sum @($authored | Where-Object Kind -eq 'docs')
    other         = & $sum @($authored | Where-Object Kind -eq 'other')
    # BY KIND, because one number could not be honest. It was headed "comment
    # lines in the code" and computed from code files only, so the 302 lines of
    # commentary in .csproj files and the GitHub workflow were measured by cloc,
    # bucketed as authored, and then summed nowhere at all — the one thing the
    # description above promises this script does not do. A file's comments
    # belong to the same kind its lines do.
    comments      = [ordered]@{
        code          = & $commentsOf @($authored | Where-Object Kind -eq 'code')
        configuration = & $commentsOf @($authored | Where-Object Kind -eq 'config')
        documentation = & $commentsOf @($authored | Where-Object Kind -eq 'docs')
        other         = & $commentsOf @($authored | Where-Object Kind -eq 'other')
    }
    files         = $authored.Count
}

# --- Detail ------------------------------------------------------------------
#
# Built unconditionally, not inside a -Json branch: the console report, the JSON
# and the page are three renderings of ONE measurement, and the way those drift
# is a section computed twice with the filters typed slightly differently.

$byLanguage = @($authored | Group-Object Language | ForEach-Object {
    [ordered]@{
        language = $_.Name
        kind     = Get-Kind $_.Name
        files    = $_.Count
        code     = & $sum $_.Group
    }
} | Sort-Object { $_.code } -Descending)

$projects = @($authored | Group-Object Project | ForEach-Object {
    [PSCustomObject]@{
        Name   = $_.Name
        Files  = $_.Count
        Code   = & $sum @($_.Group | Where-Object Kind -eq 'code')
        Config = & $sum @($_.Group | Where-Object Kind -eq 'config')
        Docs   = & $sum @($_.Group | Where-Object Kind -eq 'docs')
        # One figure across every kind here, unlike the headline. Per project
        # the question is "how much of this was explained", and splitting it
        # four ways to carry two zeroes answers it worse.
        Comments = & $commentsOf $_.Group
    }
} | Sort-Object Code, Config -Descending)

# File counts come from the classification and line counts from what cloc
# measured — binaries are classified but never reach cloc, so counting files off
# the measured rows alone loses them. The console report already did this; the
# JSON did not, and undercounted the binary bucket to zero files.
$excluded = @(foreach ($rule in $pathBuckets) {
    $all = @($classified | Where-Object Bucket -eq $rule.Name)
    if ($all.Count -eq 0) { continue }
    $counted = @($rows | Where-Object Bucket -eq $rule.Name)
    [ordered]@{
        bucket = $rule.Name
        why    = $rule.Why
        files  = $all.Count
        code   = & $sum $counted
    }
})

$result['byLanguage']   = $byLanguage
$result['byProject']    = @($projects | ForEach-Object {
    [ordered]@{
        project       = $_.Name
        isTest        = $testProjectFolders.Contains($_.Name)
        files         = $_.Files
        code          = $_.Code
        configuration = $_.Config
        documentation = $_.Docs
        comments      = $_.Comments
    }
})
$result['excluded']     = $excluded
$result['unrecognised'] = @($unrecognised.Path)

# --- Main code vs tests -------------------------------------------------
#
# Not the same cut as byProject: which PROJECT a file lives in says nothing
# about whether the lines in it are a decision worth shipping or a check that
# somebody wrote to prove one. This groups every authored row by IsTest
# instead, the same way byLanguage groups by Language, so the two totals below
# are drawn from the identical rows the headline figure is — nothing here is a
# second measurement that could quietly disagree with the first.
$mainAuthored = @($authored | Where-Object { -not $_.IsTest })
$testAuthored = @($authored | Where-Object IsTest)

$result['mainVsTests'] = [ordered]@{
    testProjects = @($testProjectFolders | Sort-Object)
    main  = [ordered]@{
        files         = $mainAuthored.Count
        code          = & $sum @($mainAuthored | Where-Object Kind -eq 'code')
        configuration = & $sum @($mainAuthored | Where-Object Kind -eq 'config')
        documentation = & $sum @($mainAuthored | Where-Object Kind -eq 'docs')
        other         = & $sum @($mainAuthored | Where-Object Kind -eq 'other')
        comments      = & $commentsOf $mainAuthored
    }
    tests = [ordered]@{
        files         = $testAuthored.Count
        code          = & $sum @($testAuthored | Where-Object Kind -eq 'code')
        configuration = & $sum @($testAuthored | Where-Object Kind -eq 'config')
        documentation = & $sum @($testAuthored | Where-Object Kind -eq 'docs')
        other         = & $sum @($testAuthored | Where-Object Kind -eq 'other')
        comments      = & $commentsOf $testAuthored
    }
}

$payload = [PSCustomObject]$result | ConvertTo-Json -Depth 6

if ($Json) {
    $payload
    return
}

if ($Html) {
    if (-not $OutputPath) { $OutputPath = Join-Path ([IO.Path]::GetTempPath()) 'count-lines.html' }
    # Resolve against the SESSION's location rather than the process working
    # directory — PowerShell's own cd doesn't move the latter, so a relative
    # -OutputPath would otherwise land somewhere the caller never named.
    $OutputPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
    if (Test-Path -LiteralPath $OutputPath -PathType Container) {
        $OutputPath = Join-Path $OutputPath 'count-lines.html'
    }
    $jsonPath = [IO.Path]::ChangeExtension($OutputPath, '.json')

    Set-Content -LiteralPath $jsonPath -Value $payload -Encoding UTF8

    # The page carries its OWN copy of the data rather than fetching the sibling
    # file. A page opened from disk has an opaque origin, so fetch('...json') is
    # refused by CORS — and the failure is a console message behind a blank
    # page, which is exactly the shape of bug this repo keeps getting bitten by.
    # The .json is written beside it anyway, for anything else that wants it.
    #
    # \u003c is a valid JSON escape for '<', and stops a path or language name
    # ever closing the script element early.
    $embedded = $payload -replace '<', '\u003c'
    $title    = (Split-Path -Leaf $RepoRoot) -replace '[<&]', '-'

    Set-Content -LiteralPath $OutputPath -Encoding UTF8 -Value (
        (Get-ReportPageTemplate).Replace('__TITLE__', $title).Replace('__DATA__', $embedded))

    Write-Host ''
    Write-Host "  Page  $OutputPath" -ForegroundColor Cyan
    Write-Host "  Data  $jsonPath" -ForegroundColor DarkGray
    Write-Host ''
    if (-not $NoOpen) { Start-Process $OutputPath }
    return
}

# --- Report ------------------------------------------------------------------

function Write-Row {
    param([string]$Label, $Value, [string]$Colour = 'Gray', [int]$Width = 28)
    $number = if ($Value -is [string]) { $Value } else { '{0,10:N0}' -f $Value }
    Write-Host ('  {0}{1}' -f $Label.PadRight($Width), $number) -ForegroundColor $Colour
}

function Format-Files {
    param([int]$Count)
    if ($Count -eq 1) { '1 file' } else { "$Count files" }
}

$name = Split-Path -Leaf $RepoRoot
Write-Host ''
Write-Host "  $name — hand-written lines" -ForegroundColor Cyan
Write-Host "  $(('-' * 38))" -ForegroundColor DarkGray

Write-Row 'Code'          $result.code          'White'
Write-Row 'Configuration' $result.configuration 'White'
Write-Row 'Documentation' $result.documentation 'DarkGray'
if ($result.other -gt 0) { Write-Row 'Other'  $result.other 'DarkGray' }
Write-Host "  $(('-' * 38))" -ForegroundColor DarkGray
Write-Row 'Total' ($result.code + $result.configuration + $result.documentation + $result.other) 'Cyan'
Write-Host ''
# Only the kinds that have any, named. With one kind the sentence reads exactly
# as it always did; with more, the total goes first, because "27,030 in the code
# and 302 in configuration" otherwise invites adding them up by hand.
#
# PSCustomObject rather than a hashtable: @{ Lines = 302 }.Lines is 302, but a
# hashtable's own .Count is its NUMBER OF KEYS, and a property called Count here
# would have silently reported 2.
$commentKinds = @(
    @(
        [PSCustomObject]@{ Label = 'in the code';      Lines = $result.comments.code }
        [PSCustomObject]@{ Label = 'in configuration'; Lines = $result.comments.configuration }
        [PSCustomObject]@{ Label = 'in documentation'; Lines = $result.comments.documentation }
        [PSCustomObject]@{ Label = 'in other files';   Lines = $result.comments.other }
    ) | Where-Object { $_.Lines -gt 0 })

$commentText = ''
if ($commentKinds.Count -eq 1) {
    $commentText = ', plus {0:N0} comment lines {1}' -f $commentKinds[0].Lines, $commentKinds[0].Label
}
elseif ($commentKinds.Count -gt 1) {
    $parts = @($commentKinds | ForEach-Object { '{0:N0} {1}' -f $_.Lines, $_.Label })
    $total = ($commentKinds | Measure-Object Lines -Sum).Sum
    $commentText = ', plus {0:N0} comment lines — {1} and {2}' -f $total, ($parts[0..($parts.Count - 2)] -join ', '), $parts[-1]
}

Write-Host ("  Across {0:N0} files{1}." -f $result.files, $commentText) -ForegroundColor DarkGray

# Only worth a section when something was actually classified as a test
# project — a repo with none would otherwise print a "tests" column of zeroes
# beside a "main" column that is just the headline figure restated.
if ($testProjectFolders.Count -gt 0) {
    $mvt = $result.mainVsTests
    Write-Host ''
    Write-Host '  Main code vs tests' -ForegroundColor Cyan
    Write-Host ("      test project(s), by <IsTestProject>true</IsTestProject>: {0}" -f
        ($mvt.testProjects -join ', ')) -ForegroundColor DarkGray
    Write-Host ('  {0}{1,12} {2,12}' -f ''.PadRight(16), 'main', 'tests') -ForegroundColor DarkGray
    Write-Row 'Code'          ('{0,12:N0} {1,12:N0}' -f $mvt.main.code,          $mvt.tests.code)          'White'    16
    Write-Row 'Configuration' ('{0,12:N0} {1,12:N0}' -f $mvt.main.configuration, $mvt.tests.configuration) 'Gray'     16
    Write-Row 'Documentation' ('{0,12:N0} {1,12:N0}' -f $mvt.main.documentation, $mvt.tests.documentation) 'DarkGray' 16
    if ($mvt.main.other -gt 0 -or $mvt.tests.other -gt 0) {
        Write-Row 'Other' ('{0,12:N0} {1,12:N0}' -f $mvt.main.other, $mvt.tests.other) 'DarkGray' 16
    }
    Write-Row 'Files' ('{0,12:N0} {1,12:N0}' -f $mvt.main.files, $mvt.tests.files) 'DarkGray' 16
}

foreach ($kind in @('code', 'config', 'docs')) {
    $group = @($authored | Where-Object Kind -eq $kind)
    if ($group.Count -eq 0) { continue }
    $heading = switch ($kind) { 'code' { 'Code' } 'config' { 'Configuration' } 'docs' { 'Documentation' } }
    Write-Host ''
    Write-Host "  $heading by language" -ForegroundColor Cyan
    $group | Group-Object Language | Sort-Object { ($_.Group | Measure-Object Code -Sum).Sum } -Descending | ForEach-Object {
        Write-Row ('{0} ({1})' -f $_.Name, (Format-Files $_.Count)) (($_.Group | Measure-Object Code -Sum).Sum)
    }
}

if ($ByProject) {
    # Already grouped above for the JSON and the page. A project that is only
    # documentation is in that list and not in these columns, so it is dropped
    # here rather than printed as a name against two zeroes.
    $columns = @($projects | Where-Object { $_.Code -or $_.Config })
    # Long project names would otherwise push the columns out of line.
    $width = [Math]::Max(28, (($columns.Name | Measure-Object Length -Maximum).Maximum + 2))

    Write-Host ''
    Write-Host '  By project' -ForegroundColor Cyan
    Write-Host ('  {0}{1,10} {2,8} {3,9}' -f ''.PadRight($width), 'code', 'config', 'comments') -ForegroundColor DarkGray
    $columns | ForEach-Object {
        # Dimmed rather than a separate list: a project can hold both, e.g. a
        # test project that also carries its own config files, and two lists
        # would need this figured out twice.
        $colour = if ($testProjectFolders.Contains($_.Name)) { 'DarkGray' } else { 'Gray' }
        Write-Row $_.Name ('{0,10:N0} {1,8:N0} {2,9:N0}' -f $_.Code, $_.Config, $_.Comments) $colour $width
    }
}

Write-Host ''
Write-Host '  Left out of the figures above' -ForegroundColor Cyan
foreach ($rule in $pathBuckets) {
    # File counts come from the classification (binaries never reach cloc);
    # line counts from what cloc actually measured.
    $all     = @($classified | Where-Object Bucket -eq $rule.Name)
    if ($all.Count -eq 0) { continue }
    $counted = @($rows | Where-Object Bucket -eq $rule.Name)
    $lines   = if ($counted.Count -gt 0) { ($counted | Measure-Object Code -Sum).Sum } else { 0 }
    Write-Row ('{0} ({1})' -f $rule.Name, (Format-Files $all.Count)) $lines 'DarkGray'
    Write-Host ('      {0}' -f $rule.Why) -ForegroundColor DarkGray
    if ($IncludeExcluded) {
        $counted | Sort-Object Code -Descending | ForEach-Object {
            Write-Host ('      {0,8:N0}  {1}' -f $_.Code, $_.Path) -ForegroundColor DarkGray
        }
    }
}

if ($unrecognised.Count -gt 0) {
    $extensions = ($unrecognised.Path | ForEach-Object { [IO.Path]::GetExtension($_) } |
        Where-Object { $_ } | Sort-Object -Unique) -join ' '
    Write-Host ''
    Write-Host ("  {0} file(s) cloc does not recognise, counted nowhere: {1}" -f $unrecognised.Count, $extensions) -ForegroundColor Yellow
    if ($IncludeExcluded) {
        $unrecognised.Path | ForEach-Object { Write-Host "      $_" -ForegroundColor DarkGray }
    }
}

Write-Host ''
