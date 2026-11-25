#nullable enable

using System;
using System.Globalization;
using CastrumTactics.AI;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;

namespace CastrumTactics.Match;

/// <summary>Determines who supplies the selections for a local authoritative match.</summary>
public enum GameMode
{
    /// <summary>One human plays against a CPU opponent.</summary>
    VersusCpu,
    /// <summary>Two humans share the same device.</summary>
    LocalMultiplayer
}

/// <summary>Defines immutable input ownership, CPU difficulty, and gameplay rules for one match.</summary>
public sealed class MatchConfiguration
{
    /// <summary>Creates a match configuration, defaulting to Knight Support with Player One against a Normal CPU.</summary>
    /// <param name="mode">The input mode for the match.</param>
    /// <param name="humanPlayer">The sole human player in Versus CPU.</param>
    /// <param name="difficulty">The CPU profile selected at match creation.</param>
    /// <param name="ruleSet">The gameplay rules selected independently of input mode and difficulty.</param>
    /// <exception cref="ArgumentOutOfRangeException">A supplied value is undefined.</exception>
    public MatchConfiguration(GameMode mode = GameMode.VersusCpu, PlayerId humanPlayer = PlayerId.PlayerOne,
        CpuDifficulty difficulty = CpuDifficulty.Normal, RuleSet ruleSet = RuleSet.KnightSupport)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (!Enum.IsDefined(humanPlayer)) throw new ArgumentOutOfRangeException(nameof(humanPlayer));
        if (!Enum.IsDefined(difficulty)) throw new ArgumentOutOfRangeException(nameof(difficulty));
        if (!Enum.IsDefined(ruleSet)) throw new ArgumentOutOfRangeException(nameof(ruleSet));
        Mode = mode;
        HumanPlayer = humanPlayer;
        Difficulty = difficulty;
        RuleSet = ruleSet;
    }

    /// <summary>Gets the mode controlling which players accept human input.</summary>
    public GameMode Mode { get; }

    /// <summary>Gets the sole human player in Versus CPU.</summary>
    public PlayerId HumanPlayer { get; }

    /// <summary>Gets the opponent in Versus CPU, or null when both players are human.</summary>
    public PlayerId? CpuPlayer => Mode == GameMode.LocalMultiplayer ? null
        : HumanPlayer == PlayerId.PlayerOne ? PlayerId.PlayerTwo : PlayerId.PlayerOne;

    /// <summary>Gets the selected CPU difficulty for this match.</summary>
    public CpuDifficulty Difficulty { get; }

    /// <summary>Gets the gameplay rules used for this match and its restarts.</summary>
    public RuleSet RuleSet { get; }

    /// <summary>Checks whether the human interface owns the supplied player.</summary>
    /// <param name="player">The player attempting to use the human interface.</param>
    /// <returns>True for either defined player in local play, or the configured human in Versus CPU.</returns>
    public bool AllowsHumanInput(PlayerId player) => Enum.IsDefined(player) && player != CpuPlayer;

    internal static bool IsPlayable(MatchPhase phase) => phase is MatchPhase.Opening or MatchPhase.Main;

    internal static SessionCommandResult Submit(IGameSession session, PlayerId player, BoardPosition position,
        MatchPhase phase, long roundId) => phase switch
    {
        MatchPhase.Opening => session.SubmitOpeningSelection(new(player, position, roundId)),
        MatchPhase.Main => session.SubmitMainPhaseSelection(new(player, position, roundId)),
        _ => SessionCommandResult.WrongPhase
    };
}

internal sealed record MatchLaunchOptions(MatchConfiguration Configuration, string? MapId, int? Seed)
{
    internal static MatchLaunchOptions Parse(string[] args, MatchConfiguration configuration, string? mapId, int? seed)
    {
        GameMode mode = configuration.Mode;
        RuleSet ruleSet = configuration.RuleSet;
        var used = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i++)
        {
            string option = args[i];
            if (option is not ("--mode" or "--map" or "--seed" or "--ruleset"))
                throw new ArgumentException($"Unknown match option '{option}'.");
            if (!used.Add(option))
                throw new ArgumentException($"Specify {option} only once.");
            if (++i == args.Length || args[i].StartsWith("--", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(args[i]))
                throw new ArgumentException($"Pass a value after {option}.");

            switch (option)
            {
                case "--mode":
                    mode = args[i] switch
                    {
                        "cpu" => GameMode.VersusCpu,
                        "local" => GameMode.LocalMultiplayer,
                        _ => throw new ArgumentException("Pass 'cpu' or 'local' after --mode.")
                    };
                    break;
                case "--map":
                    mapId = args[i];
                    break;
                case "--ruleset":
                    ruleSet = args[i] switch
                    {
                        "classic" => RuleSet.Classic,
                        "knight-support" => RuleSet.KnightSupport,
                        _ => throw new ArgumentException("Pass 'classic' or 'knight-support' after --ruleset.")
                    };
                    break;
                case "--seed":
                    if (!int.TryParse(args[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                        throw new ArgumentException("Pass a signed 32-bit CPU seed after --seed.");
                    seed = value;
                    break;
            }
        }

        if (string.IsNullOrEmpty(mapId)) mapId = null;
        if (mapId is not null) PredefinedMapCatalog.BuiltIn.Get(mapId);
        return new(new(mode, configuration.HumanPlayer, configuration.Difficulty, ruleSet), mapId, seed);
    }

    internal PredefinedMap? GetInitialMapChoice() => MapId is { } id ? PredefinedMapCatalog.BuiltIn.Get(id) : null;
}
