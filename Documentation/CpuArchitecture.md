# Configurable CPU decision engine

The synchronous `CastrumTactics.AI` engine reads immutable public domain snapshots and returns round-tagged proposals. The caller owns the random source and submits the proposal through the session. Knight Support is the official ruleset and the target for strategic search and competitive testing. Deprecated Classic remains available in development configuration and uses the static evaluator.

## Calling the engine

```csharp
var engine = new CpuDecisionEngine();
var profile = CpuDifficultyProfiles.Get(CpuDifficulty.Hard);
var random = new Random(42);
CpuDecision? decision = engine.Decide(snapshot, PlayerId.PlayerOne, profile, random, cancellationToken);
```

A proposal contains the player, position, phase, and round ID from its input snapshot. Terminal phases and empty legal sets return null. Undefined players, null inputs, and invalid profile values throw. Cancellation throws `OperationCanceledException`. The engine checks the token during ranking, graph propagation and pair resolution, and before returning. Calls share immutable inputs and own their scratch state. Supply independent random streams for concurrent calls and for each player. With the default engine constructor, equivalent resolved inputs, profiles, and random sequences reproduce decisions on the same engine and .NET runtime.

The opening reads `EligiblePositions` and previews draft pairs with `GameSessionSnapshot.PreviewDraft`. Main uses the snapshot's immutable `MainState` and calls `MainPhaseState.Advance` for both choices together. Both paths share the live match resolver, including forced draft collisions, support elimination, territory, and scoring. The CPU ignores submission flags and receives no pending coordinates.

## Simultaneous search

The following sequence describes the expected/adverse policy used by Easy through Very Hard and by score-based fallbacks. Monster uses the same authoritative resolver and tactical shortlist to build the payoff matrices described below.

1. Rank all legal actions for each player on the current resolved board. At wide nodes, keep roughly half the shortlist from the top tactical ranks, then reserve room for a castle upgrade, a capture or defensive response, and frontier alternatives. Frontier ordering combines estimated marginal reach with distance from already shortlisted targets. A low-branching node is exhaustive within its limits.
2. Evaluate each own action against one common opponent shortlist. Reply probabilities combine 90% softmax over the opponent's public tactical scores with 10% uniform probability. In both the draft and Main, evaluate an omitted legal same-square reply once and include its value only in the adverse outcome. The expected value uses the same common reply distribution for every candidate. A collision already in the common set contributes to both values.
3. Resolve every tested pair through the domain. Rate an action with `(1 - risk) × expected + risk × adverse`, where adverse is the minimum tested value. In Main, actions that win against every tested reply take precedence, followed by actions with no immediate loss among tested replies. Sampling stays in the best available tier. Replies outside the shortlist can still change a result.
4. Hard, Very Hard, and Monster can revisit a small group of root finalists. At each nonterminal resolved child, they generate both players' choices afresh and analyze another simultaneous pair matrix. The child value blends 15% of the immediate nonterminal estimate with 85% of its best continuation. Terminal children retain their exact value. The deeper pass is used only when all retained finalists fit. Otherwise play uses the comparable completed first pass. If the first pass cannot complete a comparable set, it falls back to the static legal ranking.

| Difficulty | Main depth | Own / common reply limit | Pair budget | Risk | Reply temperature |
| --- | ---: | ---: | ---: | ---: | ---: |
| Easy | 1 | 5 / 4 | 64 | 0.15 | 1.8 |
| Normal | 1 | 8 / 6 | 120 | 0.24 | 1.4 |
| Hard | 2 | 12 / 8 | 360 | 0.32 | 1.2 |
| Very Hard | 2 | 14 / 9 | 500 | 0.36 | 1.1 |
| Monster | 2 normally, up to 3 for a provable win | 18 / 10 | 850 | 0.40 | 1.0 |

Depth two uses up to four finalists for Hard and Very Hard, and six for Monster. Each finalist receives the same root replies as at depth one. Child search starts at width three or four, contracts to fit the remaining budget and can widen on boards up to 25 cells. A pair budget counts all authoritative simulations in one decision, including extra collisions. The scene worker also has a 450 ms elapsed safeguard between resolutions. The public parameterless engine used for benchmarking relies on deterministic pair budgets.

Passes beyond depth two compare up to six configured finalists from the best completed tier. Each pass uses the same common root replies, ranks both players from each resolved child and charges every new pair to the same decision budget. A partial pass cannot replace the last completed comparison. [CpuSearchOptions](xref:CastrumTactics.AI.CpuSearchOptions) supports one through six turns and configurable continuation breadth. A legal same-square reply remains an adverse-only check when it is outside the common replies. An experimental check can test apparent immediate wins against omitted replies.

