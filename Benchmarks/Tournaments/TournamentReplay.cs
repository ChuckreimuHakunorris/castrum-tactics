using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using static Program;
using static CastrumTactics.Development.TournamentHistory;
using static CastrumTactics.Development.TournamentPlanner;

namespace CastrumTactics.Development;

internal static class TournamentReplay
{
    internal static GameSessionSnapshot Reconstruct(Manifest manifest, ScheduledMatch match, TournamentResult record,
        string? targetPhase = null, int targetRound = 0)
    {
        var map = StudyBattlefields.Resolve(match.Map);
        if (Identify(map, manifest.Config.Maps.HeldOut).Digest != manifest.Maps.Single(m => m.Id == match.Map).Digest)
            throw new InvalidDataException($"Map definition changed for '{match.Map}'.");
        var session = new LocalGameSession(map.Definition, manifest.Config.Ruleset);
        GameSessionSnapshot? target = null;
        int forced = 0, collisions = 0;
        string? openingDigest = null;
        for (int i = 0; i < record.Rounds.Count; i++)
        {
            var round = record.Rounds[i];
            var before = session.Current;
            bool draft = before.Phase == MatchPhase.Opening;
            string phase = draft ? "draft" : "main";
            int number = (draft ? before.ResolvedRounds : before.ResolvedMainTurns) + 1;
            if (!IsPlayable(before.Phase) || round.Phase != phase || round.Number != number)
                throw new InvalidDataException($"Unexpected phase or round at action {i + 1}.");
            if (phase == targetPhase && number == targetRound) target = before;
            Play(session, round.Red.Move, round.Blue.Move, match.BlueSubmitsFirst);
            var after = session.Current;
            CheckScores(round, after);
            if (draft && after.Phase != MatchPhase.Opening && record.OpeningBoardDigest is not null)
                openingDigest = OpeningDigest(after);
            if (draft && round.Red.Move == round.Blue.Move) collisions++;
            if (draft && after.ResolvedRounds == before.ResolvedRounds + 2)
            {
                forced++;
                string last = Coordinate(before.EligiblePositions.Single(p => p != Position(round.Red.Move) && p != Position(round.Blue.Move)));
                if (++i == record.Rounds.Count) throw new InvalidDataException("Missing forced draft collision.");
                var auto = record.Rounds[i];
                if (auto.Phase != "auto-draft-collision" || auto.Number != after.ResolvedRounds || auto.Red.Move != last || auto.Blue.Move != last)
                    throw new InvalidDataException("Forced draft collision does not reproduce.");
                CheckScores(auto, after);
            }
        }
        var final = session.Current;
        if (record.OpeningBoardDigest != openingDigest || record.DraftRounds != final.ResolvedRounds || record.MainTurns != final.ResolvedMainTurns || record.Opening != OpeningOutcome(final) ||
            record.Collisions != collisions || record.ForcedCollisions != forced || record.RedScore != final.PlayerOneScore || record.BlueScore != final.PlayerTwoScore ||
            (record.Status == "completed" && (IsPlayable(final.Phase) || record.Winner != final.FinalResult?.Winner ||
                record.HistoryDigest != HistoryDigest(match.Map, manifest.Config.Ruleset, record.Rounds))))
            throw new InvalidDataException("Stored result does not reproduce.");
        return targetPhase is null ? final : target ?? throw new ArgumentException($"No recorded {targetPhase} round {targetRound}.");

        static void CheckScores(TournamentRound step, GameSessionSnapshot state)
        {
            if (step.RedScore != state.PlayerOneScore || step.BlueScore != state.PlayerTwoScore)
                throw new InvalidDataException("Round score does not reproduce.");
        }
    }
}
