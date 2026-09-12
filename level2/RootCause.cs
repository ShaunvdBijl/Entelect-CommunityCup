using System.Text.Json;

namespace RootCause;

public static class Program
{
    public static void Main(string[] args)
    {
        Run(args);
    }

    public static void Run(string[] args)
    {
        var mapPath = args.Length > 0 ? args[0] : null;
        var outputPath = args.Length > 1 ? args[1] : "submission_level2.json";

        var maps = Maps.Load(mapPath);
        var plan = Planner.Build(maps);
        var json = Submission.Serialize(plan.Actions);
        File.WriteAllText(outputPath, json);

        var score = Scorer.Estimate(plan);
        Console.WriteLine($"Wrote {outputPath}");
        Console.WriteLine($"Actions: {plan.Actions.Count} ticks, plants scheduled: {plan.Placements.Count}");
        Console.WriteLine($"Harvest species: {score.Species}  coverage: {score.Coverage}/{Scorer.CMax}");
        Console.WriteLine($"Entropy H={score.Entropy:F4}  Main={score.Main:F4}  Longevity={score.Longevity:F4}");
        Console.WriteLine($"Estimated Final Score (0.8M + 0.2L) = {score.Final:F4}");
        foreach (var c in score.Counts.OrderBy(x => x.Index))
            Console.WriteLine($"  [{c.Index,2}] {c.Name,-22} {c.Count,4}  p={c.Share:F4}");
    }
}

internal readonly record struct Cell(int R, int C);

internal sealed class Maps
{
    public const int Rows = 70;
    public const int Cols = 100;
    public readonly byte[] Terrain = new byte[Rows * Cols];
    public readonly byte[] Soil = new byte[Rows * Cols];

    public const byte TerrainDirt = 0;
    public const byte TerrainWater = 1;
    public const byte TerrainPath = 2;

    public const byte SoilDirt = 0;
    public const byte SoilMud = 1;
    public const byte SoilClay = 2;

    public static int Id(int r, int c) => r * Cols + c;
    public static bool InBounds(int r, int c) => (uint)r < Rows && (uint)c < Cols;

    public bool Habitable(int r, int c)
    {
        if (!InBounds(r, c)) return false;
        var t = Terrain[Id(r, c)];
        return t != TerrainWater && t != TerrainPath;
    }

    public bool AdjTerrain(int r, int c, byte type)
    {
        ReadOnlySpan<(int, int)> deltas = [(-1, 0), (1, 0), (0, -1), (0, 1)];
        foreach (var (dr, dc) in deltas)
        {
            var rr = r + dr;
            var cc = c + dc;
            if (InBounds(rr, cc) && Terrain[Id(rr, cc)] == type) return true;
        }
        return false;
    }

    public static Maps Load(string? explicitPath = null)
    {
        string[] candidates = explicitPath != null
            ? [explicitPath]
            : ["2.json", "data/2.json", "../data/2.json", "../../data/2.json", "Level2/2.json"];

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                try
                {
                    var maps = new Maps();
                    using var doc = JsonDocument.Parse(File.ReadAllText(path));
                    var root = doc.RootElement;
                    if (root.TryGetProperty("cells", out var cells))
                    {
                        foreach (var cell in cells.EnumerateArray())
                        {
                            var r = cell.GetProperty("row").GetInt32();
                            var c = cell.GetProperty("col").GetInt32();
                            var t = (byte)cell.GetProperty("terrain").GetInt32();
                            var s = (byte)cell.GetProperty("soil").GetInt32();
                            var id = Id(r, c);
                            maps.Terrain[id] = t;
                            maps.Soil[id] = s;
                        }
                        Console.WriteLine($"Loaded map configuration from: {path}");
                        return maps;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed parsing {path}: {ex.Message}");
                }
            }
        }

