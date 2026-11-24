#nullable enable

using System.Linq;
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
    private const string RestartMatchCaption = "Restart Match";
    private const string RestartDraftCaption = "Restart Draft";
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
    private StableInstructionLayout _instruction = null!;
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
    private Vector2 _restartButtonMinimum;
    private float _scoreWidth;
    private PlayerId _activePlayer = PlayerId.PlayerOne;
    private GameSessionSnapshot _displayed = null!;

    /// <summary>Gets or sets the built-in map identifier for deterministic scene startup.</summary>
    /// <remarks>A blank value selects randomly. The <c>--map</c> user argument takes precedence.</remarks>
    [Export]
    public string MapIdOverride { get; set; } = "";

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
        _instruction = new StableInstructionLayout(_status, _detail,
            [OpeningInstruction, MainInstruction, DraftRetryInstruction, WaitingFor(PlayerId.PlayerOne), WaitingFor(PlayerId.PlayerTwo)],
            InstructionStyle, [.. Rejections.Select(rejection => rejection.Text), UnknownRejection], RejectionStyle);
        _players = GetNode<BoxContainer>("%Players");
        _playerOne = GetNode<Button>("%PlayerOne");
        _playerTwo = GetNode<Button>("%PlayerTwo");
        _scores = GetNode<HBoxContainer>("%Scores");
        _playerOneScore = GetNode<Label>("%PlayerOneScore");
        _playerTwoScore = GetNode<Label>("%PlayerTwoScore");
        _submit = GetNode<Button>("%Submit");
        _restart = GetNode<Button>("%Restart");
        PredefinedMap map;
        try
        {
            map = SelectMap();
        }
        catch (System.Exception ex) when (ex is System.ArgumentException
            or System.Collections.Generic.KeyNotFoundException or System.IO.InvalidDataException)
        {
            GD.PushError(ex.Message);
            GetTree().Quit(1);
            return;
        }

        _session = new LocalGameSession(map.Definition);
        string mapDescription = $"{map.Name} ({map.Definition.Width} × {map.Definition.Height})";
        _phase.MouseFilter = MouseFilterEnum.Pass;
        _phase.TooltipText = mapDescription;
        GetWindow().Title = $"Castrum Tactics — {mapDescription}";
        GD.Print($"Selected map '{map.Id}' ({map.Definition.Width} × {map.Definition.Height}).");

        var scoreOne = GetNode<PanelContainer>("%ScoreOne");
        var scoreTwo = GetNode<PanelContainer>("%ScoreTwo");
        ApplyFactionAccent(_playerOne, scoreOne, PlayerId.PlayerOne);
        ApplyFactionAccent(_playerTwo, scoreTwo, PlayerId.PlayerTwo);
        _scoreWidth = scoreOne.GetCombinedMinimumSize().X + scoreTwo.GetCombinedMinimumSize().X
            + _scores.GetThemeConstant("separation");
        _layout.AddThemeConstantOverride("separation", MatchLayout.PanelSeparation);
        _playerButtonMinimum = MeasurePlayerButtonMinimum(_playerOne, PlayerId.PlayerOne)
            .Max(MeasurePlayerButtonMinimum(_playerTwo, PlayerId.PlayerTwo));
        _submitButtonMinimum = StableInstructionLayout.MeasureButtonMinimum(_submit, SubmitCaption, ReviseCaption);
        _restartButtonMinimum = StableInstructionLayout.MeasureButtonMinimum(_restart, RestartDraftCaption, RestartMatchCaption);
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

    private PredefinedMap SelectMap()
    {
        string[] args = OS.GetCmdlineUserArgs();
        int option = System.Array.IndexOf(args, "--map");
        if (option >= 0)
        {
            if (option + 1 >= args.Length || args[option + 1].StartsWith("-", System.StringComparison.Ordinal))
            {
                throw new System.ArgumentException("Pass a predefined map identifier after --map.");
            }

            return PredefinedMapCatalog.BuiltIn.Get(args[option + 1]);
        }

        return string.IsNullOrEmpty(MapIdOverride)
            ? PredefinedMapCatalog.BuiltIn.SelectRandom()
            : PredefinedMapCatalog.BuiltIn.Get(MapIdOverride);
    }

    /// <summary>Stops observing session and layout updates when the scene leaves the tree.</summary>
    public override void _ExitTree()
    {
        if (_session is null)
        {
            return;
        }

        Resized -= UpdateLayout;
        if (OS.HasFeature("android"))
        {
            GetViewport().SizeChanged -= UpdateLayout;
        }

        _session.StateChanged -= DisplayState;
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
        }

        Vector2 playerMinimum = _playerButtonMinimum.Max(tapTarget);
        Vector2 submitMinimum = _submitButtonMinimum.Max(tapTarget);
        _playerOne.CustomMinimumSize = playerMinimum;
        _playerTwo.CustomMinimumSize = playerMinimum;
        _submit.CustomMinimumSize = submitMinimum;
        _restart.CustomMinimumSize = _restartButtonMinimum.Max(tapTarget);

        Rect2 content = MatchLayout.GetContentRect(viewport, safeArea);
        _frame.OffsetLeft = content.Position.X;
        _frame.OffsetTop = content.Position.Y;
        _frame.OffsetRight = content.End.X - Size.X;
        _frame.OffsetBottom = content.End.Y - Size.Y;

        // Use the largest control requirements across phases to keep the breakpoint independent of match state.
        float panelPadding = _controls.GetThemeStylebox("panel").GetMinimumSize().X;
        float contentWidth = Mathf.Max(Mathf.Max(playerMinimum.X, submitMinimum.X),
            Mathf.Max(_restart.CustomMinimumSize.X, _scoreWidth));
        float sidebarWidth = Mathf.Max(MatchLayout.SidebarWidth, panelPadding + contentWidth);
        bool wide = MatchLayout.UseSidebar(content.Size, sidebarWidth);
        _layout.Vertical = !wide;
        _players.Vertical = wide;
        _controls.CustomMinimumSize = new Vector2(wide ? sidebarWidth : 0, 0);
        _instruction.Layout(Mathf.Floor((wide ? sidebarWidth : content.Size.X) - panelPadding));
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
            _instruction.Slot.Show();
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
        _restart.Text = snapshot.Phase == MatchPhase.Finished ? RestartMatchCaption : RestartDraftCaption;
        _scores.Visible = snapshot.FinalResult is not null;
        _status.Visible = !canSubmit;
        _instruction.Slot.Visible = snapshot.FinalResult is null;
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
        StableInstructionLayout.MeasureButtonMinimum(button, PlayerButtonCaption(player, active: true, submitted: false),
            PlayerButtonCaption(player, active: true, submitted: true));

    private static string PlayerButtonCaption(PlayerId player, bool active, bool submitted)
    {
        string state = submitted ? "Submitted" : "Choosing";
        return $"{PlayerName(player)}\n{(active ? "Active · " : "")}{state}";
    }

    private static string PlayerName(PlayerId player) => player == PlayerId.PlayerOne ? "Player One" : "Player Two";

    private static string WaitingFor(PlayerId player) => $"Waiting for {PlayerName(player)}.";
}
