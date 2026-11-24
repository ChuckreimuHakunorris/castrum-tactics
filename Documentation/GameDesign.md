# Castrum Tactics — Game Design Document

**Document status:** Revised draft  
**Version:** 0.5\
**Last updated:** 2026-11-23  
**Project type:** 2D turn-based grid strategy game  
**Primary purpose:** Playable game and professional software-engineering portfolio project

> This document records the design and development decisions discussed so far. It is a working baseline, not a claim that every rule or technical choice is final. Items marked **Open decision** require confirmation before the relevant feature is implemented. For the main-phase rules and local integration, see [Main-Phase Rules and Architecture](MainPhaseArchitecture.md).

---

## 1. Project vision

Castrum Tactics is a two-player, turn-based strategy game played on a configurable grid. Players expand their influence by creating knights, upgrading their own knights into castles, and capturing enemy knights. The objective is to control more territory than the opponent when the game ends.

The project has two equally important goals:

1. Build an enjoyable, coherent strategy game with room for casual play and competitive play.
2. Demonstrate professional software-engineering ability through the quality of its code, architecture, tests, documentation, repository practices, and project management.

The project should be developed deliberately. A feature is not considered complete merely because it appears to work; it should also be appropriately tested, maintainable, documented, and integrated.

## 2. Product principles and scope

### 2.1 Core principles

- **Configurable boards:** Support rectangular and square boards rather than hard-coding the original 5×5 layout.
- **Distinct terrain and pieces:** Terrain determines whether a square can be used; pieces and ownership are separate concepts.
- **Simultaneous resolution:** Where both players submit a move for the same turn, resolve the submissions against the same pre-turn state. Processing order must not advantage either player.
- **Creative map design:** Casual maps should allow asymmetry and unusual layouts.
- **Competitive integrity:** Ranked play may apply additional structural and fairness checks, but validation should not unnecessarily constrain creative maps.
- **Shared rules implementation:** Human players, AI, and multiplayer validation should use the same authoritative game rules.
- **Professional engineering:** Prefer simple, explicit, testable designs over unnecessary abstraction or premature complexity.

### 2.2 Intended platforms and technology direction

- **Game engine and language:** Godot 4 .NET, with C# as the primary programming language.
- **Initial renderer:** Godot's Compatibility renderer.
- **Development editor:** VS Code with the Codex extension.
- **Target platforms:** Windows and Android.
- **Version control and collaboration:** Git and GitHub.
- **Multiplayer backend:** Open decision. Self-hosted, open-source Nakama remains a candidate.
- **Database candidate:** PostgreSQL, subject to the backend's actual requirements.
- **Art tools:** Free tools such as Krita where suitable.
- **Distribution:** itch.io for PC builds; direct APK distribution is a possible Android route. Store distribution requirements and costs must be checked before committing.

The project has a strict **€0 budget**. Recommendations should therefore prioritize tools and services that can be used at no cost. Any free-tier limits, hosting costs, platform fees, or licensing conditions must be checked before adoption.

Unity was rejected because it consumed too many resources on the development computer, which has 8 GB of system RAM.

Multiplayer backend selection, hosting, deployment, and distribution still need evaluation.

### 2.3 Platform export requirements

