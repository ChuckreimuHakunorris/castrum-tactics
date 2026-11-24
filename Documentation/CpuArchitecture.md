# Configurable CPU decision engine

Milestone 8A provides a synchronous, engine-independent decision source for local play and future simulations. `CastrumTactics.AI` references `CastrumTactics.Domain`. The separate assembly makes the public domain surface the CPU's dependency boundary. The CPU accepts a snapshot and returns a proposal. The caller owns the random source and submits the proposal through the session. The session checks the phase, round, player, and legality again when it receives the command.

## Calling the engine

```csharp
var engine = new CpuDecisionEngine();
var profile = CpuDifficultyProfiles.Get(CpuDifficulty.Hard);
var random = new Random(42);
CpuDecision? decision = engine.Decide(snapshot, PlayerId.PlayerOne, profile, random, cancellationToken);
```

A proposal contains the player, position, phase, and round ID from its input snapshot. Terminal phases and empty legal sets return null. Undefined players, null inputs, and invalid profile values throw. Cancellation throws `OperationCanceledException`. The engine checks the token before and during board analysis and candidate scoring, and again before returning. The engine has no mutable instance state and each invocation owns its temporary analysis. Concurrent calls can share the engine, immutable snapshots, and immutable profiles. Use an independent random source for each concurrent operation and an independent stream for each CPU player. Equivalent resolved inputs, profiles, and random sequences reproduce decisions with the same engine and .NET runtime.

The opening reads `EligiblePositions`. The main phase reads both published legal sets and the published territory analysis. The CPU uses only resolved board data and ignores submission flags. An opponent who has already submitted provides the same evaluation inputs as an opponent who has yet to submit.

## Evaluation features

`CpuFeatures` describes evaluation values normalized to [0, 1]. `CpuEvaluationWeights` describes strategic preferences in [0, 10] and validates them during profile construction. The engine adds the weighted benefits and subtracts weighted risk. Score differences are measured on that weighted scale.

| Feature | Opening draft | Main phase |
| --- | --- | --- |
| Expansion | Adjacent empty playable cells divided by eight. Rocks and graves reduce the count. | Contested empty neighbors beyond the current friendly legal set, divided by eight. Applies only when an ordinary selection would leave a friendly knight on the candidate square. |
| Territory | The candidate's eight-connected region as a share of playable cells. Regions with more enemy than friendly sources receive full weight. Other regions use 80% of that share. | Estimated influence over contested empty neighbors. Each contributes less when existing friendly or opposing pieces already cover it. Exact secured territory comes from the snapshot and does not contribute candidate-specific points. |
| Capture | Unused. | Removing an enemy knight is worth 0.5. Keeping a knight adds 0.25 plus 0.125 per health above two, capped at a total of 1. At health one or lower an ordinary capture leaves a grave and contributes zero expansion and influence. |
| Fortification | Unused. | An upgrade is worth 0.8 when the opponent can also select that knight and 0.25 otherwise. The upgrade preserves existing selection range. A collision during the upgrade destroys the friendly knight and leaves a grave. |
| Mobility | Unused. | Estimates enemy options lost when the captured knight was their only adjacent source on a contested legal tile. Own-knight upgrade options still available to them are excluded. |
| Bottleneck | Counts separated groups in the traversable eight-neighbor ring around the candidate. Each extra group contributes 0.5, up to 1. | Gives the local gate estimate full weight for a castle upgrade or a capture that would leave a grave, and 35% otherwise. Traversal excludes castles, graves, and rocks. The ring can miss longer routes around the square. |
| Spread | The region's share of playable space divided by one plus its friendly piece count and nearby friendly support. | Unused. |
| Risk | Estimates opponent appeal from their expansion, territory, and spread at each public start. Scales by remaining starts and the cost of missing a first knight. A collision at a gate adds cost. Once knights exist, denying a strong start can offset part of that cost. | Shared targets carry a collision cost. The cost is lower when a collision still removes the desired opposing knight, especially on health-one ground. A potential new knight also carries exposure proportional to the nearby enemy legal options. |

