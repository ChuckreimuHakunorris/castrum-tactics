# Configurable CPU decision engine

Milestone 8A provides a synchronous, engine-independent decision source for local play and future simulations. `CastrumTactics.AI` references `CastrumTactics.Domain`. The separate assembly makes the public domain surface the CPU's dependency boundary. The CPU accepts a snapshot and returns a proposal. The caller owns the random source and submits the proposal through the session. The session checks the phase, round, player, and legality again when it receives the command.

## Calling the engine

```csharp
var engine = new CpuDecisionEngine();
var profile = CpuDifficultyProfiles.Get(CpuDifficulty.Hard);
var random = new Random(42);
CpuDecision? decision = engine.Decide(snapshot, PlayerId.PlayerOne, profile, random, cancellationToken);
```

A proposal contains the player, position, phase, and round ID. Terminal phases and empty legal sets return null. Undefined players, null inputs, and invalid profile values throw. Cancellation throws `OperationCanceledException`. The engine checks the token before and during board analysis and candidate scoring, and again before returning. Each call builds its own temporary analysis. Use independent random sources for each player and for concurrent calls. Equivalent snapshots, profiles, and random sequences reproduce decisions with the same engine and .NET runtime.

The opening reads `EligiblePositions`. The main phase reads both published legal sets and the published territory analysis. The CPU uses only resolved board data and ignores submission flags. An opponent who has already submitted provides the same evaluation inputs as an opponent who has yet to submit.

## Evaluation features

Each feature is normalized to the interval [0, 1]. The engine multiplies each feature by its configured weight, adds the benefits, and subtracts weighted risk.

| Feature | Opening draft | Main phase |
| --- | --- | --- |
| Expansion | Adjacent empty playable cells divided by eight. Rocks and graves reduce the count. | Contested empty neighbors beyond the current friendly legal set, divided by eight. Applies only when an ordinary selection would leave a friendly knight on the candidate square. |
| Territory | The candidate's eight-connected region as a share of playable cells. Regions with more enemy than friendly sources receive full weight. Other regions use 80% of that share. | Estimated influence over contested empty neighbors. Each contributes less when existing friendly or opposing pieces already cover it. Exact secured territory comes from the snapshot and does not contribute candidate-specific points. |
| Capture | Unused. | Removing an enemy knight is worth 0.5. Keeping a knight adds 0.25 plus 0.125 per health above two, capped at a total of 1. At health one an ordinary capture would leave a grave and gains neither expansion nor influence. |
| Fortification | Unused. | An upgrade is worth 0.8 when the opponent can also select that knight and 0.25 otherwise. The upgrade adds no movement range. A shared target still carries collision risk. |
| Mobility | Unused. | Estimates enemy options lost when the captured knight was their only adjacent source on a contested legal tile. Own-knight upgrade options still available to them are excluded. |
| Bottleneck | Counts separated groups in the traversable eight-neighbor ring around the candidate. Each extra group contributes 0.5, up to 1. | Gives the local gate estimate full weight for a castle upgrade or a capture that would leave a grave, and 35% otherwise. Traversal excludes castles, graves, and rocks. The ring can miss longer routes around the square. |
| Spread | The region's share of playable space divided by one plus its friendly piece count and nearby friendly support. | Unused. |
| Risk | Estimates opponent appeal from their expansion, territory, and spread at each public start. Scales by remaining starts and the cost of missing a first knight. A collision at a gate adds cost. Once knights exist, denying a strong start can offset part of that cost. | Shared targets carry a collision cost. The cost is lower when a collision still removes the desired opposing knight, especially on health-one ground. A potential new knight also carries exposure proportional to the nearby enemy legal options. |

Opening regions are measured once on the draft board. Remaining starts are still empty and the estimates carry no persistent territorial ownership. The opening collision cost starts at 1.0 with no knights and at most three remaining starts, 0.7 earlier in an empty draft, or 0.4 once knights are established. A gate adds up to 0.2, and denial reduces the cost by up to 0.2 once established. Relative opponent appeal and the square root of half the remaining start count scale the resulting cost.

In Main, a shared placement or upgrade starts with collision cost 0.65. Shared captures use 0.25 when a knight would survive the ordinary capture and 0.05 otherwise. If a new knight may occupy the square, exposure adds up to 0.35 based on the fraction of adjacent squares available to the opponent.