        Console.WriteLine("Notice: 2.json not found. Falling back to default layout.");
        return FallbackLevel2();
    }

    private static Maps FallbackLevel2()
    {
        var m = new Maps();
        Fill(m.Soil, 5, 13, 5, 17, SoilMud);
        Fill(m.Soil, 56, 66, 40, 50, SoilMud);
        Fill(m.Soil, 4, 21, 66, 89, SoilClay);
        Fill(m.Soil, 43, 63, 4, 26, SoilClay);
        Fill(m.Terrain, 6, 20, 68, 87, TerrainWater);
        Fill(m.Terrain, 44, 61, 6, 25, TerrainWater);
        for (var r = 35; r <= 36; r++)
        for (var c = 0; c < Cols; c++)
            if (c % 6 < 3) m.Terrain[Id(r, c)] = TerrainPath;
        return m;
    }

    private static void Fill(byte[] arr, int r0, int r1, int c0, int c1, byte v)
    {
        for (var r = r0; r <= r1; r++)
        for (var c = c0; c <= c1; c++)
            arr[Id(r, c)] = v;
    }
}

internal readonly record struct Placement(int Tick, int Plant, int Row, int Col);

internal sealed class TickAction
{
    public int Tick { get; init; }
    public List<PlantAction> Plants { get; init; } = [];
}

internal sealed class PlantAction
{
    public int plant_index { get; init; }
    public int row { get; init; }
    public int col { get; init; }
}

internal sealed class Plan
{
    public required List<TickAction> Actions { get; init; }
    public required List<Placement> Placements { get; init; }
    public required List<Placement> Harvest { get; init; }
}

internal static class Plants
{
    public const int Grass = 1, Rose = 2, Moss = 3, Vine = 4, Sun = 5, Lav = 6;
    public const int Orange = 7, Fern = 8, Glow = 9, Canopy = 10, Reed = 11, Oak = 12;
    public const int Moon = 15, Iron = 16, Mire = 18, Razor = 19;

    public static readonly int[] HarvestSpecies =
        [Grass, Rose, Moss, Vine, Sun, Lav, Orange, Fern, Glow, Canopy, Reed, Oak, Moon, Iron, Mire, Razor];

    public static readonly Dictionary<int, string> Names = new()
    {
        [Grass] = "Grass", [Rose] = "Rose Bush", [Moss] = "Blue Moss", [Vine] = "Crimson Vine",
        [Sun] = "Dwarf Sunflower", [Lav] = "Lavender", [Orange] = "Orange Blossom", [Fern] = "Silver Fern",
        [Glow] = "Glowcap Fungus", [Canopy] = "Purple Canopy Tree", [Reed] = "Stone Reed", [Oak] = "Oak Tree",
        [Moon] = "Moonpetal Lily", [Iron] = "Ironthorn Shrub", [Mire] = "Mire Bloom", [Razor] = "Razorgrass",
    };
}

internal static class Planner
{
    public const int HarvestStart = 401;
    public const int LastTick = 499;
    public const int Cap = 20;
    public const int RainTick = 250;

    public static Plan Build(Maps maps)
    {
        var unlockOcc = new byte[Maps.Rows * Maps.Cols];
        var unlock = PlanUnlock(maps, unlockOcc);
        var harvest = PlanHarvest(maps);
        var placements = new List<Placement>(unlock.Count + harvest.Count);
        placements.AddRange(unlock);
        placements.AddRange(harvest);

        return new Plan
        {
            Actions = Pack(placements),
            Placements = placements,
            Harvest = harvest,
        };
    }

    private static List<Cell> Collect(Maps maps, Func<int, int, bool> pred)
    {
        var list = new List<Cell>();
        for (var r = 0; r < Maps.Rows; r++)
        for (var c = 0; c < Maps.Cols; c++)
            if (pred(r, c)) list.Add(new Cell(r, c));
        return list;
    }

    private static List<Cell> Take(List<Cell> pool, int n, byte[] occ)
    {
        var outList = new List<Cell>(n);
        foreach (var cell in pool)
        {
            if (outList.Count >= n) break;
            var i = Maps.Id(cell.R, cell.C);
            if (occ[i] != 0) continue;
            occ[i] = 1;
            outList.Add(cell);
        }
        return outList;
    }

