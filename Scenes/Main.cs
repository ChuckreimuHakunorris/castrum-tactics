
using Godot;

public partial class Main : Node2D
{
    public override void _Ready()
    {
        var statusLabel = GetNode<Label>("StatusLabel");

        statusLabel.Text =
            "CASTRUM TACTICS\n\n" +
            "Android Export Test\n" +
            "C# is running successfully!";

        GD.Print("Castrum Tactics initialized successfully!");
    }
}
