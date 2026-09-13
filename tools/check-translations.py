#!/usr/bin/env python3
"""Validate the published translation files.

Run by CI on every pull request and usable locally:

    python tools/check-translations.py

A published Translations/<locale>/strings.csv must:
  - have the header  key,section,node,order,speaker,translation
    (older key,speaker,translation and key,translation are still accepted)
  - lines starting with '#' are section headers and are ignored
  - key: 16 lowercase hex digits (SHA-256 prefix of the source string)
  - no duplicate keys, no empty translations
  - contain no English source text (a source_en column is the tell)
and nothing under Translations/_discovered/ may be committed - the working
copies there carry the game's script in plain English.
"""
import csv
import io
import re
import sys
from pathlib import Path

KEY = re.compile(r"^[0-9a-f]{16}$")
IDENT = re.compile(r"^(?:[A-Za-z0-9_]*|L\d\d [A-Za-z]+|UI)$")
ROOT = Path(__file__).resolve().parent.parent
TRANSLATIONS = ROOT / "Translations"


def display(path: Path) -> str:
    """Repository-relative path, so reports read Translations/ja/strings.csv."""
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def check_file(path: Path) -> list[str]:
    problems = []
    name = display(path)
    with io.open(path, encoding="utf-8-sig", newline="") as f:
        # Header comments and blank lines are skipped, but reports must point
        # at the line in the file, so remember where each kept line came from.
        kept = [(i, line) for i, line in enumerate(f, start=1) if line.strip() and not line.startswith("#")]
    origin = [i for i, _ in kept]
    reader = csv.reader(line for _, line in kept)
    try:
        header = next(reader)
    except StopIteration:
        return [f"{name}: empty file"]
    accepted = (
        ["key", "section", "node", "order", "speaker", "translation"],
        ["key", "speaker", "translation"],
        ["key", "translation"],
    )
    if header not in accepted:
        problems.append(
            f"{name}: header is {header!r}; the published file must be "
            f"'key,section,node,order,speaker,translation' (run tools/hash-strings.ps1 before committing)"
        )
        return problems
    col = {column: i for i, column in enumerate(header)}
    width = len(header)
    seen = {}
    consumed = reader.line_num
    for row in reader:
        # A quoted field can span lines; a row starts right after the lines
        # the reader had consumed before it.
        n = origin[consumed]
        consumed = reader.line_num
        if len(row) != width:
            problems.append(f"{name}:{n}: expected {width} fields, got {len(row)}")
            continue
        key, translation = row[0], row[-1]
        if not KEY.match(key):
            # Deliberately do not echo the key: on a public repository the
            # report is visible, and a plain-text key is the game's script.
            problems.append(f"{name}:{n}: key is not 16 lowercase hex digits")
        if key in seen:
            problems.append(f"{name}:{n}: duplicate key (see line {seen[key]})")
        seen.setdefault(key, n)
        if not translation.strip():
            problems.append(f"{name}:{n}: empty translation")
        # section/node are game-internal identifiers, never sentences.
        for column in ("section", "node"):
            if column in col and not IDENT.match(row[col[column]]):
                problems.append(f"{name}:{n}: {column} does not look like an identifier")
    return problems


def is_tracked(path: Path) -> bool:
    """True if git tracks the file. The maintainer keeps an untracked copy
    locally on purpose; only a committed one is a problem."""
    import subprocess
    try:
        out = subprocess.run(
            ["git", "ls-files", "--error-unmatch", str(path)],
            cwd=ROOT, capture_output=True, text=True,
        )
        return out.returncode == 0
    except OSError:
        return path.exists()


def main() -> int:
    problems = []
    discovered = TRANSLATIONS / "_discovered"
    if discovered.is_dir():
        for f in sorted(discovered.iterdir()):
            if f.is_file() and is_tracked(f):
                problems.append(f"{display(f)}: must not be committed (contains source text)")
    for locale_dir in sorted(TRANSLATIONS.iterdir()):
        if not locale_dir.is_dir() or locale_dir.name.startswith("_"):
            continue
        local = locale_dir / "strings.local.csv"
        if local.exists() and is_tracked(local):
            problems.append(f"{display(local)}: must not be committed (contains source text)")
        strings = locale_dir / "strings.csv"
        if strings.exists():
            problems.extend(check_file(strings))
        else:
            problems.append(f"{display(locale_dir)}: no strings.csv")
    for p in problems:
        print(p)
    if problems:
        print(f"\n{len(problems)} problem(s).")
        return 1
    print("translations OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
