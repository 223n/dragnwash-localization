#!/usr/bin/env bash
# The step before committing a translation: rewrite Translations/<locale>/
# strings.csv into the published form, each row keyed by the hash of the
# English it translates (tools/hash-strings.ps1, the same thing the in-game
# "Hash for commit" button does).
#
#   docker compose run --rm hash        every locale under Translations/
#   docker compose run --rm hash ja     that locale's published file
#
# It is a PowerShell script, and this image carries PowerShell, so a translator
# on macOS or Linux needs nothing of their own installed. The file it rewrites
# is in the mounted working tree, so the result is right there afterwards, to
# be read with git diff and committed.
#
# With no argument the script also picks up a locale's working copy
# (Translations/_discovered/<locale>.working.csv) when there is one; naming a
# locale here converts the published file itself, as -Path does.
set -u

if [ "$#" -eq 0 ]; then
    exec pwsh -NoProfile -File tools/hash-strings.ps1
fi

paths=()
for locale in "$@"; do
    file="Translations/$locale/strings.csv"
    if [ ! -f "$file" ]; then
        echo "There is no $file in this repository." >&2
        echo "Starting a new language? Make the folder, put the display name in name.txt, and see CONTRIBUTING.md." >&2
        exit 2
    fi
    paths+=("$file")
done

exec pwsh -NoProfile -File tools/hash-strings.ps1 -Path "${paths[@]}"
