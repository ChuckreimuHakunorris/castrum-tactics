namespace CastrumTactics.Domain.Session;

/// <summary>Coordinates the two participants and owns the authoritative state for a local Unlimited Draft.</summary>
public sealed class LocalGameSession : IGameSession
{
    private readonly MapDefinition _map;
    private OpeningDraft _draft;
    private BoardPosition? _playerOnePending;
    private BoardPosition? _playerTwoPending;

    /// <summary>Initializes a fresh draft with the map's starting positions and ground health.</summary>
    /// <param name="map">The map for the initial draft and any restart. It must have at least two starting positions.</param>
    /// <exception cref="ArgumentNullException">The map is null.</exception>
    /// <exception cref="ArgumentException">The map has fewer than two starting positions.</exception>
    public LocalGameSession(MapDefinition map)
    {
        ArgumentNullException.ThrowIfNull(map);

        // A draft needs at least two starts for both players to be able to claim a knight.
        if (map.StartingPositions.Count < 2)
        {
            throw new ArgumentException("Add at least two starting positions to the map before starting a local session.",
                nameof(map));
        }

        _map = map;
        _draft = new OpeningDraft(map);
        Current = new GameSessionSnapshot(_draft, 1, false, false);
    }

    /// <inheritdoc />
    public GameSessionSnapshot Current { get; private set; }

    /// <inheritdoc />
    public event Action<GameSessionSnapshot>? StateChanged;

    /// <inheritdoc />
    public SessionCommandResult SubmitOpeningSelection(OpeningSelection selection)
    {
        if (selection.Player is not (PlayerId.PlayerOne or PlayerId.PlayerTwo))
        {
            return SessionCommandResult.InvalidPlayer;
        }

        if (selection.RoundId != Current.RoundId)
        {
            return SessionCommandResult.StaleRound;
        }

        if (_draft.Phase != MatchPhase.Opening)
        {
            return SessionCommandResult.WrongPhase;
        }

        if (!_draft.CanSelect(selection.Position))
        {
            return SessionCommandResult.IneligiblePosition;
        }

        BoardPosition? playerOne = selection.Player == PlayerId.PlayerOne ? selection.Position : _playerOnePending;
        BoardPosition? playerTwo = selection.Player == PlayerId.PlayerTwo ? selection.Position : _playerTwoPending;
        long roundId = Current.RoundId;

        if (playerOne is { } first && playerTwo is { } second)
        {
            // Resolve in player order against the unchanged board before discarding the pending choices.
            _draft = _draft.Resolve(first, second);
            playerOne = null;
            playerTwo = null;
            roundId++;
        }

        _playerOnePending = playerOne;
        _playerTwoPending = playerTwo;
        Publish(roundId);
        return SessionCommandResult.Accepted;
    }

    /// <inheritdoc />
    public SessionCommandResult RestartDraft(long roundId)
    {
        if (roundId != Current.RoundId)
        {
            return SessionCommandResult.StaleRound;
        }

        if (_draft.Phase != MatchPhase.OpeningRestartRequired)
        {
            return SessionCommandResult.WrongPhase;
        }

        _draft = new OpeningDraft(_map);
        _playerOnePending = null;
        _playerTwoPending = null;
        Publish(roundId + 1);
        return SessionCommandResult.Accepted;
    }

    private void Publish(long roundId)
    {
        Current = new GameSessionSnapshot(_draft, roundId, _playerOnePending.HasValue, _playerTwoPending.HasValue);
        StateChanged?.Invoke(Current);
    }
}
