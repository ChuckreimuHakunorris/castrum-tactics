namespace CastrumTactics.Domain.Session;

/// <summary>Coordinates both players in a local match from the Unlimited Draft through final scoring.</summary>
public sealed class LocalGameSession : IGameSession
{
    private readonly MapDefinition _map;
    private OpeningDraft _draft;
    private MainPhaseState? _mainPhase;
    private int _resolvedMainTurns;
    private BoardPosition? _playerOnePending;
    private BoardPosition? _playerTwoPending;

    /// <summary>Initializes a fresh Classic draft with the map's starting positions and ground health.</summary>
    /// <param name="map">The map for the initial draft and any restart. It must have at least two starting positions.</param>
    /// <exception cref="ArgumentNullException">The map is null.</exception>
    /// <exception cref="ArgumentException">The map has fewer than two starting positions.</exception>
    public LocalGameSession(MapDefinition map) : this(map, RuleSet.Classic)
    {
    }

    /// <summary>Initializes a fresh draft with rules that remain fixed through resolution and restart.</summary>
    /// <param name="map">The map for the initial draft and any restart. It must have at least two starting positions.</param>
    /// <param name="ruleSet">The gameplay rules to apply for the entire session.</param>
    /// <exception cref="ArgumentNullException">The map is null.</exception>
    /// <exception cref="ArgumentException">The map has fewer than two starting positions.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The ruleset is undefined.</exception>
    public LocalGameSession(MapDefinition map, RuleSet ruleSet)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!Enum.IsDefined(ruleSet)) throw new ArgumentOutOfRangeException(nameof(ruleSet));

        // A draft needs at least two starts for both players to be able to claim a starting piece.
        if (map.StartingPositions.Count < 2)
        {
            throw new ArgumentException("Add at least two starting positions to the map before starting a local session.",
                nameof(map));
        }

        _map = map;
        _draft = new OpeningDraft(map, ruleSet);
        Current = CreateSnapshot(1);
    }

    /// <inheritdoc />
    public GameSessionSnapshot Current { get; private set; }

    /// <inheritdoc />
    public event Action<GameSessionSnapshot>? StateChanged;

    /// <inheritdoc />
    public SessionCommandResult SubmitOpeningSelection(OpeningSelection selection) =>
        SubmitSelection(selection.Player, selection.Position, selection.RoundId, MatchPhase.Opening);

    /// <inheritdoc />
    public SessionCommandResult SubmitMainPhaseSelection(MainPhaseCommand command) =>
        SubmitSelection(command.Player, command.Position, command.RoundId, MatchPhase.Main);

    private SessionCommandResult SubmitSelection(PlayerId player, BoardPosition position, long roundId, MatchPhase phase)
    {
        if (player is not (PlayerId.PlayerOne or PlayerId.PlayerTwo))
        {
            return SessionCommandResult.InvalidPlayer;
        }

        if (roundId != Current.RoundId)
        {
            return SessionCommandResult.StaleRound;
        }

        if (Current.Phase != phase)
        {
            return SessionCommandResult.WrongPhase;
        }

        MainPhaseState? mainPhase = _mainPhase;
        bool canSelect = mainPhase is null
            ? _draft.CanSelect(position)
            : mainPhase.GetLegalSelections(player).Contains(position);
        if (!canSelect)
        {
            return SessionCommandResult.IneligiblePosition;
        }

        BoardPosition? playerOne = player == PlayerId.PlayerOne ? position : _playerOnePending;
        BoardPosition? playerTwo = player == PlayerId.PlayerTwo ? position : _playerTwoPending;

        if (playerOne is { } first && playerTwo is { } second)
        {
            // Resolve against the same snapshot that validated the submissions before clearing either choice.
            roundId += ResolveRound(first, second);
            playerOne = null;
            playerTwo = null;
        }

        _playerOnePending = playerOne;
        _playerTwoPending = playerTwo;
        Publish(roundId);
        return SessionCommandResult.Accepted;
    }

    private int ResolveRound(BoardPosition first, BoardPosition second)
    {
        if (_mainPhase is { } mainPhase)
        {
            _mainPhase = mainPhase.Advance(new(PlayerId.PlayerOne, first), new(PlayerId.PlayerTwo, second));
            _resolvedMainTurns++;
            return 1;
        }

        var (draft, main, rounds) = _draft.ResolveTransition(first, second);
        _draft = draft;
        _mainPhase = main;
        return rounds;
    }

    /// <inheritdoc />
    public SessionCommandResult RestartDraft(long roundId)
    {
        if (roundId != Current.RoundId)
        {
            return SessionCommandResult.StaleRound;
        }

        if (Current.Phase is not (MatchPhase.OpeningRestartRequired or MatchPhase.Finished))
        {
            return SessionCommandResult.WrongPhase;
        }

        _draft = new OpeningDraft(_map, _draft.RuleSet);
        _mainPhase = null;
        _resolvedMainTurns = 0;
        _playerOnePending = null;
        _playerTwoPending = null;
        Publish(roundId + 1);
        return SessionCommandResult.Accepted;
    }

    private void Publish(long roundId)
    {
        Current = CreateSnapshot(roundId);
        StateChanged?.Invoke(Current);
    }

    private GameSessionSnapshot CreateSnapshot(long roundId) =>
        new(_draft, _mainPhase, _resolvedMainTurns, roundId, _playerOnePending.HasValue, _playerTwoPending.HasValue);
}
