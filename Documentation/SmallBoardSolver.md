# Exhaustive small-board solver

The reported comparisons use the named historical opponents and settings. See the [8E compact reference study](CpuDifficultyCalibration.md) for later results. A certified nonnegative value establishes that a player can avoid losing within the modeled game. An opponent can still offer a win, and an outcome bound alone gives no territory-margin guarantee.

Milestone 8D.2.1 studies official Knight Support in the development benchmark executable. It uses the archived Pocket Meadow map and the A1 and C3 castle assignments.

## Solution scope and utility

- A **value result** establishes the equilibrium utility from an identified position. Symmetry alone can sometimes establish the value.
- A **complete solution from a position** supplies an optimal policy for each player from there. The exhaustive run additionally certifies every position reached by any legal action history, including play after deviations.
- A **draft solution** needs an explicit model of the restart outcome. The Main-phase tablebase starts with the successful draft.
- A **partial tablebase** contains certified subgraphs. An interrupted run can leave the requested opening and other positions unresolved. A finite resource budget is independent of match termination.

All values in matrices use the Red perspective. Outcome utility is +1 for a Red win, 0 for a draw, and −1 for a Blue win. Expected Red match points, using half a point per draw, are `(value + 1) / 2`. Margin utility is Red secured territory minus Blue secured territory at termination. It is optimized separately. Win, draw and loss probabilities and expected turns are properties of a specified pair of policies.

## Mathematical method

For each resolved position, the solver obtains both legal selection sets from [MainPhaseState](xref:CastrumTactics.Domain.MainPhaseState), calls the authoritative resolver on their Cartesian product, and uses the child values as matrix entries. Both choices come from the same pre-turn position. The domain supplies draft previews, post-action Knight Support, territory and terminal scores.

For payoff matrix M, Red maximizes the minimum column expectation over probability vectors p. Blue minimizes the maximum row expectation over probability vectors q. A pure saddle is certified by equal best pure security levels. Otherwise a positive shift gives A = M + c. The solver maximizes the sum of the entries of y subject to Ay ≤ 1 and y ≥ 0. Normalizing the primal and dual vectors gives the Blue and Red policies. The shifted value is the reciprocal of the LP objective. The simplex tableau uses reduced `BigInteger` fractions and Bland's pivot rule, including for degenerate games.

The exact path checks nonnegative, unit-sum policies and exact equality of the minimum column expectation, matrix value, and maximum row expectation. This gives zero primal–dual gap and zero unilateral exploitability. Terminal utilities are exact integers from domain scores. Induction over the graph certifies the whole solved scope. JSON emits exact fractions as strings. CPU experiments use floating-point summaries and diagnostics. A pure action's security level is its worst column or row in the appropriate player's utility. Dominance counts use weak dominance by another pure action, with strict advantage against at least one reply. In a forced loss a weakly dominated action can still attain the value.

### Certified intervals

An interrupted exact run encountered fractions exceeding 30,000 printed digits across numerator and denominator in both objectives. With `--interval-scale D`, the solver solves the entrywise lower and upper matrices separately in exact arithmetic. Red uses the lower game's maximizer and Blue uses the upper game's minimizer. Their guarantees bracket the true game by monotonicity. Small exact ratios are retained. Other endpoints are rounded outward to the 1/D grid for propagation. The lower endpoint is rounded down and the upper endpoint up. These remain certificates over the authoritative game, including earlier turns. By induction, a player who uses its certified policy at each state can secure its root endpoint against arbitrary play. The interval width bounds unilateral exploitability.

A matrix-game value is 1-Lipschitz in the maximum entrywise change, so the gap grows by at most 2/D per decision along a path. A singleton certifies exact optimality. A wider interval certifies approximate optimality, and the midpoint shown in JSON is an estimate. The exact-mode command uses `--interval-scale 0`. The default Pocket Meadow run uses `--fast-policy true` to generate candidate probabilities in binary64, then converts them to exact discrete probabilities. Certification evaluates every adverse reply with rational arithmetic. A candidate is accepted only if its certified gap is within the widest child interval plus 1/D. Other positions fall back to the exact endpoint solvers. This limits propagation to an increase of at most 3/D per turn, or 5.1 × 10⁻⁷ across 17 turns with D = 10⁸. The interval tablebase records the measured bounds, and symmetric positions keep the bounds invariant under player exchange. Outcome and margin can have different precision at the same position. The interval tablebase carries the endpoints and continues to use exact fractions for policy sampling.

