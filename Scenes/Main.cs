#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CastrumTactics.AI;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using CastrumTactics.Match;
using Godot;

/// <summary>Presents either local input mode and forwards selections to the authoritative session.</summary>
public partial class Main : Control
{
    private const string SelectionInstruction = "Select a target. Select it again or press Submit.";
    private const string DraftRetryInstruction = "No starting knights were placed.";
    private const string CastleDraftRetryInstruction = "No starting castles were placed.";
    private const string CpuWaiting = "Waiting for CPU. You can revise.";
    private const string CpuFailure = "CPU could not move. Start a New Match.";
    private const string InstructionStyle = "MatchInstruction";
    private const string RejectionStyle = "MatchRejection";
    private const float DesktopButtonHeight = 34f;
    private const string SubmitCaption = "Submit selection";
    private const string ReviseCaption = "Revise selection";
    private const string RestartMatchCaption = "Restart Match";
    private const string RestartDraftCaption = "Restart Draft";
    private const string UnknownRejection = "The match command was rejected.";
    private static readonly (SessionCommandResult Result, string Text)[] Rejections =
    [
        (SessionCommandResult.IneligiblePosition, "Square unavailable. Pick a marked square."),
        (SessionCommandResult.StaleRound, "Turn advanced. Choose again."),
        (SessionCommandResult.WrongPhase, "Action unavailable in this phase.")
    ];

    private readonly MatchLifetime _match = new();
    private MatchLaunchOptions _launch = null!;
    private MatchConfiguration? _startupConfiguration;
    private MatchConfiguration _configuration = null!;
    private int? _startupSeed;
    private string? _startupMap;
    private PredefinedMap? _mapChoice;
    private MatchPresentation? _presentation;
    private readonly Dictionary<string, string> _visualOverrides = new(StringComparer.Ordinal);
    private bool _initialized;
    private GameSessionSnapshot _displayed = null!;
    private PlayerId _activePlayer;

    private VBoxContainer _frame = null!;
    private BoxContainer _layout = null!;
    private PanelContainer _controls = null!;
    private BoardRenderer _board = null!;
    private TerritoryScoreBanner _banner = null!;
    private Label _phase = null!;
    private Label _activeMap = null!;
    private Label _ruleSetBadge = null!;
    private Label _status = null!;
    private StableInstructionLayout _instruction = null!;
    private Label _detail = null!;
    private BoxContainer _players = null!;
    private Button _playerOne = null!;
    private Button _playerTwo = null!;
    private Button _submit = null!;
    private Button _restart = null!;
    private BoxContainer _actions = null!;
    private Button _newMatch = null!;
    private NewMatchDialog _newMatchDialog = null!;
    private Vector2 _playerButtonMinimum;
    private Vector2 _submitButtonMinimum;
    private Vector2 _restartButtonMinimum;

    /// <summary>Gets or sets the startup input mode when no code or command-line override is supplied.</summary>
    [Export]
    public GameMode Mode { get; set; } = GameMode.VersusCpu;

    /// <summary>Gets or sets the startup CPU difficulty when no code configuration is supplied.</summary>
    [Export]
    public CpuDifficulty Difficulty { get; set; } = CpuDifficulty.Normal;

    /// <summary>Gets or sets the startup ruleset, defaulting to the official Knight Support rules.</summary>
    [Export]
    public RuleSet Rules { get; set; } = RuleSet.KnightSupport;

    /// <summary>Gets or sets the human faction for startup in Versus CPU.</summary>
    [Export]
    public PlayerId HumanPlayer { get; set; } = PlayerId.PlayerOne;

    /// <summary>Gets or sets the built-in map identifier for deterministic scene startup.</summary>
    /// <remarks>A blank value selects randomly. The <c>--map</c> user argument takes precedence at startup.
    /// A confirmed New Match choice supersedes the startup preference.</remarks>
    [Export]
    public string MapIdOverride { get; set; } = "";

    /// <summary>Configures the initial match before adding the scene to the tree.</summary>
    /// <param name="configuration">The immutable ruleset, mode, difficulty and input owners.</param>
    /// <param name="mapId">A catalog identifier. Null uses the editor setting and an empty value selects randomly.</param>
    /// <param name="randomSeed">An optional deterministic seed for the CPU in each new match.</param>
    /// <remarks>New Match can replace the map choice and CPU difficulty. The configured ruleset remains active.</remarks>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="InvalidOperationException">The scene has already initialized.</exception>
    public void Configure(MatchConfiguration configuration, string? mapId = null, int? randomSeed = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (_initialized) throw new InvalidOperationException("Configure the match before adding the scene to the tree.");
        _startupConfiguration = configuration;
        _startupMap = mapId;
        _startupSeed = randomSeed;
    }

