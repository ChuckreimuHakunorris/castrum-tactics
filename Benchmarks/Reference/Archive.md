# Castrum Tactics: Knight Support CPU baseline

This archive freezes the Knight Support playtests reported on 25 November 2026. The game code used was the tree on main at `afb0dcbab5b789ed8820017c84648ed4dd3bbe14`, identical to the tested PR #13 head `f89403f4e9118d1c54edd519adcc29ec2c74526f`. The bundled game assemblies are the unmodified Domain and AI from that tree. Tests ran on Linux x64 with .NET SDK 10.0.404/runtime 10.0.15; selected UI replays used Godot 4.7.2 .NET. No game source, branch, or PR was changed.

## What’s inside

| Path | Contents |
| --- | --- |
| `reference/benchmark-moves.jsonl` | One record per trial, with map (Knight Support throughout), CPU profile and side, exact RNG seeds, both draft selections and every turn, scores, and whether the draft failed. |
| `reference/manual-moves.jsonl` | Every hand-picked match move, both sides’ coordinates, exact per-decision CPU seeds, and scores. |
| `reference/matches.csv`, `reference/duel.csv` | The original recorded benchmark outcomes; no header in the original format. |
| `bench/Program.cs` | Runnable benchmark and exact experimental opponent implementation (see `Probe` and `Value`). |
| `manual/` | Manual-play harness and the two original move logs. |
| `baseline-engine/` | Pinned game DLLs so the benchmark can be rerun without checking out or modifying the game repo. |
| `ui-checks/` | External Godot harnesses for the earlier ruleset comparisons and the Pocket Meadow tactical replay; not needed for the batch runs. |

Coordinates are column letters from the left and one-based row numbers from the top. Red = Player One; Blue = Player Two. Both moves were picked from the same pre-turn board, before either choice was revealed. An `auto-draft-collision` is the sole remaining draft tile, resolved by the game without consuming a CPU decision seed; its score and the preceding submitted draft entry show the same post-resolution snapshot. A `retry` is an all-graves draft and is not a draw or a scored defeat.

## Benchmark design

All six maps: Pocket Meadow, Little Crossing, Seven Cairns, Twin Basins, Sixteen Standards, Banner Plain.

- `normal`: all five difficulty levels against the shipped Normal, with natural drafts (1,440 trials).
- `probe`: all five levels against the experimental one-turn opponent, with fixed drafts (240 trials).
- `duel`: Monster against shipped Very Hard, with natural drafts (288 trials).
- Each seed runs once with the tested CPU as Red and once as Blue. Seed indices are 1–24 for normal and duel, 1–4 for probe. Tested CPU seed = index × 104729 + 67. Reference seed = index × 10949 + 1109. Batch play uses one RNG stream per side per match. The manual games use a different, app-style stream: both launch with seed 42; each CPU decision gets a new Random initialized with the next value from that stream. The exact resulting seeds are in the manual JSONL.
- Fixed draft placements by map (Red/Blue): A1/C3, B1/D4, C5/G5, C4/H4, E3/E8, A1/L12, in the order above. Any other eligible starts are collided in the order returned by the game. The full draft is recorded in each trace.

The experimental opponent sees only the public board and legal targets. It evaluates up to 10 moves of its own. If the opponent has more than eight legal replies, it uses the top six from the frozen Normal ranking, plus the same-square collision if legal and not already in the list. For each move, it resolves each tested reply with the real rules, then combines 65% mean value and 35% worst-case value. The position value uses secured territory, knight and castle support, and legal-move counts, with a large terminal win/loss value. Exact weights, tie-breaking, candidate thresholds, and seeds are in the C# source. It does not know the CPU’s actual pending choice and does not search more than one simultaneous turn.

## Rerunning and comparing

Requires the .NET 10 SDK. From the extracted archive:

```sh
export CASTRUM_TRACE_JSONL="$PWD/new-benchmark-moves.jsonl"
dotnet run -c Release --project bench -- normal probe duel > new-results.csv
```

Use a fresh output filename: the harness appends to the JSONL. Match records may come out in a different order because they run in parallel. The archive is sorted by group, map, difficulty label, seed index, and tested side. The CSV columns are group, map, level, seed index, tested side, end state, Main turns, score margin from the tested CPU’s perspective, moves also legal for the rival, attempted captures, unsupported placements lost on non-collision turns, losses to the rival’s direct selection, same-target Main moves.

For the later comparison, keep the boards, side swaps, seeds, and opponents fixed. In particular, freeze **both** the experimental opponent code and the baseline Normal engine it uses to shortlist moves. Do not just swap out the referenced AI DLL for the benchmark as a whole: that would also change the control. Swap only the decision maker under test or bridge the new one to this frozen harness. If the game rules or map layouts also change, treat that as a separate comparison.

Use the recorded manual paths as fixed-position checks, not as scripts that a changed Monster must follow. Let the new CPU play its own move from the same position at the selected turn and record the resulting branch of play.

### Manual match checks

- Open Meadow, Monster as Blue: 11–9 Red win after 19 Main turns.
- Little Crossing, Monster as Blue: 5–6 Red loss after eight Main turns.
- Pocket Meadow tactical case: draft A1/C3, then A2/B2, B2/A2, B1/B2. On turn four, Monster as Red chose B2 while Blue chose B1, ending 1–5. Red choosing B1 against that same reply instead leaves 1–1 and Main Turn 5. This tests one reply, not whether B1 is optimal against all replies.

Run `bash manual/replay.sh` to feed those original hand-picked inputs to the frozen Monster and verify both full logs. For the UI checks, use an isolated game checkout and pass the script as an absolute path to Godot with `--headless --path <game-checkout> --script <archive-path>/ui-checks/<script>.gd`. `RunawaySmoke.gd` is the original Classic-only replay against milestone 8B (main commit `52dca2bc62e7e8836d981afb8e149706f6004bb4`).
