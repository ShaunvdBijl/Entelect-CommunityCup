using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RootCause;

public static class Program
{
    public static void Main(string[] args)
    {
        var mapPath = args.Length > 0 ? args[0] : "2.json";
        var outputPath = args.Length > 1 ? args[1] : "submission_level2.json";

        var sim = new ClosedLoopSim(mapPath);
        var actions = sim.Run();

        File.WriteAllText(outputPath, Submission.Serialize(actions));
        Console.WriteLine($"Wrote {outputPath} with {actions.Sum(a => a.Plants.Count)} valid actions.");
    }
}

internal sealed class ClosedLoopSim
{
    public const int Rows = 70;
    public const int Cols = 100;
    public const int TotalCells = Rows * Cols;
    public const int MaxTicks = 500;
    public const int RainTick = 250;

    public byte[] Species = new byte[TotalCells];
    public ushort[] Age = new ushort[TotalCells];
    public byte[] Nutrients = new byte[TotalCells];
    public byte[] Terrain = new byte[TotalCells];
    public byte[] Soil = new byte[TotalCells];
    public bool[] Shaded = new bool[TotalCells];
    public bool[] DeadMatter = new bool[TotalCells];

    public int[] Counts = new int[32];
    public int LivingCount = 0;
    public bool Loamcrawlers, Nectaris, Solwings, Virexids, Grazeleths;

    public ClosedLoopSim(string mapPath)
    {
        Array.Fill(Nutrients, (byte)100);
        LoadMap(mapPath);
    }

    private void LoadMap(string path)
    {
        string[] searchPaths = [path, "2.json", "../2.json", "data/2.json", "../data/2.json"];
        var found = searchPaths.FirstOrDefault(File.Exists);
        if (found == null) return;

        using var doc = JsonDocument.Parse(File.ReadAllText(found));
        foreach (var c in doc.RootElement.GetProperty("cells").EnumerateArray())
        {
            int r = c.GetProperty("row").GetInt32();
            int col = c.GetProperty("col").GetInt32();
            int i = r * Cols + col;
            Terrain[i] = (byte)c.GetProperty("terrain").GetInt32();
            Soil[i] = (byte)c.GetProperty("soil").GetInt32();
        }
    }

    public List<TickAction> Run()
    {
        var plan = new List<TickAction>();

        for (int tick = 0; tick < MaxTicks; tick++)
        {
            var scheduled = new List<PlantAction>(20);

            // 1. Maintain foundational nursery (Grass, Rose, Lavender) so fauna never despawn
            MaintainNurseries(tick, scheduled);

            // 2. Mid-game unlocks (Blue Moss, Mire Bloom after rain, Reeds)
            HandleUnlocks(tick, scheduled);

            // 3. Harvest Phase (Ticks 405-498): Deploy balanced species on pristine empty soil
            if (tick >= 405 && scheduled.Count < 20)
            {
                DeployHarvest(tick, scheduled);
            }

            // Execute scheduled actions
            foreach (var a in scheduled)
            {
                ApplyPlanting(a.plant_index, a.row, a.col);
            }

            if (scheduled.Count > 0)
            {
                plan.Add(new TickAction { Tick = tick, Plants = scheduled });
            }

            // Step natural world physics (Spread, Metabolism, Shade, Fauna)
            SimulateStep(tick);
        }

        return plan;
    }

    private void MaintainNurseries(int tick, List<PlantAction> batch)
    {
        // Keep living Grass >= 290 cells (4.1%) for Loamcrawlers and Verdelopes
        if (Counts[1] < 290 && batch.Count < 20 && tick < 400)
        {
            PlantSpecies(1, 290 - Counts[1], 0, 20, 0, 40, batch);
        }

        // Keep Rose Bush >= 25 cells for Loamcrawlers / Orange Blossom
        if (Counts[2] < 25 && batch.Count < 20 && tick < 400)
        {
            PlantSpecies(2, 25 - Counts[2], 0, 15, 41, 60, batch);
        }

        // Keep Lavender >= 20 cells for Virexids / Nectaris
        if (Counts[6] < 20 && batch.Count < 20 && tick < 400)
        {
            PlantSpecies(6, 20 - Counts[6], 16, 25, 41, 60, batch);
        }
    }

    private void HandleUnlocks(int tick, List<PlantAction> batch)
    {
        // Blue Moss (requires Loamcrawlers + Grass > 3% + Rose Bush > 1%)
        if (Loamcrawlers && Counts[1] > 210 && Counts[2] > 70 && Counts[3] < 200 && tick < 380)
        {
            PlantSpecies(3, 20, 0, 25, 61, 99, batch);
        }

        // Mire Bloom (requires Blue Moss > 5% [350 cells] + Rain at tick 250)
        if (tick >= RainTick && Counts[3] > 350 && Counts[18] < 50)
        {
            PlantMireBloom(batch);
        }

        // Oaks in central grove (Takes 20 ticks to cast shade)
        if (tick == 401 && Counts[12] < 80)
        {
            PlantOakGrove(batch);
        }
    }

