#!/usr/bin/env python3
"""Keep data/script_order.csv and the packs in step with the game after an update
(experimental; see https://github.com/TomXV/dragnwash-modframework/wiki/Dialogue).

Needs the game's own data, which the plugin writes under
BepInEx/plugins/DragNWashLocalization/Translations/_discovered/ (F6 in the game):
dialogue_lines.csv (line id, key, node, speaker, English) and, after F7,
script_order.csv. Nothing from there is committed; this tool only writes keys.

    python tools/rekey.py augment --discovered <dir>
        Adds the norm, fp and nlen columns to data/script_order.csv from the
        English in dialogue_lines.csv, keeping every other column as it is.
        Use once for a script order made by an older build of the plugin.

    python tools/rekey.py replay --discovered <dir> [--old data/script_order.csv]
        Runs the four resolver layers from the old script order over the game's
        current lines and reports how many lines each layer finds, and which
        lines need a human look. Prints counts and identifiers only, never text.

    python tools/rekey.py packs --discovered <dir> [--old data/script_order.csv] [--dry-run]
        For every line the update changed, adds a row with the line's new key
        to each Translations/<locale>/strings.csv, right after the row with the
        old key and with the same translation, so the line stays translated
        with the plain hash lookup too. Old rows are kept. Then copy the new
        script_order.csv into data/ and run augment.
"""
import argparse
import csv
import sys
from collections import Counter, defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import linekeys as lk  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
ORDER = ROOT / "data" / "script_order.csv"
COLUMNS = ["section", "phase", "node", "order", "line_id", "key", "speaker", "condition", "norm", "fp", "nlen"]


def read_lines(discovered: Path):
    with open(discovered / "dialogue_lines.csv", encoding="utf-8-sig", newline="") as f:
        return [r for r in csv.DictReader(f) if r.get("source_en")]


def read_order(path: Path):
    with open(path, encoding="utf-8-sig", newline="") as f:
        return list(csv.DictReader(f))