    /// <summary>Initializes the selected input mode, session, and responsive controls.</summary>
    public override void _Ready()
    {
        _initialized = true;
        try
        {
            var configuration = _startupConfiguration ?? new MatchConfiguration(Mode, HumanPlayer, Difficulty, Rules);
            _launch = MatchLaunchOptions.Parse(OS.GetCmdlineUserArgs(), configuration, _startupMap ?? MapIdOverride, _startupSeed);
            _configuration = _launch.Configuration;
            _mapChoice = _launch.GetInitialMapChoice();
        }
        catch (Exception ex) when (ex is ArgumentException or System.Collections.Generic.KeyNotFoundException or System.IO.InvalidDataException)
        {
            GD.PushError(ex.Message);
            GetTree().Quit(1);
            return;
        }

        BindControls();
        StartMatch(_configuration);
    }

    /// <summary>Consumes completed CPU work on the scene and session's owning thread.</summary>
    /// <param name="delta">Elapsed time since the previous frame.</param>
    public override void _Process(double delta)
    {
        if (_match.Session is not { } session || _match.Cpu is not { } cpu) return;
        Exception? previousFailure = cpu.Failure;
        cpu.Pump(session);
        if (cpu.Failure is { } failure && !ReferenceEquals(previousFailure, failure))
        {
            GD.PushError(failure.ToString());
            UpdateControls();
        }
    }

    /// <summary>Detaches the session and invalidates worker requests before the scene exits.</summary>
    public override void _ExitTree()
    {
        if (_match.Session is null) return;
        _match.Detach(DisplayState);
        _presentation = null;
        Resized -= UpdateLayout;
        if (OS.HasFeature("android")) GetViewport().SizeChanged -= UpdateLayout;
    }

    private void BindControls()
    {
        _frame = GetNode<VBoxContainer>("Frame");
        _layout = GetNode<BoxContainer>("Frame/Layout");
        _controls = GetNode<PanelContainer>("Frame/Layout/Controls");
        _board = GetNode<BoardRenderer>("%BoardRenderer");
        _banner = GetNode<TerritoryScoreBanner>("Frame/Layout/BoardRenderer/TerritoryScoreBanner");
        _phase = GetNode<Label>("%Phase");
        _activeMap = GetNode<Label>("%ActiveMap");
        _ruleSetBadge = GetNode<Label>("%RuleSetBadge");
        _status = GetNode<Label>("%Status");
        _detail = GetNode<Label>("%Detail");
        _instruction = new StableInstructionLayout(_status, _detail,
            [SelectionInstruction, DraftRetryInstruction, CastleDraftRetryInstruction,
                WaitingFor(PlayerId.PlayerOne), WaitingFor(PlayerId.PlayerTwo), CpuWaiting],
            InstructionStyle, [.. Rejections.Select(rejection => rejection.Text), UnknownRejection, CpuFailure], RejectionStyle);
        _players = GetNode<BoxContainer>("%Players");
        _playerOne = GetNode<Button>("%PlayerOne");
        _playerTwo = GetNode<Button>("%PlayerTwo");
        _submit = GetNode<Button>("%Submit");
        _restart = GetNode<Button>("%Restart");
        _actions = GetNode<BoxContainer>("%Actions");
        _newMatch = GetNode<Button>("%NewMatch");
        _newMatchDialog = GetNode<NewMatchDialog>("ConfirmNewMatch");
        bool versusCpu = _configuration.CpuPlayer.HasValue;
        _newMatchDialog.Initialize(versusCpu, LoadVisuals);

        var panel = (StyleBoxFlat)_controls.GetThemeStylebox("panel").Duplicate();
        foreach (Side side in new[] { Side.Left, Side.Top, Side.Right, Side.Bottom })
            panel.SetContentMargin(side, 6);
        _controls.AddThemeStyleboxOverride("panel", panel);
        ApplyFactionAccent(_playerOne, PlayerId.PlayerOne, versusCpu);
        ApplyFactionAccent(_playerTwo, PlayerId.PlayerTwo, versusCpu);
        _layout.AddThemeConstantOverride("separation", MatchLayout.PanelSeparation);
        _playerButtonMinimum = MeasurePlayerButtonMinimum(_playerOne, PlayerId.PlayerOne)
            .Max(MeasurePlayerButtonMinimum(_playerTwo, PlayerId.PlayerTwo));
        _submitButtonMinimum = StableInstructionLayout.MeasureButtonMinimum(_submit, SubmitCaption, ReviseCaption);
        _restartButtonMinimum = StableInstructionLayout.MeasureButtonMinimum(_restart, RestartDraftCaption, RestartMatchCaption);
        _playerOne.Pressed += () => SwitchPlayer(PlayerId.PlayerOne);
        _playerTwo.Pressed += () => SwitchPlayer(PlayerId.PlayerTwo);
        _board.SelectionChanged += UpdateControls;
        _board.SelectionConfirmed += SubmitSelection;
        _submit.Pressed += SubmitSelection;
        _restart.Pressed += RestartMatch;
        _newMatch.Pressed += () => _newMatchDialog.Open(_mapChoice, _configuration.Difficulty,
            MatchConfiguration.IsPlayable(_displayed.Phase));
        _newMatchDialog.Confirmed += StartNewMatch;

        Resized += UpdateLayout;
        if (OS.HasFeature("android")) GetViewport().SizeChanged += UpdateLayout;
    }