### Termination

For each playable square with ground health h, use rank h+2 when empty, h+1 with a knight, and zero with a castle or grave. Inaccessible squares also contribute zero. The rank is a nonnegative integer and the sum strictly decreases on every legal Main pair.

| Authoritative transition | Rank change |
| --- | --- |
| Placement | Empty to knight at the same health lowers rank by one. |
| Upgrade | A knight becomes a permanent castle at zero rank. |
| Capture | An ownership change lowers health or converts the captured knight to a grave. |
| Collision | A playable empty or knight square becomes a grave, including collisions while health remains positive. |
| Support elimination | An unsupported knight becomes a grave and decreases rank further. |

The distinct actions use the same pre-turn state and reduce rank independently. The post-action rules can only decrease rank further. Pocket Meadow starts at rank 35. Reachable knights have health at least one, so a collision costs at least two rank and a distinct pair costs at least one per action. The resulting upper bound is 17 Main turns. The physical census finds histories attaining it. Repeated positions are transpositions between different histories.

### Identity, memoization and symmetry

The versioned key is a dimension and ruleset header followed by row-major terrain, piece, owner and full ground health for every square. Equality compares the complete encoding. Draft submission history, Main turn count, round ID, and private pending coordinates play no part in legal selection, resolution or utility after a successful opening. Main versus Finished is derived from the resolved physical board. Draft is a distinct scope with separate authoritative eligibility and resolution.

The solver computes rotations and reflections with or without player exchange that preserve the exact root board. Chebyshev neighborhoods, region traversal, both players' rule parameters and cellwise resolution commute with these transformations. The paired-action and terminal result mappings are bijective. Player exchange swaps the policies and negates Red utility. Coordinate remapping is applied to policy retrieval. The cache uses the smallest ordinal transformed key within a root's symmetry group and stores one representative, the sign on each edge, and its canonical orientation. It reconstructs boards for diagnostics from compact keys and shares identical uniform equilibrium policies. Use `--symmetry false` for an unreduced run.

`--ignore-locked-health true` additionally groups positions that differ only in castle and grave health. Those cells are permanently unselectable and never change type or owner. Territory and support use their type and owner, with no dependence on their health. Thus every legal pair in one representative has the same terminal utility or leads to an equivalent child. This gives a bisimulation, so exact values and policies apply to all physical positions represented by that game. The representative retains its full board for inspection. Position and edge counts from this mode describe the quotient; action-history counts and maximum depth still describe the original game. Use `--enumerate-only true` with full health identity to measure physical-state statistics separately.

A symmetry orbit of size k contributes k physical positions. Each representative's outgoing action pairs are enumerated. Multiplying its outgoing count by k gives physical action-pair edges. Distinct physical destinations are counted before canonicalization. Depth-layer propagation aggregates exact history counts over each orbit because each member has the same count of outgoing pairs to any destination orbit. Distinct positions may be reachable at more than one depth. The automated tests compare every strategy and all resulting state statistics of a tractable position against the unreduced graph.

### Pocket Meadow draft

Authoritative preview classifies A1/A1 and C3/C3 as `OpeningRestartRequired`, with no final score. Off-diagonal pairs reach Main. A half-turn maps one successful assignment onto the other without exchanging colors. A half-turn with color exchange preserves one successful root and reverses the sign of both utilities, so the successful opening value is exactly zero for both objectives regardless of computational limits.

