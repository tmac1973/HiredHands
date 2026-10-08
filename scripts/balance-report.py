#!/usr/bin/env python3
"""Summarise Hired Hands balance logs (*.jsonl from scripts/fetch-balance.sh) for tuning.

    scripts/balance-report.py balance/AMP_Valheim01 [--since-day N]

Prints: fights by job/level against each enemy (kill and death rates, fight length, damage per second dealt and
taken), deaths and what caused them, what gatherers bring home per in-game day, upkeep payment, and hiring.
"""
import argparse
import collections
import json
import pathlib
import statistics
import sys


def load(folder, since_day):
    rows = []
    for f in sorted(pathlib.Path(folder).glob("*.jsonl")):
        for line in f.read_text(encoding="utf-8", errors="replace").splitlines():
            try:
                r = json.loads(line)
            except ValueError:
                continue
            if r.get("day", 0) >= since_day:
                rows.append(r)
    return rows


def pct(n, d):
    return f"{100 * n / d:3.0f}%" if d else "  - "


def avg(xs):
    return statistics.fmean(xs) if xs else 0.0


def table(title, header, rows):
    print(f"\n## {title}\n")
    if not rows:
        print("(none)")
        return
    widths = [max(len(str(x)) for x in col) for col in zip(header, *rows)]
    fmt = "  ".join(f"{{:<{w}}}" for w in widths)
    print(fmt.format(*header))
    print(fmt.format(*["-" * w for w in widths]))
    for r in rows:
        print(fmt.format(*r))


# The shipped blocking/dodging chances by level (Core/Data/DefaultData.cs), for --compare-defense.
DEFENSE_TABLE = {
    1: (0.50, 0.15, 0.30), 2: (0.57, 0.23, 0.37), 3: (0.63, 0.31, 0.44), 4: (0.70, 0.39, 0.51),
    5: (0.76, 0.46, 0.59), 6: (0.83, 0.54, 0.66), 7: (0.89, 0.62, 0.73), 8: (0.95, 0.70, 0.80),
}
MIN_ROLLS = 30  # a rate is judged only with at least this many rolls


def rate(wins, rolls, expected):
    """Observed rate against the table, or n/a when there are too few rolls to judge."""
    if rolls < MIN_ROLLS:
        return f"n/a (n={rolls})"
    r = wins / rolls
    flag = "" if abs(r - expected) <= 0.12 else " !"
    return f"{100 * r:.0f}% vs {100 * expected:.0f}% (n={rolls}){flag}"


