<#
.SYNOPSIS
  Rebuild Translations/<locale>/strings.csv in the published form.

.DESCRIPTION
  The repository does not carry the game's English text. Each published row is
  keyed by the first 16 hex digits of SHA-256 over the UTF-8 bytes of the exact
  source string. Translators work locally with source_en rows (a working copy
  from the in-game menu, or plain source_en,translation rows) and run this
  before committing. Rows already in key form pass through unchanged.

  A row keyed by a Yarn line ID (line:6046bedf) translates that one line only,
  for English said by more than one character. It is kept when it has a
  translation and written at that line's place in the script. The speaker
  column of a hash row lists every character who says the English.

  Rows are written in the order the game plays them, with '#' section
  headers, using data/script_order.csv and data/level_flow.csv from the
  repository (generated in-game by F1 -> Tools -> Export game flow). Keys the
  order does not know (UI text) go last. No game installation is needed.

  Identical to the in-game "Hash for commit" button.

.PARAMETER Path
  The strings.csv (or working copy) to convert. Defaults to every locale under
  Translations/. When a locale's working copy exists under
  Translations/_discovered/<locale>.working.csv it is used as the input.
#>
[CmdletBinding()]
param(
  [string[]]$Path
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Get-Key([string]$text) {
  $sha = [System.Security.Cryptography.SHA256]::Create()
  try {
    $digest = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($text))
    return (($digest[0..7] | ForEach-Object { $_.ToString('x2') }) -join '')
  } finally { $sha.Dispose() }
}

function Escape-Csv([string]$v) {
  if ($null -eq $v) { return '' }
  if ($v -match '[,"\r\n]') { return '"' + ($v -replace '"', '""') + '"' }
  return $v
}

# Import-Csv cannot skip comment lines, so strip them first.
function Read-Csv([string]$file) {
  $lines = [System.IO.File]::ReadAllLines($file, [System.Text.Encoding]::UTF8) | Where-Object { -not $_.StartsWith('#') }
  if ($lines.Count -lt 2) { return @() }
  return ($lines | ConvertFrom-Csv)
}

# ---- play order ------------------------------------------------------------
$order = @(); $levels = @{}
$orderFile = Join-Path $root 'data/script_order.csv'
$flowFile = Join-Path $root 'data/level_flow.csv'
if (Test-Path $orderFile) { $order = @(Read-Csv $orderFile) }
if (Test-Path $flowFile) {
  foreach ($l in Read-Csv $flowFile) {
    $idx = [int]$l.level
    $sec = ('L{0:00} {1}' -f ($idx + 1), $l.dragon)
    $h = 'Level {0}: {1}' -f ($idx + 1), $l.dragon
    if ($l.weather) { $h += " ($($l.weather))" }
    if ($l.set_flags) { $h += ' | sets ' + ($l.set_flags -replace ' \| ', ', ') }
    if ($l.end_flags) { $h += ' | ends ' + ($l.end_flags -replace ' \| ', ', ') }
    $levels[$sec] = $h
  }
}
function Section-Title([string]$s) {
  switch ($s) {
    'Cutscene' { 'Cutscenes (started by game code)' }
    'Reaction' { 'Dragon reactions (started by game code)' }
    'Unused'   { 'Unused nodes (not reachable in the current game)' }
    default    { if ($levels.ContainsKey($s)) { $levels[$s] } else { $s } }
  }
}

# ---- inputs ----------------------------------------------------------------
$targets = @()
if ($Path) {
  foreach ($p in $Path) { $targets += @{ Input = $p; Output = $p } }
} else {
  Get-ChildItem -Path (Join-Path $root 'Translations') -Directory | Where-Object { $_.Name -notlike '_*' } | ForEach-Object {
    $out = Join-Path $_.FullName 'strings.csv'
    $work = Join-Path $root ('Translations/_discovered/' + $_.Name + '.working.csv')
    $in = if (Test-Path $work) { $work } else { $out }
    if (Test-Path $in) { $targets += @{ Input = $in; Output = $out } }
  }
}

# key -> every speaker in play order ("Ryan/Alexander")
$speakers = @{}
foreach ($e in $order) {
  $k = ([string]$e.key).Trim().ToLowerInvariant()
  if (-not $e.speaker) { continue }
  if (-not $speakers.ContainsKey($k)) { $speakers[$k] = New-Object System.Collections.Generic.List[string] }
  if (-not $speakers[$k].Contains([string]$e.speaker)) { $speakers[$k].Add([string]$e.speaker) }
}
$lineIdPattern = '^line:[A-Za-z0-9_.\-]{1,59}$'

