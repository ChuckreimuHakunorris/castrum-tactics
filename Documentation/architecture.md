# Architecture

## Implemented

The proof of concept uses a single Godot scene. Its [`Main`](xref:Global.Main) script extends Godot's `Node2D`. When the scene is ready, it updates the on-screen status label and writes a startup message to the log. This verifies that C# runs in the exported game on Windows and Android.

## Planned

The pure C# game logic will own board state, legal moves, turn resolution, and win conditions. It should be possible to exercise the rules in .NET tests without starting Godot.

Godot scripts and scenes will handle player input, render board state, and connect the game logic to the UI and platform services. Godot code will pass player commands to the rules and display the resulting state. Dependencies will flow from Godot integration to the game logic so the rules can be shared by the local game, AI, and future multiplayer validation.

The [Game Design Document](GameDesign.md) records the planned rules and open decisions.
