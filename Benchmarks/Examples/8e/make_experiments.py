"""Generate the predeclared 8E experiment manifests into an ignored directory."""
import argparse
import json
from pathlib import Path

TUNING = ["alder-common", "banner-plain", "bellflower-reach", "twin-basins"]
VALIDATION = ["briar-commons", "crownfall-basin", "linnet-plain", "sentinel-pasture"]
HOLDOUT = ["flint-crescent", "mossglass-plain", "osprey-heights", "whitehart-plain"]
OPENING = {"depth": 1, "candidateLimit": 16, "pairBudget": 256, "policy": "equilibrium"}
LEVELS = ["Easy", "Normal", "Hard", "VeryHard", "Monster"]


def opponent(label, level, version="frozen8d4", stream=None, **overrides):
    return {"id": label, "opponent": f"{version}:{level}", "streamId": stream or label, **overrides}


def experiments():
    def schedule(participants, *, maps=TUNING, seeds=(3251, 3433), fixed=False, references=None, **settings):
        return {
            "format": "baseline" if references else "round-robin",
            "references": references or [],
            "participants": participants,
            "maps": {"ids": maps},
            "ruleset": "knightSupport",
            "opening": {"mode": "fixed" if fixed else "natural"},
            "swapSeats": True,
            "swapStarts": fixed,
            "separatePhaseStreams": True,
            "seeds": list(seeds),
            "workers": 3,
            "strategicMetrics": False,
            **settings,
        }

    for high, low in [("Normal", "Easy"), ("VeryHard", "Hard")]:
        yield f"draft-{high.lower()}", schedule([
            opponent("candidate", high, mainOpponent="frozen8d4:Hard"),
            opponent("reference", low, mainOpponent="frozen8d4:Hard"),
        ], references=["reference"])
    for level, lower in [("Normal", "Easy"), ("VeryHard", "Hard")]:
        main_candidates = [
            ("resilient", {"search": {"resilientEvaluation": True, "verifyTerminalWins": True}}),
            ("steadier", {"mainTemperature": 0.2 if level == "Normal" else 0.04}),
            ("equilibrium", {"search": {"resilientEvaluation": True, "equilibrium": "root", "verifyTerminalWins": True}}),
        ]
        for label, options in main_candidates:
            yield f"{level.lower()}-main-{label}", schedule([
                opponent("candidate", level, "strategic", **options),
                opponent("reference", level),
                opponent("lower", lower),
            ], fixed=True, references=["reference", "lower"])
        yield f"{level.lower()}-opening", schedule([
            opponent("candidate", level, "strategic", openingSearch=OPENING, mainOpponent="frozen8d4:Hard"),
            opponent("reference", level, mainOpponent="frozen8d4:Hard"),
            opponent("lower", lower, mainOpponent="frozen8d4:Hard"),
        ], references=["reference", "lower"])

    for budget in [850, 2000, 4000, 8000]:
        yield f"budget-{budget}", schedule([
            opponent("candidate", "Monster", "strategic", pairBudget=budget),
            opponent("reference", "Monster"),
        ], fixed=True, references=["reference"], seeds=(3613,))
    search = {"resilientEvaluation": True, "equilibrium": "continuations", "verifyTerminalWins": True}
    for label, budget, overrides in [
        ("breadth-850", 850, {"ownCandidates": 24, "opponentCandidates": 16}),
        ("reply-2000", 2000, {"opponentCandidates": 24}),
        ("breadth-2000", 2000, {"ownCandidates": 26, "opponentCandidates": 18}),
        ("breadth-4000", 4000, {"ownCandidates": 32, "opponentCandidates": 24}),
        ("depth-two-2000", 2000, {"depth": 2, "proofOnlyDeepening": False}),
        ("continuation-4000", 4000, {"continuationOwnCandidates": 8, "continuationOpponentCandidates": 8}),
        ("proof-8000", 8000, {"continuationOwnCandidates": 8, "continuationOpponentCandidates": 8, "deepFinalists": 6}),
    ]:
        yield label, schedule([
            opponent("candidate", "Monster", "strategic", pairBudget=budget, search=search | overrides),
            opponent("reference", "Monster"),
        ], fixed=True, references=["reference"], seeds=(3613,))
    yield "snapshot-variants", schedule([
        opponent("base", "Monster", timeLimitMs=450),
        opponent("veryhard-base", "VeryHard", timeLimitMs=450),
        opponent("veryhard-live", "VeryHard", "strategic", timeLimitMs=450),
        *[opponent(f"limit-{budget}", "Monster", "strategic", timeLimitMs=guard, pairBudget=budget)
          for budget, guard in [(850, 450), (2000, 500), (4000, 1000), (8000, 1000)]],
        opponent("wide", "Monster", "strategic", timeLimitMs=450, pairBudget=850,
                 search=search | {"ownCandidates": 24, "opponentCandidates": 16}),
    ])
    for fixed in [False, True]:
        mode = "fixed" if fixed else "natural"
        yield f"validate-monster-{mode}", schedule([
            opponent("candidate", "Monster", "strategic", timeLimitMs=450, pairBudget=850,
                     search=search | {"ownCandidates": 24, "opponentCandidates": 16}),
            opponent("reference", "Monster", timeLimitMs=450),
        ], maps=VALIDATION, seeds=(4271, 4441, 4591), fixed=fixed, references=["reference"])
    for level in ["Easy", "Hard", "Monster"]:
        same_main = f"frozen8d4:{level}"
        yield f"opening-sensitivity-{level.lower()}", schedule([
            opponent("candidate", "Monster", mainOpponent=same_main),
            opponent("reference", "Monster", "frozen8d3", mainOpponent=same_main),
        ], maps=VALIDATION, references=["reference"], seeds=(3863, 4003, 4201))
    for label, options in [
        ("depth-two", OPENING | {"depth": 2, "pairBudget": 900}),
        ("preview", OPENING | {"depth": 2, "pairBudget": 1200, "mainPreviewDepth": 1}),
    ]:
        yield f"opening-{label}", schedule([
            opponent("candidate", "Monster", "strategic", openingSearch=options, mainOpponent="frozen8d4:Hard"),
            opponent("reference", "Monster", mainOpponent="frozen8d4:Hard"),
        ], maps=TUNING + ["three-banners", "pocket-meadow"], references=["reference"], seeds=(3613, 3793))

    for fixed in [False, True]:
        mode = "fixed" if fixed else "natural"
        for variant in (["resilient"] if fixed else ["resilient", "resilient-with-opening"]):
            yield f"validate-ladder-{variant}-{mode}", schedule([
                opponent("easy", "Easy"), opponent("normal", "Normal", "strategic",
                    search={"resilientEvaluation": True, "verifyTerminalWins": True},
                    **({"openingSearch": OPENING} if variant == "resilient-with-opening" else {})),
                opponent("hard", "Hard"), opponent("veryhard", "VeryHard"),
                opponent("monster", "Monster"),
            ], maps=VALIDATION, seeds=(4271, 4441, 4591), fixed=fixed)

    for fixed in [False, True]:
        mode = "fixed" if fixed else "natural"
        levels = ["Normal", "Hard", "VeryHard"]
        variants = [
            opponent(level.lower(), level, "strategic", search={"resilientEvaluation": True, "verifyTerminalWins": True})
            if level in levels else opponent(level.lower(), level) for level in LEVELS
        ]
        yield f"validate-layered-{mode}", schedule(variants, maps=VALIDATION, seeds=(4271, 4441), fixed=fixed)
    yield "validate-normal-draft-only", schedule([
        opponent(level.lower(), level, "strategic", openingSearch=OPENING) if level == "Normal"
        else opponent(level.lower(), level) for level in LEVELS
    ], maps=VALIDATION, seeds=(4271, 4441, 4591))

    multi_maps = ["banner-plain", "briar-commons", "crownfall-basin", "flint-crescent",
                  "granite-orchard", "honeystone-green", "linnet-plain", "osprey-heights",
                  "sentinel-pasture", "twin-basins", "whitehart-plain", "wrenfield"]
    for pairs in [1, 2]:
        yield f"catalog-fixed-{pairs}", schedule([
            opponent("monster", "Monster"), opponent("hard", "Hard")
        ], maps=multi_maps, seeds=(6211, 6397), fixed=True,
           opening={"mode": "fixed", "pairs": pairs, "seed": 6419})

    for fixed in [False, True]:
        mode = "fixed" if fixed else "natural"
        yield f"validate-release-{mode}", schedule([
            opponent("easy", "Easy"),
            opponent("normal", "Normal", "strategic", openingSearch=OPENING),
            opponent("hard", "Hard"),
            opponent("veryhard", "VeryHard", "strategic",
                     search={"resilientEvaluation": True, "verifyTerminalWins": True}),
            opponent("monster", "Monster"),
        ], maps=VALIDATION, seeds=(6607, 6791), fixed=fixed)

    for size, budget in [(8, 2000), (10, 2000), (12, 4000)]:
        maps = [f"study-{family}-{size}" for family in ["open", "block"]]
        yield f"size-scaled-{size}", schedule([
            opponent("monster", "Monster", "strategic", pairBudget=budget),
            opponent("hard", "Hard"),
        ], maps=maps, fixed=True, references=["hard"], seeds=(5501, 5669),
           opening={"mode": "fixed", "fixed": {m: [f"A1/{chr(64 + size)}{size}"] for m in maps}})

    # Pinned reference ladder. A promoted candidate requires its own fresh holdout.
    for fixed in [False, True]:
        mode = "fixed" if fixed else "natural"
        yield f"holdout-{mode}", schedule([
            opponent(level.lower(), level) for level in LEVELS
        ], maps=HOLDOUT, seeds=(4903, 5059, 5279), fixed=fixed)
        yield f"holdout-candidate-{mode}", schedule([
            opponent(level.lower(), level, "strategic", search={"resilientEvaluation": True, "verifyTerminalWins": True})
            if level == "VeryHard" else opponent(level.lower(), level) for level in LEVELS
        ], maps=HOLDOUT, seeds=(4903, 5059, 5279), fixed=fixed)
        yield f"holdout-production-{mode}", schedule([
            opponent(level.lower(), level, "strategic") for level in LEVELS
        ], maps=HOLDOUT, seeds=(4903, 5059, 5279), fixed=fixed)
        maps = ["pocket-meadow", "corner-watch", "stone-center", "open-meadow",
                "alder-common", "honeystone-green", "twelve-towers"] + [
            f"study-{family}-{size}" for family in ["open", "block"] for size in [4, 6, 8, 10, 12]
        ]
        fixed_options = {"opening": {"mode": "fixed", "fixed": {
            f"study-{family}-{size}": [f"A1/{chr(64 + size)}{size}"]
            for family in ["open", "block"] for size in [4, 6, 8, 10, 12]
        }}} if fixed else {}
        yield f"size-{mode}", schedule([
            opponent("monster", "Monster"), opponent("hard", "Hard"), opponent("veryhard", "VeryHard"),
            opponent("old-monster", "Monster", "frozen8d3")
        ], maps=maps, seeds=(5501, 5669), fixed=fixed, references=["hard", "old-monster"], **fixed_options)
        yield f"size-calibrated-{mode}", schedule([
            opponent("veryhard", "VeryHard", "strategic"), opponent("hard", "Hard")
        ], maps=maps, seeds=(5501, 5669), fixed=fixed, references=["hard"], **fixed_options)
        yield f"size-equal-{mode}", schedule([
            opponent("equal-1", "Monster"), opponent("equal-2", "Monster"),
        ], maps=maps, seeds=(5501, 5669), fixed=fixed, **fixed_options)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    args = parser.parse_args()
    args.directory.mkdir(parents=True, exist_ok=True)
    for name, config in experiments():
        (args.directory / f"{name}.json").write_text(
            json.dumps({"id": f"8e-{name}", **config}, indent=2) + "\n", encoding="utf-8")