Opening regions are measured once on the draft board. Remaining starts are still empty and the estimates carry no persistent territorial ownership. The opening collision cost starts at 1.0 with no knights and at most three remaining starts, 0.7 earlier in an empty draft, or 0.4 once knights are established. A gate adds up to 0.2, and denial reduces the cost by up to 0.2 once established. Relative opponent appeal and the square root of half the remaining start count scale the resulting cost.

In Main, a shared placement or upgrade starts with collision cost 0.65. Shared captures use 0.25 when a knight would survive the ordinary capture and 0.05 otherwise. If a new knight may occupy the square, exposure adds up to 0.35 based on the fraction of adjacent squares available to the opponent.

The eight-neighbor divisor and the region share normalize counts. The opening urgency, appeal model, denial credit, and gate penalty and the main-phase action values and costs are provisional strategic assumptions shared by the profiles. Feature weights control their relative importance. Neighbor support counts knights and castles. The published territory and legal sets determine which targets can be scored. These static scores estimate tactical value on one resolved board. Exact hypothetical territory and changes to board-wide connectivity are deferred to 8C.

## Difficulty profiles and sampling

| Difficulty | Opening pool | Main pool | Opening temp. | Main temp. | Expansion | Territory | Capture | Fortification | Mobility | Bottleneck | Spread | Risk |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Easy | 16 | 12 | 0.90 | 1.00 | 2.2 | 0.7 | 1.5 | 0.8 | 0.3 | 0.2 | 0.5 | 0.35 |
| Normal | 12 | 8 | 0.75 | 0.60 | 2.2 | 1.2 | 1.6 | 1.1 | 0.6 | 0.5 | 0.8 | 0.65 |
| Hard | 10 | 5 | 0.65 | 0.30 | 2.0 | 1.6 | 1.8 | 1.4 | 0.9 | 0.8 | 1.1 | 0.95 |
| Very Hard | 8 | 3 | 0.55 | 0.12 | 2.0 | 1.9 | 1.9 | 1.6 | 1.1 | 1.1 | 1.3 | 1.15 |
| Monster | 16 | 1 | 0.50 | 0 | 2.0 | 2.1 | 2.0 | 1.7 | 1.2 | 1.2 | 1.4 | 1.25 |

All five profiles use 0.20 ordinary and 0.70 urgent opening diversity, with both maximum score differences disabled. The provisional tuning narrows the Main pool and emphasizes coverage, connectivity, and collision risk as difficulty rises. Monster's opening limit covers the largest start list in the current catalog. Empirical balance remains to be measured.

Custom profiles take positive limits, finite temperatures capped at 10, and finite diversity shares from 0 through 1. Opening temperature must be positive. Either phase can specify a finite, nonnegative maximum score difference from its best candidate. Pass `null` to disable a cutoff. For example, this profile filters Main candidates to within 1.5 weighted score units of the best before accounting for cutoff ties.

```csharp
var custom = new CpuProfile(
    new CpuEvaluationWeights(2.2, 1.2, 1.6, 1.1, 0.6, 0.5, 0.8, 0.65),
    openingCandidateLimit: 12, mainCandidateLimit: 8,
    mainTemperature: 0.60, openingTemperature: 0.75,
    openingDiversity: 0.20, urgentOpeningDiversity: 0.70,
    mainMaximumScoreDifference: 1.5);
```

The static evaluator scores every legal candidate. The limits govern the sampling pools. The engine orders candidates by descending score, using row-major coordinates for a reproducible order at equal scores. It applies the nominal pool limit and optional score gap, retaining at least two Opening choices when available and at least one Main choice. It then includes every candidate within an absolute `1e-12` score difference of the last retained score. This numerical margin covers floating-point roundoff on the weighted scale and is anchored to that score. The configurable score gap expresses a separate strategic preference about how far below the best score the pool may reach, subject to the opening minimum and numerical cutoff ties.

Positive temperatures use softmax after subtracting the best score. In Main, positive temperature can deliberately select lower-rated moves from the retained pool. At zero temperature, the engine samples uniformly among scores within `1e-12` of the best. A unique best receives probability one, and inferior scores receive zero. Randomizing among equivalent best choices preserves Monster's preference for the strongest rated move.

