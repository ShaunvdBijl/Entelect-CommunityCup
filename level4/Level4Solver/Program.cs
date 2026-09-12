using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace EntelectHackathon.Level4;

public static class Program
{
    public static int Main(string[] args)
    {
        string baseDir = AppContext.BaseDirectory;
        string projectData = Path.GetFullPath(Path.Combine(baseDir, "data"));
        string srcData = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "data"));
        string currDir = Directory.GetCurrentDirectory();
        string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        string extra = Path.Combine(downloads, "additional-resources");
        string repoResources = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "resources"));

        string[] roots = [baseDir, currDir, projectData, srcData, extra, downloads, repoResources];

        string levelPath = GetArg(args, "--config") ?? FirstExisting(roots, "4.json");
        string plantPath = GetArg(args, "--plants") ?? FirstExisting(roots, "plant_dataset.json");
        string unlockPath = GetArg(args, "--unlocks") ?? FirstExisting(roots, "plant_unlock_conditions.json");
        string animalPath = GetArg(args, "--animals") ?? FirstExisting(roots, "animals.json");
        string classPath = GetArg(args, "--classifications") ?? FirstExisting(roots, "classifications.json");
        string outputPath = GetArg(args, "--out") ?? "submission_level4.json";

        Console.WriteLine("Entelect Hackathon — Level 4 Forest (isolated solver)");
        Console.WriteLine($"Config: {levelPath}");

        if (!File.Exists(levelPath) || !File.Exists(plantPath))
        {
            Console.Error.WriteLine($"Missing input files. Checked roots:\n{string.Join("\n", roots)}");
            return 1;
        }

        var cfg = L4Loader.Load(levelPath, plantPath, unlockPath, animalPath, classPath);
        Console.WriteLine($"Grid {cfg.Rows}x{cfg.Cols}, ticks {cfg.Ticks}, animals={cfg.AnimalsEnabled}");
        Console.WriteLine($"Starting species: {string.Join(", ", cfg.StartingPlants.OrderBy(x => x))}");

        var engine = new L4Engine(cfg);
        var solver = new L4Solver(engine);
        var sw = Stopwatch.StartNew();
        var plan = solver.GeneratePlan();
        sw.Stop();
        Console.WriteLine($"Plan generated: {plan.Count} plantings in {sw.ElapsedMilliseconds} ms");

        var replay = new L4Engine(cfg);
        replay.RunScheduled(plan);
        var score = replay.Score();
        Console.WriteLine($"Coverage {score.Coverage:P2}  Entropy {score.Entropy:F4}  Longevity {score.Longevity:F4}");
        Console.WriteLine($"Distinct species {score.Distinct}  Final {score.Final:F4}");
        Console.WriteLine($"Active fauna: {string.Join(", ", replay.ActiveAnimals)}");
        Console.Write("Unlocked:");
        for (byte i = 1; i <= 31; i++)
        {
            if (replay.IsUnlocked(i)) Console.Write($" {i}");
        }
        Console.WriteLine();
        Console.WriteLine("Final species breakdown:");
        for (byte i = 1; i <= 31; i++)
        {
            int c = replay.SpeciesCount(i);
            if (c > 0)
            {
                double share = (double)c / Math.Max(1, replay.OccupiedCount);
                Console.WriteLine($"  [{i,2}] {cfg.PlantsByIndex[i].Name,-24}: {c,5} ({share:P2})");
            }
        }

        WriteSubmission(outputPath, plan);
        Console.WriteLine($"Wrote submission to {Path.GetFullPath(outputPath)}");
        return 0;
    }

    private static string? GetArg(string[] args, string key)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static string FirstExisting(string[] roots, string file)
    {
        string p = L4Pathing.Resolve(file, roots);
        return File.Exists(p) ? p : file;
    }

    private static void WriteSubmission(string path, List<L4Planting> plan)
    {
        var grouped = plan
            .GroupBy(a => a.Tick)
            .OrderBy(g => g.Key);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("actions");
            foreach (var group in grouped)
            {
                writer.WriteStartObject();
                writer.WriteNumber("tick", group.Key);
                writer.WriteStartArray("plants");
                int n = 0;
                foreach (var a in group)
                {
                    if (n++ >= 20) break;
                    writer.WriteStartObject();
                    writer.WriteNumber("index", a.PlantIndex);
                    writer.WriteNumber("plant_index", a.PlantIndex);
                    writer.WriteNumber("row", a.Row);
                    writer.WriteNumber("col", a.Col);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        File.WriteAllText(path, Encoding.UTF8.GetString(stream.ToArray()));
    }
}