    private void StartMatch(MatchConfiguration configuration)
    {
        PredefinedMap map = _mapChoice ?? PredefinedMapCatalog.BuiltIn.SelectRandom();
        _match.Detach(DisplayState);
        _presentation = null;
        _configuration = configuration;
        _activePlayer = configuration.HumanPlayer;
        if (configuration.Mode == GameMode.LocalMultiplayer) _activePlayer = PlayerId.PlayerOne;
        _displayed = null!;

        var session = _match.Create(map.Definition, configuration, _launch.Seed);
        _presentation = new MatchPresentation(session);
        _board.SetVisuals(LoadVisuals(map));

        string mapDescription = NewMatchDialog.Describe(map);
        _activeMap.Text = mapDescription;
        _phase.TooltipText = $"{map.Name} ({map.Definition.Width} × {map.Definition.Height})";
        GetWindow().Title = $"Castrum Tactics — {mapDescription}";
        GD.Print($"Selected map '{map.Id}' ({map.Definition.Width} × {map.Definition.Height}).");

        // Publish the battlefield captions before attaching the observer and rendering the first snapshot.
        session.StateChanged += DisplayState;
        SetProcess(_match.Cpu is not null);
        DisplayState(session.Current);
        UpdateLayout();
    }

    // Scene hosts can inject an unshipped descriptor before the first match is composed.
    internal void SetVisualDescriptor(string mapId, string json)
    {
        if (_initialized) throw new InvalidOperationException("Supply visual descriptors before the scene is ready.");
        _visualOverrides[mapId] = json;
    }

    private BoardVisuals? LoadVisuals(PredefinedMap map)
    {
        string? source = _visualOverrides.GetValueOrDefault(map.Id) ?? BoardVisuals.ReadEmbedded(map.Id);
        return BoardVisuals.Load(map, source, message => GD.PushWarning(message));
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

        bool versusCpu = _configuration.CpuPlayer.HasValue;
        var actionMinimum = new Vector2(0, DesktopButtonHeight).Max(tapTarget);
        Vector2 playerMinimum = versusCpu ? _playerButtonMinimum : _playerButtonMinimum.Max(actionMinimum);
        Vector2 submitMinimum = _submitButtonMinimum.Max(actionMinimum);
        _playerOne.CustomMinimumSize = playerMinimum;
        _playerTwo.CustomMinimumSize = playerMinimum;
        _submit.CustomMinimumSize = submitMinimum;
        _restart.CustomMinimumSize = _restartButtonMinimum.Max(actionMinimum);
        _newMatch.CustomMinimumSize = actionMinimum;
        Rect2 content = MatchLayout.GetContentRect(viewport, safeArea);
        _newMatchDialog.Fit(content, actionMinimum);
        _frame.OffsetLeft = content.Position.X;
        _frame.OffsetTop = content.Position.Y;
        _frame.OffsetRight = content.End.X - Size.X;
        _frame.OffsetBottom = content.End.Y - Size.Y;

        // Use the longest captions so a state change never takes space away from the board.
        float panelPadding = _controls.GetThemeStylebox("panel").GetMinimumSize().X;
        float contentWidth = Mathf.Max(Mathf.Max(playerMinimum.X, submitMinimum.X), _restart.CustomMinimumSize.X);
        contentWidth = Mathf.Max(contentWidth, _newMatch.GetCombinedMinimumSize().X);
        float sidebarWidth = Mathf.Max(MatchLayout.SidebarWidth, panelPadding + contentWidth);
        bool wide = MatchLayout.UseSidebar(content.Size, sidebarWidth);
        _layout.Vertical = !wide;
        _players.Vertical = wide;
        _actions.Vertical = wide;
        _controls.CustomMinimumSize = new Vector2(wide ? sidebarWidth : 0, 0);
        _instruction.Layout(Mathf.Floor((wide ? sidebarWidth : content.Size.X) - panelPadding));
    }

