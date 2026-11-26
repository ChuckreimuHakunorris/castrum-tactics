using CastrumTactics.Development.Solver;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using static Program;
using static CastrumTactics.Development.TournamentPlanner;

namespace CastrumTactics.Development;

internal static class TournamentHistory
{
    internal static string OpeningDigest(GameSessionSnapshot snapshot) =>
        Digest(Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(
            PositionIdentity.Key(snapshot.Board, snapshot.RuleSet))));

    internal static bool IsPlayable(MatchPhase phase) => phase is MatchPhase.Opening or MatchPhase.Main;
    internal static string OpeningOutcome(GameSessionSnapshot state) => state.Phase switch
    {
        MatchPhase.Opening => "unfinished",
        MatchPhase.OpeningRestartRequired => "retry",
        MatchPhase.Finished when state.ResolvedMainTurns == 0 => "draftscore",
        _ => "main"
    };

    internal static string HistoryDigest(string map, RuleSet rules, IEnumerable<TournamentRound> rounds) =>
        Digest($"{map}|{rules}|{string.Join(';', rounds.Select(round => $"{round.Phase}|{round.Red.Move}/{round.Blue.Move}"))}");
}