Under free unlimited retries and independent fair coin choices on every attempt, either player can guarantee a half chance of a successful draft per attempt, even against a history-dependent opponent. Completion then occurs almost surely with a mean of two attempts, and the expected utility is zero. If players coordinate distinct squares the draft can finish in one attempt. If both repeatedly choose the same square it can continue indefinitely. Scoring abandonment or assigning retry costs requires explicit assumptions. The tablebase opponent uses fixed starts.

## Commands and outputs

Build the development executable from the repository root. Put large artifacts in ignored `exports/` or the external project-files directory.

```sh
dotnet build Benchmarks -c Release
mkdir -p exports/solver
dotnet Benchmarks/bin/Release/net10.0/CastrumTactics.Benchmarks.dll solver \
  --ignore-locked-health true --interval-scale 100000000 --fast-policy true \
  --max-states 3200000 --max-seconds 450 --max-mib 3000 \
  --fixture Benchmarks/Reference/pocket-meadow.json --analysis 90 \
  --output exports/solver/pocket.json --tablebase exports/solver/pocket-meadow.ctb
dotnet Benchmarks/bin/Release/net10.0/CastrumTactics.Benchmarks.dll solver-query \
  exports/solver/pocket-meadow.ctb --path A2/B2,B2/A2,B1/B2
dotnet Benchmarks/bin/Release/net10.0/CastrumTactics.Benchmarks.dll tournament run \
  --config Benchmarks/Examples/pocket-solver-comparison.json
```

- `solver` defaults to archived Pocket Meadow and A1/C3. For the Pocket Meadow opening it defaults to locked-health equivalence and certified interval continuations. Use `--map`, `--starts A1/C3`, and `--path Red/Blue,Red/Blue` for an explicit successful opening and position. A selected position is solved through all its descendants.
- `--fixture` traces the archived tactical example. `--replay` accepts a legacy JSONL match file or a tournament output directory. Tournament inspection reconstructs published snapshots and reruns opponent diagnostics with seed 42. Realized move records are separate from these diagnostic samples and their distributions.
- Add `--opponent strategic:Monster --side red --seed 42` to `solver-query` to compare an individual CPU decision and its complete reported policy against both objectives. Use `--opponent study` to run all documented variants on one position.
- `--analysis N` samples approximately N representative positions in rank thirds and tests both sides, plus the root. It reports the current evaluator's terms, separate solved utilities, action selection gaps, and restricted-reply diagnostics.
- `--analysis-split tuning`, `held-out` or `both` partitions locked-health states across full geometry and player exchange before sampling. The split excludes the root, which is reported separately. The study reports interval bounds for each mean policy gap, and the root summary separately reports forced draw and win-or-draw guarantees. [8D.3 analysis and results](CpuStrategicEvaluation.md)
- `--output` writes JSON. Each position lists coordinates, board and territory, both payoff matrices, policies, pure guarantees, dominance flags, and exact value bounds. `solver-query` reconstructs the queried position through a legal session history and rechecks its matrix certificate using stored child values.
- Single-worker DFS supports configurable limits on visited representatives, solve time and managed memory, and cooperatively responds to Ctrl+C. Memory is sampled, so the threshold is a soft cap. A limit returns exit code 3 and reports discovered and certified counts. With a tablebase path supplied, a resource limit saves completed subgraphs when available. Restarting an exhaustive run recomputes the graph. Report generation and serialization have separate costs.

The compressed `.ctb` stores solver and state versions, battlefield fingerprint, successful draft and optional position path, Domain assembly SHA-256, rules, both utilities, scope, precision, values, and exact policy fractions. An integrity checksum covers the metadata and rows. Use the same compatible Domain build for solving, querying and tournaments. The format is experimental and intended for trusted local data. The CLI rejects wrong fingerprints, incompatible versions, invalid policy vectors and missing positions. Rebuilding verifies each matrix's two exact bounds.

A tournament participant uses `"opponent": "solver:pocket-meadow"` and a `tablebase` path whose filename stem matches the identifier. The manifest pins the content checksum. The opponent uses the same public snapshot and caller-owned seeded stream as the other participants. Exact integer rejection sampling draws from the specified fractions without accessing either pending choice. Unsolved positions cause explicit match failures.