foreach ($t in $targets) {
  $rows = @{}; $inputOrder = New-Object System.Collections.Generic.List[string]
  $lineRows = [ordered]@{}
  $converted = 0; $kept = 0; $dropped = 0; $lineKept = 0
  foreach ($r in Read-Csv $t.Input) {
    $rawKey = if ($r.PSObject.Properties['key']) { ([string]$r.key).Trim() } else { '' }
    if ($rawKey -cmatch $lineIdPattern) {
      $tr = if ($r.PSObject.Properties['translation']) { [string]$r.translation } else { '' }
      if ($tr -ne '' -and -not $lineRows.Contains($rawKey)) { $lineRows[$rawKey] = $tr }
      continue
    }
    $key = $rawKey.ToLowerInvariant()
    $src = if ($r.PSObject.Properties['source_en']) { [string]$r.source_en } else { '' }
    if ($src -ne '') {
      $hashed = Get-Key $src
      if ($key -ne '' -and $key -ne $hashed) { $dropped++; continue }
      $key = $hashed; $converted++
    } elseif ($key -match '^[0-9a-f]{16}$') {
      $kept++
    } else {
      $dropped++; continue
    }
    if ($rows.ContainsKey($key)) { continue }
    $who = if ($r.PSObject.Properties['speaker']) { [string]$r.speaker } else { '' }
    $tr = if ($r.PSObject.Properties['translation']) { [string]$r.translation } else { '' }
    if ($tr -eq '') { continue }   # nothing to publish for an untranslated line
    $rows[$key] = @{ Speaker = $who; Translation = $tr }
    $inputOrder.Add($key)
  }

  # StringBuilder.AppendLine uses [Environment]::NewLine, which is CRLF on
  # Windows. StringWriter lets the newline be stated outright, so the file is
  # LF on every platform, matching the repository (see .gitattributes).
  $out = New-Object System.IO.StringWriter
  $out.NewLine = "`n"
  $out.WriteLine('key,section,node,order,speaker,translation')
  # Keep the comment block under the header of the published file (language,
  # provisional notice, credits), up to the first section header.
  if (Test-Path $t.Output) {
    foreach ($line in ([System.IO.File]::ReadAllLines($t.Output, [System.Text.Encoding]::UTF8) | Select-Object -Skip 1)) {
      if (-not $line.StartsWith('#') -or $line.StartsWith('# =====') -or $line.StartsWith('# ---')) { break }
      $out.WriteLine($line)
    }
  }
  $done = New-Object System.Collections.Generic.HashSet[string]
  $lastSection = $null; $lastNode = $null
  foreach ($e in $order) {
    $k = ([string]$e.key).Trim().ToLowerInvariant()
    $lid = [string]$e.line_id
    $hashRow = $rows.ContainsKey($k) -and -not $done.Contains($k)
    $lineRow = $lid -ne '' -and $lineRows.Contains($lid)
    if (-not $hashRow -and -not $lineRow) { continue }
    if ($e.section -ne $lastSection) {
      $out.WriteLine(''); $out.WriteLine('# ===== ' + (Section-Title $e.section) + ' =====')
      $lastSection = $e.section; $lastNode = $null
    }
    if ($e.node -ne $lastNode) {
      $title = $(if ($e.phase) { $e.phase + ': ' } else { '' }) + $e.node + $(if ($e.condition) { ' | if ' + $e.condition } else { '' })
      $out.WriteLine('# --- ' + $title + ' ---')
      $lastNode = $e.node
    }
    if ($hashRow) {
      $who = if ($speakers.ContainsKey($k)) { $speakers[$k] -join '/' } elseif ($rows[$k].Speaker) { $rows[$k].Speaker } else { $e.speaker }
      $out.WriteLine($k + ',' + (Escape-Csv $e.section) + ',' + (Escape-Csv $e.node) + ',' + $e.order + ',' + (Escape-Csv $who) + ',' + (Escape-Csv $rows[$k].Translation))
      [void]$done.Add($k)
    }
    if ($lineRow) {
      $out.WriteLine($lid + ',' + (Escape-Csv $e.section) + ',' + (Escape-Csv $e.node) + ',' + $e.order + ',' + (Escape-Csv $e.speaker) + ',' + (Escape-Csv $lineRows[$lid]))
      $lineRows.Remove($lid); $lineKept++
    }
  }
  $left = @($inputOrder | Where-Object { -not $done.Contains($_) })
  if ($left.Count -gt 0) {
    if ($order.Count -gt 0) { $out.WriteLine(''); $out.WriteLine('# ===== UI and other text (not part of the dialogue script) =====') }
    foreach ($k in $left) {
      $who = if ($rows[$k].Speaker) { $rows[$k].Speaker } else { 'UI' }
      $sec = if ($order.Count -gt 0) { 'UI' } else { '' }
      $out.WriteLine($k + ',' + $sec + ',,,' + (Escape-Csv $who) + ',' + (Escape-Csv $rows[$k].Translation))
    }
  }
  if ($lineRows.Count -gt 0) {
    $out.WriteLine(''); $out.WriteLine('# ===== Per-line translations not found in the script order =====')
    foreach ($lid in @($lineRows.Keys)) {
      $out.WriteLine($lid + ',,,,,' + (Escape-Csv $lineRows[$lid])); $lineKept++
    }
  }
  [System.IO.File]::WriteAllText($t.Output, $out.ToString(), (New-Object System.Text.UTF8Encoding $false))
  Write-Host ("{0} <- {1}: {2} converted, {3} already hashed, {4} per-line, {5} malformed dropped, {6} in play order, {7} other" -f $t.Output, $t.Input, $converted, $kept, $lineKept, $dropped, $done.Count, $left.Count)
}
