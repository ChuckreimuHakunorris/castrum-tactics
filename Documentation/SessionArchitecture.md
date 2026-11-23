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

- Implemented for local two-human testing on one device. Human versus CPU is a later input mode.
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
- Determining match progression and, eventually, game completion.

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

1. The scene displays the session's eligible positions and lets the active player highlight a provisional choice.
2. The scene submits an [`OpeningSelection`](xref:CastrumTactics.Domain.Session.OpeningSelection) with the player, position, and displayed round identifier.
3. The session validates the choice through the draft rules and keeps it private. A player may replace their own pending choice before the other player submits.
4. The second player's submission resolves both selections against the same board. The session clears pending choices and advances the identifier.
5. The session publishes the new snapshot. The scene clears the provisional highlight and renders the updated board and status.

CPU decision-making may be asynchronous, but it must not require a separate gameplay implementation.

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

Milestone 4 publishes a [`GameSessionSnapshot`](xref:CastrumTactics.Domain.Session.GameSessionSnapshot) with an immutable board, eligible positions, resolved-round count, round identifier, phase, and each player's submission flag. No coordinate from a pending command is published. Rejected commands leave the current snapshot and pending choices intact.

The local demonstration passes a player identifier chosen by a UI toggle. Changing the active player clears the provisional highlight. The session can restart only when the draft requires it, and advances the identifier on restart so commands from earlier rounds cannot affect the new draft.

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

**Later milestones**

- Introduce CPU decision-making through the same command mechanism.
- Implement main-phase rules in the shared domain.
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