## Research results and implications for 8D.3

### Pocket Meadow opening solution

The successful opening has an **exact outcome value of 0**, or 0.5 expected match points for each player. Its **exact margin value is also 0**. In either objective the unique optimal initial action for each side is **B2 with probability 1**. Both castle assignments give the same result.

| Red / Blue | B2 | C2 | B3 |
| --- | ---: | ---: | ---: |
| B1 | −x | 0 | 0 |
| A2 | −x | 0 | 0 |
| B2 | 0 | +x | +x |

The outcome magnitude x is certified near 0.69491525 and the margin magnitude is near 1.49430321. The JSON reports give rational lower and upper bounds. Positivity of x suffices to certify the saddle and its uniqueness. Against B2, any nonzero probability on either outside square gives away value, so no mixed first action is also optimal. The opening's exact guarantee extends through the supplied continuation policies for both sides. The full tablebase is exact at some positions and interval-certified at the others, so it is an exact opening solution and interval-certified full reachable-state policy coverage. The data is in `Benchmarks/Reference/pocket-meadow-solver-results.json` and the generated tablebase.

The reported policy pair plays B2/B2 and draws with probability one under either objective. For the selected pair, win/draw/loss probabilities are exactly 0/1/0 and the expected Main length is 507829/93312 turns (about 5.44). The reported pair lifts each representative policy along the edges traversed by play. An independent Boolean pass over all legal replies establishes that neither player can force a drawn terminal board on its own. Neither player can force a win since the outcome value is zero. These statements are compatible because the opponent has choices outside the equilibrium policy. Later play can require mixing, even though the opening is pure.

The policy solve visited 641,567 representatives of 2,562,886 positions after grouping inert health and accounting for geometry and player identity. The policy solve made 11,879,117 pair resolutions and took 81.6 s. Peak RSS including reports and the tablebase was about 1.02 GiB. Certified gaps across all positions are at most 7 × 10⁻⁸ for outcome and 8 × 10⁻⁸ for margin. Among 2,550,362 nonterminal positions in this quotient, 1,345,586 require mixing for at least one side under outcome and 1,699,224 under margin. These counts use strict interval separation, so they certify a lower bound on the frequency of required mixing, about 52.8% and 66.6% respectively. The frequencies describe strategically distinct positions under the health projection.

### Pocket Meadow physical census

The table counts physical states from one successful castle assignment. The opposite assignment has an isomorphic graph. A transition counts one legal action pair from a physical state. Different action pairs can reach the same destination.

| Measure | Exact count |
| --- | ---: |
| Unique resolved boards | 47,344,695 |
| Nonterminal boards | 43,935,744 |
| Terminal boards | 3,408,951 |
| Red wins / draws / Blue wins among terminal boards | 1,224,022 / 960,907 / 1,224,022 |
| Symmetry representatives | 11,842,458 |
| Action-pair transitions | 361,860,294 |
| Distinct source–destination edges | 359,811,294 |
| Repeated-destination pair transitions beyond first discovery | 314,515,600 |
| Complete action-pair histories | 123,495,304,538,020 |
| All legal history prefixes including length zero | 222,343,005,838,434 |
| Mean / maximum pair branching per nonterminal board | 8.2361 / 49 |
| Longest Main phase | 17 turns |

The peak number of distinct positions at one depth is 21,283,894 after nine turns. Twenty-two terminal boards occur at depth 17. The histogram and the joint legal-action-count distribution are in the repository at `Benchmarks/Reference/pocket-meadow-state-counts.json`. Cross-depth duplicates remain possible, so the depth counts should be read separately from the global total. The measured history totals include all legal action choices, regardless of policy probability.

The physical pass stores each board with seven bits per square and identifies the fixed dimensions and rules from the run. Its state keys preserve every cell field. It resolves 90,506,859 pairs across representatives. The measured traversal took 89.8 s with about 1.92 GiB peak RSS for the process. The packed counter agrees with the unreduced solver on a tractable board in automated tests.

