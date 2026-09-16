#!/usr/bin/env python3
"""Validate the published translation files.

Run by CI on every pull request and usable locally:

    python tools/check-translations.py

A published Translations/<locale>/strings.csv must:
  - have the header  key,section,node,order,speaker,translation
    (older key,speaker,translation and key,translation are still accepted)
  - lines starting with '#' are section headers and are ignored
  - key: 16 lowercase hex digits (SHA-256 prefix of the source string), or a
    Yarn line ID such as line:6046bedf for a row that translates one line only
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
LINE_ID = re.compile(r"^line:[A-Za-z0-9_.\-]{1,59}$")
IDENT = re.compile(r"^(?:[A-Za-z0-9_]*|L\d\d [A-Za-z]+|UI)$")
ROOT = Path(__file__).resolve().parent.parent
TRANSLATIONS = ROOT / "Translations"


def display(path: Path) -> str:
    """Repository-relative path, so reports read Translations/ja/strings.csv."""
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def comment_lines(text):
    """Line numbers (1-based) of the comment records in text.

    The game (src/DragNWashLocalization/CsvReader.cs) treats a '#' as a
    comment only when it starts a record outside quotes. Deciding that on the
    parsed fields instead would silently drop a quoted "#..." value, which
    CsvReader keeps - and which CsvReader.Escape quotes so that it survives
    the round trip. The quote parity carried across lines is what separates a
    comment from a line that merely sits inside a quoted, multi-line value.
    """
    found = set()
    in_quotes = False
    for number, line in enumerate(text.splitlines(), start=1):
        if not in_quotes and line[:1] == "#":
            # Not a record; its quotes cannot open one either.
            found.add(number)
            continue
        if line.count('"') % 2 == 1:
            in_quotes = not in_quotes
    return found


def check_file(path: Path) -> list[str]:
    problems = []
    name = display(path)
    # Parse first, then drop comment rows. Filtering physical lines before the
    # CSV parser sees them also removes lines that belong to a quoted
    # multi-line value, which silently changes the value being validated: the
    # game (src/DragNWashLocalization/CsvReader.cs) only treats '#' as a
    # comment at the start of a record, never inside quotes.
    #
    # Reports must point at the line in the file, so remember where each
    # record started. reader.line_num is the last physical line the record
    # used, so the next record starts on the line after it.
    with io.open(path, encoding="utf-8-sig", newline="") as f:
        comments = comment_lines(f.read())
    with io.open(path, encoding="utf-8-sig", newline="") as f:
        reader = csv.reader(f)
        rows = []
        previous = 0
        try:
            for row in reader:
                start = previous + 1
                previous = reader.line_num
                if not row:            # a blank line between records
                    continue
                if start in comments:
                    continue
                rows.append((start, row))
        except csv.Error as exc:
            return [f"{name}:{reader.line_num}: could not be parsed as CSV ({exc})"]
    if not rows:
        return [f"{name}: empty file"]
    header = rows[0][1]
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
    for n, row in rows[1:]:
        if len(row) != width:
            problems.append(f"{name}:{n}: expected {width} fields, got {len(row)}")
            continue
        key, translation = row[0], row[-1]
        if not KEY.match(key) and not LINE_ID.match(key):
            # Deliberately do not echo the key: on a public repository the
            # report is visible, and a plain-text key is the game's script.
            problems.append(f"{name}:{n}: key is not 16 lowercase hex digits or a line ID")
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
