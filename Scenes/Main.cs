#nullable enable

using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using Godot;

/// <summary>Presents a responsive local match, including the final board and scores, and forwards commands to the game session.</summary>
public partial class Main : Control
{
    private const string OpeningInstruction = "Select a gold dot, then submit.";
    private const string MainInstruction = "Select a gold-outlined square, then submit.";
    private const string DraftRetryInstruction = "No starting knights were placed.";
    private const string InstructionStyle = "SecondaryLabel";
    private const string RejectionStyle = "RejectionLabel";
    private const string SubmitCaption = "Submit selection";
    private const string ReviseCaption = "Revise selection";
    private static readonly (SessionCommandResult Result, string Text)[] Rejections =
    [
        (SessionCommandResult.IneligiblePosition, "Square unavailable. Pick a marked square."),
        (SessionCommandResult.StaleRound, "Turn advanced. Choose again."),
        (SessionCommandResult.WrongPhase, "Action unavailable in this phase.")
    ];
    private const string UnknownRejection = "The match command was rejected.";

    private IGameSession _session = null!;
    private VBoxContainer _frame = null!;
    private BoxContainer _layout = null!;
    private PanelContainer _controls = null!;
    private BoardRenderer _board = null!;
    private Label _phase = null!;
    private Label _status = null!;
    private Control _instructionSlot = null!;
    private Label _detail = null!;
    private BoxContainer _players = null!;
    private Button _playerOne = null!;
    private Button _playerTwo = null!;
    private HBoxContainer _scores = null!;
    private Label _playerOneScore = null!;
    private Label _playerTwoScore = null!;
    private Button _submit = null!;
    private Button _restart = null!;
    private Vector2 _playerButtonMinimum;
    private Vector2 _submitButtonMinimum;
    private PlayerId _activePlayer = PlayerId.PlayerOne;
    private GameSessionSnapshot _displayed = null!;

    /// <summary>Starts the local demonstration, applies faction accents, and binds the controls to session and layout updates.</summary>
    public override void _Ready()
    {
        _frame = GetNode<VBoxContainer>("Frame");
        _layout = GetNode<BoxContainer>("Frame/Layout");
        _controls = GetNode<PanelContainer>("Frame/Layout/Controls");
        _board = GetNode<BoardRenderer>("%BoardRenderer");
        _phase = GetNode<Label>("%Phase");
        _status = GetNode<Label>("%Status");
        _detail = GetNode<Label>("%Detail");
        _instructionSlot = BindInstructionSlot();
        _players = GetNode<BoxContainer>("%Players");
        _playerOne = GetNode<Button>("%PlayerOne");
        _playerTwo = GetNode<Button>("%PlayerTwo");
        _scores = GetNode<HBoxContainer>("%Scores");
        _playerOneScore = GetNode<Label>("%PlayerOneScore");
        _playerTwoScore = GetNode<Label>("%PlayerTwoScore");
        _submit = GetNode<Button>("%Submit");
        _restart = GetNode<Button>("%Restart");
        _session = new LocalGameSession(DemonstrationMap.Create());

        ApplyFactionAccent(_playerOne, GetNode<PanelContainer>("%ScoreOne"), PlayerId.PlayerOne);
        ApplyFactionAccent(_playerTwo, GetNode<PanelContainer>("%ScoreTwo"), PlayerId.PlayerTwo);
        _playerButtonMinimum = MeasurePlayerButtonMinimum(_playerOne, PlayerId.PlayerOne)
            .Max(MeasurePlayerButtonMinimum(_playerTwo, PlayerId.PlayerTwo));
        _submitButtonMinimum = MeasureButtonMinimum(_submit, SubmitCaption, ReviseCaption);
        _playerOne.Pressed += () => SwitchPlayer(PlayerId.PlayerOne);
        _playerTwo.Pressed += () => SwitchPlayer(PlayerId.PlayerTwo);
        _board.SelectionChanged += UpdateControls;
        _submit.Pressed += SubmitSelection;
        _restart.Pressed += RestartMatch;
        Resized += UpdateLayout;
        if (OS.HasFeature("android"))
        {
            GetViewport().SizeChanged += UpdateLayout;
        }

        UpdateLayout();
        _session.StateChanged += DisplayState;
        DisplayState(_session.Current);
    }

    /// <summary>Stops observing session and layout updates when the scene leaves the tree.</summary>
    public override void _ExitTree()
    {
        Resized -= UpdateLayout;
        if (OS.HasFeature("android"))
        {
            GetViewport().SizeChanged -= UpdateLayout;
        }

        if (_session is not null)
        {
            _session.StateChanged -= DisplayState;
        }
    }

