#nullable enable

using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using Godot;

/// <summary>Presents the local opening draft and forwards both players' commands to the game session.</summary>
public partial class Main : Control
{
    private IGameSession _session = null!;
    private BoardRenderer _board = null!;
    private Label _status = null!;
    private HBoxContainer _players = null!;
    private Button _playerOne = null!;
    private Button _playerTwo = null!;
    private Button _submit = null!;
    private Button _restart = null!;
    private PlayerId _activePlayer = PlayerId.PlayerOne;
    private long _displayedRoundId;

    /// <summary>Starts the local demonstration and binds the scene controls to session updates.</summary>
    public override void _Ready()
    {
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
        _restart.Pressed += RestartDraft;
        _session.StateChanged += DisplayState;
        DisplayState(_session.Current);
    }

    /// <summary>Stops observing the session when the scene leaves the tree.</summary>
    public override void _ExitTree()
    {
        if (_session is not null)
        {
            _session.StateChanged -= DisplayState;
        }
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
            _board.ClearSelection();
        }

        UpdateControls();
    }

    private void SubmitSelection()
    {
        if (_board.SelectedPosition is { } position)
        {
            ShowRejection(_session.SubmitOpeningSelection(new OpeningSelection(_activePlayer, position, _displayedRoundId)));
        }
    }

    private void RestartDraft() => ShowRejection(_session.RestartDraft(_displayedRoundId));

    private void ShowRejection(SessionCommandResult result)
    {
        if (result != SessionCommandResult.Accepted)
        {
            DisplayState(_session.Current);
            _status.Text = result switch
            {
                SessionCommandResult.IneligiblePosition => "That starting position is unavailable. Choose a gold dot.",
                SessionCommandResult.StaleRound => "The draft has advanced. Choose for the current round.",
                SessionCommandResult.WrongPhase => "This action is unavailable in the current phase.",
                _ => "The draft command was rejected."
            };
        }
    }

    private void DisplayState(GameSessionSnapshot snapshot)
    {
        _displayedRoundId = snapshot.RoundId;
        _board.SetBoard(snapshot.Board, snapshot.EligiblePositions);
        UpdateControls();
    }

    private void UpdateControls()
    {
        GameSessionSnapshot snapshot = _session.Current;
        bool isOpening = snapshot.Phase == MatchPhase.Opening;
        bool hasSubmitted = snapshot.HasSubmitted(_activePlayer);
        _playerOne.SetPressedNoSignal(_activePlayer == PlayerId.PlayerOne);
        _playerTwo.SetPressedNoSignal(_activePlayer == PlayerId.PlayerTwo);
        _players.Visible = isOpening;
        _submit.Visible = isOpening;
        _submit.Disabled = !isOpening || _board.SelectedPosition is null;
        _submit.Text = hasSubmitted ? "Revise selection" : "Submit selection";
        _restart.Visible = snapshot.Phase == MatchPhase.OpeningRestartRequired;

        if (!isOpening)
        {
            _status.Text = snapshot.Phase == MatchPhase.Main
                ? "Draft complete. Both players have starting knights."
                : "The draft ended without starting knights. Restart to try again.";
            return;
        }

        PlayerId opponent = _activePlayer == PlayerId.PlayerOne ? PlayerId.PlayerTwo : PlayerId.PlayerOne;
        string instruction = hasSubmitted
            ? $"Waiting for {PlayerName(opponent)}. You can still revise."
            : snapshot.HasSubmitted(opponent)
                ? $"{PlayerName(opponent)} has submitted. Choose a gold dot and submit."
                : "Choose a gold dot and submit.";
        _status.Text = $"Round {snapshot.ResolvedRounds + 1} · {PlayerName(_activePlayer)}\n{instruction}";
    }

    private static string PlayerName(PlayerId player) => player == PlayerId.PlayerOne ? "Player One" : "Player Two";
}
