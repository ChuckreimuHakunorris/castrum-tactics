# Castrum Tactics — Game Session Architecture

**Status:** Accepted architectural direction  
**Scope:** Gameplay presentation, session management, input sources, and game-rule authority

## 1. Core architectural principle

Castrum Tactics must use the same gameplay rules and presentation system regardless of whether the opponent is controlled by a human, a CPU, or a remote player.

The source of a player's decisions must not determine how those decisions are validated or resolved.

**The gameplay scene is never authoritative.** It displays game state, collects local input, and presents results. Game-rule validation and turn resolution are performed outside the presentation layer.

The architecture must support singleplayer and multiplayer without duplicating gameplay rules or creating separate gameplay scenes.

## 2. Separation of responsibilities

The system is divided into three primary responsibilities.

### 2.1 Presentation layer — Godot gameplay scene

The gameplay scene is responsible for:

- Rendering the board, pieces, and other visual elements.
- Collecting mouse and touchscreen input.
- Converting player interactions into gameplay selections.
- Displaying selection highlights, animations, and match status.
- Receiving and presenting game-state updates.

The gameplay scene must not:

- Determine authoritative move validity.
- Resolve simultaneous turns.
- Modify authoritative game state directly.
- Contain separate gameplay-rule implementations for different game modes.
- Depend on whether its opponent is human, CPU-controlled, or remote.

The same gameplay scene should be reusable for local and online matches. In multiplayer, each client runs its own instance.

### 2.2 Session layer — Match coordination

A session abstraction provides the interface between the gameplay scene and the authoritative game system.

The session is responsible for coordinating command submission, match progress, and state updates.

The architecture defines two kinds of session:

**LocalGameSession**

- Runs a complete local match between two people sharing one device. Human versus CPU is a later input mode.
- Coordinates selections from both participants.
- Invokes the shared domain rules locally.
- Maintains the authoritative local match state.
- Provides resulting state updates to the gameplay scene.

**RemoteGameSession**

- Used for online multiplayer.
- Sends local player commands to the authoritative server.
- Receives acknowledgments, match status, and authoritative state updates.
- Does not independently determine official move outcomes.
- Handles network-related concerns such as waiting for server responses and, eventually, connection interruptions.

Both implementations must expose a consistent interface to the gameplay scene.

[`IGameSession`](xref:CastrumTactics.Domain.Session.IGameSession) is the presentation contract. In the local implementation, commands return acceptance or a rejection reason synchronously and accepted commands publish a snapshot on the caller's thread.

### 2.3 Domain layer — Shared game rules

The domain layer is responsible for:

- Representing game state.
- Validating gameplay selections against the applicable rules.
- Determining legal actions.
- Resolving simultaneous selections.
- Applying resulting state transitions.
- Determining draft progression and main-phase completion.
- Deriving territory and each player's legal selections from a resolved board, and scoring the match when either legal set is empty.

The domain must remain independent of Godot, input devices, networking, and AI implementations.

**Local sessions and authoritative multiplayer servers must use the same domain rules.**

The intended result is one rules implementation, regardless of game mode.

## 3. Input sources

A gameplay selection may originate from:

- A human player using mouse or touchscreen input.
- A CPU opponent selecting a position through an AI algorithm.
- A remote human player submitting a command through the network.

These sources should produce equivalent gameplay commands.

The rules must not distinguish between commands based on their source.

CPU opponents should interact with the gameplay system through commands, not by simulating mouse clicks or directly manipulating the board.

A future player-agent abstraction, such as `IPlayerAgent`, may be useful for encapsulating decision-making. However, it should not be introduced until concrete requirements justify it.

## 4. Authority and match execution

### 4.1 Local matches

For local matches, the local session is authoritative.

The current local sequence is:

1. The scene displays shared opening positions or the active player's legal main-phase targets and lets the active player highlight a provisional choice.
2. The scene submits an [`OpeningSelection`](xref:CastrumTactics.Domain.Session.OpeningSelection) or [`MainPhaseCommand`](xref:CastrumTactics.Domain.Session.MainPhaseCommand) with the player, position, and displayed round identifier.
3. The session checks the player, round identifier, phase, and eligibility from the applicable domain rules. It keeps an accepted choice private and allows revisions until the other player submits.
4. The second player's submission resolves both selections against the same board. If exactly one opening start remains, the session resolves both players selecting it before publishing. The session clears pending choices and advances the identifier once per resolved round. After a successful draft or main-phase turn, it evaluates the resulting board for the next main-phase turn or final result.
5. The session publishes the new snapshot. The scene clears the provisional highlight and renders the updated board and status. A completed match keeps its board, territory, and final result visible and rejects further gameplay submissions.

The CPU decision engine currently returns synchronous proposals against the public snapshot. The future asynchronous caller must verify that the originating session is still active and that the proposal's phase and session-scoped round ID are current. Human submissions and revisions within that round leave the decision valid. See the [Milestone 8B integration lifecycle](CpuArchitecture.md#milestone-8b-integration-lifecycle).

### 4.2 Online multiplayer

For online matches, the server is authoritative.

The expected sequence is:

1. The gameplay scene receives the local player's selection.
2. `RemoteGameSession` sends the selection to the server.
3. The server authenticates the player, validates the command, and stores the pending selection.
4. The server waits until the necessary selections are available.
5. The server resolves the turn using the shared domain rules.
6. The server distributes the authoritative result to the clients.
7. Each client's remote session updates its gameplay scene.

Clients must not determine official turn outcomes.

The server must not trust a player identity supplied solely by the client. Submitted commands must be associated with the authenticated participant and the correct match.

## 5. Simultaneous turns and information visibility

Castrum Tactics uses simultaneous selections.

The following requirements apply to every game mode:

- Both players' selections must be validated against the same pre-resolution game state.
- Turn resolution must not depend on the order in which commands arrive or are processed.
- The authoritative system must collect the necessary selections before resolving a turn.
- A player's pending selection must not be revealed to the opponent before resolution.
- CPU opponents must not receive access to hidden information unavailable to human opponents.
- Multiplayer clients should receive authoritative turn results rather than an opponent's unrevealed pending selection.

The shared domain rules must implement deterministic simultaneous resolution.

## 6. Session and presentation states

The gameplay scene should be capable of representing session progress without knowing how that progress is managed internally.

Possible presentation states include:

- **Choosing:** The local player may make a selection.
- **Submitted:** The local selection has been submitted.
- **Waiting:** The session is waiting for the opponent or authoritative system.
- **Resolving:** The resulting state is being processed or presented.
- **Finished:** The match has ended.

A submission acknowledgment and a completed turn are distinct events.

A [`GameSessionSnapshot`](xref:CastrumTactics.Domain.Session.GameSessionSnapshot) publishes an immutable board, phase, round identifier, turn counts, and each player's submission flag. `EligiblePositions` is the shared opening list. `GetMainPhaseLegalSelections` supplies player-specific targets during Main. Main and Finished snapshots include territory, while Finished also includes the final score and winner or draw. Pending coordinates stay private. Rejected commands leave the current snapshot and pending choices intact.

The local scene passes a player identifier chosen by a UI toggle. Changing the active player clears the provisional highlight and refreshes their targets. `RestartDraft` accepts the current round identifier in `OpeningRestartRequired` or `Finished`. It restores the original map, clears both pending choices, turn counts, territory and final result, and publishes a fresh Opening snapshot. The identifier advances on restart so commands from the previous match are stale. The scene clears the provisional highlight and makes Player One active.

## 7. Architectural constraints

The following constraints should guide future implementation:

1. Do not duplicate gameplay rules between singleplayer and multiplayer.
2. Do not place authoritative rules or turn resolution in Godot scene scripts.
3. Do not make the gameplay scene depend directly on CPU or networking implementations.
4. Keep the domain library independent of Godot and infrastructure.
5. Treat remote game-state updates as authoritative in multiplayer.
6. Keep CPU decision-making separate from game-rule execution.
7. Design session interfaces around the needs of the gameplay scene, avoiding premature abstractions.
8. Preserve testability: domain rules and local session coordination should be testable without launching Godot.
9. Do not implement networking merely to establish these architectural boundaries.

## 8. Implementation strategy

Adopt the architecture incrementally.

**Milestone 4 — Opening draft**

- Implement the opening-draft rules in the domain layer.
- Establish a minimal session abstraction.
- Implement local session coordination first.
- Connect the existing board-selection UI to the local session.
- Add automated tests for opening selections, collisions, and simultaneous resolution.

**Milestone 5 — Main-phase domain rules**

- [`MainPhaseState`](xref:CastrumTactics.Domain.MainPhaseState) evaluates a resolved board and validates both selections against that evaluation. See [Main-Phase Rules and Architecture](MainPhaseArchitecture.md).

**Milestone 6 — Local match integration**

- The session evaluates the successful opening board and publishes Main or Finished in the same update. It then coordinates simultaneous main-phase submissions through final scoring. Godot renders the published targets, territory, and result.

**Later milestones**

- Introduce CPU decision-making through the same command mechanism.
- Introduce `RemoteGameSession` when multiplayer development begins.
- Connect the multiplayer server to the shared domain rules.
- Expand session status handling, error reporting, and reconnection behavior as required.

Do not build a speculative networking or player-agent framework before it is needed.

## 9. Open design decisions

The following remain undecided:

- Asynchronous acknowledgments, transport snapshots, and thread dispatch for a remote `IGameSession`.
- Whether to move session coordination into a separate assembly as more implementations are added.
- How CPU decision-making is scheduled and canceled.
- The multiplayer backend, network protocol, and server hosting model.
- Timeout, disconnection, reconnection, and forfeiture behavior.
- Network-specific session status and error reporting.
- Multiplayer restart authority.

These decisions should be made when the corresponding functionality is implemented.

## 10. Summary

**One gameplay scene. One authoritative rules implementation. Different session implementations depending on where authority resides.**

- Presentation displays the game and submits player intentions.
- Sessions coordinate commands and deliver results.
- Domain rules validate and resolve gameplay.
- Local sessions are authoritative for singleplayer.
- Servers are authoritative for multiplayer.
- Human players, CPU opponents, and remote players use the same gameplay command model.

This separation is a foundational architectural requirement for Castrum Tactics.
