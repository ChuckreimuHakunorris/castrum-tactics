using CastrumTactics.Domain;

namespace CastrumTactics.AI;

// Distances use half-turn units. They describe a static route through the resolved position.
internal sealed class InvasionReach(
    BoardPosition[] positions, int[][] neighbors, CellState[] cells, TerritoryControl[] control, RuleSetDefinition rules)
{
    private const int Horizon = 16;
    private readonly int[] _one = new int[positions.Length];
    private readonly int[] _two = new int[positions.Length];
    private readonly Queue<int>[] _waves = Enumerable.Range(0, Horizon).Select(_ => new Queue<int>()).ToArray();
    private readonly int[][] _steps = PlacementSteps(positions, neighbors, rules);

    internal void Reset()
    {
        Array.Fill(_one, Horizon);
        Array.Fill(_two, Horizon);
    }

    internal void Seed(int source, bool one, int delay, int range, IReadOnlySet<BoardPosition> legal)
    {
        int[] field = one ? _one : _two;
        var owner = one ? PlayerId.PlayerOne : PlayerId.PlayerTwo;
        if (control[source] == TerritoryControl.Contested)
            field[source] = Math.Min(field[source], delay);

        var origin = positions[source];
        foreach (int target in range <= 1 ? neighbors[source] : _steps[source])
        {
            var position = positions[target];
            if (Math.Max(Math.Abs(origin.X - position.X), Math.Abs(origin.Y - position.Y)) > range ||
                cells[target].Owner == owner || !legal.Contains(position)) continue;
            Relax(field, target, delay, owner, threatened: false);
        }
    }

    internal void Propagate(IReadOnlySet<BoardPosition> oneLegal, IReadOnlySet<BoardPosition> twoLegal,
        CancellationToken token)
    {
        Expand(_one, PlayerId.PlayerOne, twoLegal, token);
        Expand(_two, PlayerId.PlayerTwo, oneLegal, token);
    }

    internal double Advantage(int index)
    {
        int one = _one[index], two = _two[index];
        // Credit a lead only where at least one formation can reach the cell within the horizon.
        return Math.Clamp((two - one) / 6.0, -1, 1) * (Horizon - Math.Min(one, two)) / Horizon;
    }

    private void Expand(int[] field, PlayerId owner, IReadOnlySet<BoardPosition> enemyLegal, CancellationToken token)
    {
        // Begin with legal launches. Expansion through existing friendly pieces originates from their own launches.
        for (int i = 0; i < field.Length; i++)
            if (field[i] < Horizon && cells[i].Owner != owner)
                _waves[field[i]].Enqueue(i);
        for (int distance = 0; distance < Horizon; distance++)
        {
            while (_waves[distance].TryDequeue(out int source))
            {
                token.ThrowIfCancellationRequested();
                if (field[source] != distance) continue;
                var origin = positions[source];
                foreach (int target in _steps[source])
                {
                    var position = positions[target];
                    if (Math.Max(Math.Abs(origin.X - position.X), Math.Abs(origin.Y - position.Y)) >
                        Math.Min(rules.KnightPlacementRange, rules.KnightSupportRange) || cells[target].Owner == owner)
                        continue;
                    if (Relax(field, target, distance, owner, enemyLegal.Contains(position)))
                        _waves[field[target]].Enqueue(target);
                }
            }
        }
    }

    private bool Relax(int[] field, int target, int distance, PlayerId owner, bool threatened)
    {
        var cell = cells[target];
        if (control[target] != TerritoryControl.Contested || !cell.IsPlayable || cell.Piece == PieceType.Castle ||
            cell.Owner != null && cell.Owner != owner && cell.GroundHealth <= 1) return false;
        // A capture that leaves a foothold can extend the route, with an extra allowance for resistance.
        int cost = 2 + (cell.Owner != null && cell.Owner != owner ? 2 : 0) + (threatened ? 1 : 0);
        int candidate = distance + cost;
        if (candidate >= field[target]) return false;
        field[target] = candidate;
        return true;
    }

    private static int[][] PlacementSteps(BoardPosition[] positions, int[][] neighbors, RuleSetDefinition rules)
    {
        int range = Math.Max(rules.KnightPlacementRange, rules.CastlePlacementRange);
        if (range <= 1) return neighbors;
        return positions.Select(origin => positions.Select((target, index) => (target, index))
            .Where(item => item.target != origin &&
                Math.Max(Math.Abs(item.target.X - origin.X), Math.Abs(item.target.Y - origin.Y)) <= range)
            .Select(item => item.index).ToArray()).ToArray();
    }
}
