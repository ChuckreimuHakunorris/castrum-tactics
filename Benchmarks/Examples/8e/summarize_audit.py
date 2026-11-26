"""Join authoritative catalog geometry and opening audit with measured matches."""
import argparse
import csv
import json
from pathlib import Path
from statistics import mean


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("audit", type=Path)
    parser.add_argument("tournaments", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    audit = json.loads(args.audit.read_text(encoding="utf-8"))
    summaries = json.loads(args.tournaments.read_text(encoding="utf-8"))
    catalog = summaries["catalog"]["maps"]
    rows = []
    for layout in audit:
        map_id = layout["id"]
        key = f"hard/monster/{map_id}"
        if key not in catalog:
            continue
        natural = catalog[key]
        samples = layout["openings"]
        playable = [s for s in samples if s["phase"] == "main"]
        terminal = sum(s["phase"] == "finished" for s in samples)
        retries = sum(s["phase"] == "openingRestartRequired" for s in samples)
        min_actions = min(min(s["redLegal"], s["blueLegal"]) for s in playable)
        max_asymmetry = max(abs(s["redLegal"] - s["blueLegal"]) for s in playable)
        flags = []
        if len(layout["regions"]) > 1:
            flags.append("review disconnected regions")
        if layout["regionsWithoutStarts"]:
            flags.append("check regions without starts")
        if layout["bottlenecks"]:
            flags.append("test articulation routes")
        if natural["retry"]:
            flags.append("repeat natural draft with more policies")
        if terminal or natural["draftScore"]:
            flags.append("replay immediate terminal openings")
        if min_actions <= 1 or max_asymmetry >= 12:
            flags.append("compare mirrored starts with wide legal-count swings")
        if natural["meanSecondPairs"] is not None and natural["meanSecondPairs"] >= 350:
            flags.append("profile Monster search on saved positions")
        shared = [s["sharedLegal"] for s in playable]
        castle_counts = sorted({s["redCastles"] for s in playable})
        fixed = {n: summaries.get(f"catalog-fixed-{n}", {}).get("maps", {}).get(f"hard/monster/{map_id}", {})
                 for n in (1, 2)}
        rows.append({
            "map": map_id, "width": layout["width"], "height": layout["height"], "playable": layout["playable"],
            "rocks": layout["rocks"], "rockDensity": round(layout["rockDensity"], 4),
            "starts": " ".join(layout["starts"]), "minStartDistance": layout["minStartDistance"],
            "maxStartDistance": layout["maxStartDistance"], "disconnectedStartPairs": layout["disconnectedStartPairs"],
            "regions": " ".join(map(str, layout["regions"])), "regionsWithoutStarts": layout["regionsWithoutStarts"],
            "bottlenecks": " ".join(layout["bottlenecks"]), "symmetricTransforms": len(layout["symmetries"]),
            "openingSamples": len(samples), "playableSamples": len(playable),
            "sampleTerminal": terminal, "sampleRetry": retries, "castlesPerSide": " ".join(map(str, castle_counts)),
            "minLegalForEither": min_actions, "largestLegalDifference": max_asymmetry,
            "meanSharedLegal": round(mean(shared), 2), "maxContactFronts": max(s["contactFronts"] for s in playable),
            "maxMarginalReachDifference": max(abs(s["redMarginalReach"] - s["blueMarginalReach"]) for s in playable),
            "naturalScored": natural["scored"], "naturalRetry": natural["retry"], "naturalDraftScore": natural["draftScore"],
            "naturalMonsterPoints": None if natural["pointRate"] is None else round(1 - natural["pointRate"], 4),
            "naturalMonsterPairs": natural["meanSecondPairs"], "naturalMeanTurns": natural["meanTurns"],
            "fixedOneMonsterPoints": monster_points(fixed[1]), "fixedTwoMonsterPoints": monster_points(fixed[2]),
            "followUp": " | ".join(flags), "digest": layout["digest"],
        })
    with args.output.open("w", encoding="utf-8", newline="") as file:
        writer = csv.DictWriter(file, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)


def monster_points(summary):
    rate = summary.get("pointRate")
    return None if rate is None else round(1 - rate, 4)


if __name__ == "__main__":
    main()