    private static int Cheb(int r0, int c0, int r1, int c1) =>
        Math.Max(Math.Abs(r0 - r1), Math.Abs(c0 - c1));

    private static bool SoilOk(byte soil, ReadOnlySpan<byte> preferred)
    {
        foreach (var p in preferred)
            if (soil == p) return true;
        return false;
    }

    private static bool InQuarantineZone(int r, int c) => r <= 22 && c <= 39;

    private static List<Placement> PlanUnlock(Maps maps, byte[] occ)
    {
        byte[] dirtMud = [0, 1];
        var qPool = Collect(maps, (r, c) =>
            InQuarantineZone(r, c) && maps.Habitable(r, c) && SoilOk(maps.Soil[Maps.Id(r, c)], dirtMud));

        var waves = new (int Plant, int Count, int Tick)[]
        {
            (Plants.Grass, 380, 0),    // Verdelopes: >5% (350). Starves at tick 100-118 -> 380 dead matter
            (Plants.Rose, 100, 20),    // Loamcrawlers & Blue Moss (>1% Rose, >3% Grass)
            (Plants.Lav, 160, 26),     // Nectaris (>2%), Virexids (Lav>=10, Grass>=10) -> Reed & Vine unlock
            (Plants.Rose, 160, 35),    // Rose >2% + Nectaris -> Orange Blossom unlock
            (Plants.Sun, 240, 44),     // Sunflower >3% + Rose >2% -> Solwings unlock
            (Plants.Moss, 240, 56),    // Blue Moss >3% + Loamcrawlers -> Silver Fern unlock
            (Plants.Vine, 320, 68),    // Crimson Vine >4% + Blue Moss >1% -> Canopy Tree unlock
            (Plants.Grass, 320, 96),   // Sustains Grass >4% for Loamcrawlers when Wave 1 starves
            (Plants.Rose, 40, 112),    // Sustains Rose count >= 10 for Loamcrawlers
            (Plants.Glow, 20, 122),    // Dead matter >5% (380 cells) + Loamcrawlers -> Glowcap unlock
            (Plants.Moss, 380, 226),   // Alive at tick 250 (>5% = 350 cells) + Rain at 250 -> Mire Bloom unlock
            (Plants.Mire, 20, 252),    // First Mire Bloom placement
            (Plants.Lav, 310, 300),    // Lavender >4% (280) + Blue Moss >2% -> Moonpetal unlock
            (Plants.Moss, 180, 316),   // Blue Moss >2% (140)
            (Plants.Moon, 10, 325),    // First Moonpetal placement
            (Plants.Grass, 320, 326),  // Grass >4% for Grazeleths
            (Plants.Rose, 310, 342),   // Rose >4% (280) + Grazeleths -> Ironthorn unlock
            (Plants.Iron, 20, 358),    // First Ironthorn placement
            (Plants.Grass, 80, 359),   // Pushes Grass >5% (350) + Verdelopes -> Razorgrass unlock
            (Plants.Razor, 20, 364),   // First Razorgrass placement
        };

        var outList = new List<Placement>();
        var cursor = 0;
        foreach (var (plant, count, tick) in waves)
        {
            List<Cell> source = plant switch
            {
                Plants.Reed => Collect(maps, (r, c) =>
                    maps.Habitable(r, c) && maps.AdjTerrain(r, c, Maps.TerrainPath) &&
                    SoilOk(maps.Soil[Maps.Id(r, c)], dirtMud)),
                Plants.Mire => Collect(maps, (r, c) =>
                    maps.Habitable(r, c) && maps.Soil[Maps.Id(r, c)] == Maps.SoilClay &&
                    maps.AdjTerrain(r, c, Maps.TerrainWater)),
                _ => qPool,
            };

            var taken = 0;
            var i = cursor;
            var guard = 0;
            while (taken < count && guard < source.Count * 2)
            {
                var cell = source[i % source.Count];
                i++;
                guard++;
                var id = Maps.Id(cell.R, cell.C);
                occ[id] = 1;
                outList.Add(new Placement(tick, plant, cell.R, cell.C));
                taken++;
            }
            cursor = source.Count == 0 ? 0 : i % source.Count;
        }

        return outList;
    }

