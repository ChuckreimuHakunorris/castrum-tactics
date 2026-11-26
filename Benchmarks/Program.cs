internal static partial class Program
{
    private static int Main(string[] args)
    {
        try
        {
            switch (args.FirstOrDefault())
            {
                case "match": RunMatches(args[1..]); break;
                case "fixtures": RunFixtures(); break;
                case "replay" when args.Length == 2: Replay(args[1]); break;
                case "tournament": return RunTournamentCommand(args[1..]);
                case "opening-study": return RunOpeningStudy(args[1..]);
                case "battlefield-audit": return RunBattlefieldAudit(args[1..]);
                case "snapshot-performance": return RunSnapshotPerformance(args[1..]);
                case "action-sensitivity": return RunActionSensitivity(args[1..]);
                case "solver-query": return QuerySolver(args[1..]);
                case "solver": return RunSolver(args[1..]);
                default:
                    Console.Error.WriteLine("Use match [--maps id,id --first strategic:Monster --second probe --seed 1 " +
                        "--count 2 --mode fixed|draft --starts A1/C3 --rules knight-support|classic --swap true|false], " +
                        "fixtures, replay <matches.jsonl>, solver, battlefield-audit, action-sensitivity, snapshot-performance, opening-study <config> <output>, or tournament --help.");
                    return 2;
            }
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
    }
}
