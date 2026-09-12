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


def check_file(path: Path) -> list[str]:
    problems = []
    with io.open(path, encoding="utf-8-sig", newline="") as f:
        reader = csv.reader(line for line in f if line.strip() and not line.startswith("#"))
        try:
            header = next(reader)
        except StopIteration:
            return [f"{path}: empty file"]
        accepted = (
            ["key", "section", "node", "order", "speaker", "translation"],
            ["key", "speaker", "translation"],
            ["key", "translation"],
        )
        if header not in accepted:
            problems.append(
                f"{path}: header is {header!r}; the published file must be "
                f"'key,section,node,order,speaker,translation' (run tools/hash-strings.ps1 before committing)"
            )
            return problems
        col = {name: i for i, name in enumerate(header)}
        width = len(header)
        seen = {}
        for n, row in enumerate(reader, start=2):
            if len(row) != width:
                problems.append(f"{path}:{n}: expected {width} fields, got {len(row)}")
                continue
            key, translation = row[0], row[-1]
            if not KEY.match(key):
                # Deliberately do not echo the key: on a public repository the
                # report is visible, and a plain-text key is the game's script.
                problems.append(f"{path}:{n}: key is not 16 lowercase hex digits")
            if key in seen:
                problems.append(f"{path}:{n}: duplicate key (see line {seen[key]})")
            seen.setdefault(key, n)
            if not translation.strip():
                problems.append(f"{path}:{n}: empty translation")
            # section/node are game-internal identifiers, never sentences.
            for name in ("section", "node"):
                if name in col and not IDENT.match(row[col[name]]):
                    problems.append(f"{path}:{n}: {name} does not look like an identifier")
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
                problems.append(f"{f}: must not be committed (contains source text)")
    for locale_dir in sorted(TRANSLATIONS.iterdir()):
        if not locale_dir.is_dir() or locale_dir.name.startswith("_"):
            continue
        local = locale_dir / "strings.local.csv"
        if local.exists() and is_tracked(local):
            problems.append(f"{local}: must not be committed (contains source text)")
        strings = locale_dir / "strings.csv"
        if strings.exists():
            problems.extend(check_file(strings))
        else:
            problems.append(f"{locale_dir}: no strings.csv")
    for p in problems:
        print(p)
    if problems:
        print(f"\n{len(problems)} problem(s).")
        return 1
    print("translations OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
