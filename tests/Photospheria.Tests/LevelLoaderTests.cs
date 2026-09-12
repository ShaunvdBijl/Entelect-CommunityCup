using System.IO;
using Photospheria.Core.Model;
using Photospheria.Core.Simulation;
using Xunit;

namespace Photospheria.Tests;

public class LevelLoaderTests
{
    [Fact]
    public void LoadLevel1Json_ParsesDimensionsTicksSeasonsAndTerrain()
    {
        string current = Directory.GetCurrentDirectory();
        string? levelPath = null;
        for (int i = 0; i < 6; i++)
        {
            string candidate = Path.Combine(current, "1 (1).json");
            if (File.Exists(candidate))
            {
                levelPath = candidate;
                break;
            }
            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }

        Assert.NotNull(levelPath);
        Assert.True(File.Exists(levelPath), $"Level file not found at {levelPath}");

        var config = LevelLoader.LoadFromFile(levelPath);

        Assert.Equal(50, config.Width);
        Assert.Equal(50, config.Height);
        Assert.Equal(500, config.TotalTicks);

        // Check seasons
        Assert.Equal(Season.Spring, config.SeasonProvider(0));
        Assert.Equal(Season.Summer, config.SeasonProvider(100));
        Assert.Equal(Season.Autumn, config.SeasonProvider(200));
        Assert.Equal(Season.Winter, config.SeasonProvider(300));
        Assert.Equal(Season.Spring, config.SeasonProvider(400));
        Assert.Equal(Season.Spring, config.SeasonProvider(499));

        // Check terrain of cell (0, 10) which is terrain 2, soil 0 in 1 (1).json
        Assert.NotNull(config.TerrainMap);
        Assert.NotNull(config.SoilMap);
        Assert.Equal(2, config.TerrainMap[0, 10]);
        Assert.Equal(0, config.SoilMap[0, 10]);
    }
}
