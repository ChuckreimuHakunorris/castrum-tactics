using CastrumTactics.Domain;

internal static class DemonstrationMap
{
    internal static MapDefinition Create()
    {
        const TerrainType playable = TerrainType.Playable;
        const TerrainType blocked = TerrainType.Inaccessible;
        TerrainType[,] terrain =
        {
            { playable, playable, blocked,  playable, playable, playable, playable },
            { playable, playable, blocked,  playable, playable, blocked,  playable },
            { playable, playable, playable, playable, playable, blocked,  playable },
            { playable, blocked,  playable, playable, playable, playable, playable },
            { playable, playable, playable, playable, blocked,  playable, playable },
        };
        BoardPosition[] startingPositions = [new(1, 1), new(4, 0), new(3, 2), new(5, 3), new(1, 4)];

        return new MapDefinition(terrain.GetLength(1), terrain.GetLength(0), terrain, startingPositions);
    }
}