```sh
dotnet Benchmarks/bin/Release/net10.0/CastrumTactics.Benchmarks.dll solver   --enumerate-only true --max-states 45000000 --max-mib 5700 --max-seconds 540   --output exports/solver/pocket-counts.json
```

### Archived B2/B1 fixture

After A1/C3 and the three pairs A2/B2, B2/A2, B1/B2, the secured scores are 1–1. Blue has a forced win. The exact Red outcome value is −1 and the separately optimized margin value is −8/3. Blue can force the win with B1, C1 or C2. The historical B2/B1 pair ends 1–5. The recorded alternative B1/B1 continues at 1–1 and is still a forced loss. That distinction makes this a useful regression for separating immediate tactical defense from a change in match prospects.

The table gives the complete outcome matrix at this fixture. Rows are Red choices.

| Red / Blue | B1 | C1 | B2 | C2 | A3 | B3 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| B1 | −1 | −1 | −1 | −1 | 0 | −79/142 |
| C1 | −1 | −1 | −1 | −1 | −4/5 | −1 |
| B2 | −1 | −1 | 0 | −1 | 1 | 0 |
| C2 | −1 | −1 | −1 | −1 | −177/289 | −7/9 |

Against optimal outcome play, all Red first actions guarantee −1. Against less accurate responses they differ sharply. Margin play selects C2 for Red and B1 for Blue, each with probability one in the certified policy. The historical and alternate moves in this fixture are stored in the repository. Full original per-turn 8C and 8D.2 tournament exports are external.

### A mixed-strategy and evaluator counterexample

Extend the archived fixture prefix with C1/A3 and C1/B1. Red's heuristic score is +0.8644 while the solved Red outcome is −3/4 and its margin is −9/11. Both sides need to randomize on B1 and B2 for outcome, at 3/4 and 1/4 respectively. Red's best deterministic guarantee is −1, a gap of 1/4. Blue's is +2/3, a gap of 1/12. Under the outcome equilibrium pair Red loses with probability 3/4 and draws with probability 1/4. Under margin play, Red instead uses probabilities 6/11 and 5/11, and Blue uses 9/11 and 2/11.

The active 2 × 2 submatrix for outcome is shown below. The solver certifies these policies against the full 3 × 5 matrix, including omitted rows and columns.

| Red / Blue | B1 | B2 |
| --- | ---: | ---: |
| B1 | −2/3 | −1 |
| B2 | −1 | 0 |

### Why Monster loses to Probe

The supplied repository contains the archived fixture and aggregate 8C and 8D.2 results. The original per-turn tournament exports are external and were unavailable in this workspace. A fresh four-match seed-42 reconstruction with both seat and start swaps reproduced four production Monster losses, each 1–4 from Monster's perspective, and the documented B3 collision finish. The run has new match IDs. The archived B2/B1 fixture was replayed from its saved moves.

In every reconstruction, the first consequential decision is **Main turn 1** in a neutral position. Monster gave half its probability to each outside square and zero to B2. Probe chose B2 with certainty. From Monster's perspective the realized child value was about −0.694915, or about 0.153 expected points with equilibrium continuation. Monster's two-turn search had all three replies, including B2, in the common matrix. For the outside choices it estimated expected/adverse values of +0.429/+0.174, compared with −0.390/−0.571 for B2. The solver certifies the reverse ranking. Candidate or root-reply omission cannot account for this opening error. The search's horizon, heuristic and response aggregation together rank the moves incorrectly. The zero probability on B2 makes the policy exploitable even though it randomizes between the two equivalent outside squares.

| Turn | Monster / Probe | Monster outcome value before → after | Observation |
| --- | --- | --- | --- |
| 1 | C2 / B2 | 0 → ≈−0.694915 | Monster uses an outside square and Probe takes the unique optimal action. |
| 2 | B2 / C2 | ≈−0.694915 → −1 | Monster chooses a pure recapture where it needs a mix to retain the better guarantee. Probe's counter reaches a forced win against that choice. |
| 3 | B2 / B2 | −1 → −1 | Center collision. |
| 4 | B3 / A2 | −1 → −1 | Monster had one legal choice. |
| 5 | A3 / B3 | −1 → −1 | The match still displays 1–1. |
| 6 | B3 / B3 | −1 → −1 | Collision finishes 1–4. |

