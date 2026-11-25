"""Summarize one or more 8C match logs without changing the recorded match histories."""

import argparse
import collections
import json
import math
import pathlib
import statistics


def read_records(path):
    raw = pathlib.Path(path).read_bytes()
    encoding = "utf-16" if raw.startswith((b"\xff\xfe", b"\xfe\xff")) else "utf-8-sig"
    return [json.loads(line) for line in raw.decode(encoding).splitlines() if line]


def percentile(values, fraction):
    return sorted(values)[math.ceil(len(values) * fraction) - 1]


def summarize(label, rows):
    counts = collections.Counter()
    metrics = collections.Counter()
    samples = collections.defaultdict(list)
    for row in rows:
        counts[row["opening"]] += 1
        if row["opening"] != "retry":
            margin = row["redScore"] - row["blueScore"]
            margin *= 1 if row["firstSide"] == "red" else -1
            counts["win" if margin > 0 else "loss" if margin < 0 else "draw"] += 1
        if row["opening"] == "main":
            metrics["avoidable"] += row["avoidableImmediateDefeat"]
            metrics["interior"] += row["interiorPlacements"]
            metrics["placements"] += row["placements"]
        for turn in row["rounds"]:
            decision = turn[row["firstSide"]]
            if decision["legal"] > 0:
                samples[turn["phase"]].append(decision)

    print(f"{label}: {counts['win']}-{counts['draw']}-{counts['loss']} W-D-L, "
          f"{counts['retry']} retry, {counts['draftscore']} scored opening, "
          f"{metrics['avoidable']} avoidable losses, {metrics['interior']}/{metrics['placements']} interior placements")
    for phase, decisions in sorted(samples.items()):
        describe(phase, decisions)
        if phase == "main":
            complex_positions = [item for item in decisions if item["legal"] >= 16]
            if complex_positions:
                describe("main with at least 16 legal actions", complex_positions)


def describe(phase, decisions):
    times = [item["milliseconds"] for item in decisions]
    pairs = [item["pairs"] for item in decisions]
    allocations = [item["allocatedBytes"] for item in decisions]
    depth_two = sum(item["depth"] >= 2 for item in decisions)
    print(f"  {phase}: n={len(times)}, latency ms "
          f"{statistics.median(times):.3f}/{percentile(times, 0.95):.3f}/{max(times):.3f} median/P95/max, "
          f"pairs {statistics.median(pairs):g}/{max(pairs)} median/max, "
          f"{depth_two} depth two, {statistics.median(allocations) / 1024:.1f} KiB median allocation")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("logs", nargs="+")
    args = parser.parse_args()
    matchups = collections.defaultdict(list)
    for path in args.logs:
        for row in read_records(path):
            key = (row["ruleset"], row["first"], row["second"], "fixed" if row["fixedStarts"] else "draft")
            matchups[key].append(row)
    for key, rows in matchups.items():
        summarize(" / ".join(key), rows)
        for map_id in sorted({row["map"] for row in rows}):
            summarize(map_id, [row for row in rows if row["map"] == map_id])


if __name__ == "__main__":
    main()