    private void UpdateLayout()
    {
        var viewport = new Rect2(Vector2.Zero, Size);
        Rect2 safeArea = viewport;
        Vector2 tapTarget = Vector2.Zero;
        if (OS.HasFeature("android"))
        {
            Transform2D canvasToWindow = GetViewport().GetFinalTransform() * GetGlobalTransformWithCanvas();
            safeArea = canvasToWindow.AffineInverse() * (Rect2)DisplayServer.GetDisplaySafeArea();
            tapTarget = MatchLayout.GetTapTarget(canvasToWindow.Scale.Abs(), DisplayServer.ScreenGetDpi());
            _restart.CustomMinimumSize = tapTarget;
        }

        Vector2 playerMinimum = _playerButtonMinimum.Max(tapTarget);
        Vector2 submitMinimum = _submitButtonMinimum.Max(tapTarget);
        _playerOne.CustomMinimumSize = playerMinimum;
        _playerTwo.CustomMinimumSize = playerMinimum;
        _submit.CustomMinimumSize = submitMinimum;

        Rect2 content = MatchLayout.GetContentRect(viewport, safeArea);
        _frame.OffsetLeft = content.Position.X;
        _frame.OffsetTop = content.Position.Y;
        _frame.OffsetRight = content.End.X - Size.X;
        _frame.OffsetBottom = content.End.Y - Size.Y;

        bool wide = MatchLayout.UseSidebar(content.Size);
        _layout.Vertical = !wide;
        _players.Vertical = wide;
        float panelPadding = _controls.GetThemeStylebox("panel").GetMinimumSize().X;
        float sidebarWidth = Mathf.Max(MatchLayout.SidebarWidth, panelPadding + Mathf.Max(playerMinimum.X, submitMinimum.X));
        _controls.CustomMinimumSize = new Vector2(wide ? sidebarWidth : 0, 0);
        LayoutPanelText(wide ? sidebarWidth : content.Size.X, panelPadding);
    }