def write_order(path: Path, rows):
    with open(path, "w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, fieldnames=COLUMNS, lineterminator="\n")
        w.writeheader()
        for r in rows:
            w.writerow({c: r.get(c, "") for c in COLUMNS})


def augment(args):
    english = {r["line_id"]: r["source_en"] for r in read_lines(args.discovered)}
    rows = read_order(args.order)
    done = missing = 0
    for r in rows:
        text = english.get(r["line_id"])
        if text is None or lk.key(text) != r["key"]:
            missing += 1
            continue
        n = lk.normalize(text)
        r["norm"], r["fp"], r["nlen"] = lk.key(n), lk.fingerprint_text(text), str(len(n))
        done += 1
    write_order(args.order, rows)
    print(f"{args.order}: keys added to {done} row(s); {missing} row(s) have no matching line in the game data and were left without them.")
    return 0 if missing == 0 else 1


class Record:
    __slots__ = ("line_id", "key", "norm", "fp", "nlen", "node", "speaker")

    def __init__(self, r):
        self.line_id, self.key, self.node, self.speaker = r["line_id"], r["key"], r["node"], r["speaker"]
        self.norm = r.get("norm") or None
        self.fp = int(r["fp"], 16) if r.get("fp") else 0
        self.nlen = int(r["nlen"]) if r.get("nlen") else 0


def resolve(records, by_id, by_key, by_norm, by_node, line_id, node, speaker, text):
    """The same four layers as LineResolver.Resolve; returns (record, layer, review) or None."""
    if line_id in by_id:
        rec = by_id[line_id]
        return rec, "line id", rec.key != lk.key(text)
    k = lk.key(text)
    if k in by_key:
        return by_key[k], "hash", False
    n = lk.normalize(text)
    same = by_norm.get(lk.key(n), [])
    if len(same) == 1:
        return same[0], "normalized", False
    if len(n) < lk.MIN_FUZZY_LENGTH:
        return None
    if node:
        pool = by_node.get(node)
        if pool is None:
            return None
    else:
        pool = records
    fp = lk.fingerprint(n)
    best, tied = lk.MAX_FUZZY_DISTANCE + 1, []
    for rec in pool:
        if not rec.fp or rec.nlen < lk.MIN_FUZZY_LENGTH:
            continue
        d = lk.distance(fp, rec.fp)
        if d < best:
            best, tied = d, [rec]
        elif d == best:
            tied.append(rec)
    if len(tied) > 1 and speaker:
        tied = [t for t in tied if t.speaker == speaker]
    if len(tied) != 1:
        return None
    return tied[0], "fuzzy", True


def matches(old_order: Path, discovered: Path):
    """Yields (line, record or None, layer, needs_review) for every current line."""
    records = [Record(r) for r in read_order(old_order)]
    by_id = {r.line_id: r for r in records if r.line_id}
    by_key = {}
    for r in records:
        by_key.setdefault(r.key, r)
    by_norm, by_node = defaultdict(list), defaultdict(list)
    for r in records:
        if r.norm:
            by_norm[r.norm].append(r)
        if r.node:
            by_node[r.node].append(r)
    for line in read_lines(discovered):
        hit = resolve(records, by_id, by_key, by_norm, by_node, line["line_id"], line["node"], line["speaker"], line["source_en"])
        if hit is None:
            yield line, None, None, False
        else:
            yield line, hit[0], hit[1], hit[2]


def replay(args):
    counts = Counter()
    review = []
    for line, rec, layer, needs_review in matches(args.old, args.discovered):
        if rec is None:
            counts["not found (new line?)"] += 1
            continue
        counts[layer + (" (text changed)" if needs_review else "")] += 1
        if needs_review:
            review.append((line["node"], line["line_id"], rec.line_id, layer))
    for name, n in counts.most_common():
        print(f"{n:6}  {name}")
    if review:
        print(f"\n{len(review)} line(s) to review (node, line id now, record it matched, layer):")
        for row in review:
            print("  " + "  ".join(row))
    return 0


def packs(args):
    # old key -> new keys the same lines carry now (only where they differ)
    moved = defaultdict(set)
    for line, rec, layer, needs_review in matches(args.old, args.discovered):
        if rec is None or not needs_review:
            continue
        new_key = lk.key(line["source_en"])
        if new_key != rec.key:
            moved[rec.key].add(new_key)
    if not moved:
        print("No line changed its key; the packs are up to date.")
        return 0
    total = 0
    for pack in sorted((ROOT / "Translations").glob("*/strings.csv")):
        raw = pack.read_bytes()
        newline = "\r\n" if b"\r\n" in raw else "\n"
        lines = raw.decode("utf-8-sig").split(newline)
        present = {l.split(",", 1)[0] for l in lines if l and not l.startswith("#")}
        out, added = [], 0
        for l in lines:
            out.append(l)
            if not l or l.startswith("#"):
                continue
            key, _, rest = l.partition(",")
            for new_key in sorted(moved.get(key, ())):
                if new_key not in present:
                    out.append(new_key + "," + rest)
                    present.add(new_key)
                    added += 1
        if added and not args.dry_run:
            pack.write_bytes((newline.join(out)).encode("utf-8"))
        print(f"{pack.relative_to(ROOT)}: {added} row(s) {'would be ' if args.dry_run else ''}added")
        total += added
    print(f"{len(moved)} key(s) moved; {total} row(s) {'would be ' if args.dry_run else ''}added across the packs. Those lines are worth a look: the English changed.")
    return 0


def main(argv):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="cmd", required=True)
    a = sub.add_parser("augment")
    a.add_argument("--discovered", type=Path, required=True)
    a.add_argument("--order", type=Path, default=ORDER)
    a.set_defaults(run=augment)
    r = sub.add_parser("replay")
    r.add_argument("--discovered", type=Path, required=True)
    r.add_argument("--old", type=Path, default=ORDER)
    r.set_defaults(run=replay)
    k = sub.add_parser("packs")
    k.add_argument("--discovered", type=Path, required=True)
    k.add_argument("--old", type=Path, default=ORDER)
    k.add_argument("--dry-run", action="store_true")
    k.set_defaults(run=packs)
    args = p.parse_args(argv[1:])
    return args.run(args)


if __name__ == "__main__":
    sys.exit(main(sys.argv))