Opening mixes uniform probability into the retained pool. The urgent setting applies when two starts remain and neither player has a knight. With the built-in urgent share, either start receives at least 35% probability. A single retained candidate receives probability one. Excluded candidates receive zero.

```text
finalProbability = (1 - diversity) * softmaxProbability + diversity / retainedCandidateCount
```

Each CPU samples independently. On the initial Banner Plain board all four corners score equally. Monster previously assigned 50%, 50%, 0%, and 0% in row-major order. It now assigns 25% to each. For two identically configured independent CPUs, the theoretical first-round collision probability on this board changes from 50% to 25%. Collisions and unsuccessful drafts remain possible.

## Diagnostics and cost

`DecideWithDiagnostics` returns the selected proposal plus all legal candidates in rank order. Each candidate includes its normalized features, weighted score, and sampling probability. The ordinary decision method does not create a report or a read-only diagnostic wrapper.

Board traversal takes time proportional to board area. Candidate evaluation checks a bounded eight-cell neighborhood, including the local ring connectivity check. Ranking costs O(C log C) for C legal candidates. Temporary space is proportional to board area and candidate count. Calls reuse the public territory and legality data and perform no hypothetical turn resolution.

With Monster, Player One, and seed 42, Pocket Meadow's two opening starts each have score 2.580 and probability 50%. The sample selects `(2, 2)`. On the Main regression fixture with two identical contested regions separated by rock, the two captures each score 1.5328125 and receive 50%. The two upgrades each score 0.5475 and receive zero.

### Preliminary timing

Measured on Windows with an Intel Core i5-7500 at 3.40 GHz, .NET 10.0, and a Release build. Each case warmed up for 128 calls and timed 2,000 ordinary Monster decisions against one fixed snapshot. Times include per-decision allocations and sampling. The main snapshots are early positions with four or five legal selections, so these results do not bound late turns or mobile performance.

| Map | Opening median / P95 | Main median / P95 |
| --- | ---: | ---: |
| Pocket Meadow, 3 × 3 | 9.1 / 13.7 µs | 11.9 / 15.6 µs |
| Demonstration, 7 × 5 | 30.9 / 35.7 µs | 18.7 / 59.2 µs |
| Twelve Towers, 12 × 12 | 97.2 / 194.8 µs | 11.9 / 32.6 µs |

## Milestone 8B integration lifecycle

1. The scene passes each public snapshot to its `CpuMatchCoordinator`. The coordinator starts one request per playable phase and round, skipping terminal snapshots and rounds the CPU has already submitted.
2. A `Task.Run` worker calls the synchronous engine with that immutable snapshot, a fresh random instance derived from the match's seed source, and a cancellation token. The worker has no Godot or session reference.
3. The engine returns a proposal tagged with the input player, phase, and round. The scene pumps completions on the Godot main thread, outside session callbacks.
4. The coordinator checks active session identity, cancellation, phase, round, and player, and verifies that the CPU still owes a submission. It sends an `OpeningSelection` or `MainPhaseCommand` on the same thread. The session validates phase, round, player, and legality.

`RoundId` is scoped to one session. The coordinator checks session identity even if two sessions publish the same round ID. Superseded, terminal, restarted, and abandoned rounds invalidate outstanding requests and request cancellation. Retired workers have their faults observed and their cancellation sources disposed after they exit. A human submission or revision within the same unresolved round leaves the CPU's inputs and proposal valid. Both players use the same resolved pre-round board.

If the worker faults, returns no valid proposal, or the command is rejected, the scene displays an error and lets the player start a New Match. The coordinator keeps its profile for the life of a match, including restarts. New Match applies the difficulty selected in the scene.

## Milestone 8C evaluation

Evaluate pairs of opposing choices through the authoritative simultaneous resolver. Reuse the static features for ranking and fallback. The current engine has fixed collision and exposure costs, a static opening appeal model, and local gate estimates that can miss longer paths. Board-wide effects and exact hypothetical territory belong in the later evaluation.