A deeper win is proven only when one choice wins against every legal reply and, at each resulting public state, some continuation has the same property. Terminal outcomes remain separate from heuristic estimates. Production Monster enters the additional pass only on a root with at most six shortlisted own actions and six legal replies. It compares up to three finalists with continuation width up to six per side. If the pass cannot prove a win, it uses the established two-turn tier. The wider and fully heuristic deeper settings remain available for experiments. [8D.2 search experiments](CpuSearchOptimization.md)

Draft search aims to compare all eligible starts. With `n` starts and `k` common replies, the complete pass costs `n × k + (n - k)` previews. The second term covers starts omitted from the common set. The search narrows the common list from at most two more replies than the Main limit until the pass fits, with a minimum of one common reply. If the remaining budget or elapsed time interrupts a row, only completed rows enter strategic sampling. When fewer than two rows finish on a multi-choice draft, the engine falls back to the static ranking to preserve opening diversity.

Each pair previews the next draft state or opening completion, including any forced final collision within the same preview. Its score combines the modeled outcome, scaled by the larger of four and the square root of board area, with 25% of the static score. A failed draft receives a -4 outcome before scaling. An opening that finishes immediately uses the signed value `3 + 0.6 × area` plus `0.3 × margin`. The existing softmax and uniform diversity keep multiple draft selections viable. The previews project the value of the remaining space.

## Strategic position value

Monster uses the resilient evaluator and bounded outcome utility in Main. Its root and selected continuation matrices share the existing 850-pair budget. A completed matrix supplies the action probabilities and includes a common column for each affordable legal collision. When a comparable second tier does not fit, the engine retains the completed first tier. Compact roots can still enter the three-turn proof-only pass. An immediate win is checked against omitted replies before the engine reports it as proved. [Models and measured results](CpuStrategicEvaluation.md)

The 8D.2 evaluator used for Easy through Very Hard and draft previews clamps its signed nonterminal sum to ±1,000. Its Main terminal win or loss uses ±10,000 plus the signed final margin clamped to ±1,000. A terminal draw is zero. Monster’s bounded Main utility uses the same internal scale of 10,000 with no margin term. [Strategic evaluation](CpuStrategicEvaluation.md)

| Term | Weight and calculation |
| --- | --- |
| Secured territory | 0.90 times the authoritative score lead in Main and 0.30 in draft previews. |
| Prospective influence | 0.62 times the sum of per-cell advantage. Each player propagates their strongest reachable source, attenuating one point per eight-connected step. A castle starts at strength 8 and a knight at 7, losing one point if directly threatened and one if it has fewer than two supporters and all are threatened. Rocks, graves, and castles block traversal. Main propagation is through contested cells. |
| Material | Each castle adds 1.15. Knights add 0.43, or 0.30 when directly threatened. |
| Formation | Each knight adds 0.24 with castle support, 0.15 with multiple or safe support, and loses 0.15 otherwise. The source piece's ruleset survival range determines support. |
| Ground | Subtracts 0.26 times the sum of per-cell influence advantage multiplied by the fraction of ground health lost. This makes degradation more costly to the side expected to benefit from the surviving ground. Graves also change graph connectivity and authoritative scoring. |
| Mobility | 0.28 times useful legal empty-square reach. Each side counts a reachable cell once and discounts it by that side's positive influence advantage. |

For each traversable square, influence advantage is `clamp((own - opponent) / 3, -1, 1) × min(4, max(own, opponent)) / 4`. Saturation and strongest-source propagation give diminishing returns when a new knight duplicates a nearby formation. Reinforcement, castle upgrades, and closing a boundary can still add distinct formation or secured value. New fronts gain value through independently reachable territory and through the shortlist's geographic diversity.

The graph cannot establish eventual control, and a single strongest source can underrate overlapping long-term pressure. The model can miss replies outside the shortlist, misjudge contested corridors and repeat an unfavorable engagement. It does not learn an opponent's tendencies across turns. The benchmark comparisons and tactical failure cases are documented separately. [Milestone 8C benchmark report](CpuBenchmarks.md)

## Static candidate features

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
| Risk | Estimates opponent appeal from their expansion, territory, and spread at each public start. Scales by remaining starts and the cost of missing a first foothold. A collision at a gate adds cost. Once starting pieces exist, denying a strong start can offset part of that cost. | Shared targets carry a collision cost. The cost is lower when a collision still removes the desired opposing knight, especially on health-one ground. A potential new knight also carries exposure proportional to the nearby enemy legal options. |

