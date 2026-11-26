using CastrumTactics.AI;

internal static class MatchCaptions
{
    internal static string DifficultyName(CpuDifficulty difficulty) =>
        difficulty == CpuDifficulty.VeryHard ? "Very Hard" : difficulty.ToString();
}
