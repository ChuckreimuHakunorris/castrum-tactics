using CastrumTactics.Domain;

namespace CastrumTactics.Development.Solver;

internal readonly record struct BoardTransform(int Geometry, bool Exchange)
{
    internal BoardPosition Map(BoardPosition p, int width, int height) =>
        PositionIdentity.Transform(p, width, height, Geometry);

    internal string Apply(string key, int width, int height)
    {
        if (this == default) return key;
        Span<char> result = key.Length <= 512 ? stackalloc char[key.Length] : new char[key.Length];
        Write(key, result, width, height);
        return new(result);
    }

    internal void Write(ReadOnlySpan<char> key, Span<char> result, int width, int height)
    {
        key[..5].CopyTo(result);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var target = Map(new(x,y), width, height);
            int from = 5 + 3 * (y * width + x);
            int to = 5 + 3 * (target.Y * width + target.X);
            int cell = key[from];
            if (Exchange && (cell >> 3) != 0) cell ^= 3 << 3;
            result[to] = (char)cell;
            result[to + 1] = key[from + 1];
            result[to + 2] = key[from + 2];
        }
    }

    internal Equilibrium PolicyToCanonical(Equilibrium game, BoardPosition[] red, BoardPosition[] blue, int width, int height)
    {
        var transform = this;
        Rational[] Reorder(BoardPosition[] actions, Rational[] policy) => actions.Select((p, i) => (At: transform.Map(p, width, height), i))
            .OrderBy(x => x.At.Y).ThenBy(x => x.At.X).Select(x => policy[x.i]).ToArray();
        var r = Reorder(red, game.Red);
        var b = Reorder(blue, game.Blue);
        return Exchange ? Negate(game) with { Red = b, Blue = r } : game with { Red = r, Blue = b };
    }

    internal Equilibrium PolicyFromCanonical(Equilibrium game, BoardPosition[] red, BoardPosition[] blue, int width, int height)
    {
        var transform = this;
        Rational[] Reorder(BoardPosition[] actions, Rational[] policy)
        {
            var result = new Rational[actions.Length];
            int source = 0;
            foreach (var x in actions.Select((p, i) => (At: transform.Map(p, width, height), i))
                .OrderBy(x => x.At.Y).ThenBy(x => x.At.X))
                result[x.i] = policy[source++];
            return result;
        }
        return Exchange
            ? Negate(game) with { Red = Reorder(red, game.Blue), Blue = Reorder(blue, game.Red) }
            : game with { Red = Reorder(red, game.Red), Blue = Reorder(blue, game.Blue) };
    }
    internal static Equilibrium Negate(Equilibrium game) =>
        game with { Value = -game.Value, Lower = -game.Upper, Upper = -game.Lower };

}

internal sealed class BoardSymmetry(BoardTransform[] transforms)
{
    internal BoardTransform[] Transforms { get; } = transforms;
    internal bool ExchangesPlayers { get; } = transforms.Any(transform => transform.Exchange);

    internal static BoardSymmetry ForRoot(MainPhaseState state, bool enabled)
    {
        if (!enabled) return new([new(0, false)]);
        var board = state.Board;
        string root = PositionIdentity.Key(board, state.RuleSet);
        return new((from geometry in Enumerable.Range(0, board.Width == board.Height ? 8 : 4)
                    from exchange in new[] { false, true }
                    let transform = new BoardTransform(geometry, exchange)
                    where transform.Apply(root, board.Width, board.Height) == root
                    select transform).ToArray());
    }

    internal (string Key, BoardTransform Transform) Canonicalize(string raw, int width, int height)
    {
        if (Transforms.Length == 1) return (raw, default);
        Span<char> best = raw.Length <= 512 ? stackalloc char[raw.Length] : new char[raw.Length];
        Span<char> temporary = raw.Length <= 512 ? stackalloc char[raw.Length] : new char[raw.Length];
        raw.AsSpan().CopyTo(best);
        BoardTransform choice = default;
        foreach (var transform in Transforms)
        {
            if (transform == default) continue;
            transform.Write(raw, temporary, width, height);
            if (temporary.SequenceCompareTo(best) >= 0) continue;
            temporary.CopyTo(best);
            choice = transform;
        }
        return (choice == default ? raw : new(best), choice);
    }

    internal int OrbitSize(string raw, int width, int height)
    {
        if (Transforms.Length == 1) return 1;
        Span<char> temporary = raw.Length <= 512 ? stackalloc char[raw.Length] : new char[raw.Length];
        int stabilizer = 1;
        foreach (var transform in Transforms)
        {
            if (transform == default) continue;
            transform.Write(raw, temporary, width, height);
            if (temporary.SequenceEqual(raw)) stabilizer++;
        }
        return Transforms.Length / stabilizer;
    }
}
