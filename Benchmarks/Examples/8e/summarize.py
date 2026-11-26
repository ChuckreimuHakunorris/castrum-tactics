"""Summarize tournament shards by paired block and canonical action history."""
import argparse
import json
from math import ceil
from collections import Counter, defaultdict
from pathlib import Path
from random import Random
from statistics import mean


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


def average(values):
    values = list(values)
    return round(mean(values), 6) if values else None


def quantile(values, fraction):
    ordered = sorted(values)
    return ordered[min(len(ordered) - 1, max(0, ceil(len(ordered) * fraction) - 1))]


def interval(blocks):
    """Resample maps and then independent seed blocks within each selected map."""
    if len(blocks) < 3 or sum(map(len, blocks.values())) < 5:
        return None
    values = [list(v) for v in blocks.values()]
    rng = Random(8167)
    draws = []
    for _ in range(2000):
        maps = rng.choices(values, k=len(values))
        draws.append(mean(mean(rng.choices(rows, k=len(rows))) for rows in maps))
    return [round(quantile(draws, p), 6) for p in (.025, .975)]


def transformed(move, width, height, geometry):
    x, y = ord(move[0]) - 65, int(move[1:]) - 1
    if geometry & 4:
        x, y = y, x
    return (width - 1 - x if geometry & 1 else x, height - 1 - y if geometry & 2 else y)


def canonical(result, swapped, geometry):
    # Normalize participant order first, then choose a representative under map-and-start symmetries.
    width, height, symmetries = geometry
    positional = [(r["phase"], r["red"]["move"], r["blue"]["move"]) for r in result["rounds"]]
    return min(tuple((phase, transformed(b if swapped else a, width, height, t),
                      transformed(a if swapped else b, width, height, t)) for phase, a, b in positional)
               for t in symmetries)


def rows_for(path, audit):
    manifest = read(path / "manifest.json")
    maps = {m["id"]: m for m in manifest["maps"]}
    for game in manifest["matches"]:
        file = path / "matches" / f'{game["id"]}.json'
        if not file.exists():
            yield game, None
            continue
        result = read(file)["payload"]
        layout = maps[game["map"]]
        width, height = layout["width"], layout["height"]
        swapped = game["seatsSwapped"]
        margin = (result["redScore"] - result["blueScore"]) * (-1 if swapped else 1)
        scored = result["status"] == "completed" and result["opening"] != "retry"
        first_side = "playerTwo" if swapped else "playerOne"
        winner = result["winner"]
        rounds = result["rounds"]
        decisions = [r[side] for r in rounds for side in ("red", "blue") if r[side]["measured"]]
        result = {
            "status": result["status"], "opening": result["opening"], "scored": scored,
            "points": (0.5 if winner is None else int(winner == first_side)) if scored else None,
            "margin": margin, "area": width * height - layout["rocks"],
            "width": width, "height": height, "mainTurns": result["mainTurns"],
            "collisions": result["collisions"], "draftRounds": result["draftRounds"] - result["forcedCollisions"],
            "mainCollisions": sum(r["phase"] == "main" and r["red"]["move"] == r["blue"]["move"] for r in rounds),
            "decisionMs": sum(d["milliseconds"] for d in decisions),
            "mainPairs": [[r[side]["pairs"] for r in rounds if r["phase"] == "main" and r[side]["pairs"] is not None]
                          for side in (("blue", "red") if swapped else ("red", "blue"))],
            "history": canonical(result, swapped, (width, height, audit[game["map"]]["symmetries"]))
                       if game["map"] in audit and scored else result["historyDigest"],
        }
        yield game, result


def summary(games):
    scored = [(g, r) for g, r in games if r is not None and r["scored"]]
    completed = [r for _, r in games if r and r["status"] == "completed"]
    counts = Counter(r["points"] for _, r in scored)
    seen = set()
    unique = []
    blocks = defaultdict(list)
    for game, result in scored:
        key = (game["map"], result["history"])
        if key not in seen:
            seen.add(key)
            unique.append(result)
        blocks[game["map"], game["seed"]].append(result)

    maps = defaultdict(list)
    seen_blocks = set()
    for (map_id, _), block in sorted(blocks.items()):
        trace = tuple(sorted((r["history"] for r in block), key=repr))
        if (map_id, trace) not in seen_blocks:
            maps[map_id].append(mean(r["points"] for r in block))
            seen_blocks.add((map_id, trace))
    return {
        "scheduled": len(games), "scored": len(scored),
        "wins": counts[1], "draws": counts[.5], "losses": counts[0],
        "retry": sum(r["opening"] == "retry" for r in completed),
        "draftScore": sum(r["opening"] == "draftscore" for r in completed),
        "failed": sum(r is not None and r["status"] == "failed" for _, r in games),
        "limited": sum(r is not None and r["status"] == "limit" for _, r in games),
        "pending": sum(r is None for _, r in games),
        "pointRate": average(r["points"] for _, r in scored),
        "distinctHistories": len(unique), "distinctPointRate": average(r["points"] for r in unique),
        "blocks": len(seen_blocks), "maps": len(maps),
        "mapBlockPointRate": average(mean(v) for v in maps.values()), "lowHigh95": interval(maps),
        "meanMargin": average(r["margin"] for _, r in scored),
        "meanNormalizedMargin": average(r["margin"] / r["area"] for _, r in scored),
        "meanTurns": average(r["mainTurns"] for _, r in scored),
        "meanDecisionMsPerMatch": average(r["decisionMs"] for _, r in scored),
        "meanFirstPairs": average(p for _, r in scored for p in r["mainPairs"][0]),
        "meanSecondPairs": average(p for _, r in scored for p in r["mainPairs"][1]),
        "mainCollisions": sum(r["mainCollisions"] for r in completed),
        "draftCollisions": sum(r["collisions"] for r in completed),
        "submittedDraftRounds": sum(r["draftRounds"] for r in completed),
    }


def size_bin(area):
    return "01–16" if area <= 16 else "17–39" if area <= 39 else "40–79" if area <= 79 else "80–109" if area <= 109 else "110+"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", type=Path)
    parser.add_argument("--audit", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    audit = {m["id"]: m for m in read(args.audit)}
    result = {}
    for path in sorted(args.root.iterdir()):
        if not (path / "manifest.json").exists():
            continue
        groups, map_groups, sizes = defaultdict(list), defaultdict(list), defaultdict(list)
        for game, item in rows_for(path, audit):
            pair = f'{game["first"]}/{game["second"]}'
            groups[pair].append((game, item))
            map_groups[pair, game["map"]].append((game, item))
            if item is not None and path.name.startswith("size-"):
                sizes[pair, size_bin(item["area"])].append((game, item))
        result[path.name] = {
            "pairings": {pair: summary(games) for pair, games in groups.items()},
            "maps": {f"{pair}/{map_id}": summary(games) for (pair, map_id), games in map_groups.items()},
            "sizes": {f"{pair}/{size}": summary(games) for (pair, size), games in sizes.items()},
        }
    args.out.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
