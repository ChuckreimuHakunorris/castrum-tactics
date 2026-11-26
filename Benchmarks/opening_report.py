"""Summarize a fixed-opening study by resolved configuration within each map split."""

import argparse
import itertools
import json
import statistics
from collections import defaultdict
from pathlib import Path


FEATURES = (
    "legacy", "evaluation", "ablation", "secured", "coverage", "accessible",
    "footholds", "routes", "legal", "friendlySpread", "enemyDistance",
    "earlyMargin", "earlyFormation",
)


def correlation(a, b):
    if len(a) < 2 or min(statistics.pvariance(a), statistics.pvariance(b)) == 0:
        return None
    return statistics.correlation(a, b)


def standings(rows, feature, target):
    maps = defaultdict(list)
    for row in rows:
        maps[row["map"]].append(row)
    right = compared = 0
    for choices in maps.values():
        for a, b in itertools.combinations(choices, 2):
            x, y = a[feature] - b[feature], a[target] - b[target]
            if abs(x) < 1e-12 or abs(y) < 1e-12:
                continue
            compared += 1
            right += x * y > 0
    return {
        "pearson": correlation([r[feature] for r in rows], [r[target] for r in rows]),
        "withinMapRankAgreement": right / compared if compared else None,
        "comparablePairs": compared,
    }


def aggregate(rows):
    by_opening = defaultdict(list)
    for row in rows:
        by_opening[row["map"], row["key"]].append(row)
    results = []
    for (map_id, key), group in by_opening.items():
        def oriented(name):
            return statistics.mean(
                (-r[name] if r["startsSwapped"] and name != "enemyDistance" else r[name])
                for r in group
            )

        results.append({
            "map": map_id, "key": key,
            "points": statistics.mean(1-r["points"] if r["startsSwapped"] else r["points"] for r in group),
            "margin": oriented("margin"),
            **{feature: oriented(feature) for feature in FEATURES},
        })
    return results


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("outcomes", type=Path)
    args = parser.parse_args()
    with args.outcomes.open(encoding="utf-8") as source:
        data = [json.loads(line) for line in source if line.strip()]
    completed = [r for r in data if r["status"] == "completed" and r["opening"] != "retry"]
    reports = []
    for split in sorted({r["split"] for r in completed}):
        for opponent in sorted({r["opponent"] for r in completed}):
            subset = [r for r in completed if r["split"] == split and r["opponent"] == opponent]
            if not subset:
                continue
            openings = aggregate(subset)
            reports.append({
                "split": split, "opponent": opponent, "matches": len(subset), "configurations": len(openings),
                "meanTurns": statistics.mean(r["mainTurns"] for r in subset),
                "castleCounts": sorted({r["redCastles"] for r in subset}),
                "features": {
                    feature: {target: standings(openings, feature, target) for target in ("points", "margin")}
                    for feature in FEATURES
                },
            })
    print(json.dumps({"scheduled": len(data), "scored": len(completed), "reports": reports}, indent=2))


if __name__ == "__main__":
    main()