A direct query of the reversed opening found that the one-turn Monster configuration selects B2 with certainty, while the two-, three- and four-turn variants still assign B2 zero probability. This isolates a change introduced by looking deeper with the existing search and evaluator. The full-matrix experiments below test a different aggregation policy separately.

Probe's turn-two pure reply guarantees it about +0.64 from that state, below the ≈+0.694915 optimal guarantee. It exploits Monster's actual response. The archived B2/B1 fixture shows the same early pattern with A2/B2 followed by B2/A2. Its turn-four collision alternative changes the immediate score, after both early mistakes have already occurred.

### CPU and search comparison

The main direct study uses 91 representatives selected in rank thirds and checks both seats, for 182 decisions per CPU. A guarantee replaces the tested side's first policy only and assumes solved continuation. Policy probabilities come from engine diagnostics or the archived Probe's exact uniform tie set. The chosen-move frequency uses one seed-42 draw per seat. Reported gaps use certified midpoints and binary64 arithmetic. These are comparative measurements on the sampled positions, with policy guarantees independent of the opponent sampled in a match.

| CPU | Mean outcome gap | Mean margin gap | Sampled pure-optimal choices | Sampled weakly dominated choices |
| --- | ---: | ---: | ---: | ---: |
| Frozen static Monster | 0.218 | 0.313 | 66 | 24 |
| Frozen strategic 8C Monster | 0.187 | 0.261 | 70 | 26 |
| Production 8D.2 Monster | 0.187 | 0.259 | 70 | 26 |
| One-turn Monster | 0.196 | 0.264 | 71 | 26 |
| Two-turn Monster, narrowed replies | 0.241 | 0.327 | 66 | 35 |
| Wider depth two, 2,000 pairs | 0.180 | 0.250 | 72 | 26 |
| Narrower depth three, 2,000 pairs | 0.181 | 0.258 | 72 | 20 |
| Depth three, 4,000 pairs | 0.175 | 0.251 | 73 | 19 |
| Depth four, 8,000 pairs | 0.180 | 0.256 | 74 | 17 |
| Dot's Probe | 0.150 | 0.213 | 78 | 19 |

The all-difficulty comparison is included in JSON. Production's mean outcome gaps for Easy through Monster were 0.173, 0.155, 0.162, 0.170 and 0.187. Greater difficulty on this small, stratified sample did not correspond to a smaller theoretical gap. Probe also had a gap in mixed-required positions. The narrowed Monster common reply set omitted an adverse outcome reply in 80 of 182 decisions, compared with zero for production Monster. Wider and deeper configurations moved the mean by hundredths, with no monotone gain from depth. The gated depth-three run matched the ungated depth-three run on this sample. Production Monster's mean depth used was about 2.05 against a completed mean of 2.42.

A controlled comparison on 24 side–positions evaluated all root and continuation replies with the same transformed strategic score at the cutoff, and authoritative win, draw and loss values at terminals. It used the outcome objective. The full-matrix policy gaps at depths one to four were 0.056, 0.111, 0.015 and 0.002. Selecting only the best pure worst case from the same search matrices gave 0.131, 0.177, 0.119 and 0.119. The horizon-two regression and persistent deterministic gap isolate two distinct effects of the evaluation horizon and the final policy. This comparison changes the decision method and uses a much smaller sample than the production profile study.

A separate tablebase-reference tournament completed 112 games, four seeds per opponent with both swap factors. Raw win–draw–loss counts from the reference's perspective follow. The scheduled outcomes include repeated histories.

| Opponent | Reference W–D–L |
| --- | ---: |
| Static Monster | 14–0–2 |
| Frozen 8C Monster | 14–0–2 |
| Production 8D.2 Monster | 14–0–2 |
| Wider depth two | 14–0–2 |
| Depth three, 4,000 pairs | 14–0–2 |
| Depth four, 8,000 pairs | 14–2–0 |
| Probe | 2–14–0 |

