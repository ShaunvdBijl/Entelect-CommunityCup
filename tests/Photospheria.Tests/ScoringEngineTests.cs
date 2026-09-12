using Photospheria.Core.Model;
using Photospheria.Core.Scoring;
using Photospheria.Core.Simulation;
using Xunit;

namespace Photospheria.Tests;

public class ScoringEngineTests
{
    [Fact]
    public void DiversityEntropy_UniformDistribution_EqualsOne()
    {
        // 5 species, each has 20 plants
        int[] counts = [20, 20, 20, 20, 20];
        double entropy = ScoringEngine.CalculateEntropy(counts, totalSpecies: 5);

        Assert.Equal(1.0, entropy, precision: 5);
    }

    [Fact]
    public void DiversityEntropy_Monoculture_EqualsZero()
    {
        // 1 species has all 100 plants
        int[] counts = [100, 0, 0, 0, 0];
        double entropy = ScoringEngine.CalculateEntropy(counts, totalSpecies: 5);

        Assert.Equal(0.0, entropy, precision: 5);
    }

    [Fact]
    public void ScoringEngine_ComputesExpectedFinalScore()
    {
        var config = new SimulationConfig { Width = 10, Height = 10, TotalTicks = 100 };
        var engine = new SimulationEngine(config);

        // Fill entire 10x10 grid uniformly with 20 of each 5 species, all aged 50 ticks
        byte[] speciesList = Level1Catalogue.WhitelistIndices;
        int cellCount = 0;
        for (int r = 0; r < 10; r++)
        {
            for (int c = 0; c < 10; c++)
            {
                byte species = speciesList[cellCount % 5];
                ref var cell = ref engine.GetCellMutable(r, c);
                cell.PlantIndex = species;
                cell.Age = 50;
                cellCount++;
            }
        }

        var breakdown = ScoringEngine.Evaluate(engine, alpha: 1.0, k: 1.0);

        // Entropy should be 1.0 (uniform distribution across 5 species)
        Assert.Equal(1.0, breakdown.Entropy, precision: 4);

        // Coverage should be 100/100 = 1.0
        Assert.Equal(1.0, breakdown.Coverage, precision: 4);

        // Main score = 1.0 * (1.0)^1 = 1.0
        Assert.Equal(1.0, breakdown.MainScore, precision: 4);

        // Longevity score = (1/100) * 100 * (50/100)^1 = 0.5
        Assert.Equal(0.5, breakdown.LongevityScore, precision: 4);

        // Final score = 0.8 * 1.0 + 0.2 * 0.5 = 0.8 + 0.1 = 0.9
        Assert.Equal(0.9, breakdown.FinalScore, precision: 4);
    }
}
