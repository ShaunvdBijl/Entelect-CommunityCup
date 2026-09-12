using System;
using System.IO;
using System.Linq;
using EntelectHackathon.Level4;
using Xunit;

namespace Photospheria.Tests;

public class Level4Tests
{
    private static string GetRepoPath(string relative)
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, relative);
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            if (File.Exists(Path.Combine(dir, "Photospheria.slnx")))
            {
                return Path.GetFullPath(Path.Combine(dir, relative));
            }
            dir = Path.GetDirectoryName(dir);
        }

        return Path.GetFullPath(relative);
    }

    [Fact]
    public void L4Loader_LoadsAll31PlantsAndUnlocks()
    {
        string level = GetRepoPath("level4/Level4Solver/4.json");
        string plants = GetRepoPath("level4/Level4Solver/plant_dataset.json");
        string unlocks = GetRepoPath("level4/Level4Solver/plant_unlock_conditions.json");
        string animals = GetRepoPath("level4/Level4Solver/animals.json");
        string classes = GetRepoPath("level4/Level4Solver/classifications.json");

        Assert.True(File.Exists(level), $"File not found: {level}");
        Assert.True(File.Exists(plants), $"File not found: {plants}");

        var cfg = L4Loader.Load(level, plants, unlocks, animals, classes);

        Assert.Equal(200, cfg.Rows);
        Assert.Equal(300, cfg.Cols);
        Assert.Equal(800, cfg.Ticks);
        Assert.True(cfg.AnimalsEnabled);

        Assert.Equal(31, cfg.PlantsByIndex.Count(p => p.Index > 0));
        Assert.Equal(5, cfg.StartingPlants.Count);
        Assert.Contains((byte)1, cfg.StartingPlants); // Grass
        Assert.Contains((byte)2, cfg.StartingPlants); // Rose Bush
        Assert.Contains((byte)5, cfg.StartingPlants); // Sunflower
        Assert.Contains((byte)6, cfg.StartingPlants); // Lavender
        Assert.Contains((byte)12, cfg.StartingPlants); // Oak Tree

        Assert.Equal(26, cfg.UnlockByPlant.Count);
        Assert.Equal(10, cfg.Animals.Length);
    }

    [Fact]
    public void L4Engine_StartingPlantsAvailable_OthersLocked()
    {
        string level = GetRepoPath("level4/Level4Solver/4.json");
        string plants = GetRepoPath("level4/Level4Solver/plant_dataset.json");
        string unlocks = GetRepoPath("level4/Level4Solver/plant_unlock_conditions.json");
        string animals = GetRepoPath("level4/Level4Solver/animals.json");
        string classes = GetRepoPath("level4/Level4Solver/classifications.json");

        var cfg = L4Loader.Load(level, plants, unlocks, animals, classes);
        var engine = new L4Engine(cfg);

        // Starting plants must be unlocked
        Assert.True(engine.IsUnlocked(1));
        Assert.True(engine.IsUnlocked(2));
        Assert.True(engine.IsUnlocked(5));
        Assert.True(engine.IsUnlocked(6));
        Assert.True(engine.IsUnlocked(12));

        // Other plants must be locked initially
        Assert.False(engine.IsUnlocked(3)); // Blue Moss
        Assert.False(engine.IsUnlocked(9)); // Glowcap Fungus
        Assert.False(engine.IsUnlocked(31)); // Worldtree Sapling
    }

    [Fact]
    public void L4Engine_OakShade_KillsGrassInShade()
    {
        string level = GetRepoPath("level4/Level4Solver/4.json");
        string plants = GetRepoPath("level4/Level4Solver/plant_dataset.json");
        string unlocks = GetRepoPath("level4/Level4Solver/plant_unlock_conditions.json");
        string animals = GetRepoPath("level4/Level4Solver/animals.json");
        string classes = GetRepoPath("level4/Level4Solver/classifications.json");

        var cfg = L4Loader.Load(level, plants, unlocks, animals, classes);
        var engine = new L4Engine(cfg);

        // Plant an Oak Tree at (10, 10)
        Assert.True(engine.TryPlant(12, 10, 10));

        // Advance until Oak reaches maturity (TTM = 20)
        for (int t = 0; t < 22; t++)
        {
            engine.Step(ReadOnlySpan<L4Planting>.Empty);
        }

        // Cell (10, 11) within radius 4 must now be shaded
        Assert.True(engine.Cell(10, 11).Shaded);

        // Grass planted in shaded cell must die under weakness culls
        Assert.True(engine.TryPlant(1, 10, 11));
        engine.Step(ReadOnlySpan<L4Planting>.Empty);
        Assert.False(engine.Cell(10, 11).Occupied);
    }

    [Fact]
    public void L4Solver_PlanRespectsConstraints()
    {
        string level = GetRepoPath("level4/Level4Solver/4.json");
        string plants = GetRepoPath("level4/Level4Solver/plant_dataset.json");
        string unlocks = GetRepoPath("level4/Level4Solver/plant_unlock_conditions.json");
        string animals = GetRepoPath("level4/Level4Solver/animals.json");
        string classes = GetRepoPath("level4/Level4Solver/classifications.json");

        var cfg = L4Loader.Load(level, plants, unlocks, animals, classes);
        var engine = new L4Engine(cfg);
        var solver = new L4Solver(engine);

        var plan = solver.GeneratePlan();

        Assert.NotEmpty(plan);
        var grouped = plan.GroupBy(p => p.Tick);
        foreach (var g in grouped)
        {
            Assert.InRange(g.Key, 0, cfg.Ticks - 1);
            Assert.True(g.Count() <= 20, $"Tick {g.Key} exceeded 20 plantings (count={g.Count()})");
            foreach (var p in g)
            {
                Assert.InRange(p.Row, 0, cfg.Rows - 1);
                Assert.InRange(p.Col, 0, cfg.Cols - 1);
                Assert.InRange(p.PlantIndex, (byte)1, (byte)31);
            }
        }
    }
}
