using CastrumTactics.Domain;

namespace CastrumTactics.AI;

// One scratch workspace per decision. Influence is a heuristic field over the board graph, separate from control.
internal sealed class StrategicPosition
{
    internal const double TerminalWin = 10000;
    private const int CastleStrength = 8;
    private readonly RuleSetDefinition _rules;
    private readonly BoardPosition[] _positions;
    private readonly int[][] _neighbors;
    private readonly (int Index, int Distance)[][] _supportNeighbors;
    private readonly CellState[] _cells;
    private readonly TerritoryControl[] _control;
    private readonly int[] _oneInfluence;
    private readonly int[] _twoInfluence;
    private readonly Queue<int>[] _waves = Enumerable.Range(0, CastleStrength + 1).Select(_ => new Queue<int>()).ToArray();
    private readonly int _width;

    internal StrategicPosition(BoardState board, RuleSetDefinition rules, CancellationToken token)
    {
        _rules = rules;
        _width = board.Width;
        int area = checked(board.Width * board.Height);
        _positions = new BoardPosition[area];
        _neighbors = new int[area][];
        _supportNeighbors = new (int, int)[area][];
        _cells = new CellState[area];
        _control = new TerritoryControl[area];
        _oneInfluence = new int[area];
        _twoInfluence = new int[area];
        int range = Math.Max(rules.KnightSupportRange, rules.CastleSupportRange);
        for (int i = 0; i < area; i++)
        {
            if (i % _width == 0) token.ThrowIfCancellationRequested();
            var position = _positions[i] = new BoardPosition(i % _width, i / _width);
            _neighbors[i] = Neighbors(board, position, 1).Select(n => n.Index).ToArray();
            _supportNeighbors[i] = Neighbors(board, position, range).ToArray();
        }
    }

    internal PositionFeatures Evaluate(BoardState board, TerritoryAnalysis territory,
        IReadOnlySet<BoardPosition> oneLegal, IReadOnlySet<BoardPosition> twoLegal, CancellationToken token,
        bool opening = false)
    {
        double material = 0, formation = 0, mobility = 0, influence = 0, health = 0;
        Array.Clear(_oneInfluence);
        Array.Clear(_twoInfluence);
        for (int i = 0; i < _positions.Length; i++)
        {
            if (i % _width == 0) token.ThrowIfCancellationRequested();
            _cells[i] = board.GetCell(_positions[i]);
            _control[i] = territory.GetControl(_positions[i]);
        }
        for (int i = 0; i < _positions.Length; i++)
        {
            if (i % _width == 0) token.ThrowIfCancellationRequested();
            CellState cell = _cells[i];
            if (cell.Owner is not { } owner) continue;
            bool one = owner == PlayerId.PlayerOne;
            int sign = one ? 1 : -1;
            var field = one ? _oneInfluence : _twoInfluence;
            var enemyLegal = one ? twoLegal : oneLegal;
            if (cell.Piece == PieceType.Castle)
            {
                field[i] = CastleStrength;
                material += sign * 1.15;
                continue;
            }

            int supports = 0;
            int safe = 0;
            bool castle = false;
            foreach (var (source, distance) in _supportNeighbors[i])
            {
                CellState other = _cells[source];
                if (other.Owner != owner || distance > _rules.SurvivalSupportRange(other.Piece)) continue;
                supports++;
                castle |= other.Piece == PieceType.Castle;
                if (!enemyLegal.Contains(_positions[source])) safe++;
            }
            // A knight exposed directly or through its sole supporter projects less reliable influence.
            bool threatened = enemyLegal.Contains(_positions[i]);
            field[i] = 7 - (threatened ? 1 : 0) - (safe == 0 && supports < 2 ? 1 : 0);
            formation += sign * (castle ? 0.24 : supports > 1 || safe > 0 ? 0.15 : -0.15);
            material += sign * (threatened ? 0.30 : 0.43);
        }

        Propagate(_oneInfluence, opening, token);
        Propagate(_twoInfluence, opening, token);
        for (int i = 0; i < _positions.Length; i++)
        {
            if (i % _width == 0) token.ThrowIfCancellationRequested();
            if (!_cells[i].IsPlayable || _cells[i].Piece == PieceType.Castle ||
                (!opening && _control[i] != TerritoryControl.Contested)) continue;
            int one = _oneInfluence[i], two = _twoInfluence[i];
            double advantage = Math.Clamp((one - two) / 3.0, -1, 1) * (Math.Min(4, Math.Max(one, two)) / 4.0);
            influence += advantage;
            health += advantage * (Math.Min(3, _cells[i].GroundHealth) - 3) / 3.0;

            // Invasion and expansion opportunities count once per cell and taper in already dominant areas.
            if (_cells[i].Piece == PieceType.None)
            {
                mobility += (oneLegal.Contains(_positions[i]) ? 1 - Math.Max(0, advantage) : 0)
                    - (twoLegal.Contains(_positions[i]) ? 1 + Math.Min(0, advantage) : 0);
            }
        }
        return new PositionFeatures(territory.PlayerOneScore - territory.PlayerTwoScore,
            influence, material, formation, health, mobility);
    }