Opening regions are measured once on the draft board. Remaining starts are still empty and the estimates carry no persistent territorial ownership. The opening collision cost starts at 1.0 with no starting pieces and at most three remaining starts, 0.7 earlier in an empty draft, or 0.4 once starting pieces are established. A gate adds up to 0.2, and denial reduces the cost by up to 0.2 once established. Relative opponent appeal and the square root of half the remaining start count scale the resulting cost.

In Main, a shared placement or upgrade starts with collision cost 0.65. Shared captures use 0.25 when a knight would survive the ordinary capture and 0.05 otherwise. If a new knight may occupy the square, exposure adds up to 0.35 based on the fraction of adjacent squares available to the opponent.

The eight-neighbor divisor and the region share normalize counts. The opening urgency, appeal model, denial credit, and gate penalty and the main-phase action values and costs are shared assumptions. These scores order strategic candidates and supply the legacy Classic policy. The simultaneous search separately values territory and connectivity after resolution.

### Knight Support evaluation

The engine reads the ruleset from the public snapshot. In the Knight Support draft the tactical prior counts each existing castle once as a source in every region it borders. Castles establish footholds immediately, which lowers the urgency assigned to a first placement. The local gate estimate adds up to 0.25 to the opening territory feature to reflect the value of an invulnerable boundary.

In Main, the **Formation** feature adds 0.25 for multiple supporters and 0.35 per adjacent castle for a prospective knight. Placing a second supporter or upgrading a sole supporter adds 0.25 per dependent friendly knight. Capturing a sole enemy supporter earns half that amount per dependent enemy knight to account for possible reinforcement or upgrades. Ground exhaustion increases that credit by 50%. Shared targets discount formation value by half. **Support Risk** adds 0.5 for a prospective knight whose sole supporter is a legal enemy target, with a 50% increase if the extra loss would exhaust the ground. A contested upgrade also gets 0.125 risk per friendly dependent vulnerable to a collision. Both features cap at 1.

The five profiles use configurable static weights of 1.2 for Formation and 1.6 for Support Risk. Custom profiles can set the `Formation` and `SupportRisk` properties on `CpuEvaluationWeights`. Both features contribute zero in Classic. They estimate local opportunities and threats from public legal sets and ruleset-defined survival ranges. Mutually supporting formations can survive independently of castles.

## Difficulty profiles and sampling

| Difficulty | Opening pool | Main pool | Opening temp. | Main temp. | Expansion | Territory | Capture | Fortification | Mobility | Bottleneck | Spread | Risk |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Easy | 16 | 12 | 0.90 | 1.00 | 2.2 | 0.7 | 1.5 | 0.8 | 0.3 | 0.2 | 0.5 | 0.35 |
| Normal | 12 | 8 | 0.75 | 0.60 | 2.2 | 1.2 | 1.6 | 1.1 | 0.6 | 0.5 | 0.8 | 0.65 |
| Hard | 10 | 5 | 0.65 | 0.30 | 2.0 | 1.6 | 1.8 | 1.4 | 0.9 | 0.8 | 1.1 | 0.95 |
| Very Hard | 8 | 3 | 0.55 | 0.12 | 2.0 | 1.9 | 1.9 | 1.6 | 1.1 | 1.1 | 1.3 | 1.15 |
| Monster | 16 | 1 | 0.50 | 0 | 2.0 | 2.1 | 2.0 | 1.7 | 1.2 | 1.2 | 1.4 | 1.25 |

All five profiles use 0.20 ordinary and 0.70 urgent opening diversity, with both maximum score differences disabled. The Main sampling limits are separate from search breadth. Monster's zero Main temperature samples equivalent best candidates. Its opening limit covers the largest start list in the current catalog.

Custom profiles take positive limits, finite temperatures capped at 10, and finite diversity shares from 0 through 1. Opening temperature must be positive. Either phase can specify a finite, nonnegative maximum score difference from its best candidate. A null score gap disables the cutoff. A custom profile can opt into Knight Support search with its `Search` initializer.

```csharp
var custom = new CpuProfile(
    new CpuEvaluationWeights(2.2, 1.2, 1.6, 1.1, 0.6, 0.5, 0.8, 0.65),
    openingCandidateLimit: 12, mainCandidateLimit: 8,
    mainTemperature: 0.60, openingTemperature: 0.75,
    openingDiversity: 0.20, urgentOpeningDiversity: 0.70,
    mainMaximumScoreDifference: 1.5)
{
    Search = new CpuSearchOptions(1, 8, 6, 120, 0.24, 1.4)
};
```

