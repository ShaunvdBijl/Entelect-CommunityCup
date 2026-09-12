using System;
using System.Collections.Generic;
using System.Linq;
using Photospheria.Core.Model;
using Photospheria.Core.Scoring;
using Photospheria.Core.Simulation;

namespace Photospheria.Solver.Zoning;

public class MacroZoningSolver
{
    private readonly SimulationConfig _config;
    private readonly SectorZoning _zoning;

    public MacroZoningSolver(SimulationConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _zoning = new SectorZoning(config.Width, config.Height);
    }

    public List<PlantingAction> GeneratePlan()
    {
        int totalTicks = _config.TotalTicks;
        int width = _config.Width;
        int height = _config.Height;

        // Group cell coordinates by species sector
        var sectorCellsBySpecies = new Dictionary<byte, List<(int Row, int Col)>>();
        foreach (var species in Level1Catalogue.WhitelistIndices)
        {
            sectorCellsBySpecies[species] = new List<(int Row, int Col)>();
        }

        for (int r = 0; r < height; r++)
        {
            for (int c = 0; c < width; c++)
            {
                if (_config.TerrainMap != null && _config.TerrainMap[r, c] != 0) continue;
                if (_config.SoilMap != null && _config.SoilMap[r, c] > 1) continue;

                byte sp = _zoning.GetSpeciesForCell(r, c);
                sectorCellsBySpecies[sp].Add((r, c));
            }
        }

        bool usePriming = totalTicks >= 300;

        // Staggered planting start ticks based on species maturity and spread velocity:
        // When priming is enabled, terminal plants survive twice as long on dead matter soil!
        var startOffsets = usePriming
            ? new Dictionary<byte, int>
            {
                [Level1Catalogue.OakTreeIndex] = Math.Min(180, totalTicks - 50),
                [Level1Catalogue.RoseBushIndex] = Math.Min(165, totalTicks - 50),
                [Level1Catalogue.LavenderIndex] = Math.Min(140, totalTicks - 40),
                [Level1Catalogue.SunflowerIndex] = Math.Min(130, totalTicks - 40),
                [Level1Catalogue.GrassIndex] = Math.Min(30, totalTicks - 10)
            }
            : new Dictionary<byte, int>
            {
                [Level1Catalogue.OakTreeIndex] = Math.Min(85, totalTicks - 25),
                [Level1Catalogue.RoseBushIndex] = Math.Min(75, totalTicks - 25),
                [Level1Catalogue.LavenderIndex] = Math.Min(60, totalTicks - 20),
                [Level1Catalogue.SunflowerIndex] = Math.Min(50, totalTicks - 20),
                [Level1Catalogue.GrassIndex] = Math.Min(22, totalTicks - 5)
            };

        var scheduledActions = new List<PlantingAction>();
        var actionsPerTick = new int[totalTicks];

        // 1. Soil Priming Phase (ADR-0002):
        // Plant sacrificial Grass early so it starves by tick 100, leaving dead matter.
        // Soil regenerates from tick 100 to 200, allowing terminal crops to consume 0.5/tick!
        if (usePriming)
        {
            int primeTick = 0;
            for (int r = 1; r < height - 1; r += 4)
            {
                for (int c = 1; c < width - 1; c += 4)
                {
                    if (_config.TerrainMap != null && _config.TerrainMap[r, c] != 0) continue;
                    if (_config.SoilMap != null && _config.SoilMap[r, c] > 1) continue;

                    while (primeTick < 10 && actionsPerTick[primeTick] >= 20)
                    {
                        primeTick++;
                    }
                    if (primeTick >= 10) break;

                    scheduledActions.Add(new PlantingAction(primeTick, r, c, Level1Catalogue.GrassIndex));
                    actionsPerTick[primeTick]++;
                }
            }
        }

        // 1. Initial Staggered Seeding per Sector
        foreach (var species in Level1Catalogue.WhitelistIndices)
        {
            int offset = startOffsets[species];
            int startTick = Math.Max(0, totalTicks - offset);
            var cells = sectorCellsBySpecies[species];

            // Stride based on spread range and rate:
            // Slower spreaders get denser seed grids; fast spreaders get wider grids
            int stride = species switch
            {
                Level1Catalogue.GrassIndex => 5,
                Level1Catalogue.RoseBushIndex => 3,
                Level1Catalogue.SunflowerIndex => 3,
                Level1Catalogue.LavenderIndex => 3,
                Level1Catalogue.OakTreeIndex => 4,
                _ => 3
            };

            var seedCandidates = cells.Where(pt => (pt.Row % stride == 0) && (pt.Col % stride == 0)).ToList();
            if (seedCandidates.Count == 0)
            {
                seedCandidates = cells;
            }

            int curTick = startTick;
            foreach (var (r, c) in seedCandidates)
            {
                while (curTick < totalTicks - 1 && actionsPerTick[curTick] >= 20)
                {
                    curTick++;
                }

                if (curTick >= totalTicks - 1) break;

                scheduledActions.Add(new PlantingAction(curTick, r, c, species));
                actionsPerTick[curTick]++;
            }
        }

        // 2. Terminal Balancing Pass:
        // Run forward simulation to evaluate species balance
        var simEngine = new SimulationEngine(_config);
        simEngine.ScheduleActions(scheduledActions);
        simEngine.RunToEnd();

        var score = ScoringEngine.Evaluate(simEngine);
        int totalPlants = score.TotalPlants;

        if (totalPlants > 0)
        {
            int targetPerSpecies = totalPlants / 5;
            int terminalWindowStart = Math.Max(0, totalTicks - 12);

            // Find under-represented species
            var underRepresented = Level1Catalogue.WhitelistIndices
                .OrderBy(sp => score.SpeciesCounts.GetValueOrDefault(sp, 0))
                .ToList();

            int balanceTick = terminalWindowStart;

            foreach (var species in underRepresented)
            {
                int deficit = targetPerSpecies - score.SpeciesCounts.GetValueOrDefault(species, 0);
                if (deficit <= 0) continue;

                var availableCells = sectorCellsBySpecies[species]
                    .Where(pt => !simEngine.GetCell(pt.Row, pt.Col).IsOccupied)
                    .ToList();

                int toPlant = Math.Min(deficit, availableCells.Count);
                for (int i = 0; i < toPlant; i++)
                {
                    while (balanceTick < totalTicks - 1 && actionsPerTick[balanceTick] >= 20)
                    {
                        balanceTick++;
                    }

                    if (balanceTick >= totalTicks - 1) break;

                    var pt = availableCells[i];
                    scheduledActions.Add(new PlantingAction(balanceTick, pt.Row, pt.Col, species));
                    actionsPerTick[balanceTick]++;
                }
            }
        }

        return scheduledActions;
    }
}
