"""Render the 8E fixed-opening size study with map/seed block intervals."""
import argparse
import json
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

BINS = ["01–16", "17–39", "40–79", "80–109", "110+"]
LABELS = ["≤16", "17–39", "40–79", "80–109", "≥110"]
COMPARISONS = {
    "monster/hard": "Monster vs Hard",
    "veryhard/hard": "Very Hard vs Hard",
    "monster/old-monster": "8D.4 vs 8D.3 Monster",
    "equal-1/equal-2": "Monster vs same policy",
}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("summary", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    summary = json.loads(args.summary.read_text(encoding="utf-8"))
    fig, axes = plt.subplots(2, 2, figsize=(8.4, 5.8), sharex=True, sharey=True, layout="constrained")
    fig.suptitle("Fixed openings · match points by playable area", fontsize=15)
    for ax, (pair, label) in zip(axes.flat, COMPARISONS.items(), strict=True):
        key = "size-equal-fixed" if pair.startswith("equal") else "size-fixed"
        sizes = summary[key]["sizes"]
        values = [sizes.get(f"{pair}/{area}", {}) for area in BINS]
        rates = [v.get("mapBlockPointRate") for v in values]
        ax.axhline(.5, color="#98a2b3", linewidth=.9, linestyle="--")
        ax.plot(range(len(rates)), rates, marker="o", color="#1e5e74", linewidth=1.2)
        for i, (row, rate) in enumerate(zip(values, rates, strict=True)):
            if rate is None:
                continue
            bounds = row["lowHigh95"]
            if bounds is not None:
                ax.vlines(i, *bounds, color="#1e5e74", linewidth=1.15, alpha=.65)
            ax.annotate(f'n={row["scored"]}', (i, rate), xytext=(0, -12 if rate > .85 else 8),
                        textcoords="offset points", ha="center", fontsize=8)
        ax.set_title(label, loc="left", fontsize=11)
        ax.set_ylim(-.03, 1.07)
        ax.set_xticks(range(len(LABELS)), LABELS)
        ax.set_yticks([0, .25, .5, .75, 1], ["0%", "25%", "50%", "75%", "100%"])
        ax.spines[["top", "right"]].set_visible(False)
    fig.supxlabel("Playable cells (rocks excluded)")
    fig.supylabel("Points per scored match")
    fig.savefig(args.output, dpi=170, facecolor="white")


if __name__ == "__main__":
    main()