    private Control BindInstructionSlot()
    {
        _detail.VerticalAlignment = VerticalAlignment.Top;
        Control parent = _detail.GetParent<Control>();
        if (parent is not Container)
        {
            return parent;
        }

        // A plain control isolates the label's minimum size from the panel's container layout.
        var slot = new Control
        {
            Name = "InstructionSlot",
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        int index = _detail.GetIndex();
        parent.AddChild(slot);
        parent.MoveChild(slot, index);
        _detail.Reparent(slot, keepGlobalTransform: false);
        _detail.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        return slot;
    }

    private void LayoutPanelText(float panelWidth, float panelPadding)
    {
        float width = Mathf.Floor(panelWidth - panelPadding);
        if (width <= 0)
        {
            return;
        }

        var textBounds = new Vector2(width, -1);
        _status.CustomMaximumSize = textBounds;
        _detail.CustomMaximumSize = textBounds;

        // Size the slot independently so the label's own minimum size cannot resize the panel.
        float height = 0f;
        Measure(OpeningInstruction, InstructionStyle);
        Measure(MainInstruction, InstructionStyle);
        Measure(DraftRetryInstruction, InstructionStyle);
        Measure(WaitingFor(PlayerId.PlayerOne), InstructionStyle);
        Measure(WaitingFor(PlayerId.PlayerTwo), InstructionStyle);
        foreach (var (_, text) in Rejections)
        {
            Measure(text, RejectionStyle);
        }

        Measure(UnknownRejection, RejectionStyle);
        _instructionSlot.CustomMinimumSize = new Vector2(0, height);

        void Measure(string text, StringName style)
        {
            Font font = _detail.GetThemeFont("font", style);
            int size = _detail.GetThemeFontSize("font_size", style);
            var breaks = TextServer.LineBreakFlag.Mandatory | TextServer.LineBreakFlag.WordBound |
                TextServer.LineBreakFlag.Adaptive | _detail.AutowrapTrimFlags;
            float textHeight = font.GetMultilineStringSize(text, HorizontalAlignment.Left, width, size, -1, breaks).Y;
            float lineCount = Mathf.Round(textHeight / font.GetHeight(size));
            float spacing = _detail.GetThemeConstant("line_spacing", style);
            height = Mathf.Max(height, Mathf.Ceil(textHeight + Mathf.Max(0f, lineCount - 1f) * spacing));
        }
    }

    private static void ApplyFactionAccent(Button button, PanelContainer score, PlayerId player)
    {
        Color color = FactionPalette.GetColor(player);
        button.Icon = button.GetThemeIcon("faction_marker");
        button.AddThemeColorOverride("icon_normal_color", color);
        button.AddThemeColorOverride("icon_hover_color", color);
        button.AddThemeColorOverride("icon_pressed_color", color);
        button.AddThemeColorOverride("icon_focus_color", color);
        button.AddThemeColorOverride("icon_hover_pressed_color", color);

        var style = (StyleBoxFlat)score.GetThemeStylebox("panel").Duplicate();
        style.BorderColor = color;
        style.BorderWidthTop = 3;
        score.AddThemeStyleboxOverride("panel", style);
    }

    private void SwitchPlayer(PlayerId player)
    {
        if (_activePlayer != player)
        {
            _activePlayer = player;
            UpdateBoardView();
        }

        UpdateControls();
    }

    private void SubmitSelection()
    {
        if (_board.SelectedPosition is { } position)
        {
            SessionCommandResult result = _displayed.Phase switch
            {
                MatchPhase.Opening => _session.SubmitOpeningSelection(new(_activePlayer, position, _displayed.RoundId)),
                MatchPhase.Main => _session.SubmitMainPhaseSelection(new(_activePlayer, position, _displayed.RoundId)),
                _ => SessionCommandResult.WrongPhase
            };
            ShowRejection(result);
        }
    }

    private void RestartMatch() => ShowRejection(_session.RestartDraft(_displayed.RoundId));

    private void ShowRejection(SessionCommandResult result)
    {
        if (result != SessionCommandResult.Accepted)
        {
            DisplayState(_session.Current);
            _detail.ThemeTypeVariation = RejectionStyle;
            _detail.Text = System.Array.Find(Rejections, rejection => rejection.Result == result).Text ?? UnknownRejection;
            _instructionSlot.Show();
        }
    }

    private void DisplayState(GameSessionSnapshot snapshot)
    {
        if (snapshot.Phase == MatchPhase.Opening &&
            _displayed?.Phase is MatchPhase.OpeningRestartRequired or MatchPhase.Finished)
        {
            _activePlayer = PlayerId.PlayerOne;
        }

        _displayed = snapshot;
        UpdateBoardView();
        UpdateControls();
    }

    private void UpdateBoardView()
    {
        _board.SetBoard(_displayed.Board, _displayed.Phase == MatchPhase.Main
            ? _displayed.GetMainPhaseLegalSelections(_activePlayer)
            : _displayed.EligiblePositions, _displayed.Territory);
    }

    private void UpdateControls()
    {
        GameSessionSnapshot snapshot = _displayed;
        bool canSubmit = snapshot.Phase is MatchPhase.Opening or MatchPhase.Main;
        bool hasSubmitted = snapshot.HasSubmitted(_activePlayer);
        _detail.ThemeTypeVariation = InstructionStyle;
        UpdatePlayerButton(_playerOne, PlayerId.PlayerOne, snapshot);
        UpdatePlayerButton(_playerTwo, PlayerId.PlayerTwo, snapshot);
        _players.Visible = canSubmit;
        _submit.Visible = canSubmit;
        _submit.Disabled = !canSubmit || _board.SelectedPosition is null;
        _submit.Text = hasSubmitted ? ReviseCaption : SubmitCaption;
        _restart.Visible = snapshot.Phase is MatchPhase.OpeningRestartRequired or MatchPhase.Finished;
        _restart.Text = snapshot.Phase == MatchPhase.Finished ? "Restart Match" : "Restart Draft";
        _scores.Visible = snapshot.FinalResult is not null;
        _status.Visible = !canSubmit;
        _instructionSlot.Visible = snapshot.FinalResult is null;
        _phase.Text = snapshot.Phase switch
        {
            MatchPhase.Opening => $"Draft · Round {snapshot.ResolvedRounds + 1}",
            MatchPhase.Main => $"Main · Turn {snapshot.ResolvedMainTurns + 1}",
            MatchPhase.Finished => "Match complete",
            _ => "Draft ended"
        };

        if (snapshot.FinalResult is { } result)
        {
            _status.Text = result.Winner is { } winner ? $"{PlayerName(winner)} wins" : "Draw";
            _playerOneScore.Text = result.PlayerOneScore.ToString();
            _playerTwoScore.Text = result.PlayerTwoScore.ToString();
            return;
        }

        if (!canSubmit)
        {
            _status.Text = "Restart the draft";
            _detail.Text = DraftRetryInstruction;
            return;
        }

        PlayerId opponent = _activePlayer == PlayerId.PlayerOne ? PlayerId.PlayerTwo : PlayerId.PlayerOne;
        bool isOpening = snapshot.Phase == MatchPhase.Opening;
        _detail.Text = hasSubmitted
            ? WaitingFor(opponent)
            : isOpening ? OpeningInstruction : MainInstruction;
    }

    private void UpdatePlayerButton(Button button, PlayerId player, GameSessionSnapshot snapshot)
    {
        bool active = _activePlayer == player;
        button.SetPressedNoSignal(active);
        button.Text = PlayerButtonCaption(player, active, snapshot.HasSubmitted(player));
    }

    private static Vector2 MeasurePlayerButtonMinimum(Button button, PlayerId player) =>
        MeasureButtonMinimum(button, PlayerButtonCaption(player, active: true, submitted: false),
            PlayerButtonCaption(player, active: true, submitted: true));

    private static Vector2 MeasureButtonMinimum(Button button, params string[] captions)
    {
        // Reserve for each caption so button state changes cannot change the container's minimum size.
        string current = button.Text;
        Vector2 minimum = button.CustomMinimumSize;
        foreach (string caption in captions)
        {
            button.Text = caption;
            minimum = minimum.Max(button.GetMinimumSize());
        }

        button.Text = current;
        return minimum;
    }

    private static string PlayerButtonCaption(PlayerId player, bool active, bool submitted)
    {
        string state = submitted ? "Submitted" : "Choosing";
        return $"{PlayerName(player)}\n{(active ? "Active · " : "")}{state}";
    }

    private static string PlayerName(PlayerId player) => player == PlayerId.PlayerOne ? "Player One" : "Player Two";

    private static string WaitingFor(PlayerId player) => $"Waiting for {PlayerName(player)}.";
}
