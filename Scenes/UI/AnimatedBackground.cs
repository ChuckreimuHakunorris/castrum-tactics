using Godot;

/// <summary>Fills its parent with a blue gradient using an animated or time-independent material.</summary>
/// <remarks>Instance AnimatedBackground.tscn to supply the materials and viewport anchors.</remarks>
[Tool]
public partial class AnimatedBackground : ColorRect
{
    [Export] private ShaderMaterial _animatedMaterial = null!;
    [Export] private ShaderMaterial _staticMaterial = null!;
    private bool _motionEnabled = true;

    /// <summary>Gets or sets whether the gradient and drifting lights animate. Defaults to true.</summary>
    /// <remarks>Set this on a scene instance or at runtime. Disabling motion selects a shader without TIME.</remarks>
    [Export]
    public bool MotionEnabled
    {
        get => _motionEnabled;
        set
        {
            _motionEnabled = value;
            if (IsNodeReady())
            {
                ApplyMaterial();
            }
        }
    }

    /// <summary>Applies the configured material after the scene instance has loaded its exports.</summary>
    public override void _Ready() => ApplyMaterial();

    private void ApplyMaterial() => Material = MotionEnabled ? _animatedMaterial : _staticMaterial;
}
