namespace CastrumTactics.Domain;

/// <summary>Describes a cell's static terrain accessibility independently of its appearance.</summary>
public enum TerrainType
{
    /// <summary>A blocked cell that cannot hold a piece or serve as a starting position.</summary>
    Inaccessible = 0,

    /// <summary>Ground that can participate in gameplay, subject to its current piece state.</summary>
    Playable
}
