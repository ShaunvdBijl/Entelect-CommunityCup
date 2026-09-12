using System.Text;
using System.Text.Json;

namespace EntelectHackathon.Level3;

public static class Program
{
    public static int Main(string[] args)
    {
        string baseDir = AppContext.BaseDirectory;
        string projectData = Path.GetFullPath(Path.Combine(baseDir, "data"));
        string srcData = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "data"));
        string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        string extra = Path.Combine(downloads, "additional-resources");
        string repoResources = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "resources"));

        string[] roots = [projectData, srcData, extra, downloads, repoResources, Directory.GetCurrentDirectory()];

        string levelPath = GetArg(args, "--config") ?? L3Pathing.Resolve("3.json", roots);
        string plantPath = GetArg(args, "--plants") ?? FirstExisting(roots, "plant_dataset.json");
        string unlockPath = GetArg(args, "--unlocks") ?? FirstExisting(roots, "plant_unlock_conditions.json");
        string animalPath = GetArg(args, "--animals") ?? FirstExisting(roots, "animals.json");
        string classPath = GetArg(args, "--classifications") ?? FirstExisting(roots, "classifications.json");
        string outputPath = GetArg(args, "--out") ?? "submission_level3.json";

        Console.WriteLine("Entelect Hackathon — Level 3 Park Potential (isolated solver)");
        Console.WriteLine($"Config: {levelPath}");

        if (!File.Exists(levelPath) || !File.Exists(plantPath))
        {
            Console.Error.WriteLine("Missing input JSON. Pass --config/--plants or place files in Level3/data.");
            return 1;
        }

        var cfg = L3Loader.Load(levelPath, plantPath, unlockPath, animalPath, classPath);
        Console.WriteLine($"Grid {cfg.Rows}x{cfg.Cols}, ticks {cfg.Ticks}, animals={cfg.AnimalsEnabled}");
        Console.WriteLine($"Starting species: {string.Join(", ", cfg.StartingPlants.OrderBy(x => x))}");

        var engine = new L3Engine(cfg);
        var solver = new L3Solver(engine);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var plan = solver.GeneratePlan();
        sw.Stop();
        Console.WriteLine($"Plan generated: {plan.Count} plantings in {sw.ElapsedMilliseconds} ms");

        var replay = new L3Engine(cfg);
        replay.RunScheduled(plan);
        var score = replay.Score();
        Console.WriteLine($"Coverage {score.Coverage:P2}  Entropy {score.Entropy:F4}  Longevity {score.Longevity:F4}");
        Console.WriteLine($"Distinct species {score.Distinct}  Final {score.Final:F4}");
        Console.WriteLine($"Active fauna: {string.Join(", ", replay.ActiveAnimals)}");
        Console.Write("Unlocked:");
        for (byte i = 1; i < 32; i++)
        {
            if (replay.IsUnlocked(i)) Console.Write($" {i}");
        }
        Console.WriteLine();

        WriteSubmission(outputPath, plan);
        Console.WriteLine($"Wrote {Path.GetFullPath(outputPath)}");
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
        string p = L3Pathing.Resolve(file, roots);
        return File.Exists(p) ? p : file;
    }

    private static void WriteSubmission(string path, List<L3Planting> plan)
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