    private static void ApplyFactionAccent(Button button, PlayerId player, bool passive)
    {
        Color color = FactionPalette.GetColor(player);
        button.Icon = button.GetThemeIcon("faction_marker");
        foreach (string state in new[] { "normal", "hover", "pressed", "focus", "hover_pressed", "disabled" })
            button.AddThemeColorOverride($"icon_{state}_color", color);
        if (passive)
        {
            button.AddThemeStyleboxOverride("disabled", button.GetThemeStylebox("normal"));
            button.AddThemeColorOverride("font_disabled_color", button.GetThemeColor("font_color"));
            button.Disabled = true;
            button.MouseFilter = MouseFilterEnum.Ignore;
            button.FocusMode = FocusModeEnum.None;
        }
    }

    private void SwitchPlayer(PlayerId player)
    {
        if (!_configuration.AllowsHumanInput(player)) return;
        if (_activePlayer != player)
        {
            _activePlayer = player;
            UpdateBoardView();
        }
        UpdateControls();
    }

    private void SubmitSelection()
    {
        if (_match.Session is not { } session || _match.Cpu?.Failure is not null ||
            !_configuration.AllowsHumanInput(_activePlayer) || _board.SelectedPosition is not { } position)
        {
            return;
        }

        var result = MatchConfiguration.Submit(session, _activePlayer, position, _displayed.Phase, _displayed.RoundId);
        if (result == SessionCommandResult.Accepted)
        {
            _board.ClearSelection();
            UpdateControls();
        }
        ShowRejection(result);
    }

    private void RestartMatch()
    {
        if (_match.Session is { } session) ShowRejection(session.RestartDraft(_displayed.RoundId));
    }

    private void StartNewMatch()
    {
        _mapChoice = _newMatchDialog.MapChoice;
        StartMatch(new MatchConfiguration(_configuration.Mode, _configuration.HumanPlayer,
            _newMatchDialog.SelectedDifficulty, _configuration.RuleSet));
    }

    private void ShowRejection(SessionCommandResult result)
    {
        if (result == SessionCommandResult.Accepted || _match.Session is not { } session) return;
        DisplayState(session.Current);
        _detail.ThemeTypeVariation = RejectionStyle;
        _detail.Text = Array.Find(Rejections, rejection => rejection.Result == result).Text ?? UnknownRejection;
        _instruction.Slot.Show();
    }

    private void DisplayState(GameSessionSnapshot snapshot)
    {
        bool versusCpu = _configuration.CpuPlayer.HasValue;
        bool changedRound = _displayed is null || _displayed.RoundId != snapshot.RoundId || _displayed.Phase != snapshot.Phase;
        if (!versusCpu && snapshot.Phase == MatchPhase.Opening &&
            _displayed?.Phase is MatchPhase.OpeningRestartRequired or MatchPhase.Finished)
        {
            _activePlayer = PlayerId.PlayerOne;
        }

        _displayed = snapshot;
        _presentation?.Observe(snapshot);
        _match.Cpu?.Observe(snapshot);
        // A submission flag can change without changing either target set or the provisional highlight.
        if (changedRound) UpdateBoardView();
        _banner.ShowScores(snapshot.PlayerOneScore, snapshot.PlayerTwoScore);
        UpdateControls();
    }

    private void UpdateBoardView()
    {
        var (active, opposing) = MatchPresentation.Targets(_displayed, _activePlayer);
        _board.SetMatchBoard(_displayed.Board, active, opposing, _displayed.Territory);
    }