def compare_defense(fights):
    """Phase 05 of plan/combat-ai: each hireling (one per matchup run) is a run; off against on per matchup."""
    runs = collections.defaultdict(list)
    for f in fights:
        if "defense" in f:
            runs[f["hid"]].append(f)
    groups = collections.defaultdict(list)
    for hid, fs in runs.items():
        first = fs[0]
        stars = max(f.get("elvl", 1) for f in fs) - 1  # tests may add stars after the first swing
        key = (first["job"], first["lvl"], first["enemy"] + ("*" * stars), first["defense"])
        groups[key].append(fs)
    matchups = sorted({k[:3] for k in groups})
    out = []
    for job, lvl, enemy in matchups:
        read_x, parry_x, dodge_x = DEFENSE_TABLE.get(lvl, (0, 0, 0))
        for mode in ("off", "on"):
            rs = groups.get((job, lvl, enemy, mode), [])
            if not rs:
                continue
            n = len(rs)
            taken = [sum(f["taken"] for f in fs) / max(1.0, fs[0]["hpMax"]) for fs in rs]
            died = sum(any(f["outcome"] == "died" for f in fs) for fs in rs)
            won = sum(fs[-1]["outcome"] == "kill" and not any(f["outcome"] == "died" for f in fs) for fs in rs)
            secs = [sum(f["secs"] for f in fs) for fs in rs]
            tot = collections.Counter()
            for fs in rs:
                for f in fs:
                    for k in ("reads", "misses", "blocks", "parries", "parryRolls", "parryWins", "projReads", "projBlocks",
                              "dodges", "dodgedHits", "dodgeRolls", "dodgeWins"):
                        tot[k] += f.get(k, 0)
            out.append((job, lvl, enemy, mode, n, f"{100 * avg(taken):.0f}%", f"{100 * statistics.median(taken):.0f}%", pct(won, n), pct(died, n),
                        f"{avg(secs):.0f}s",
                        rate(tot["reads"], tot["reads"] + tot["misses"], read_x) if mode == "on" else f"reads {tot['reads']}",
                        rate(tot["parryWins"], tot["parryRolls"], parry_x) if mode == "on" else f"rolls {tot['parryRolls']}",
                        rate(tot["dodgeWins"], tot["dodgeRolls"], dodge_x) if mode == "on" else f"dodges {tot['dodges']}",
                        f"{tot['blocks']}/{tot['parries']}", f"{tot['dodges']}/{tot['dodgedHits']}"))
    table("Blocking and dodging: off against on (one row per matchup and mode; rates vs the levels table, ! = more than 12 points off)",
          ["job", "lvl", "enemy", "mode", "runs", "taken", "median", "won", "died", "length", "read rate", "parry roll", "dodge roll",
           "blocks/parries", "dodges/missed"], out)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("folder")
    ap.add_argument("--since-day", type=int, default=0, help="only in-game days from this one on")
    ap.add_argument("--min-fights", type=int, default=3, help="hide job/enemy pairs with fewer fights")
    ap.add_argument("--compare-defense", action="store_true", help="only the blocking/dodging off-against-on comparison (fights with a defense field)")
    a = ap.parse_args()
    rows = load(a.folder, a.since_day)
    if not rows:
        sys.exit(f"no records in {a.folder}")
    if a.compare_defense:
        compare_defense([r for r in rows if r.get("type") == "fight"])
        return
    by = collections.defaultdict(list)
    for r in rows:
        by[r["type"]].append(r)
    days = sorted({r.get("day", 0) for r in rows})
    versions = sorted({r.get("v", "?") for r in rows})
    print(f"# Hired Hands balance report\n\n{len(rows)} records, in-game days {days[0]}-{days[-1]}, mod {', '.join(versions)}")
    print("Counts: " + ", ".join(f"{k} {len(v)}" for k, v in sorted(by.items())))

    # Fights: per job+level and enemy (with stars).
    groups = collections.defaultdict(list)
    for f in by["fight"]:
        groups[(f["job"], f["lvl"], f["enemy"] + ("*" * f.get("elvl", 1) if f.get("elvl", 1) > 1 else ""))].append(f)
    out = []
    for (job, lvl, enemy), fs in sorted(groups.items()):
        if len(fs) < a.min_fights:
            continue
        n = len(fs)
        kills = sum(f["outcome"] == "kill" for f in fs)
        died = sum(f["outcome"] == "died" for f in fs)
        secs = [f["secs"] for f in fs if f["secs"] > 0]
        dps = [f["dealt"] / f["secs"] for f in fs if f["secs"] > 1]
        tps = [f["taken"] / f["secs"] for f in fs if f["secs"] > 1]
        hp_left = [f["hpEnd"] / f["hpMax"] for f in fs if f.get("hpMax")]
        out.append((job, lvl, enemy, n, pct(kills, n), pct(died, n), f"{avg(secs):.0f}s", f"{avg(dps):.1f}", f"{avg(tps):.1f}",
                    f"{100 * avg(hp_left):.0f}%"))
    table("Fights (job, level, enemy)", ["job", "lvl", "enemy", "n", "kill", "died", "length", "dps out", "dps in", "hp left"], out)

    # Overall per job+level.
    per = collections.defaultdict(list)
    for f in by["fight"]:
        per[(f["job"], f["lvl"])].append(f)
    table("Fights by job and level", ["job", "lvl", "fights", "kill", "died", "dps out", "dps in"],
          [(j, l, len(fs), pct(sum(f["outcome"] == "kill" for f in fs), len(fs)), pct(sum(f["outcome"] == "died" for f in fs), len(fs)),
            f"{avg([f['dealt'] / f['secs'] for f in fs if f['secs'] > 1]):.1f}", f"{avg([f['taken'] / f['secs'] for f in fs if f['secs'] > 1]):.1f}")
           for (j, l), fs in sorted(per.items())])

    # Deaths.
    killers = collections.Counter((d["job"], d["lvl"], d["killer"], d.get("biome", "")) for d in by["death"])
    table("Deaths (job, level, killed by, biome)", ["job", "lvl", "killer", "biome", "n"], [(*k, n) for k, n in killers.most_common(30)])

    # Gathering per in-game day.
    gathered = collections.defaultdict(lambda: collections.Counter())
    hid_days = collections.defaultdict(set)
    for d in by["deliver"]:
        gathered[(d["job"], d["lvl"])][d["item"]] += d["n"]
        hid_days[(d["job"], d["lvl"])].add((d["hid"], d.get("day", 0)))
    table("Delivered per hireling per in-game day", ["job", "lvl", "hireling-days", "per day"],
          [(j, l, len(hid_days[(j, l)]), ", ".join(f"{item} {n / max(1, len(hid_days[(j, l)])):.0f}" for item, n in c.most_common(6)))
           for (j, l), c in sorted(gathered.items())])

    # Upkeep.
    up = by["upkeep"]
    if up:
        paid = sum(u["paid"] for u in up)
        unpaid = sum(u["unpaid"] for u in up)
        print(f"\n## Upkeep\n\n{len(up)} board-days: {paid} hireling-days paid, {unpaid} unpaid ({pct(unpaid, paid + unpaid).strip()}), "
              f"{sum(u['leaving'] for u in up)} quit for lack of pay")

    hires = collections.Counter((h["job"], h["lvl"], h.get("boardLvl", 0)) for h in by["hire"])
    table("Hires (job, level, board level)", ["job", "lvl", "board", "n"], [(*k, n) for k, n in sorted(hires.items())])
    if by["dropped"]:
        print(f"\nNote: {sum(d['n'] for d in by['dropped'])} records were dropped by busy clients (queue full).")


if __name__ == "__main__":
    main()
