namespace Photospheria.Core.Model;

public enum Season : byte
{
    Spring = 0,
    Summer = 1,
    Autumn = 2,
    Winter = 3
}

public class SimulationConfig
{
    public int Width { get; init; } = 100;
    public int Height { get; init; } = 100;
    public int TotalTicks { get; init; } = 200;

    public byte[,]? TerrainMap { get; init; }
    public byte[,]? SoilMap { get; init; }

    /// <summary>
    /// Function or lookup returning the season for a given tick.
    /// Default standard 4-season cycle (e.g., 50 ticks per season for 200 ticks).
    /// </summary>
    public System.Func<int, Season> SeasonProvider { get; init; } = tick =>
    {
        // Default 50 ticks per season cycle
        int cycle = (tick / 50) % 4;
        return (Season)cycle;
    };
}