The reference's guarantee is in expectation. Its sampled losses illustrate why a zero value cannot establish a guaranteed terminal draw or a guarantee against losing individual matches. The tournament used the same published snapshot and seeded stream contract as conventional opponents.

### Evaluator implications and 8D.3

In the 91-position sample, the production strategic score's Pearson correlation was 0.817 for outcome and 0.866 for margin. Material was the strongest single measured term at 0.842 and 0.891. Secured territory was 0.608 and 0.654, prospective influence 0.453 and 0.470, formation 0.313 and 0.325, ground −0.295 and −0.295, and mobility 0.054 and 0.051. Ten boards had opposite signs between the score and the signed outcome estimate, seven in the early group. The outcome-scaled score's mean absolute error was 0.372. Monster's adverse-then-expected ranking agreed with the solved pure guarantee ordering on about 72% of comparable pairs. These associations do not identify causal weights. Material combines castles and capture exposure, and the features are correlated. The study does not isolate a standalone effect for Castle placement or redundant interior expansion.

For 8D.3, start with held-out evaluation fixtures for the turn-one center decision, the turn-two mixed defense and the exact collision and sign-reversal positions. Test frontier and support changes while keeping continuation and reply settings fixed. Measure full-response security levels before adding depth, and evaluate a small mixed policy alongside the current selection once a position demonstrably requires mixing. Track outcome and margin separately and repeat any successful evaluation change on larger board layouts.

### Scaling

Linux x86-64, .NET 10.0.15, single worker. Times measure the solve pass and peak RSS includes report generation. The larger maps used a 300,000-state cap.

| Root | Physical positions | Cached representatives | Resolved pairs | Solve time | Peak RSS |
| --- | ---: | ---: | ---: | ---: | ---: |
| Corner Watch, 3 × 3 | 178,255 | 44,973 | 180,534 | 2.7 s | 91 MiB |
| Corner Watch, unreduced | 178,255 | 178,255 | 715,325 | 7.8 s | 178 MiB |
| Stone Center, 3 × 3 | 1,084,878 | 272,039 | 883,980 | 6.9 s | 223 MiB |
| Narrow Yard, 3 × 4 | Unresolved | 300,000 visited | 743,737 | 5.8 s | 182 MiB |
| Little Crossing, 4 × 4 | Unresolved | 300,000 visited | 1,152,688 | 6.9 s | 202 MiB |

All raw Corner Watch statistics agreed across the two enumeration modes, including depth counts, legal-action counts and 77,037,492 complete action histories. Stone Center has 294,785,558 complete histories and a maximum of 15 Main turns. The saved Little Crossing position after Main turn 7 solved exactly in five states. Its Red outcome and margin are both −1, with one Main turn remaining. The early Pocket Meadow run retaining all cell health reached 3,403,760 representatives, with 3,403,754 certified and 18,868,784 pair resolutions. Its eight-minute solve budget expired before the root. Peak RSS including partial export was about 1.92 GiB.

### Verification

The solution build passed with warnings treated as errors. All 331 tests passed. The tests cover exact matrix certificates, an independent two-row intersection solver, a forced collision via the public session, the archived fixture's continuation, complete identity, transpositions, the full/reduced correspondence, seeded sampling, resource limits and tablebase corruption. Both Godot 4.7.2 .NET runtime regressions passed. DocFX 2.81.0 generated API metadata from the verified build assemblies and built the site with warnings treated as errors. The cloud environment disallowed the Unix pipe used by DocFX's project-based Roslyn build host.

The [8D.4 draft research](CpuOpeningStrategy.md#mathematical-draft-examples) uses the certified Main values to solve small drafts under an explicit retry model, including Three Banners.

The [8E mathematical reruns](CpuDifficultyCalibration.md#mathematical-regressions) record fresh certificates and policy gaps for the three compact roots and reassess Three Banners.
