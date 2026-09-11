<#
.SYNOPSIS
  Convert a Translations/<locale>/strings.csv from source_en rows to key rows.

.DESCRIPTION
  The repository does not carry the game's English text. Each row of a
  published strings.csv is keyed by the first 16 hex digits of SHA-256 over
  the UTF-8 bytes of the exact source string. Translators work locally with
  plain source_en rows (the plugin accepts both layouts in one file) and run
  this before committing. Rows already in key form pass through unchanged.

  Identical to the in-game "Hash strings.csv for commit" button.

.PARAMETER Path
  The strings.csv to rewrite in place. Defaults to every locale under
  Translations/ next to this script's repository root.
#>
[CmdletBinding()]
param(
  [string[]]$Path
)

$ErrorActionPreference = 'Stop'

function Get-Key([string]$text) {
  $sha = [System.Security.Cryptography.SHA256]::Create()
  try {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($text)
    $digest = $sha.ComputeHash($bytes)
    return (($digest[0..7] | ForEach-Object { $_.ToString('x2') }) -join '')
  } finally { $sha.Dispose() }
}

function Escape-Csv([string]$v) {
  if ($null -eq $v) { return '' }
  if ($v -match '[,"\r\n]') { return '"' + ($v -replace '"', '""') + '"' }
  return $v
}

if (-not $Path) {
  $root = Split-Path -Parent $PSScriptRoot
  $Path = Get-ChildItem -Path (Join-Path $root 'Translations') -Directory |
    Where-Object { $_.Name -notlike '_*' } |
    ForEach-Object { Join-Path $_.FullName 'strings.csv' } |
    Where-Object { Test-Path $_ }
}

foreach ($file in $Path) {
  $rows = Import-Csv -Path $file -Encoding UTF8
  $converted = 0; $kept = 0; $dropped = 0
  $out = New-Object System.Text.StringBuilder
  [void]$out.AppendLine('key,speaker,translation')
  foreach ($r in $rows) {
    $key = if ($r.PSObject.Properties['key']) { ([string]$r.key).Trim().ToLowerInvariant() } else { '' }
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
    $who = if ($r.PSObject.Properties['speaker']) { [string]$r.speaker } else { '' }
    [void]$out.AppendLine($key + ',' + (Escape-Csv $who) + ',' + (Escape-Csv ([string]$r.translation)))
  }
  [System.IO.File]::WriteAllText($file, $out.ToString(), (New-Object System.Text.UTF8Encoding $false))
  Write-Host ("{0}: {1} converted, {2} already hashed, {3} malformed dropped" -f $file, $converted, $kept, $dropped)
}
