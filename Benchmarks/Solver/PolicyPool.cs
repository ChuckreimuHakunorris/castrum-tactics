namespace CastrumTactics.Development.Solver;

// Most small-board equilibria are uniform over a few tied actions. Share their immutable vectors.
internal sealed class PolicyPool
{
    private readonly Dictionary<(Rational Value, int Red, int Blue), Equilibrium> _games = [];

    internal Equilibrium Intern(Equilibrium game)
    {
        if (!game.IsExact) return game;
        int red = UniformMask(game.Red), blue = UniformMask(game.Blue);
        if (red == 0 || blue == 0) return game;
        var key = (game.Value, red, blue);
        if (_games.TryGetValue(key, out var previous)) return previous;
        if (_games.Count < 65_536) _games.Add(key, game);
        return game;
    }

    private static int UniformMask(Rational[] policy)
    {
        if (policy.Length > 30) return 0;
        int mask = 1 << policy.Length;
        Rational weight = default;
        for (int i = 0; i < policy.Length; i++)
        {
            if (policy[i].IsZero) continue;
            if (!weight.IsZero && policy[i] != weight) return 0;
            weight = policy[i];
            mask |= 1 << i;
        }
        return mask;
    }
}
