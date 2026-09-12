using System;
using System.Collections.Generic;
using Photospheria.Core.Model;
using Photospheria.Core.Simulation;

namespace Photospheria.Core.Scoring;

public readonly record struct ScoreBreakdown(
    double Entropy,
    double Coverage,
    double MainScore,
    double LongevityScore,
    double FinalScore,
    int TotalPlants,
    int TotalCells,
    IReadOnlyDictionary<byte, int> SpeciesCounts
);

public static class ScoringEngine
{
    public static double CalculateEntropy(ReadOnlySpan<int> counts, int totalSpecies)
    {
        if (totalSpecies <= 1) return 0.0;

        int totalCount = 0;
        for (int i = 0; i < counts.Length; i++)
        {
            totalCount += counts[i];
        }

        if (totalCount == 0) return 0.0;

        double sum = 0.0;
        double logBase = Math.Log(totalSpecies);

        for (int i = 0; i < counts.Length; i++)
        {
            if (counts[i] <= 0) continue;
            double p = (double)counts[i] / totalCount;
            sum += p * (Math.Log(p) / logBase);
        }

        return -sum;
    }

    public static ScoreBreakdown Evaluate(SimulationEngine engine, double alpha = 1.0, double k = 1.0)
    {
        int width = engine.Width;
        int height = engine.Height;
        int totalCells = width * height;
        int totalTicks = engine.TotalTicks;

        var speciesCounts = new Dictionary<byte, int>();
        foreach (var species in Level1Catalogue.WhitelistIndices)
        {
            speciesCounts[species] = 0;
        }

        int totalPlants = 0;
        double longevitySum = 0.0;

        for (int r = 0; r < height; r++)
        {
            for (int c = 0; c < width; c++)
            {
                ref readonly var cell = ref engine.GetCell(r, c);
                if (cell.IsOccupied)
                {
                    totalPlants++;
                    if (!speciesCounts.TryGetValue(cell.PlantIndex, out int currentCount))
                    {
                        speciesCounts[cell.PlantIndex] = 1;
                    }
                    else
                    {
                        speciesCounts[cell.PlantIndex] = currentCount + 1;
                    }

                    double normalizedLifespan = totalTicks > 0 ? (double)cell.Age / totalTicks : 0.0;
                    longevitySum += Math.Pow(normalizedLifespan, k);
                }
            }
        }

        Span<int> countsSpan = stackalloc int[Level1Catalogue.WhitelistIndices.Length];
        for (int i = 0; i < Level1Catalogue.WhitelistIndices.Length; i++)
        {
            speciesCounts.TryGetValue(Level1Catalogue.WhitelistIndices[i], out countsSpan[i]);
        }

        double entropy = CalculateEntropy(countsSpan, Level1Catalogue.WhitelistIndices.Length);
        double coverage = totalCells > 0 ? (double)totalPlants / totalCells : 0.0;
        double sampleSizeFactor = Math.Pow(coverage, alpha);
        double mainScore = entropy * sampleSizeFactor;
        double longevityScore = totalCells > 0 ? longevitySum / totalCells : 0.0;
        double finalScore = 0.8 * mainScore + 0.2 * longevityScore;

        return new ScoreBreakdown(
            Entropy: entropy,
            Coverage: coverage,
            MainScore: mainScore,
            LongevityScore: longevityScore,
            FinalScore: finalScore,
            TotalPlants: totalPlants,
            TotalCells: totalCells,
            SpeciesCounts: speciesCounts
        );
    }
}