    internal double Frontier(BoardPosition position, PlayerId player)
    {
        int i = Index(position);
        var friendly = player == PlayerId.PlayerOne ? _oneInfluence : _twoInfluence;
        var enemy = player == PlayerId.PlayerOne ? _twoInfluence : _oneInfluence;
        double gains = 0;
        foreach (int j in _neighbors[i])
        {
            if (_cells[j].Piece != PieceType.None || _control[j] != TerritoryControl.Contested) continue;
            // Useful reach extends the leading edge or creates pressure in enemy-influenced ground.
            gains += Math.Clamp(7 - friendly[j], 0, 2) * (friendly[j] <= enemy[j] + 2 ? 1 : 0.25);
        }
        return gains / 16.0;
    }

    internal int InfluenceLead(BoardPosition position, PlayerId player)
    {
        int index = Index(position);
        int lead = _oneInfluence[index] - _twoInfluence[index];
        return player == PlayerId.PlayerOne ? lead : -lead;
    }

    private void Propagate(int[] influence, bool opening, CancellationToken token)
    {
        for (int i = 0; i < influence.Length; i++)
        {
            if (influence[i] > 0) _waves[influence[i]].Enqueue(i);
        }
        for (int strength = CastleStrength; strength > 1; strength--)
        {
            Queue<int> wave = _waves[strength];
            while (wave.TryDequeue(out int i))
            {
                token.ThrowIfCancellationRequested();
                if (influence[i] != strength) continue;
                foreach (int j in _neighbors[i])
                {
                    if (influence[j] >= strength - 1 || !_cells[j].IsPlayable || _cells[j].Piece == PieceType.Castle ||
                        (!opening && _control[j] != TerritoryControl.Contested)) continue;
                    influence[j] = strength - 1;
                    if (strength > 2) _waves[strength - 1].Enqueue(j);
                }
            }
        }
    }

    private int Index(BoardPosition position) => position.Y * _width + position.X;

    private IEnumerable<(int Index, int Distance)> Neighbors(BoardState board, BoardPosition position, int range)
    {
        for (int y = Math.Max(0, position.Y - range); y <= Math.Min(board.Height - 1, position.Y + range); y++)
        {
            for (int x = Math.Max(0, position.X - range); x <= Math.Min(board.Width - 1, position.X + range); x++)
            {
                int distance = Math.Max(Math.Abs(position.X - x), Math.Abs(position.Y - y));
                if (distance != 0) yield return (y * _width + x, distance);
            }
        }
    }

    internal readonly record struct PositionFeatures(double Secured, double Influence, double Material,
        double Formation, double Health, double Mobility)
    {
        internal CpuStrategicFeatures Contributions(PlayerId player, bool opening = false)
        {
            int sign = player == PlayerId.PlayerOne ? 1 : -1;
            return new(sign * (opening ? 0.30 : 0.90) * Secured, sign * 0.62 * Influence, sign * Material,
                sign * Formation, sign * 0.26 * Health, sign * 0.28 * Mobility);
        }

        internal double Score(PlayerId player, bool opening = false)
        {
            var c = Contributions(player, opening);
            return Math.Clamp(c.Secured + c.Influence + c.Material + c.Formation + c.Ground + c.Mobility, -1000, 1000);
        }
    }
}
