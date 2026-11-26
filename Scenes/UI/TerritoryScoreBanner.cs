#nullable enable

using Godot;
using CastrumTactics.Domain;

/// <summary>Displays both authoritative territory totals in faction order above the rendered grid.</summary>
public partial class TerritoryScoreBanner : Control
{
    private const float MaximumReserve = 36f;
    private const float ScoreGroupWidth = 64f;
    private const float IconSize = 17f;
    private const float ContentHeight = 28f;
    private Control _content = null!;
    private Label _one = null!;
    private Label _two = null!;
    private Label _title = null!;
    private TextureRect _oneShield = null!;
    private TextureRect _twoShield = null!;

    /// <summary>Applies the shared faction palette and binds the score display.</summary>
    public override void _Ready()
    {
        _content = GetNode<Control>("Content");
        _one = GetNode<Label>("Content/PlayerOneScore");
        _two = GetNode<Label>("Content/PlayerTwoScore");
        _title = GetNode<Label>("Content/Title");
        _oneShield = GetNode<TextureRect>("Content/PlayerOneShield");
        _twoShield = GetNode<TextureRect>("Content/PlayerTwoShield");
        _oneShield.SelfModulate = FactionPalette.GetColor(PlayerId.PlayerOne);
        _twoShield.SelfModulate = FactionPalette.GetColor(PlayerId.PlayerTwo);
        Resized += LayoutContents;
        LayoutContents();
    }

    /// <summary>Displays the current authoritative scores without changing the banner's size.</summary>
    /// <remarks>Call after the banner has entered the scene and bound its score labels in <c>_Ready</c>.</remarks>
    /// <param name="playerOneScore">The number of secured squares for Player One.</param>
    /// <param name="playerTwoScore">The number of secured squares for Player Two.</param>
    public void ShowScores(int playerOneScore, int playerTwoScore)
    {
        _one.Text = playerOneScore.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _two.Text = playerTwoScore.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Draws the themed surface at the exact width provided by the board.</summary>
    public override void _Draw() => DrawStyleBox(GetThemeStylebox("panel", "ScorePanel"), new Rect2(Vector2.Zero, Size));

    internal static float ReservedHeight(float availableHeight) => Mathf.Min(MaximumReserve, availableHeight * 0.32f);

    internal void AlignWith(Rect2 grid, float reserve)
    {
        Position = new Vector2(grid.Position.X, grid.Position.Y - reserve);
        Size = new Vector2(grid.Size.X, reserve - Mathf.Min(4f, reserve * 0.15f));
    }

    private void LayoutContents()
    {
        if (Size.X <= 0 || Size.Y <= 0) return;

        // Scale only the contents on tight boards. The panel always uses the exact grid bounds.
        float scale = Mathf.Min(1f, Mathf.Min(Size.X / (2 * ScoreGroupWidth), Size.Y / ContentHeight));
        float width = Size.X / scale;
        _content.Scale = Vector2.One * scale;
        _content.Position = new Vector2(0, (Size.Y - ContentHeight * scale) / 2);
        _oneShield.Position = new Vector2(8, (ContentHeight - IconSize) / 2);
        _twoShield.Position = new Vector2(width - 8 - IconSize, _oneShield.Position.Y);
        _one.Position = new Vector2(28, 0);
        _two.Position = new Vector2(width - ScoreGroupWidth, 0);
        _title.Position = new Vector2(ScoreGroupWidth, 0);
        _title.Size = new Vector2(width - 2 * ScoreGroupWidth, ContentHeight);
        _title.Visible = width >= 2 * ScoreGroupWidth + _title.GetMinimumSize().X + 12;
        QueueRedraw();
    }
}
