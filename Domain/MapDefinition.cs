using System.Collections.ObjectModel;

namespace CastrumTactics.Domain;

/// <summary>Defines the dimensions, terrain, and candidate starting positions of a map.</summary>
/// <remarks>The terrain and starting positions are copied at construction and remain fixed.</remarks>
public sealed class MapDefinition
{
    private readonly TerrainType[,] _terrain;
    private readonly ReadOnlyCollection<BoardPosition> _startingPositions;

    /// <summary>Creates a validated map definition from a terrain grid and starting positions.</summary>
    /// <param name="width">The positive number of columns.</param>
    /// <param name="height">The positive number of rows.</param>
    /// <param name="terrain">The terrain grid indexed by row and then column, <c>[y, x]</c>.</param>
    /// <param name="startingPositions">Distinct positions on playable terrain. An empty sequence is allowed.</param>
    /// <exception cref="ArgumentNullException">The terrain or starting positions are null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The width or height is less than one.</exception>
    /// <exception cref="ArgumentException">
    /// The terrain dimensions or values are invalid, or a starting position is out of bounds, duplicated,
    /// or on inaccessible terrain.
    /// </exception>
    public MapDefinition(int width, int height, TerrainType[,] terrain, IEnumerable<BoardPosition> startingPositions)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(startingPositions);

        if (terrain.GetLength(0) != height || terrain.GetLength(1) != width ||
            terrain.GetLowerBound(0) != 0 || terrain.GetLowerBound(1) != 0)
        {
            throw new ArgumentException("Terrain must be a zero-based grid with the declared height and width.", nameof(terrain));
        }

        Width = width;
        Height = height;
        _terrain = (TerrainType[,])terrain.Clone();
        foreach (TerrainType cellTerrain in _terrain)
        {
            if (!Enum.IsDefined(cellTerrain))
            {
                throw new ArgumentException("Terrain contains an undefined accessibility value.", nameof(terrain));
            }
        }

        BoardPosition[] positions = startingPositions.ToArray();
        var seenPositions = new HashSet<BoardPosition>();
        foreach (BoardPosition position in positions)
        {
            if (!IsWithinBounds(position) || GetTerrain(position) != TerrainType.Playable)
            {
                throw new ArgumentException("Each starting position must be on playable terrain within the map.", nameof(startingPositions));
            }

            if (!seenPositions.Add(position))
            {
                throw new ArgumentException("Starting positions must be unique.", nameof(startingPositions));
            }
        }

        _startingPositions = Array.AsReadOnly(positions);
    }

    /// <summary>Gets the number of columns in the map.</summary>
    public int Width { get; }

    /// <summary>Gets the number of rows in the map.</summary>
    public int Height { get; }

    /// <summary>Gets the eligible starting positions in the order supplied at construction.</summary>
    public IReadOnlyList<BoardPosition> StartingPositions => _startingPositions;

    /// <summary>Checks whether the position identifies a cell in the map.</summary>
    /// <param name="position">The position to check.</param>
    /// <returns>True if both coordinates are nonnegative and within the map dimensions.</returns>
    public bool IsWithinBounds(BoardPosition position)
    {
        return position.X >= 0 && position.X < Width && position.Y >= 0 && position.Y < Height;
    }

    /// <summary>Gets the fixed terrain accessibility at a map position.</summary>
    /// <param name="position">The cell position to look up.</param>
    /// <returns>The terrain at the specified position.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The position is outside the map.</exception>
    public TerrainType GetTerrain(BoardPosition position)
    {
        if (!IsWithinBounds(position))
        {
            throw new ArgumentOutOfRangeException(nameof(position), position, "Position is outside the map.");
        }

        return _terrain[position.Y, position.X];
    }
}