The scores compare candidates on the same board. They estimate tactical value, and the authoritative domain determines the result of simultaneous submissions.

## Difficulty profiles and sampling

| Difficulty | Pool | Main temp. | Draft temp. | Expansion | Territory | Capture | Fortification | Mobility | Bottleneck | Spread | Risk |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Easy | 12 | 1.00 | 0.90 | 2.2 | 0.7 | 1.5 | 0.8 | 0.3 | 0.2 | 0.5 | 0.35 |
| Normal | 8 | 0.60 | 0.75 | 2.2 | 1.2 | 1.6 | 1.1 | 0.6 | 0.5 | 0.8 | 0.65 |
| Hard | 5 | 0.30 | 0.65 | 2.0 | 1.6 | 1.8 | 1.4 | 0.9 | 0.8 | 1.1 | 0.95 |
| Very Hard | 3 | 0.12 | 0.55 | 2.0 | 1.9 | 1.9 | 1.6 | 1.1 | 1.1 | 1.3 | 1.15 |
| Monster | 1 | 0 | 0.50 | 2.0 | 2.1 | 2.0 | 1.7 | 1.2 | 1.2 | 1.4 | 1.25 |

The built-in tuning narrows the pool and emphasizes coverage, connectivity, and collision risk as difficulty rises. These are provisional values. The engine has no empirical balance or expert-strength claim. Tuning tools can construct a profile from a weight record, a positive candidate limit, and finite temperatures. Weights and temperatures are capped at 10. Opening temperature must be positive.

The engine orders candidates by descending score, using row-major coordinates for ties. It samples the retained pool with softmax after subtracting the best score. Zero temperature selects the first best candidate; a single candidate has probability one. The opening pool retains at least two choices when available. Opening sampling mixes 20% uniform probability into the retained pool. With only two starts left and neither player holding a knight, the uniform share rises to 70%, putting at least 35% probability on either start. Collisions and unsuccessful drafts remain possible.

## Diagnostics and cost

`DecideWithDiagnostics` returns the selected proposal plus all legal candidates in rank order. Each candidate includes its normalized features, weighted score, and sampling probability. The ordinary decision method does not create a report or a read-only diagnostic wrapper.

Board traversal takes time proportional to board area. Candidate evaluation checks a bounded eight-cell neighborhood, including the local ring connectivity check. Ranking costs O(C log C) for C legal candidates. Temporary space is proportional to board area and candidate count. Calls reuse the public territory and legality data and perform no hypothetical turn resolution.

With Monster, Player One, and seed 42, Pocket Meadow's two opening starts each had score 2.580 and probability 50%. The sample selected `(2, 2)`. On the resolved 12 × 12 Twelve Towers fixture, Monster selected main-phase `(1, 1)` with score 2.312 and probability 100%. Its nonzero features were expansion 0.50 and territory 0.625.

### Preliminary timing

Measured on Windows with an Intel Core i5-7500 at 3.40 GHz, .NET 10.0, and a Release build. Each case warmed up for 128 calls and timed 2,000 ordinary Monster decisions against one fixed snapshot. Times include per-decision allocations and sampling. The main snapshots are early positions with four or five legal selections, so these results do not bound late turns or mobile performance.

| Map | Opening median / P95 | Main median / P95 |
| --- | ---: | ---: |
| Pocket Meadow, 3 × 3 | 9.1 / 13.7 µs | 11.9 / 15.6 µs |
| Demonstration, 7 × 5 | 30.9 / 35.7 µs | 18.7 / 59.2 µs |
| Twelve Towers, 12 × 12 | 97.2 / 194.8 µs | 11.9 / 32.6 µs |

## Next integration steps

- In 8B, run the synchronous engine off the Godot main thread. Pass the same immutable snapshot to either input source, use independent random streams, cancel the worker on scene exit or restart, and submit only through the session with the proposal's round ID.
- In 8C, evaluate pairs of opposing choices through the authoritative simultaneous resolver. Reuse the static features for ranking and fallback. Local gate estimates, fixed threat costs, and draft appeal are current limitations that can inform the later response model.