    private static List<Cell> BuildOakGrove(Maps maps, byte[] occ)
    {
        var oaks = new List<Cell>(124);
        const int r0 = 24, c0 = 50, side = 12;
        for (var i = 0; i < side && oaks.Count < 124; i++)
        for (var j = 0; j < side && oaks.Count < 124; j++)
        {
            var r = r0 + i;
            var c = c0 + j;
            if (maps.Habitable(r, c) && !InQuarantineZone(r, c))
            {
                oaks.Add(new Cell(r, c));
                occ[Maps.Id(r, c)] = 1;
            }
        }
        return oaks;
    }

    private static bool InShade(int r, int c, List<Cell> casters, int radius)
    {
        foreach (var o in casters)
            if (Cheb(r, c, o.R, o.C) <= radius) return true;
        return false;
    }

    private static List<Placement> PlanHarvest(Maps maps)
    {
        var occ = new byte[Maps.Rows * Maps.Cols];
        var oaks = BuildOakGrove(maps, occ);

        const int harvestSlots = (LastTick - HarvestStart + 1) * Cap; // 99 * 20 = 1980
        var allSpecies = Plants.HarvestSpecies;
        var quota = new Dictionary<int, int>();
        var baseQ = harvestSlots / allSpecies.Length;
        var rem = harvestSlots % allSpecies.Length;

        for (var i = 0; i < allSpecies.Length; i++)
            quota[allSpecies[i]] = baseQ + (i < rem ? 1 : 0);

        quota[Plants.Oak] = oaks.Count;

        var mire = Take(Collect(maps, (r, c) =>
            !InQuarantineZone(r, c) && maps.Habitable(r, c) &&
            maps.Soil[Maps.Id(r, c)] == Maps.SoilClay &&
            maps.AdjTerrain(r, c, Maps.TerrainWater)), quota[Plants.Mire], occ);
        quota[Plants.Mire] = mire.Count;

        var reeds = Take(Collect(maps, (r, c) =>
            !InQuarantineZone(r, c) && maps.Habitable(r, c) &&
            maps.AdjTerrain(r, c, Maps.TerrainPath) &&
            SoilOk(maps.Soil[Maps.Id(r, c)], [0, 1])), quota[Plants.Reed], occ);
        quota[Plants.Reed] = reeds.Count;

        var moonPool = Collect(maps, (r, c) =>
            !InQuarantineZone(r, c) && maps.Habitable(r, c) &&
            SoilOk(maps.Soil[Maps.Id(r, c)], [0, 1]) &&
            InShade(r, c, oaks, 4) && occ[Maps.Id(r, c)] == 0);
        var moons = Take(moonPool, quota[Plants.Moon], occ);
        quota[Plants.Moon] = moons.Count;

        var moss = Take(Collect(maps, (r, c) =>
            !InQuarantineZone(r, c) && maps.Habitable(r, c) &&
            SoilOk(maps.Soil[Maps.Id(r, c)], [0, 1]) &&
            !InShade(r, c, oaks, 4) && (r + c) % 2 == 0), quota[Plants.Moss], occ);
        quota[Plants.Moss] = moss.Count;

        var vinePool = Collect(maps, (r, c) =>
            !InQuarantineZone(r, c) && maps.Habitable(r, c) &&
            SoilOk(maps.Soil[Maps.Id(r, c)], [0, 1]) &&
            !InShade(r, c, oaks, 4) && c % 3 == 0);
        vinePool.Sort((a, b) => a.C != b.C ? a.C.CompareTo(b.C) : a.R.CompareTo(b.R));
        var vines = Take(vinePool, quota[Plants.Vine], occ);
        quota[Plants.Vine] = vines.Count;

        var generalSpecies = new[]
        {
            Plants.Canopy, Plants.Fern, Plants.Iron, Plants.Glow,
            Plants.Rose, Plants.Lav, Plants.Orange, Plants.Sun, Plants.Razor, Plants.Grass
        };

        var generalCells = new Dictionary<int, List<Cell>>();
        foreach (var s in generalSpecies)
        {
            var pool = Collect(maps, (r, c) =>
            {
                if (InQuarantineZone(r, c) || occ[Maps.Id(r, c)] != 0) return false;
                if (!maps.Habitable(r, c) || !SoilOk(maps.Soil[Maps.Id(r, c)], [0, 1])) return false;
                if (s is Plants.Grass or Plants.Sun or Plants.Canopy)
                    return !InShade(r, c, oaks, 4);
                return true;
            });
            generalCells[s] = Take(pool, quota[s], occ);
        }

        var leftover = Collect(maps, (r, c) =>
            !InQuarantineZone(r, c) && maps.Habitable(r, c) &&
            SoilOk(maps.Soil[Maps.Id(r, c)], [0, 1]) && occ[Maps.Id(r, c)] == 0);
        var li = 0;
        foreach (var s in generalSpecies)
        {
            while (generalCells[s].Count < quota[s] && li < leftover.Count)
            {
                var cell = leftover[li++];
                if (occ[Maps.Id(cell.R, cell.C)] != 0) continue;
                if (s is Plants.Grass or Plants.Sun && InShade(cell.R, cell.C, oaks, 4)) continue;
                occ[Maps.Id(cell.R, cell.C)] = 1;
                generalCells[s].Add(cell);
            }
        }

        var earlyBatch = new List<(int Plant, Cell Cell)>();
        foreach (var o in oaks) earlyBatch.Add((Plants.Oak, o)); // Ticks 401..407 (Matures at ~421)
        foreach (var c in mire) earlyBatch.Add((Plants.Mire, c));
        foreach (var c in reeds) earlyBatch.Add((Plants.Reed, c));
        foreach (var c in moss) earlyBatch.Add((Plants.Moss, c));
        foreach (var c in vines) earlyBatch.Add((Plants.Vine, c));

        var midBatch = new List<(int Plant, Cell Cell)>();
        int[] midSpecies = [Plants.Canopy, Plants.Fern, Plants.Iron, Plants.Glow, Plants.Rose, Plants.Lav];
        foreach (var s in midSpecies)
            foreach (var cell in generalCells[s]) midBatch.Add((s, cell));

        var lateBatch = new List<(int Plant, Cell Cell)>();
        foreach (var c in moons) lateBatch.Add((Plants.Moon, c)); // Ticks 428+ (Oak shade is active)

        int[] lateSpecies = [Plants.Orange, Plants.Sun, Plants.Razor, Plants.Grass];
        foreach (var s in lateSpecies)
            foreach (var cell in generalCells[s]) lateBatch.Add((s, cell));

        var harvestPlacements = new List<Placement>(harvestSlots);
        var tick = HarvestStart;
        var inTick = 0;

        void AppendBatch(List<(int Plant, Cell Cell)> batch)
        {
            foreach (var (p, cell) in batch)
            {
                if (tick > LastTick) break;
                harvestPlacements.Add(new Placement(tick, p, cell.R, cell.C));
                inTick++;
                if (inTick >= Cap)
                {
                    inTick = 0;
                    tick++;
                }
            }
        }

        AppendBatch(earlyBatch);
        AppendBatch(midBatch);
        AppendBatch(lateBatch);

        return harvestPlacements;
    }