    private void UpdateControls()
    {
        GameSessionSnapshot snapshot = _displayed;
        bool playable = MatchConfiguration.IsPlayable(snapshot.Phase);
        bool hasSubmitted = snapshot.HasSubmitted(_activePlayer);
        bool cpuFailed = _match.Cpu?.Failure is not null;
        _detail.ThemeTypeVariation = cpuFailed ? RejectionStyle : InstructionStyle;
        UpdatePlayerButton(_playerOne, PlayerId.PlayerOne, snapshot, playable, cpuFailed);
        UpdatePlayerButton(_playerTwo, PlayerId.PlayerTwo, snapshot, playable, cpuFailed);
        _players.Visible = playable || _configuration.CpuPlayer.HasValue;
        _submit.Visible = playable;
        _submit.Disabled = !playable || cpuFailed || _board.SelectedPosition is null;
        _submit.Text = hasSubmitted ? ReviseCaption : SubmitCaption;
        _restart.Visible = !playable;
        _restart.Text = snapshot.Phase == MatchPhase.Finished ? RestartMatchCaption : RestartDraftCaption;
        _status.Visible = !playable;
        _instruction.Slot.Visible = snapshot.FinalResult is null;
        _phase.Text = snapshot.Phase switch
        {
            MatchPhase.Opening => $"Draft · Round {snapshot.ResolvedRounds + 1}",
            MatchPhase.Main => $"Main · Turn {snapshot.ResolvedMainTurns + 1}",
            MatchPhase.Finished => "Match complete",
            _ => "Draft ended"
        };
        _ruleSetBadge.Text = RuleSetName(snapshot.RuleSet);

        if (snapshot.FinalResult is { } result)
        {
            _status.Text = ResultCaption(result.Winner);
        }
        else if (!playable)
        {
            _status.Text = "Restart the draft";
            _detail.Text = snapshot.RuleSet == RuleSet.KnightSupport ? CastleDraftRetryInstruction : DraftRetryInstruction;
        }
        else if (cpuFailed)
        {
            _detail.Text = CpuFailure;
        }
        else
        {
            PlayerId opponent = _activePlayer == PlayerId.PlayerOne ? PlayerId.PlayerTwo : PlayerId.PlayerOne;
            _detail.Text = hasSubmitted
                ? (_configuration.CpuPlayer.HasValue ? CpuWaiting : WaitingFor(opponent))
                : SelectionInstruction;
        }
    }

    private void UpdatePlayerButton(Button button, PlayerId player, GameSessionSnapshot snapshot, bool playable, bool cpuFailed)
    {
        bool active = _activePlayer == player;
        bool submitted = snapshot.HasSubmitted(player);
        if (_configuration.CpuPlayer is { } cpu)
        {
            string state = submitted ? "Submitted" : player == cpu ? (cpuFailed ? "Unavailable" : "Thinking") : "Choosing";
            string name = player == cpu ? $"CPU · {MatchCaptions.DifficultyName(_configuration.Difficulty)}" : "You";
            button.Text = playable ? $"{name}\n{state}" : name;
            return;
        }

        button.SetPressedNoSignal(active);
        button.Text = PlayerButtonCaption(player, active, submitted);
    }

    private Vector2 MeasurePlayerButtonMinimum(Button button, PlayerId player) => _configuration.CpuPlayer.HasValue
        ? StableInstructionLayout.MeasureButtonMinimum(button, "CPU · Very Hard\nUnavailable", "You\nSubmitted")
        : StableInstructionLayout.MeasureButtonMinimum(button, PlayerButtonCaption(player, active: true, submitted: false),
            PlayerButtonCaption(player, active: true, submitted: true));

    private string ResultCaption(PlayerId? winner) => winner switch
    {
        null => "Draw",
        _ when _configuration.CpuPlayer is null => $"{PlayerName(winner.Value)} wins",
        _ when winner == _configuration.HumanPlayer => "You win",
        _ => "CPU wins"
    };

    private static string PlayerButtonCaption(PlayerId player, bool active, bool submitted)
    {
        string state = submitted ? "Submitted" : "Choosing";
        return $"{PlayerName(player)}\n{(active ? "Active · " : "")}{state}";
    }

    private static string RuleSetName(RuleSet ruleSet) => ruleSet == RuleSet.KnightSupport ? "Knight Support" : "Classic";

    private static string PlayerName(PlayerId player) => player == PlayerId.PlayerOne ? "Player One" : "Player Two";

    private static string WaitingFor(PlayerId player) => $"Waiting for {PlayerName(player)}.";
}