    private void DeployHarvest(int tick, List<PlantAction> batch)
    {
        byte[] targetPalette = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 15, 16, 18, 19];
        var prioritized = targetPalette.OrderBy(p => Counts[p]).ToArray();

        foreach (var p in prioritized)
        {
            if (batch.Count >= 20) break;
            if (!IsUnlocked(p, tick)) continue;

            if (p == 18) PlantMireBloom(batch);
            else if (p == 11) PlantStoneReed(batch);
            else if (p == 15) PlantMoonpetal(batch);
            else PlantSpecies(p, 1, 26, 69, 0, 99, batch);
        }
    }

    private bool IsUnlocked(byte plant, int tick) => plant switch
    {
        1 or 2 or 5 or 6 or 12 => true,
        3 => Loamcrawlers && Counts[1] > 210 && Counts[2] > 70,
        4 => (Nectaris || Grazeleths) && Counts[2] > 0 && Counts[6] > 0,
        7 => (Nectaris || Solwings) && Counts[2] > 140,
        8 => (Loamcrawlers || Grazeleths) && Counts[3] > 210,
        9 => Loamcrawlers && DeadMatterCount() > 350,
        10 => Counts[3] > 70 && Counts[4] > 280,
        11 => Virexids,
        15 => Nectaris && Counts[6] > 280 && Counts[3] > 140,
        16 => Grazeleths && Counts[2] > 280,
        18 => tick >= RainTick && Counts[3] > 350,
        19 => Counts[1] > 350,
        _ => false
    };

    private int DeadMatterCount() => DeadMatter.Count(d => d);

    private void PlantSpecies(byte p, int maxCount, int r0, int r1, int c0, int c1, List<PlantAction> batch)
    {
        for (int r = r0; r <= r1 && batch.Count < 20 && maxCount > 0; r++)
        for (int c = c0; c <= c1 && batch.Count < 20 && maxCount > 0; c++)
        {
            int i = r * Cols + c;
            // STRICT ENGINE RULE: Must be habitable, correct soil, and STRICTLY UNOCCUPIED
            if (Terrain[i] == 0 && (Soil[i] == 0 || Soil[i] == 1) && Species[i] == 0)
            {
                if (batch.Any(b => b.row == r && b.col == c)) continue;
                batch.Add(new PlantAction { plant_index = p, row = r, col = c });
                maxCount--;
            }
        }
    }

    private void PlantMireBloom(List<PlantAction> batch)
    {
        for (int r = 0; r < Rows && batch.Count < 20; r++)
        for (int c = 0; c < Cols && batch.Count < 20; c++)
        {
            int i = r * Cols + c;
            if (Terrain[i] == 0 && Soil[i] == 2 && Species[i] == 0 && IsAdj(r, c, 1))
            {
                if (batch.Any(b => b.row == r && b.col == c)) continue;
                batch.Add(new PlantAction { plant_index = 18, row = r, col = c });
            }
        }
    }

    private void PlantStoneReed(List<PlantAction> batch)
    {
        for (int r = 0; r < Rows && batch.Count < 20; r++)
        for (int c = 0; c < Cols && batch.Count < 20; c++)
        {
            int i = r * Cols + c;
            if (Terrain[i] == 0 && Species[i] == 0 && IsAdj(r, c, 2))
            {
                if (batch.Any(b => b.row == r && b.col == c)) continue;
                batch.Add(new PlantAction { plant_index = 11, row = r, col = c });
            }
        }
    }

    private void PlantMoonpetal(List<PlantAction> batch)
    {
        for (int r = 20; r <= 34 && batch.Count < 20; r++)
        for (int c = 40; c <= 60 && batch.Count < 20; c++)
        {
            int i = r * Cols + c;
            if (Terrain[i] == 0 && Species[i] == 0 && Shaded[i])
            {
                if (batch.Any(b => b.row == r && b.col == c)) continue;
                batch.Add(new PlantAction { plant_index = 15, row = r, col = c });
            }
        }
    }

    private void PlantOakGrove(List<PlantAction> batch)
    {
        for (int r = 22; r <= 32 && batch.Count < 20; r += 2)
        for (int c = 42; c <= 58 && batch.Count < 20; c += 2)
        {
            int i = r * Cols + c;
            if (Terrain[i] == 0 && Species[i] == 0)
            {
                if (batch.Any(b => b.row == r && b.col == c)) continue;
                batch.Add(new PlantAction { plant_index = 12, row = r, col = c });
            }
        }
    }

    private bool IsAdj(int r, int c, byte terrain)
    {
        int[] dr = [-1, 1, 0, 0], dc = [0, 0, -1, 1];
        for (int d = 0; d < 4; d++)
        {
            int nr = r + dr[d], nc = c + dc[d];
            if ((uint)nr < Rows && (uint)nc < Cols && Terrain[nr * Cols + nc] == terrain) return true;
        }
        return false;
    }

    private void ApplyPlanting(int plant, int r, int c)
    {
        int i = r * Cols + c;
        Species[i] = (byte)plant;
        Age[i] = 0;
        Nutrients[i] = 100;
        Counts[plant]++;
        LivingCount++;
    }

    private void SimulateStep(int tick)
    {
        // 1. Natural spread (forward-projecting exact occupancy so we never collide)
        var nextSpecies = (byte[])Species.Clone();
        for (int r = 0; r < Rows; r++)
        for (int c = 0; c < Cols; c++)
        {
            int i = r * Cols + c;
            byte p = Species[i];
            if (p == 0) continue;

            Age[i]++;
            // Grass natural spread (VonNeumann, rate 2, maturity 1)
            if (p == 1 && Age[i] >= 1 && Age[i] % 2 == 0)
            {
                SpreadTo(r - 1, c, 1, nextSpecies);
                SpreadTo(r + 1, c, 1, nextSpecies);
                SpreadTo(r, c - 1, 1, nextSpecies);
                SpreadTo(r, c + 1, 1, nextSpecies);
            }
        }
        Species = nextSpecies;

        // 2. Oak Shade
        Array.Fill(Shaded, false);
        for (int r = 0; r < Rows; r++)
        for (int c = 0; c < Cols; c++)
        {
            int i = r * Cols + c;
            if (Species[i] == 12 && Age[i] >= 20)
            {
                for (int dr = -4; dr <= 4; dr++)
                for (int dc = -4; dc <= 4; dc++)
                {
                    int nr = r + dr, nc = c + dc;
                    if ((uint)nr < Rows && (uint)nc < Cols) Shaded[nr * Cols + nc] = true;
                }
            }
        }

        // 3. Metabolic Nutrient Decay & Starvation
        for (int i = 0; i < TotalCells; i++)
        {
            if (Species[i] != 0)
            {
                if (Shaded[i] && Species[i] == 1) // Grass dies in shade
                {
                    Kill(i);
                    continue;
                }

                Nutrients[i]--;
                if (Nutrients[i] == 0)
                {
                    Kill(i);
                }
            }
            else if (DeadMatter[i] && Nutrients[i] < 100)
            {
                Nutrients[i]++;
            }
        }

        // 4. Update Fauna
        Array.Clear(Counts);
        LivingCount = 0;
        for (int i = 0; i < TotalCells; i++)
        {
            if (Species[i] != 0)
            {
                Counts[Species[i]]++;
                LivingCount++;
            }
        }

        Loamcrawlers = Counts[1] >= 280 && Counts[2] >= 10;
        Nectaris = Counts[6] >= 140 || (Counts[6] + Counts[2] + Counts[5] + Counts[7]) >= 140;
        Solwings = Counts[5] >= 210 && Counts[2] >= 140;
        Virexids = Counts[6] >= 10 && Counts[1] >= 10;
        Grazeleths = Counts[1] >= 280 && (Counts[2] + Counts[6] + Counts[7]) >= 10;
    }

    private void SpreadTo(int r, int c, byte plant, byte[] next)
    {
        if ((uint)r >= Rows || (uint)c >= Cols) return;
        int i = r * Cols + c;
        if (Terrain[i] == 0 && (Soil[i] == 0 || Soil[i] == 1) && Species[i] == 0 && next[i] == 0 && Nutrients[i] > 0)
        {
            next[i] = plant;
        }
    }

    private void Kill(int i)
    {
        Counts[Species[i]]--;
        Species[i] = 0;
        Age[i] = 0;
        DeadMatter[i] = true;
        LivingCount--;
    }
}

public sealed class TickAction
{
    public int Tick { get; init; }
    public List<PlantAction> Plants { get; init; } = [];
}

public sealed class PlantAction
{
    public int plant_index { get; init; }
    public int row { get; init; }
    public int col { get; init; }
}

public static class Submission
{
    public static string Serialize(List<TickAction> actions)
    {
        var opts = new JsonSerializerOptions { WriteIndented = true };
        var payload = new
        {
            actions = actions.Select(a => new
            {
                tick = a.Tick,
                plants = a.Plants.Select(p => new { plant_index = p.plant_index, row = p.row, col = p.col }),
            }),
        };
        return JsonSerializer.Serialize(payload, opts) + "\n";
    }
}