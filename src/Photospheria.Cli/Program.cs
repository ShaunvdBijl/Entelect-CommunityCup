using System;
using System.IO;
using System.Linq;
using Photospheria.Core.Model;
using Photospheria.Core.Scoring;
using Photospheria.Core.Simulation;
using Photospheria.Serialization.Schema;
using Photospheria.Serialization.Validation;
using Photospheria.Solver.Zoning;

namespace Photospheria.Cli;

public class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("=================================================");
        Console.WriteLine("  Photospheria Level 1 Solver & Simulation Engine ");
        Console.WriteLine("=================================================");

        int width = 100;
        int height = 100;
        int ticks = 200;
        string outputPath = "submission.json";
        string? schemaPath = null;
        string? levelPath = null;
        SerializationKeyMode mode = SerializationKeyMode.Both;

        // Parse CLI arguments
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--level" or "-l" when i + 1 < args.Length:
                    levelPath = args[++i];
                    break;
                case "--width" or "-w" when i + 1 < args.Length:
                    width = int.Parse(args[++i]);
                    break;
                case "--height" or "-h" when i + 1 < args.Length:
                    height = int.Parse(args[++i]);
                    break;
                case "--ticks" or "-t" when i + 1 < args.Length:
                    ticks = int.Parse(args[++i]);
                    break;
                case "--out" or "-o" when i + 1 < args.Length:
                    outputPath = args[++i];
                    break;
                case "--schema" or "-s" when i + 1 < args.Length:
                    schemaPath = args[++i];
                    break;
                case "--mode" when i + 1 < args.Length:
                    mode = Enum.Parse<SerializationKeyMode>(args[++i], ignoreCase: true);
                    break;
            }
        }

        if (string.IsNullOrEmpty(levelPath))
        {
            string[] defaultCandidates = ["1 (1).json", "1.json", "level1.json"];
            foreach (var candidate in defaultCandidates)
            {
                if (File.Exists(candidate))
                {
                    levelPath = candidate;
                    break;
                }
            }
        }

        SimulationConfig config;
        if (!string.IsNullOrEmpty(levelPath) && File.Exists(levelPath))
        {
            Console.WriteLine($"Loading level configuration from: {levelPath}");
            config = LevelLoader.LoadFromFile(levelPath);
            width = config.Width;
            height = config.Height;
            ticks = config.TotalTicks;
        }
        else
        {
            config = new SimulationConfig
            {
                Width = width,
                Height = height,
                TotalTicks = ticks
            };
        }

        Console.WriteLine($"Grid: {width}x{height} cells, Ticks: {ticks}");
        Console.WriteLine($"Output file: {outputPath}");

        // 1. Generate plan using Macro-Zoning and Staggered Scheduling Solver
        Console.WriteLine("\n[1/4] Generating plan using Macro-Zoning & Terminal Scheduling...");
        var solver = new MacroZoningSolver(config);
        var plan = solver.GeneratePlan();
        Console.WriteLine($"Generated {plan.Count} planting actions across {plan.Select(a => a.Tick).Distinct().Count()} ticks.");

        // 2. Validate Hard Invariants
        Console.WriteLine("\n[2/4] Validating submission invariants...");
        var validation = InvariantValidator.Validate(plan, config);
        if (!validation.IsValid)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("INVARIANT VIOLATION DETECTED:");
            foreach (var err in validation.Errors)
            {
                Console.WriteLine($"  - {err}");
            }
            Console.ResetColor();
            return 1;
        }
        Console.WriteLine("All hard invariants satisfied: <= 20 actions/tick, Level 1 whitelist, valid coordinates and ticks.");

        // 3. Forward Simulation and Score Evaluation
        Console.WriteLine("\n[3/4] Running forward rollout simulation...");
        var engine = new SimulationEngine(config);
        engine.ScheduleActions(plan);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        engine.RunToEnd();
        stopwatch.Stop();

        var score = ScoringEngine.Evaluate(engine);
        Console.WriteLine($"Simulation completed in {stopwatch.ElapsedMilliseconds} ms.");
        Console.WriteLine("---------------- Score Breakdown ----------------");
        Console.WriteLine($"Coverage:        {score.Coverage:P2} ({score.TotalPlants} / {score.TotalCells} cells)");
        Console.WriteLine($"Shannon Entropy: {score.Entropy:F4} (1.0 = perfect uniform balance)");
        Console.WriteLine($"Main Score:      {score.MainScore:F4}");
        Console.WriteLine($"Longevity Score: {score.LongevityScore:F4}");
        Console.WriteLine($"Final Score:     {score.FinalScore:F4} (0.8*Main + 0.2*Longevity)");
        Console.WriteLine("Species Distribution:");
        foreach (var species in Level1Catalogue.WhitelistIndices)
        {
            ref readonly var def = ref Level1Catalogue.Get(species);
            int count = score.SpeciesCounts.GetValueOrDefault(species, 0);
            double pct = score.TotalPlants > 0 ? (double)count / score.TotalPlants : 0.0;
            Console.WriteLine($"  [{def.Index,2}] {def.Name,-15}: {count,5} cells ({pct:P1})");
        }
        Console.WriteLine("-------------------------------------------------");

        // 4. Adaptive Serialization
        Console.WriteLine("\n[4/4] Serializing submission...");
        if (!string.IsNullOrEmpty(schemaPath) && File.Exists(schemaPath))
        {
            string schemaJson = File.ReadAllText(schemaPath);
            mode = AdaptiveActionSerializer.DetectKeyModeFromSchema(schemaJson);
            Console.WriteLine($"Schema detected key mode: {mode}");
        }
        else
        {
            Console.WriteLine($"Serializing with key mode: {mode} (includes both 'index' and 'plant_index')");
        }

        AdaptiveActionSerializer.SaveToFile(outputPath, plan, schemaPath);
        Console.WriteLine($"Submission successfully written to {Path.GetFullPath(outputPath)}");

        return 0;
    }
}
