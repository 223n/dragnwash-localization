#!/usr/bin/env python3
"""Keep data/script_order.csv and the packs in step with the game after an update
(experimental; see Drag'n Wash ModFramework's docs/STABLE_LINE_KEYS.md).

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


def replay(args):
    records = [Record(r) for r in read_order(args.old)]
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
    counts = Counter()
    review = []
    for line in read_lines(args.discovered):
        hit = resolve(records, by_id, by_key, by_norm, by_node, line["line_id"], line["node"], line["speaker"], line["source_en"])
        if hit is None:
            counts["not found (new line?)"] += 1
            continue
        rec, layer, needs_review = hit
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
    args = p.parse_args(argv[1:])
    return args.run(args)


if __name__ == "__main__":
    sys.exit(main(sys.argv))
