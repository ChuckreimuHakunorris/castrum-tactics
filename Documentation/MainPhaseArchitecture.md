# Castrum Tactics — Main-Phase Rules and Architecture

**Status:** Domain, local session, and Godot integration implemented.\
**Version:** 0.6\
**Date:** 2026-11-24\
**Scope:** Main-phase moves, simultaneous resolution, territory classification, legal selections, endgame and scoring

This specification expands the [Game Design Document](GameDesign.md) and follows the [Session Architecture](SessionArchitecture.md). The local session evaluates the board from the Unlimited Draft and runs main-phase turns through final scoring. Godot displays the board, the active human's targets, live territory counts, and the final result in both input modes.

## 1. Core architecture

The physical board and derived territorial control are **different kinds of state**. A cell may contain no piece while being secured for a player. Never overwrite the physical owner of an empty cell merely to represent territory.

The rules remain in engine-independent C#, shared by local sessions and future authoritative multiplayer servers. Godot renders snapshots and submits commands; it does not implement rules. Avoid speculative infrastructure or unnecessary interfaces.

Local match flow

1. Read the resolved physical board.
2. Analyze territorial regions (independent of legal moves).
3. Generate legal main-phase selections separately for Player One and Player Two using those classifications.
4. If either legal set is empty, finish the match and score the secured territory.
5. Otherwise accept each player's private selection against this pre-turn state.
6. Resolve both selections simultaneously into a new immutable board; clear pending selections and repeat from step 1.

[`TerritoryAnalyzer`](xref:CastrumTactics.Domain.TerritoryAnalyzer) derives classifications independently of legal moves. [`MainPhaseState`](xref:CastrumTactics.Domain.MainPhaseState) binds one board to its territory, both legal sets, and its optional final result. It can resolve a pair of player-tagged selections against that snapshot. Evaluate the returned board to obtain the next snapshot.

## 2. Main-phase legal selections

Knights and castles influence all **eight immediately adjacent squares**, including diagonals. This is not the chess-knight L-shaped pattern. Pieces remain stationary; creating a knight places a new piece in a selected square. Rocks, rivers, and graves are unselectable but do not block a piece's influence on its other neighbors.

A player's **own knight is always a potential upgrade target**, even if isolated from other friendly pieces. This self-upgrade exception does not override the restriction on secured squares.

| Target on the pre-turn board | Ordinary result when selection is legal |
|---|---|
| Empty playable square within range | Create selecting player's knight; preserve ground health |
| Enemy knight within range | Reduce ground health by 1; replace with attacker's knight if health remains positive; otherwise unowned grave at 0 |
| Own knight, anywhere | Upgrade to own castle; preserve ground health |
| Own castle or enemy castle | Illegal |
| Grave or inaccessible square | Illegal |
| Out-of-bounds square | Illegal |

**Secured-square exclusion:** Neither player may select a square that the current territory analysis marks secured for either player. This includes empty squares and a player's own knights. The exclusion applies **only to the main phase**; the opening draft retains its existing rules. Neutral and permanently unplayable squares are not legal targets. Contested squares remain subject to ordinary piece and range rules.

Ground health never becomes negative. Regular placement and upgrade do not change health. No health regeneration rule is currently specified; do not invent one.

## 3. Simultaneous resolution and command authority

The local session privately retains one choice per player for the current round. A player may revise their choice before the other player submits, as in the opening. Public snapshots report submission flags while keeping coordinates private.

Validate identity, phase, round identifier, and per-player legal selection before accepting a command. Reject invalid or stale commands without altering state.

Both accepted selections are validated against **the same pre-turn board, territory classification, and legal sets**. Submission order must not affect the result.

- **Same square:** A direct collision takes precedence over the ordinary action. Reduce that square's existing ground health by 1 (minimum 0) and always create an unowned grave, even if health remains positive. Preserve terrain.
- **Different squares:** Apply both ordinary actions independently using the pre-turn state. For instance, two players can capture one another's knights on different squares. Removing a piece must not retroactively invalidate the other player's already legal selection.
- After resolution, publish the new board, round identifier, phase, territory classifications and player-specific legal targets; clear pending commands.

## 4. Territory classification v1

Territorial classification is a **conservative region-based visual and rules mechanic**, not an exhaustive simulation of future play. Some squares that cannot actually be reached by an opponent may remain classified as contested for clarity. This is intentional until playtesting suggests otherwise.

### 4.1 Categories

| Classification | Meaning |
|---|---|
| Secured by Player One | A region encounters only Player One's knights/castles |
| Secured by Player Two | A region encounters only Player Two's knights/castles |
| Contested | The region encounters pieces belonging to both players |
| Neutral | The region encounters neither player's pieces |
| Castle cell | Always secured by that castle's owner, independently of neighbors |
| Grave | Permanently neutral; never traversed or selectable |
| Inaccessible terrain | Outside territorial control and traversal. Cannot be selected and scores zero. |

### 4.2 Algorithm

Use **eight-neighbor connected-component flood fill**. Traverse playable empty squares and knights, including knights on health-1 ground. Never traverse graves, castles, or inaccessible terrain.

For each as-yet-unvisited traversable cell:

1. Flood-fill its connected component; mark visited squares.
2. Record ownership of any knight in the component.
3. When the traversal reaches an adjacent castle, record that castle's owner but **do not enter or expand through** the castle cell.
4. Classify the entire component from the distinct owners found: Player One only, Player Two only, both (contested), or neither (neutral).
5. Classify castle cells separately as secured by their owners.

The algorithm uses each traversable cell once and checks up to eight neighbors, so analysis is linear in board area. Avoid running a fresh flood fill from every cell.

**Health-1 knight simplification:** A capture might turn a health-1 knight into a grave and prevent the attacker from advancing through it. The v1 traversal nevertheless passes through that knight, potentially leaving a technically unreachable region marked contested. Do not add a health-aware reachability search without a new design decision.

**Initial unreachable regions:** A region disconnected from every starting position by fixed inaccessible terrain cannot acquire a piece. The current analyzer scans the full board and classifies such regions as neutral on each evaluation.

**Dynamic unreachable regions:** A grave can cut off a traversable component from all remaining pieces, making it neutral. A castle can separate components but still supplies its owner to each component touching it. Any newly separated component that touches a castle therefore has that player's influence. Recalculate classifications after every resolved turn.

The scene tints secured squares with the securing player's color while preserving ground-health shades. Gold frames identify the active player's main-phase targets. A pale outline identifies the provisional selection.

**Movement versus traversal:** Graves, castles, and inaccessible terrain are flood-fill boundaries for **territory analysis**. An individual knight/castle can still influence its other eight adjacent squares regardless of blockers elsewhere; there is no line-of-sight/pathfinding requirement for legal moves.

## 5. Match completion and scoring

**End the match immediately when either player's legal main-phase selection set is empty.** Do not wait for both players; no passing mechanic exists. Check immediately upon transitioning from opening to main, and after every resolved main-phase turn.

Every contested traversable component supplies a legal selection to both players. Each player contributes an own knight on a contested square eligible for upgrade or a bordering castle in range of a contested empty or enemy-knight square. If a bordering castle touches a friendly knight in that component instead, that knight supplies the upgrade. Every legal selection is contested, so under v1 the legal sets become empty together and no contested cells remain at match completion. The tests check this invariant.

Each immutable [`TerritoryAnalysis`](xref:CastrumTactics.Domain.TerritoryAnalysis) counts every **secured playable square** for its controlling player, including empty squares and squares occupied by knights or castles. Contested and neutral playable squares, graves, and inaccessible terrain earn no points. `GameSessionSnapshot` exposes the same totals once territory exists and exposes zeros during the draft and failed-draft state. Resolved turns construct new analyses. Submission and revision snapshots reuse the current analysis. `MainPhaseState` uses its totals to construct the final result. Higher score wins and equal scores draw.

## 6. Integration responsibilities

- Physical state: retain BoardState/CellState as immutable snapshots of terrain, health, piece, and owner.
- Territory analysis: pure domain component returning derived per-cell classifications without mutating BoardState.
- Legal selections: pure domain logic receiving resolved board, territory analysis, and player identity; supply separate sets for each player.
- Resolution: pure domain simultaneous pair transition, preserving existing health and ownership invariants.
- The local session coordinates private commands and revisions, tracks round IDs, and publishes Main or Finished after each evaluation.
- Godot uses session snapshots to display legal targets, castles, secured-color overlays, and the score banner. Finished hides submission controls and keeps the board, banner, and outcome visible.

The opening phase is intentionally unchanged; do not conflate opening eligible starting positions with main-phase per-player legal selections. Use focused types and extensions rather than a large generalized framework.

## 7. Required test scenarios

- Eight-directional adjacency including diagonals, corners and rectangular maps.
- Own-knight upgrade from isolation; exclusion when that knight becomes secured.
- Per-player legal selection differences and secured-square exclusion for **both** players.
- Enemy knight capture at ground health 3, 2, 1; own knight upgrade and normal placement preserve health.
- Same-square collisions at health 3, 2, 1 always produce a grave at health 2, 1, 0.
- Distinct-square simultaneous captures both succeed; swapping submission order preserves result.
- One action destroying a piece that supplied the other action's initial range does not invalidate the latter.
- Session tests cover private choices, revisions, stale rounds, invalid commands, immutable snapshots, and final results from both the opening and main-phase turns.
- Connected components with one owner, two owners, or no owners; castles as terminal sources; graves/inaccessible terrain as barriers; diagonal connectivity.
- Health-1 knight deliberately remains traversable for territory classification.
- Static map regions inaccessible from every start; dynamically isolated regions created by graves or castles.
- A secured knight is not selectable for upgrading; a secured empty square is not selectable even by its owner.
- Entering main phase with no legal moves finishes immediately; either player's empty legal set finishes after a resolved turn.
- Finished domain evaluations have no contested squares. Secured empty squares and secured pieces earn points. Neutral cells, graves, and inaccessible terrain score zero. Ties draw.
- Analyzer does not mutate physical board; no Godot dependency in domain tests.

## 8. Open questions

Open design questions include ground-health regeneration, overlay styling, and whether playtesting warrants a more precise reachability algorithm. Future Fixed Draft, deeper CPU search, networking, multiplayer restart authority, and backend choices remain out of scope.