    private static List<TickAction> Pack(List<Placement> placements)
    {
        var byTick = new SortedDictionary<int, List<PlantAction>>();
        var overflow = new List<Placement>();

        foreach (var p in placements.OrderBy(p => p.Tick).ThenBy(p => p.Row).ThenBy(p => p.Col))
        {
            var t = Math.Clamp(p.Tick, 0, LastTick);
            if (!byTick.TryGetValue(t, out var bucket))
            {
                bucket = [];
                byTick[t] = bucket;
            }
            if (bucket.Count < Cap)
                bucket.Add(new PlantAction { plant_index = p.Plant, row = p.Row, col = p.Col });
            else
                overflow.Add(p with { Tick = t + 1 });
        }

        foreach (var p in overflow)
        {
            var t = p.Tick;
            while (t <= LastTick)
            {
                if (!byTick.TryGetValue(t, out var bucket))
                {
                    bucket = [];
                    byTick[t] = bucket;
                }
                if (bucket.Count < Cap)
                {
                    bucket.Add(new PlantAction { plant_index = p.Plant, row = p.Row, col = p.Col });
                    break;
                }
                t++;
            }
        }

        return byTick
            .Where(kv => kv.Value.Count > 0)
            .Select(kv => new TickAction { Tick = kv.Key, Plants = kv.Value })
            .ToList();
    }
}

