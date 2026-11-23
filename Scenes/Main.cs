#nullable enable

using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using Godot;

/// <summary>Presents the local match and forwards both players' commands to the game session.</summary>
public partial class Main : Control
{
    private IGameSession _session = null!;
    private BoxContainer _layout = null!;
    private VBoxContainer _controls = null!;
    private BoardRenderer _board = null!;
    private Label _status = null!;
    private HBoxContainer _players = null!;
    private Button _playerOne = null!;
    private Button _playerTwo = null!;
    private Button _submit = null!;
    private Button _restart = null!;
    private PlayerId _activePlayer = PlayerId.PlayerOne;
    private GameSessionSnapshot _displayed = null!;

    /// <summary>Starts the local demonstration and binds the controls to session and layout updates.</summary>
    public override void _Ready()
    {
        _layout = GetNode<BoxContainer>("Layout");
        _controls = GetNode<VBoxContainer>("Layout/Controls");
        _board = GetNode<BoardRenderer>("%BoardRenderer");
        _status = GetNode<Label>("%Status");
        _players = GetNode<HBoxContainer>("%Players");
        _playerOne = GetNode<Button>("%PlayerOne");
        _playerTwo = GetNode<Button>("%PlayerTwo");
        _submit = GetNode<Button>("%Submit");
        _restart = GetNode<Button>("%Restart");
        _session = new LocalGameSession(DemonstrationMap.Create());

        SetPlayerColor(_playerOne, BoardRenderer.PlayerOneColor);
        SetPlayerColor(_playerTwo, BoardRenderer.PlayerTwoColor);
        _playerOne.Pressed += () => SwitchPlayer(PlayerId.PlayerOne);
        _playerTwo.Pressed += () => SwitchPlayer(PlayerId.PlayerTwo);
        _board.SelectionChanged += UpdateControls;
        _submit.Pressed += SubmitSelection;
        _restart.Pressed += RestartMatch;
        Resized += UpdateLayout;
        UpdateLayout();
        _session.StateChanged += DisplayState;
        DisplayState(_session.Current);
    }

    /// <summary>Stops observing session and layout updates when the scene leaves the tree.</summary>
    public override void _ExitTree()
    {
        Resized -= UpdateLayout;
        if (_session is not null)
        {
            _session.StateChanged -= DisplayState;
        }
    }

    private void UpdateLayout()
    {
        // Give the board more height on wide screens while reserving room for readable controls.
        bool wide = Size.X >= 600 && Size.X >= Size.Y * 1.5f;
        _layout.Vertical = !wide;
        _controls.CustomMinimumSize = new Vector2(wide ? 244 : 0, 0);
    }

    private static void SetPlayerColor(Button button, Color color)
    {
        button.AddThemeColorOverride("font_color", color);
        button.AddThemeColorOverride("font_hover_color", color);
        button.AddThemeColorOverride("font_pressed_color", color);
        button.AddThemeColorOverride("font_focus_color", color);
        button.AddThemeColorOverride("font_hover_pressed_color", color);
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
            _status.Text = result switch
            {
                SessionCommandResult.IneligiblePosition => "That square is unavailable. Choose a marked square.",
                SessionCommandResult.StaleRound => "The match has advanced. Choose for the current turn.",
                SessionCommandResult.WrongPhase => "This action is unavailable in the current phase.",
                _ => "The match command was rejected."
            };
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
        _playerOne.SetPressedNoSignal(_activePlayer == PlayerId.PlayerOne);
        _playerTwo.SetPressedNoSignal(_activePlayer == PlayerId.PlayerTwo);
        _players.Visible = canSubmit;
        _submit.Visible = canSubmit;
        _submit.Disabled = !canSubmit || _board.SelectedPosition is null;
        _submit.Text = hasSubmitted ? "Revise selection" : "Submit selection";
        _restart.Visible = snapshot.Phase is MatchPhase.OpeningRestartRequired or MatchPhase.Finished;
        _restart.Text = snapshot.Phase == MatchPhase.Finished ? "Restart Match" : "Restart Draft";

        if (snapshot.FinalResult is { } result)
        {
            string outcome = result.Winner is { } winner ? $"{PlayerName(winner)} wins" : "Draw";
            _status.Text = $"Match complete · {outcome}\nPlayer One {result.PlayerOneScore} · Player Two {result.PlayerTwoScore}";
            return;
        }

        if (!canSubmit)
        {
            _status.Text = "The draft ended without starting knights. Restart to try again.";
            return;
        }

        PlayerId opponent = _activePlayer == PlayerId.PlayerOne ? PlayerId.PlayerTwo : PlayerId.PlayerOne;
        bool isOpening = snapshot.Phase == MatchPhase.Opening;
        string choose = isOpening ? "Choose a gold dot and submit." : "Choose a gold-outlined square and submit.";
        string instruction = hasSubmitted
            ? $"Waiting for {PlayerName(opponent)}. You can still revise."
            : snapshot.HasSubmitted(opponent)
                ? $"{PlayerName(opponent)} submitted. Choose a marked square."
                : choose;
        string turn = isOpening ? $"Draft {snapshot.ResolvedRounds + 1}" : $"Main {snapshot.ResolvedMainTurns + 1}";
        _status.Text = $"{turn} · {PlayerName(_activePlayer)}\n{instruction}";
    }

    private static string PlayerName(PlayerId player) => player == PlayerId.PlayerOne ? "Player One" : "Player Two";
}