- Install the Godot 4 .NET editor, a compatible .NET SDK, and Windows and Android export templates that match the .NET editor version. Confirm the SDK and target framework against the selected Godot release and its Android templates. The Godot 4.7 documentation specifies .NET 9 or later for Android export. [Godot C# prerequisites](https://docs.godotengine.org/en/4.7/tutorials/scripting/c_sharp/c_sharp_basics.html#prerequisites) · [Godot export templates](https://docs.godotengine.org/en/4.7/tutorials/export/exporting_projects.html#export-templates)
- Install OpenJDK 17 and the Android SDK packages required by the selected Godot release, including platform tools (`adb`), build tools, and the required SDK platform. Follow the release's Android setup instructions for additional packages such as the NDK and CMake. Configure the Java SDK and Android SDK paths in Godot. [Godot Android setup](https://docs.godotengine.org/en/4.7/tutorials/export/exporting_for_android.html#setup-on-windows-macos-and-linux)
- Godot 4 C# Android export support is experimental. Validate it early by exporting a minimal C# project, installing it on a physical Android device, and confirming that the app launches and executes C# code with the Compatibility renderer. [Godot Android export documentation](https://docs.godotengine.org/en/4.7/tutorials/export/exporting_for_android.html)
- Verify Android compatibility for third-party libraries, networking integrations, advertising SDKs, and in-app purchase integrations before adopting them. Exercise each selected integration in an exported build on a physical Android device.
- Godot 4 C# currently does not support web exports. [Godot C# platform support](https://docs.godotengine.org/en/4.7/tutorials/scripting/c_sharp/index.html#c-platform-support)

## 3. Game overview

### 3.1 Players and colours

- Two players participate in a match.
- The host player is **Red**.
- The challenger is **Blue**.

### 3.2 Board

- The original rules use a 5×5 board.
- The implementation must support configurable width and height.
- A board may be square or rectangular.
- A map defines its dimensions and the terrain and starting-position layout.

### 3.3 Objective

The objective is to control more secured playable squares than the opponent when the game ends. Empty secured squares count. Inaccessible terrain scores zero. See [final scoring](#63-final-scoring).

## 4. Board model

A square's terrain should be represented separately from its piece/occupant state.

Conceptual tile data:

- **Terrain accessibility:** Playable or Inaccessible.
- **Piece type:** None, Knight, Castle, Grave.
- **Owner:** Unowned, Player One, or Player Two. Faction color is presentation data.
- **Ground health:** Tracks accumulated degradation of the square independently of its occupant. Playable terrain starts at 3 and inaccessible terrain has 0.

Terrain artwork should reflect ground health independently of the piece or grave drawn over it. A grave can occupy ground with positive health.

### 4.1 Terrain

Playable terrain is normal gameplay ground. Rocks and rivers are visual representations of the same inaccessible terrain. Inaccessible cells:

- Cannot be occupied.
- Cannot be selected as a move or starting position.
- Do **not** block expansion to neighbouring squares beyond them.
- Do not introduce pathfinding, line-of-sight, or connectivity blocking under the current rules.

Additional terrain types may be introduced later if they have clear gameplay rules.

### 4.2 Pieces

- **Knight:** A player's basic piece.
- **Castle:** Created by upgrading one of the player's own knights.
- **Grave:** An unowned obstacle that makes a square permanently unusable. A direct collision always creates one. A capture creates one when it depletes the ground.

Knights and castles both extend the player's available selection area to the eight surrounding squares, including diagonals. A castle is stationary, but it continues to act as an expansion point just like a knight.

A move does not physically move an existing piece. Instead, a successful selection creates a new knight, captures an enemy knight, or upgrades an existing friendly knight, depending on the selected square's state.

## 5. Match phases and turn resolution

The game has three conceptual phases:

- **Opening**
- **Main**
- **Finished**

### 5.1 Opening phase: starting-position draft

Each player selects one starting position per round. Both players use the same pre-round board. A player can submit or revise a choice until the opponent submits. Pending choices stay private and leave the board and eligibility intact. Once both players have submitted, the round resolves immediately.

Two opening modes are intended:

#### Unlimited Draft

Only empty, playable positions designated by the map are eligible. Knights, castles, and graves are unavailable regardless of ownership. Each resolved position becomes occupied and stays unavailable for the rest of the opening.

A local session requires a map with at least two distinct playable starting positions so both players can claim a knight in the same round.

- Different positions give each player one knight and preserve both squares' ground health.
- Identical positions cause a direct collision. The square becomes an unowned grave and neither player gains a knight.

Both players therefore have equal knight counts after every round. There is no fixed knight allocation. If one eligible start remains after a round, both players automatically collide there. The usual collision rule creates a permanent unowned grave and reduces ground health by one. That collision counts as a separate resolved round. When the last start is resolved, a draft with knights proceeds to the main-phase endgame check, which determines whether play enters Main or finishes immediately. A draft with zero knights requires a restart. **Restart Draft** and **Restart Match** each restore the same map, initial ground health, an empty board, and the original starting positions. Multiplayer restart authority remains open.

#### Fixed Draft

- Each player receives a predefined number of starting selections/knights.
- Players choose until they have used their allocation.
- Collisions create graves.

**Open decision:** In Fixed Draft, decide whether a collision consumes the selecting player's allocation or grants a replacement selection. This must be specified before Fixed Draft is implemented.

The host should be able to choose the opening mode for a multiplayer session, subject to any restrictions imposed by ranked matchmaking or a competitive ruleset.

Unlimited Draft is implemented. Fixed Draft is planned.

### 5.2 Main phase: available selections

A player may select squares around their existing knights and castles. Both knights and castles expand the available area to all eight neighbouring squares, including diagonals.

The existing pieces remain in place. The selection produces an outcome based on the selected square:

| Selected square state | Result |
|---|---|
| Empty playable square | Create a new knight for the selecting player |
| Enemy knight | Attempt to capture it; ground health decreases |
| Own knight | Upgrade it to a castle |
| Enemy castle | Cannot be conquered |
| Rock or river | Cannot be selected |

A castle is stationary and cannot be conquered under the current rules. It still expands the player's selectable area.

**Clarified main-phase selection rule:** Knights and castles influence the **eight immediately adjacent squares** (including diagonals), not chess-knight L-shaped destinations. A player's **own knight is always a potential upgrade target**, even if it has no other friendly piece adjacent to it. This exception does not permit an upgrade on a square already classified as secured. Neither player may select **any** secured square, including an empty square or own knight; secured-square exclusion does not apply during the opening draft.

The domain derives each player's legal selection set separately from the resolved board and its territory analysis. The local session uses these rules to run the main phase and publish final scores. See [Main-Phase Rules and Architecture](MainPhaseArchitecture.md) for details.

### 5.3 Ground health, direct collisions, and capture

Ground health belongs to the square and never becomes negative. Creating a knight on empty ground or upgrading a friendly knight to a castle preserves ground health.

A direct collision occurs when both players legally select the same square in the same simultaneous turn. It decreases the square's existing ground health by one, with a floor of zero, preserves the terrain, and always creates an unowned grave. A direct collision at health 3, 2, or 1 therefore produces a grave at health 2, 1, or 0 respectively. The grave remains unusable at any health.

A capture occurs when a player legally selects an enemy knight while the opponent selects a different square. The square loses one ground health. While health remains positive, a knight belonging to the attacker occupies the square. At zero, the square becomes an unowned grave.

**Tested main-phase outcomes**

| Pre-turn selections | Expected result on the defender's square |
|---|---|
| Legal capture at health 3, opponent selects elsewhere | Attacker's knight at health 2 |
| Legal capture at health 2, opponent selects elsewhere | Attacker's knight at health 1 |
| Legal capture at health 1, opponent selects elsewhere | Unowned grave at health 0 |
| Both players legally select the same square at health 3 | Direct collision creates an unowned grave at health 2 |

Ground health does not regenerate under the currently specified actions. Whether a future mechanic restores health remains open. Starting from a normal opening, living pieces never occupy zero-health ground.

### 5.4 Simultaneous move resolution

When both players submit a main-phase selection for the same turn:

- Validate both submissions against the same pre-turn game state.
- Resolve the pair as one logical turn.
- If both legally selected the same square, direct collision takes precedence over the ordinary placement, capture, or upgrade outcome.
- The result must not depend on which player's submission happens to be processed first.

When the selections target **different squares**, both legal actions resolve independently using the pre-turn board. An action that removes a piece does not retroactively invalidate the other player's different-square selection. A same-square direct collision takes precedence over both ordinary actions. Resolution must remain independent of submission order.

## 6. Territorial control, end of game and scoring

The main phase derives **strategic territorial control separately from physical piece ownership**. A region may be secured for a player even if some squares are empty. Secured squares are unavailable to both players. The Godot scene displays their control with a faction-colored tint.

### 6.1 Conservative territorial classification v1

Perform an eight-directional connected-component flood fill through playable empty cells and knights, but **never through graves, castles, or inaccessible terrain**. For a component, record the owners of knights inside it and adjacent castles encountered as **terminal boundaries** (include the castle's owner without traversing the castle). Classify every traversable square in the component identically:

- Player One's pieces only: secured by Player One.
- Player Two's pieces only: secured by Player Two.
- Both players' pieces: contested.
- Neither player's pieces: neutral, unreachable by either player under the present board configuration.

Every castle is secured by its own player independently; graves are permanently neutral. The traversal deliberately treats a knight on ground health 1 as traversable even when an actual capture would produce a blocking grave. This may leave an effectively inaccessible region **visually contested**, and is an intentional, understandable simplification for playtesting.

Regions disconnected from every starting position by fixed inaccessible terrain remain neutral. The current domain analyzes the full board each time. A grave can sever a component's last contact with a piece and make it neutral. Castles split traversable regions, and any component that still borders a castle counts that owner's influence.

### 6.2 Endgame condition

**The match ends immediately when either player has no legal main-phase selection remaining.** No passing mechanic is needed. Check upon entering Main and after every main-phase resolution. Legal selections exclude secured territory for both players and include the own-knight upgrade exception on contested knights.

Under this algorithm, each contested component supplies a legal selection to both players, and only contested squares can be selected. Both players run out of selections together under these rules, and an ended match has no contested playable squares.

### 6.3 Final scoring

At match completion, count **all secured playable squares** for the corresponding player, including secured empty squares, knights, and castles. Neutral playable squares, graves, and inaccessible terrain score zero. The greater total wins. Equal totals draw. The details of the analyzer and invariants are in [Main-Phase Rules and Architecture](MainPhaseArchitecture.md).

## 7. Maps

Local play starts on one of 50 uniformly selected [predefined maps](MapFormat.md). The maps range from 3 × 3 to 12 × 12 and vary in terrain and candidate starts. Either restart action reuses the current map.

Maps are data-driven and should not be hard-coded into board logic.

A map should support at least:

- Map identifier and display name.
- Width and height.
- Terrain layout.
- Valid starting-position locations.
- Optional fixed starting allocation for Fixed Draft.
- Metadata needed for map selection, classification, and validation.

Starting positions and the number of starting knights are separate concepts and must not be conflated.

### 7.1 Map categories

Two categories are planned:

**Casual / unrestricted maps**
- Prioritize creativity and player freedom.
- May be asymmetrical.
- May contain disconnected playable areas.
- Should not be rejected merely because a fairness heuristic identifies an imbalance.

**Competitive / ranked maps**
- Must satisfy basic structural validity.
- May undergo additional fairness analysis or eligibility checks.
- Fairness checks should be guidance or ranked eligibility criteria, not arbitrary restrictions on all maps.
- The simultaneous opening draft is itself a balancing mechanism: players can contest desirable starting positions.

The validation system should distinguish structural validity (whether the map can function) from competitive suitability (whether it meets a particular ranked ruleset).

### 7.2 Map authoring

Predefined maps should be supported first. The architecture should nevertheless allow user-created maps without requiring a redesign of the board system.

A basic map editor is planned relatively early and should eventually allow a user to:

- Set board width and height.
- Paint terrain.
- Assign valid starting positions.
- Set the fixed starting allocation where relevant.
- Validate the map.
- Save and load map data.

The built-in maps use JSON with terrain and starting positions encoded as rows of characters. The domain loads them into immutable map definitions before starting a match. A format and versioning strategy for user-created maps remains an editor design decision.

Online map sharing is a later feature.

### 7.3 Procedural maps

Procedural map generation is a possible later feature. Multiplayer procedural maps must be reproducible, for example through a deterministic seed and a versioned generation algorithm.

## 8. Game modes and multiplayer direction

- The game is designed for two players.
- The host selects the opening mode for a session, unless a ranked ruleset fixes it.
- Casual/custom sessions should allow more flexibility.
- Ranked play may enforce standardized rules and map eligibility.

Multiplayer should be server-authoritative, particularly for ranked matches. The server must validate submitted actions and resolve turns using the same game rules as the client.

The multiplayer backend remains open, with Nakama as a candidate. The precise network protocol, hosting model, persistence requirements, reconnect behaviour, and anti-cheat strategy are not yet specified.

## 9. Technical architecture direction

Implement the core game rules in engine-independent C# classes. Keep the domain free of Godot dependencies, including scenes, nodes, rendering, and input handling, as well as networking dependencies.

A proposed separation is:

1. **Domain layer:** Board, tiles, terrain, pieces, ownership, ground health, legal-action rules, turn resolution, game phases, end conditions, and scoring.
2. **Application layer:** Coordinates match use cases, commands, turn submission, and orchestration.
3. **Infrastructure layer:** Persistence, network transport, backend integration, and external services.
4. **Godot presentation layer** handles scenes, nodes, rendering, input, UI, animations, and audio. It passes player actions to the game logic and presents the resulting state.

This is a conceptual architecture. Choose the final .NET project and assembly layout during implementation. The domain must be buildable and testable without the Godot editor or runtime. Avoid creating layers or abstractions that do not provide a real boundary or testability benefit.

AI and multiplayer should call the same domain rules rather than implement their own versions of move legality.

For simultaneous turns, the authoritative resolver must evaluate both players' selections against the same pre-turn snapshot and produce a deterministic next state.

## 10. Professional engineering and portfolio standards

Castrum Tactics is explicitly intended to serve as evidence of the developer's skills to potential employers. Professional quality is therefore a project requirement.

### 10.1 Code quality

- Use clear, consistent C# naming and formatting conventions.
- Prefer small, cohesive types and methods with explicit responsibilities.
- Apply SOLID principles where they improve the design; do not apply patterns mechanically.
- Encapsulate state and protect domain invariants.
- Use appropriate types and avoid unnecessary primitive-value ambiguity.
- Handle invalid input and errors deliberately.
- Avoid duplicated game rules and hidden dependencies.
- Keep code readable and maintainable; comments should explain intent or non-obvious decisions rather than restate code.

### 10.2 Testing

- Use xUnit to unit-test the engine-independent C# game logic without launching Godot.
- Test edge cases and simultaneous-resolution scenarios.
- Add application/integration tests where they provide meaningful confidence.
- Treat deterministic game-state transitions as especially important.
- Run tests as part of the development workflow and, where practical, automatically in CI.

### 10.3 Git and collaboration

- Use Git and GitHub as the source-control foundation.
- Commit in small, coherent units with meaningful messages.
- Use feature branches for non-trivial changes.
- Review diffs before merging.
- Keep the main branch in a buildable, understandable state.
- Use issues and pull requests (or equivalent review records) to describe work, scope, and acceptance criteria.
- Avoid committing generated files, local settings, secrets, or unnecessary binary assets.

### 10.4 Documentation and decisions

Documentation should be kept accurate as the project evolves. Planned documents include:

- `README.md` — project overview, features, setup, build/test instructions, screenshots, and portfolio presentation.
- `Documentation/GameDesign.md` — gameplay rules and design.
- `Documentation/TechnicalArchitecture.md` — architecture and major technical boundaries.
- `Documentation/DevelopmentRoadmap.md` — milestones and planned work.
- `Documentation/DecisionLog.md` — significant decisions, alternatives, rationale, and consequences.

Use Architecture Decision Records (ADRs) or a lightweight equivalent for consequential technical choices.

### 10.5 Code-review standard

Code, commit messages, documentation, and project structure should receive candid, constructive review against professional expectations.

Review findings should distinguish:

- **Critical / must fix:** correctness, security, serious design, or reliability problems.
- **Improvement / should fix:** maintainability, clarity, conventions, or avoidable technical debt.
- **Suggestion / could improve:** optional refinements or alternative approaches.

Feedback should explain what is wrong, why it matters, what a professional alternative looks like, and how to correct it. The reviewer should distinguish objective issues from subjective style preferences. The goal is for the developer to understand and be able to defend the resulting implementation, not merely accept generated code.

## 11. Development roadmap

The current proposed order is:

1. Establish the repository, Godot 4 .NET project with the Compatibility renderer, VS Code development setup, coding conventions, and documentation baseline.
2. Export and launch a minimal Windows build that opens a Godot scene and executes a small C# script.
3. Export a minimal Android APK, install and launch it on a physical Android device, and verify that the scene renders and the C# script runs. Record the tested toolchain, device, Android version, and any blockers. Verify both platform builds before beginning substantial gameplay implementation.
4. Implement the engine-independent core domain model and a basic 5×5 board. Add xUnit coverage as rules are implemented.
5. Generalize to configurable board dimensions.
6. Add terrain and predefined map data.
7. Implement the opening phase, beginning with Unlimited Draft.
8. Implement main-phase actions, captures, upgrades, simultaneous resolution, and endgame.
9. Expand automated test coverage and strengthen the core rules.
10. Build a basic map editor.
11. Implement AI opponents using the shared game logic.
12. Select the multiplayer backend, verify its Android integration, and implement multiplayer and server-authoritative validation.
13. Add accounts, rankings, leaderboards, and other online features if justified.

The sequence may change as dependencies become clearer. Multiplayer and AI should not be allowed to force duplicated or premature implementations of the core rules.

## 12. Open decisions and questions

The following items should be resolved before the corresponding feature is implemented:

1. **Fixed Draft collision accounting:** Does a collision consume a player's allocated pick, or does that player receive a replacement?
2. **Ground health lifecycle:** Is health permanently reduced throughout the match, with no restoration?
3. **Territory v1 playtesting:** Revisit conservative connectivity versus exact reachability only if playtesting demonstrates a need.
4. **Ground health restoration:** Decide whether any future mechanics can regenerate health; no such mechanic is specified at present.
5. **Fixed Draft eligibility and completion:** Precisely which map squares are eligible starting positions, and how does Fixed Draft terminate?
6. **Ranked map validation:** Which checks are mandatory structural rules, and which are optional fairness indicators?
7. **Map data format:** Confirm the representation and versioning strategy for built-in and user-created maps.
8. **Multiplayer deployment:** Select a free, maintainable hosting approach and verify its actual cost and operational constraints.
9. **Licensing and distribution:** Select an appropriate open-source license for the repository and verify third-party asset and platform terms.
10. **Multiplayer backend:** Evaluate Nakama and other viable options against the shared-rules requirement, Android compatibility, and the €0 budget.
11. **Android validation baseline:** Confirm the Godot and .NET versions and matching export templates, and choose a physical test device and minimum supported Android version.
12. **Multiplayer draft restart:** Decide who can initiate or approve a restart after an opening ends without knights.

Do not silently invent answers to these questions during implementation. Record decisions in the design document and decision log when agreed.

## 13. Definition of done

A feature should generally be considered complete when:

- Its expected behaviour and acceptance criteria are documented.
- The implementation follows the agreed architecture and coding conventions.
- Relevant automated tests pass.
- Important edge cases have been considered.
- No known critical defects remain.
- Documentation is updated where necessary.
- The change has been reviewed and can be explained by the developer.
- The change is committed with a clear, meaningful message.

---

## Change history

| Version | Date | Notes |
|---|---|---|
| 0.5 | 2026-11-23 | Implemented main-phase domain rules and confirmed zero points for inaccessible terrain. |
| 0.4 | 2026-11-23 | Clarified eight-adjacent-cell range and own-knight upgrades. Adopted conservative territory flood fill, secured-square move restriction, either-player no-moves endgame, and secured-playable-square scoring; kept inaccessible-terrain scoring open. |
| 0.3 | 2026-11-23 | Confirmed Unlimited Draft eligibility, simultaneous resolution, and restart. Defined direct collision and capture as separate ground-health transitions. |
| 0.2 | 2026-11-21 | Adopted Godot 4 .NET, C#, the Compatibility renderer, VS Code with Codex, and xUnit. Clarified the engine-independent rules architecture and open backend choice. Prioritized Windows and physical-device Android export validation. |
| 0.1 | 2026-11-19 | Initial consolidated design and engineering baseline based on project discussions and the original rules explanation. |