internal sealed class ScoreBreakdown
{
    public int Species { get; init; }
    public int Coverage { get; init; }
    public double Entropy { get; init; }
    public double Main { get; init; }
    public double Longevity { get; init; }
    public double Final { get; init; }
    public List<(int Index, string Name, int Count, double Share)> Counts { get; init; } = [];
}

internal static class Scorer
{
    public const int CMax = 7000;
    public const int TFinal = 500;
    public const int NutrientLife = 100;
    public const double Alpha = 1;
    public const double K = 2;

    public static ScoreBreakdown Estimate(Plan plan)
    {
        var alive = plan.Harvest.Where(p => TFinal - p.Tick <= NutrientLife).ToList();
        var map = Plants.HarvestSpecies.ToDictionary(s => s, _ => 0);
        foreach (var p in alive)
        {
            if (map.ContainsKey(p.Plant)) map[p.Plant]++;
            else map[p.Plant] = 1;
        }

        var counts = Plants.HarvestSpecies
            .Select(i => (Index: i, Name: Plants.Names.GetValueOrDefault(i, $"#{i}"), Count: map.GetValueOrDefault(i), Share: 0.0))
            .ToList();

        var coverage = counts.Sum(c => c.Count);
        counts = counts.Select(c => (c.Index, c.Name, c.Count, coverage == 0 ? 0.0 : c.Count / (double)coverage)).ToList();

        var entropy = Shannon(counts.Select(c => c.Count).ToList());
        var ratio = coverage / (double)CMax;
        var main = entropy * Math.Pow(ratio, Alpha);

        var longevitySum = 0.0;
        foreach (var p in alive)
        {
            var life = Math.Min(TFinal - p.Tick, NutrientLife);
            longevitySum += Math.Pow(life / (double)TFinal, K);
        }
        var longevity = longevitySum / CMax;

        return new ScoreBreakdown
        {
            Species = counts.Count(c => c.Count > 0),
            Coverage = coverage,
            Entropy = entropy,
            Main = main,
            Longevity = longevity,
            Final = 0.8 * main + 0.2 * longevity,
            Counts = counts,
        };
    }

    private static double Shannon(List<int> counts)
    {
        var present = counts.Where(c => c > 0).ToList();
        var n = present.Count;
        if (n <= 1) return 0;
        var total = present.Sum();
        if (total == 0) return 0;
        var h = 0.0;
        foreach (var c in present)
        {
            var p = c / (double)total;
            h -= p * (Math.Log(p) / Math.Log(n));
        }
        return h;
    }
}

internal static class Submission
{
    public static string Serialize(List<TickAction> actions)
    {
        var opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        var payload = new
        {
            actions = actions.Select(a => new
            {
                tick = a.Tick,
                plants = a.Plants.Select(p => new { p.plant_index, p.row, p.col }),
            }),
        };
        return JsonSerializer.Serialize(payload, opts) + "\n";
    }
}