The static evaluator scores every legal candidate and breaks score ties in row-major order. Sampling operates on the best comparable search tier or the static pool. It applies the nominal pool limit and optional score gap, retaining at least two evaluated Opening choices when available and at least one Main choice. It includes every eligible candidate within an absolute `1e-12` score difference of the last retained score. This margin covers floating-point roundoff.

Positive temperatures use softmax after subtracting the best score. In Main, positive temperature can deliberately select lower-rated moves from the retained pool. At zero temperature, the score-based sampler uses equal probabilities among scores within `1e-12` of the best. Monster uses the matrix policy's probabilities when a solve completes and the score-based sampler when no matrix policy is available.

Opening mixes uniform probability into the retained pool. The urgent setting applies when two starts remain and neither player has a starting piece. With the built-in urgent share, either start receives at least 35% probability. A single retained candidate receives probability one. Excluded candidates receive zero.

```text
finalProbability = (1 - diversity) * softmaxProbability + diversity / retainedCandidateCount
```

Each CPU samples independently. On the symmetric initial Banner Plain board all four corners remain equivalent, for a 25% first-round collision probability between identical independently sampled players. Collisions and unsuccessful drafts remain possible.

## Diagnostics and cost

`DecideWithDiagnostics` returns the proposal, tactical terms, each candidate's final score and sampling probability, and optional search details. Search details include considered actions, common reply probabilities, collisions checked, expected and adverse outcomes, proven wins, input position contributions, selected and completed depths, new and reused pairs, elapsed time, and budget and time stops. A proof-only pass can finish and leave the selected depth unchanged. Candidates outside the comparable tier receive zero probability. Ordinary `Decide` skips the diagnostics collection.

Each new simulation creates a domain board and analyzes territory and legal sets once. A scratch workspace reuses the neighbor graph and propagation arrays across simulations. Two-turn search retains root children. Deeper search can reuse up to 512 exact state-and-action pairs in one decision. The cache keys use immutable state identity and both coordinates, with a fixed acting player per decision. This also fixes the ruleset, terrain, ownership and health. The cache holds the resolved pair and immediate evaluation, so reuse does not depend on remaining depth. [Measured latency and allocation](CpuSearchOptimization.md)

## Milestone 8B integration lifecycle

1. The scene passes each public snapshot to its `CpuMatchCoordinator`. The coordinator starts one request per playable phase and round, skipping terminal snapshots and rounds the CPU has already submitted.
2. A `Task.Run` worker calls the synchronous engine with that immutable snapshot, a fresh random instance derived from the match's seed source, and a cancellation token. The worker has no Godot or session reference.
3. The engine returns a proposal tagged with the input player, phase, and round. The scene pumps completions on the Godot main thread, outside session callbacks.
4. The coordinator checks active session identity, cancellation, phase, round, and player, and verifies that the CPU still owes a submission. It sends an `OpeningSelection` or `MainPhaseCommand` on the same thread. The session validates phase, round, player, and legality.

`RoundId` is scoped to one session. The coordinator checks session identity even if two sessions publish the same round ID. Superseded, terminal, restarted, and abandoned rounds invalidate outstanding requests and request cancellation. Retired workers have their faults observed and their cancellation sources disposed after they exit. A human submission or revision within the same unresolved round leaves the CPU's inputs and proposal valid. Both players use the same resolved pre-round board.

If the worker faults, returns no valid proposal, or the command is rejected, the scene displays an error and lets the player start a New Match. The coordinator keeps its profile for the life of a match, including restarts. Confirming New Match cancels and invalidates outstanding work from the previous session and starts a new coordinator with the confirmed CPU profile and battlefield under the configured ruleset.

## Milestone 8C evaluation

Evaluate pairs of opposing choices through the authoritative simultaneous resolver, using `MainPhaseState.Evaluate(snapshot.Board, snapshot.RuleSet)` and carrying that ruleset forward through each hypothetical turn. Reuse the static features for ranking and fallback. The current engine has fixed collision and exposure costs, a static opening appeal model, and local gate estimates that can miss longer paths. Include exact territory and board-wide support effects when comparing plausible opposing replies. Seeded legality and completion checks measure playability. Competitive strength needs separate playtesting.

## Tournament evaluation

The development harness wraps CPU implementations as versioned opponents using the same immutable snapshot boundary as the application coordinator. It owns experiment profiles, timing, randomness, match scheduling and persistence. The frozen 8C evaluator, search and profiles build as their own assembly. See [CPU tournaments](CpuTournaments.md) for provenance, diagnostics and experimental workflows.

The development-only [small-board solver](SmallBoardSolver.md) evaluates simultaneous decisions against exact state values and supplies a tablebase reference for experiments.
