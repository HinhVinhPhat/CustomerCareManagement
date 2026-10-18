<#
  Validate the local skills under .agents/skills/.
  Run:  powershell -ExecutionPolicy Bypass -File tools/check-skills.ps1
  Exits 0 when every skill PASSes, 1 when there is an error.

  Checks (per the OpenCode V2 docs - https://opencode.ai/v2/docs/skills):
    - Each skill is a SKILL.md (exact casing) inside a subfolder, or a *.md file at the
      root of the skills folder.
    - The ID is derived from the path: 1-64 chars, matches ^[a-z0-9]+(-[a-z0-9]+)*$, no duplicates.
    - There is frontmatter --- ... --- with the name and description fields.
    - Every referenced child file (references/..., scripts/..., assets/...) exists.
    - Every referenced `ccm-*` skill exists.
    - The skill table in AGENTS.md points at the right SKILL.md files.
#>
[CmdletBinding()]
param(
    [string]$Root
)

$ErrorActionPreference = 'Stop'

if (-not $Root) { $Root = Split-Path -Parent $PSScriptRoot }
$skillsDir = Join-Path $Root '.agents\skills'
$agentsMd  = Join-Path $Root 'AGENTS.md'
$idPattern = '^[a-z0-9]+(-[a-z0-9]+)*$'

function Read-Utf8([string]$p) { [System.IO.File]::ReadAllText($p) }

Write-Host "Project root : $Root"
Write-Host "Skills folder: $skillsDir"
Write-Host ''

if (-not (Test-Path $skillsDir)) {
    Write-Host "ERROR: cannot find $skillsDir" -ForegroundColor Red
    exit 1
}

$skillFiles = New-Object System.Collections.Generic.List[object]
Get-ChildItem -Path $skillsDir -Recurse -File | Where-Object { $_.Name -ceq 'SKILL.md' } | ForEach-Object {
    $skillFiles.Add([pscustomobject]@{ File = $_.FullName; Id = $_.Directory.Name })
}
Get-ChildItem -Path $skillsDir -File -Filter *.md | Where-Object { $_.Name -cne 'SKILL.md' } | ForEach-Object {
    $skillFiles.Add([pscustomobject]@{ File = $_.FullName; Id = [System.IO.Path]::GetFileNameWithoutExtension($_.Name) })
}

if ($skillFiles.Count -eq 0) { Write-Host 'WARNING: no skills found.' -ForegroundColor Yellow }

$seenIds = @{}
$results = New-Object System.Collections.Generic.List[object]
$fail = 0
$warn = 0

foreach ($s in $skillFiles) {
    $errors   = New-Object System.Collections.Generic.List[string]
    $warnings = New-Object System.Collections.Generic.List[string]
    $id   = $s.Id
    $text = (Read-Utf8 $s.File) -replace "^\uFEFF", ''
    $dir  = Split-Path -Parent $s.File

    # 1. ID
    if ($id.Length -lt 1 -or $id.Length -gt 64) { $errors.Add("ID '$id' must be 1-64 characters") }
    if ($id -notmatch $idPattern) { $errors.Add("ID '$id' does not match ^[a-z0-9]+(-[a-z0-9]+)*$") }
    if ($seenIds.ContainsKey($id)) { $errors.Add("ID '$id' duplicates $($seenIds[$id])") } else { $seenIds[$id] = $s.File }

    # 2. Frontmatter
    $normalized = $text -replace "`r`n", "`n"
    $name = $null; $desc = $null
    if ($normalized -match '(?s)^---\n(.*?)\n---') {
        $fm = $Matches[1]
        foreach ($line in ($fm -split "`n")) {
            if ($line -match '^name:\s*(.*)$')        { $name = $Matches[1].Trim() }
            elseif ($line -match '^description:\s*(.*)$') { $desc = $Matches[1].Trim() }
        }
        if (-not $name) { $errors.Add('Frontmatter is missing the name field') }
        if (-not $desc) { $errors.Add('Frontmatter is missing the description field') }
        elseif ($desc.Length -gt 1024) { $warnings.Add("description is $($desc.Length) characters (recommended <= 1024 when porting elsewhere)") }
        if ($desc -and $desc[0] -notin @('"', "'") -and $desc -match ': ') {
            $warnings.Add('description contains ": " without quotes; some strict YAML parsers will error')
        }
    } else {
        $errors.Add('Missing frontmatter (a --- ... --- block at the top of the file)')
    }
    $dlen = 0; if ($desc) { $dlen = $desc.Length }

    # 3. Child files referenced in backticks: references/..., scripts/..., assets/...
    $refTokens = [regex]::Matches($text, '`((?:references|scripts|assets)/[^`\s]+)`') | ForEach-Object { $_.Groups[1].Value }
    foreach ($t in ($refTokens | Select-Object -Unique)) {
        if (-not (Test-Path (Join-Path $dir ($t -replace '/', '\')))) { $errors.Add("Referenced file does not exist: $t") }
    }

    # 4. Other skills referenced: `ccm-...`
    $skillTokens = [regex]::Matches($text, '`(ccm-[a-z0-9-]+)`') | ForEach-Object { $_.Groups[1].Value }
    foreach ($t in ($skillTokens | Select-Object -Unique)) {
        if (-not (Test-Path (Join-Path $skillsDir (Join-Path $t 'SKILL.md')))) { $errors.Add("Referenced skill does not exist: $t") }
    }

    $fail += $errors.Count
    $warn += $warnings.Count
    $results.Add([pscustomobject]@{ Id = $id; Name = $name; DescLen = $dlen; Errors = $errors; Warnings = $warnings })
}

# 5. Skill table in AGENTS.md
$agentsRefs = @()
if (Test-Path $agentsMd) {
    $agentsRefs = [regex]::Matches((Read-Utf8 $agentsMd), '\.agents/skills/([a-z0-9-]+)/SKILL\.md') |
        ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique
}

Write-Host '================ RESULT ================' -ForegroundColor Cyan
foreach ($r in $results) {
    $color = if ($r.Errors.Count -eq 0) { 'Green' } else { 'Red' }
    $tag   = if ($r.Errors.Count -eq 0) { 'PASS' } else { 'FAIL' }
    Write-Host ("{0}  {1,-22} name='{2}' desc={3} chars" -f $tag, $r.Id, $r.Name, $r.DescLen) -ForegroundColor $color
    foreach ($e in $r.Errors)   { Write-Host "      - ERROR: $e" -ForegroundColor Red }
    foreach ($w in $r.Warnings) { Write-Host "      - warning: $w" -ForegroundColor Yellow }
}

if ($agentsRefs.Count -gt 0) {
    Write-Host ''
    Write-Host 'Skill references in AGENTS.md:' -ForegroundColor Cyan
    foreach ($a in $agentsRefs) {
        $ok = Test-Path (Join-Path $skillsDir (Join-Path $a 'SKILL.md'))
        if (-not $ok) { $fail++ }
        $color = if ($ok) { 'Green' } else { 'Red' }
        Write-Host ("  [{0}] {1}" -f $(if ($ok) { 'OK' } else { 'MISSING' }), $a) -ForegroundColor $color
    }
}

Write-Host ''
Write-Host ("Total: {0} skills, {1} errors, {2} warnings." -f $results.Count, $fail, $warn) -ForegroundColor Cyan
if ($fail -gt 0) { exit 1 } else { exit 0 }
