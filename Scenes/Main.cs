using CastrumTactics.Domain;
using Godot;

/// <summary>Initializes the demonstration board in the main scene.</summary>
public partial class Main : Control
{
    /// <summary>Creates the initial board state and supplies it to the scene's renderer.</summary>
    public override void _Ready()
    {
        MapDefinition map = DemonstrationMap.Create();
        GetNode<BoardRenderer>("BoardRenderer").SetBoard(new BoardState(map), map.StartingPositions);
    }
}
