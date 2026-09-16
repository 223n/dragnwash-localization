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
  repository (generated in-game by F1 -> Translation -> Export game flow).
  Keys the order does not know (UI text) go last. No game installation is
  needed.

  When a working copy is the input, every published row it does not contain is
  kept: a working copy only holds the rows it was written with, and the
  published file may have gained rows since.

  Identical to the in-game "Hash for commit" button.

.PARAMETER Path
  Convert exactly these files, in place: each one is both the input and the
  output. Give it a published strings.csv, not a working copy - a working copy
  passed here is overwritten with the published form, which drops its source_en
  column and every untranslated row.

  With no arguments every locale under Translations/ is converted, and there a
  locale's working copy (Translations/_discovered/<locale>.working.csv) is used
  as the input when one exists. That resolution does not happen for -Path.
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

# Import-Csv cannot skip comment lines, so they are dropped after parsing.
# Stripping them from the physical lines first would also remove lines that
# belong to a quoted multi-line value, which silently rewrites the translation:
# the game (src/DragNWashLocalization/CsvReader.cs) treats '#' as a comment only
# at the start of a record, never inside quotes.
function Read-Csv([string]$file) {
  # The whole file as one string, not an array of lines: ConvertFrom-Csv only
  # joins a quoted field across physical lines when it is given a single
  # string. Handed an array it makes every line its own record, which cuts a
  # multi-line translation off at its first line - the very thing this change
  # is about.
  $text = [System.IO.File]::ReadAllText($file, [System.Text.Encoding]::UTF8)
  if ($text.Trim() -eq '') { return @() }
  # ConvertFrom-Csv takes the first physical line as the header, so anything
  # above it becomes the header and every column is lost. It skips a leading
  # '#' line on its own, but not a blank line that follows one, so drop both
  # here. The game (CsvReader.Parse) skips blank lines and treats '#' at the
  # start of a record as a comment, so this matches it.
  $text = $text -replace '^(?:[^\S\r\n]*\r?\n|#[^\r\n]*\r?\n)+', ''
  $rows = @($text | ConvertFrom-Csv)
  if ($rows.Count -eq 0) { return @() }
  # The first column's name, so a parsed comment row can be spotted by it.
  # A comment may itself contain a comma ('# ===== ... | sets a, b ====='), so
  # it can arrive split across several fields; only the first one matters.
  # Taken from the header ConvertFrom-Csv actually used rather than from the
  # first physical line: a comment line above the header would otherwise name
  # a column that does not exist and turn the filter below into a no-op.
  $first = @($rows[0].PSObject.Properties.Name)[0]
  return ($rows | Where-Object { -not ([string]$_.$first).StartsWith('#') })
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
  # Resolve against PowerShell's current location. The path is handed to
  # [System.IO.File], which resolves a relative one against the process working
  # directory - not the same thing, and Set-Location never updates it. That put
  # the rewritten file somewhere other than where the caller pointed. As a bonus
  # Resolve-Path fails outright on a path that does not exist, instead of the
  # run reading nothing and writing a file there.
  foreach ($p in $Path) {
    $rp = (Resolve-Path -LiteralPath $p).Path
    $targets += @{ Input = $rp; Output = $rp }
  }
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
    $keyCell = if ($r.PSObject.Properties['key']) { [string]$r.key } else { '' }
    $rawKey = $keyCell.Trim()
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
    } elseif ($key -cmatch '^[0-9a-f]{16}$') {
      $kept++
    } elseif ($key -ne '') {
      # A translator appending "English,訳" to a published file puts the
      # English under the key column, since that is the header. That is the
      # most natural edit there is, so take it as source text rather than
      # dropping the row - TranslationStore.ResolveKey, which the in-game
      # button uses, does the same.
      $key = Get-Key $keyCell; $converted++
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

  # A working copy only holds the rows it was written with. Rows the published
  # file gained since (a pack update, keys re-made after a game update) would
  # otherwise be lost, so keep every published row the working copy does not
  # have. The working copy wins where both do. This mirrors
  # TranslationStore.HashFileInPlace, which the in-game button uses.
  $fromPublished = 0
  if ($t.Input -ne $t.Output -and (Test-Path $t.Output)) {
    foreach ($r in Read-Csv $t.Output) {
      $pCell = if ($r.PSObject.Properties['key']) { [string]$r.key } else { '' }
      $pk = $pCell.Trim()
      if ($pk -eq '') { continue }
      $ptr = if ($r.PSObject.Properties['translation']) { [string]$r.translation } else { '' }
      if ($ptr -eq '') { continue }
      if ($pk -cmatch $lineIdPattern) {
        if (-not $lineRows.Contains($pk)) { $lineRows[$pk] = $ptr }
        continue
      }
      $pk = $pk.ToLowerInvariant()
      # The published file carries no source_en column, so a key column holding
      # something other than a key is English somebody appended by hand. Hash it
      # and keep the row, exactly as ResolveKey does; discarding it here would
      # throw a finished translation away without saying so.
      if ($pk -cnotmatch '^[0-9a-f]{16}$') { $pk = Get-Key $pCell }
      if ($rows.ContainsKey($pk)) { continue }
      $pwho = if ($r.PSObject.Properties['speaker']) { [string]$r.speaker } else { '' }
      $rows[$pk] = @{ Speaker = $pwho; Translation = $ptr }
      $inputOrder.Add($pk)
      $fromPublished++
    }
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
  Write-Host ("{0} <- {1}: {2} converted, {3} already hashed, {4} per-line, {5} malformed dropped, {6} kept from the published file, {7} in play order, {8} other" -f $t.Output, $t.Input, $converted, $kept, $lineKept, $dropped, $fromPublished, $done.Count, $left.Count)
}
