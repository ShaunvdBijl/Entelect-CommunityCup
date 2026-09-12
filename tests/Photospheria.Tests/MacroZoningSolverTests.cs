using System.Linq;
using Photospheria.Core.Model;
using Photospheria.Core.Scoring;
using Photospheria.Core.Simulation;
using Photospheria.Serialization.Validation;
using Photospheria.Solver.Zoning;
using Xunit;

namespace Photospheria.Tests;

public class MacroZoningSolverTests
{
    [Fact]
    public void MacroZoningStrategy_PartitionsGridInto5Sectors()
    {
        var zoning = new SectorZoning(width: 50, height: 50);

        // Every cell should map to a valid sector [0..4]
        for (int r = 0; r < 50; r++)
        {
            for (int c = 0; c < 50; c++)
            {
                byte species = zoning.GetSpeciesForCell(r, c);
                Assert.True(Level1Catalogue.IsValidLevel1Plant(species));
            }
        }

        // Oak Tree zone should be isolated from Grass zone by >= 4 cells
        int minDistance = zoning.GetMinDistanceBetweenSpecies(Level1Catalogue.OakTreeIndex, Level1Catalogue.GrassIndex);
        Assert.True(minDistance >= 4, $"Distance {minDistance} between Oak and Grass is < 4 cells!");
    }

    [Fact]
    public void MacroZoningSolver_GeneratesValidAndHighScoringPlan()
    {
        var config = new SimulationConfig { Width = 50, Height = 50, TotalTicks = 100 };
        var solver = new MacroZoningSolver(config);

        var actions = solver.GeneratePlan();

        // 1. Must satisfy all invariants
        var validation = InvariantValidator.Validate(actions, config);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));

        // 2. Run forward simulation
        var engine = new SimulationEngine(config);
        engine.ScheduleActions(actions);
        engine.RunToEnd();

        // 3. Evaluate score
        var score = ScoringEngine.Evaluate(engine);

        // Should have positive coverage and all 5 species present
        string countsStr = string.Join(", ", score.SpeciesCounts.Select(kv => $"{kv.Key}:{kv.Value}"));
        Assert.True(score.Coverage > 0.3, $"Coverage {score.Coverage:P1} is too low. Counts: {countsStr}");
        Assert.True(score.Entropy > 0.8, $"Entropy {score.Entropy:F3} is too low. Counts: {countsStr}");
        Assert.True(score.FinalScore > 0.2, $"Final score {score.FinalScore:F3} is too low. Counts: {countsStr}");

        // Verify all 5 species are present in the final sample
        foreach (var species in Level1Catalogue.WhitelistIndices)
        {
            Assert.True(score.SpeciesCounts[species] > 0, $"Species {species} has 0 plants");
        }
    }

    [Fact]
    public void MacroZoningSolver_FullScaleLevel1_AchievesHighEntropyAndScore()
    {
        var config = new SimulationConfig { Width = 100, Height = 100, TotalTicks = 200 };
        var solver = new MacroZoningSolver(config);

        var actions = solver.GeneratePlan();

        // 1. Invariant validation
        var validation = InvariantValidator.Validate(actions, config);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));

        // 2. Simulation
        var engine = new SimulationEngine(config);
        engine.ScheduleActions(actions);
        engine.RunToEnd();

        // 3. Evaluation
        var score = ScoringEngine.Evaluate(engine);

        Assert.True(score.Coverage > 0.35, $"Coverage {score.Coverage:P1} is below 35%");
        Assert.True(score.Entropy > 0.85, $"Entropy {score.Entropy:F3} is below 0.85");
        Assert.True(score.FinalScore > 0.25, $"Final score {score.FinalScore:F3} is below 0.25");
    }
}
