using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace EntelectHackathon.Level3;

internal enum L3Season : byte { Spring = 0, Summer = 1, Autumn = 2, Winter = 3 }

internal enum L3SpreadType : byte
{
    None = 0,
    VonNeumann = 1,
    Moore = 2,
    Row = 3,
    Column = 4,
    CrossHatch = 5
}

internal enum L3Terrain : byte
{
    Habitable = 0,
    Path = 1,
    Stone = 2,
    Crack = 3,
    Water = 4
}

[Flags]
internal enum L3PlantFlags : uint
{
    None = 0,
    NoShadeSurvival = 1 << 0,
    NoWinterSpread = 1 << 1,
    NoShadeSpread = 1 << 2,
    ShadeRequired = 1 << 3,
    DieIfIsolated = 1 << 4,
    MustRockOrPath = 1 << 5,
    MustWater = 1 << 6,
    MustBurntSoil = 1 << 7,
    NoAdjacentPlants = 1 << 8,
    DeadMatterOnlySpread = 1 << 9,
    CrackSpread = 1 << 10,
    BurntSoilOk = 1 << 11,
    NutrientRegen = 1 << 12,
    Resurrect = 1 << 13,
    Coexist = 1 << 14,
    Subsurface = 1 << 15
}

internal readonly struct L3PlantDef
{
    public readonly byte Index;
    public readonly ushort TimeToMaturity;
    public readonly ushort SpreadRate;
    public readonly L3SpreadType SpreadType;
    public readonly byte SpreadRange;
    public readonly byte Invasiveness;
    public readonly L3PlantFlags Flags;
    public readonly byte ShadeRadius;
    public readonly byte BurntRadius;
    public readonly byte PreferredSoilMask;
    public readonly byte DieIfNeighborsGreaterThan;
    public readonly sbyte AdjacentMaturityBoost;
    public readonly sbyte AdjacentMaturityPenalty;
    public readonly sbyte BurntMaturityBoost;
    public readonly string Name;

    public L3PlantDef(
        byte index,
        string name,
        ushort timeToMaturity,
        ushort spreadRate,
        L3SpreadType spreadType,
        byte spreadRange,
        byte invasiveness,
        L3PlantFlags flags,
        byte shadeRadius,
        byte burntRadius,
        byte preferredSoilMask,
        byte dieIfNeighborsGreaterThan,
        sbyte adjacentMaturityBoost,
        sbyte adjacentMaturityPenalty,
        sbyte burntMaturityBoost)
    {
        Index = index;
        Name = name;
        TimeToMaturity = timeToMaturity;
        SpreadRate = spreadRate;
        SpreadType = spreadType;
        SpreadRange = spreadRange;
        Invasiveness = invasiveness;
        Flags = flags;
        ShadeRadius = shadeRadius;
        BurntRadius = burntRadius;
        PreferredSoilMask = preferredSoilMask;
        DieIfNeighborsGreaterThan = dieIfNeighborsGreaterThan;
        AdjacentMaturityBoost = adjacentMaturityBoost;
        AdjacentMaturityPenalty = adjacentMaturityPenalty;
        BurntMaturityBoost = burntMaturityBoost;
    }

    public bool Has(L3PlantFlags flag) => (Flags & flag) != 0;
}

internal enum L3UnlockKind : byte
{
    And, Or, SpeciesPresent, SpeciesAbsent, Coverage, Count, FeatureCount, Event
}

internal sealed class L3UnlockNode
{
    public L3UnlockKind Kind;
    public L3UnlockNode[] Children = [];
    public string Key = "";
    public byte PlantIndex;
    public double Value;
    public string Op = ">";
}

internal sealed class L3AnimalDef
{
    public string Name = "";
    public L3UnlockNode Requirement = new() { Kind = L3UnlockKind.And };
}

internal readonly struct L3Command
{
    public readonly int Tick;
    public readonly bool IsEvent;
    public readonly L3Season Season;
    public readonly string EventName;

    public L3Command(int tick, L3Season season)
    {
        Tick = tick;
        IsEvent = false;
        Season = season;
        EventName = "";
    }

    public L3Command(int tick, string eventName)
    {
        Tick = tick;
        IsEvent = true;
        Season = L3Season.Spring;
        EventName = eventName;
    }
}

internal readonly struct L3Planting
{
    public readonly int Tick;
    public readonly int Row;
    public readonly int Col;
    public readonly byte PlantIndex;

    public L3Planting(int tick, int row, int col, byte plantIndex)
    {
        Tick = tick;
        Row = row;
        Col = col;
        PlantIndex = plantIndex;
    }
}

internal sealed class L3LevelConfig
{
    public int Rows;
    public int Cols;
    public int Ticks;
    public bool AnimalsEnabled;
    public byte[] Terrain = [];
    public byte[] Soil = [];
    public L3Command[] Commands = [];
    public L3PlantDef[] PlantsByIndex = [];
    public Dictionary<string, byte> NameToIndex = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<byte, L3UnlockNode> UnlockByPlant = new();
    public L3AnimalDef[] Animals = [];
    public Dictionary<string, HashSet<byte>> Groups = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<byte> StartingPlants = [];
}

internal static class L3Loader
{
    public static L3LevelConfig Load(
        string levelPath,
        string plantPath,
        string unlockPath,
        string animalPath,
        string classPath)
    {
        var cfg = new L3LevelConfig();
        LoadLevelJson(levelPath, cfg);
        LoadPlants(plantPath, cfg);
        LoadUnlocks(unlockPath, cfg);
        LoadClassifications(classPath, cfg);
        LoadAnimals(animalPath, cfg);
        InferStartingPlants(cfg);
        return cfg;
    }

    private static void LoadLevelJson(string path, L3LevelConfig cfg)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        cfg.Rows = root.GetProperty("rows").GetInt32();
        cfg.Cols = root.GetProperty("cols").GetInt32();
        cfg.Ticks = root.GetProperty("ticks").GetInt32();
        cfg.AnimalsEnabled = !root.TryGetProperty("animals_enabled", out var ae) || ae.GetBoolean();

        int n = cfg.Rows * cfg.Cols;
        cfg.Terrain = new byte[n];
        cfg.Soil = new byte[n];

        if (root.TryGetProperty("cells", out var cells))
        {
            foreach (var cell in cells.EnumerateArray())
            {
                int r = cell.GetProperty("row").GetInt32();
                int c = cell.GetProperty("col").GetInt32();
                int i = r * cfg.Cols + c;
                if ((uint)i < (uint)n)
                {
                    cfg.Terrain[i] = cell.GetProperty("terrain").GetByte();
                    cfg.Soil[i] = cell.GetProperty("soil").GetByte();
                }
            }
        }

        var cmds = new List<L3Command>();
        if (root.TryGetProperty("commands", out var commands))
        {
            foreach (var cmd in commands.EnumerateArray())
            {
                int tick = cmd.GetProperty("tick").GetInt32();
                string type = cmd.GetProperty("type").GetString() ?? "";
                if (string.Equals(type, "season", StringComparison.OrdinalIgnoreCase))
                {
                    cmds.Add(new L3Command(tick, ParseSeason(cmd.GetProperty("season").GetString())));
                }
                else if (string.Equals(type, "event", StringComparison.OrdinalIgnoreCase))
                {
                    cmds.Add(new L3Command(tick, cmd.GetProperty("event").GetString() ?? ""));
                }
            }
        }

        cmds.Sort((a, b) => a.Tick.CompareTo(b.Tick));
        cfg.Commands = cmds.ToArray();
    }

    private static void LoadPlants(string path, L3LevelConfig cfg)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var defs = new L3PlantDef[32];
        cfg.NameToIndex = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        foreach (var el in doc.RootElement.EnumerateArray())
        {
            byte index = el.GetProperty("index").GetByte();
            string name = el.GetProperty("plant").GetString() ?? $"Plant{index}";
            var growth = el.GetProperty("growth");
            ushort ttm = (ushort)growth.GetProperty("time_to_maturity").GetInt32();
            ushort rate = (ushort)growth.GetProperty("spread_rate").GetInt32();
            var spreadType = ParseSpread(growth.GetProperty("spread_type").GetString());
            byte range = (byte)growth.GetProperty("spread_range").GetInt32();
            byte inv = (byte)growth.GetProperty("invasiveness_rank").GetInt32();

            byte soilMask = 0;
            if (el.TryGetProperty("preferred_soil", out var soils))
            {
                foreach (var s in soils.EnumerateArray())
                {
                    soilMask |= (byte)(1 << s.GetInt32());
                }
            }

            var flags = L3PlantFlags.None;
            byte shade = 0, burntR = 0, dieN = 0;
            sbyte adjBoost = 0, adjPen = 0, burntBoost = 0;

            if (el.TryGetProperty("rules", out var rules))
            {
                if (rules.TryGetProperty("weaknesses", out var weaknesses))
                {
                    foreach (var w in weaknesses.EnumerateArray())
                    {
                        string t = w.GetProperty("type").GetString() ?? "";
                        switch (t)
                        {
                            case "no_shade_survival": flags |= L3PlantFlags.NoShadeSurvival; break;
                            case "no_winter_spread": flags |= L3PlantFlags.NoWinterSpread; break;
                            case "no_shade_spread": flags |= L3PlantFlags.NoShadeSpread; break;
                            case "shade_required": flags |= L3PlantFlags.ShadeRequired; break;
                            case "die_if_isolated": flags |= L3PlantFlags.DieIfIsolated; break;
                            case "no_adjacent_plants": flags |= L3PlantFlags.NoAdjacentPlants; break;
                            case "must_be_burnt_soil": flags |= L3PlantFlags.MustBurntSoil; break;
                            case "die_if_neighbors_greater_than":
                                dieN = (byte)w.GetProperty("value").GetInt32();
                                break;
                            case "must_be_adjacent_to":
                                string feat = w.TryGetProperty("feature", out var f) ? f.GetString() ?? "" : "";
                                if (feat.Contains("rock") || feat.Contains("path")) flags |= L3PlantFlags.MustRockOrPath;
                                if (feat.Contains("water")) flags |= L3PlantFlags.MustWater;
                                break;
                        }
                    }
                }

                if (rules.TryGetProperty("special", out var specials))
                {
                    foreach (var s in specials.EnumerateArray())
                    {
                        string t = s.GetProperty("type").GetString() ?? "";
                        switch (t)
                        {
                            case "shade_radius":
                                shade = (byte)s.GetProperty("value").GetInt32();
                                break;
                            case "burnt_soil_radius":
                                burntR = (byte)s.GetProperty("value").GetInt32();
                                flags |= L3PlantFlags.BurntSoilOk;
                                break;
                            case "adjacent_maturity_boost":
                                adjBoost = (sbyte)s.GetProperty("value").GetInt32();
                                break;
                            case "adjacent_maturity_penalty":
                                adjPen = (sbyte)s.GetProperty("value").GetInt32();
                                break;
                            case "burnt_soil_maturity_boost":
                                burntBoost = (sbyte)s.GetProperty("value").GetInt32();
                                flags |= L3PlantFlags.BurntSoilOk;
                                break;
                            case "dead_matter_only_spread":
                                flags |= L3PlantFlags.DeadMatterOnlySpread;
                                break;
                            case "crack_spread":
                                flags |= L3PlantFlags.CrackSpread;
                                break;
                            case "soil_nutrient_regeneration":
                                flags |= L3PlantFlags.NutrientRegen;
                                break;
                            case "resurrect_after_destruction":
                                flags |= L3PlantFlags.Resurrect;
                                break;
                            case "coexist_all_species":
                                flags |= L3PlantFlags.Coexist;
                                break;
                            case "subsurface_growth":
                                flags |= L3PlantFlags.Subsurface;
                                break;
                        }
                    }
                }
            }

            if (index is 13 or 26 or 30)
            {
                flags |= L3PlantFlags.BurntSoilOk;
            }

            defs[index] = new L3PlantDef(
                index, name, ttm, rate, spreadType, range, inv, flags,
                shade, burntR, soilMask, dieN, adjBoost, adjPen, burntBoost);
            cfg.NameToIndex[name] = index;
        }

        cfg.PlantsByIndex = defs;
    }

    private static void LoadUnlocks(string path, L3LevelConfig cfg)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            string plant = el.GetProperty("plant").GetString() ?? "";
            if (!cfg.NameToIndex.TryGetValue(plant, out byte idx)) continue;
            cfg.UnlockByPlant[idx] = ParseUnlockNode(el.GetProperty("unlock"), cfg);
        }
    }

    private static void LoadClassifications(string path, L3LevelConfig cfg)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var set = new HashSet<byte>();
            foreach (var name in prop.Value.EnumerateArray())
            {
                if (cfg.NameToIndex.TryGetValue(name.GetString() ?? "", out byte idx))
                {
                    set.Add(idx);
                }
            }

            cfg.Groups[prop.Name] = set;
            cfg.Groups[NormalizeKey(prop.Name)] = set;
        }

        AliasGroup(cfg, "Shallow-root Species", "Shallowroot Species");
        AliasGroup(cfg, "Trees", "Trees");
        AliasGroup(cfg, "Flowering Plants", "Flowering Plants");
        AliasGroup(cfg, "Seed Plants", "Seed Plants");
        AliasGroup(cfg, "Pollination Plants", "Pollination Plants");
    }

    private static void AliasGroup(L3LevelConfig cfg, string a, string b)
    {
        if (cfg.Groups.TryGetValue(a, out var set))
        {
            cfg.Groups[b] = set;
            cfg.Groups[NormalizeKey(a)] = set;
            cfg.Groups[NormalizeKey(b)] = set;
        }
        else if (cfg.Groups.TryGetValue(b, out set))
        {
            cfg.Groups[a] = set;
        }
    }

    private static void LoadAnimals(string path, L3LevelConfig cfg)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var list = new List<L3AnimalDef>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var animal = new L3AnimalDef
            {
                Name = el.GetProperty("name").GetString() ?? "",
                Requirement = ParseAnimalRequirement(el.GetProperty("requirements"), cfg)
            };
            list.Add(animal);
        }

        cfg.Animals = list.ToArray();
    }

    private static void InferStartingPlants(L3LevelConfig cfg)
    {
        var locked = cfg.UnlockByPlant.Keys.ToHashSet();
        var start = new HashSet<byte> { 1, 2, 5, 6, 12 };
        foreach (var kv in cfg.NameToIndex)
        {
            if (!locked.Contains(kv.Value) && kv.Value != 0)
            {
                start.Add(kv.Value);
            }
        }

        cfg.StartingPlants = start;
    }

    private static L3UnlockNode ParseUnlockNode(JsonElement el, L3LevelConfig cfg)
    {
        if (el.TryGetProperty("op", out var opEl))
        {
            string op = opEl.GetString() ?? "AND";
            var node = new L3UnlockNode
            {
                Kind = string.Equals(op, "OR", StringComparison.OrdinalIgnoreCase) ? L3UnlockKind.Or : L3UnlockKind.And
            };
            var children = new List<L3UnlockNode>();
            if (el.TryGetProperty("children", out var ch))
            {
                foreach (var c in ch.EnumerateArray())
                {
                    children.Add(ParseUnlockNode(c, cfg));
                }
            }

            node.Children = children.ToArray();
            return node;
        }

        string type = el.TryGetProperty("type", out var tEl) ? tEl.GetString() ?? "" : "";
        var leaf = new L3UnlockNode { Op = el.TryGetProperty("operator", out var oEl) ? oEl.GetString() ?? ">" : ">" };
        if (el.TryGetProperty("value", out var vEl))
        {
            leaf.Value = vEl.ValueKind == JsonValueKind.Number ? vEl.GetDouble() : 0;
        }

        switch (type)
        {
            case "species_present":
                leaf.Kind = L3UnlockKind.SpeciesPresent;
                leaf.Key = el.GetProperty("species").GetString() ?? "";
                break;
            case "species_absent":
                leaf.Kind = L3UnlockKind.SpeciesAbsent;
                leaf.Key = el.GetProperty("species").GetString() ?? "";
                break;
            case "coverage":
                leaf.Kind = L3UnlockKind.Coverage;
                leaf.Key = el.GetProperty("plant").GetString() ?? "";
                if (cfg.NameToIndex.TryGetValue(leaf.Key, out byte cIdx)) leaf.PlantIndex = cIdx;
                break;
            case "count":
                leaf.Kind = L3UnlockKind.Count;
                leaf.Key = el.GetProperty("plant").GetString() ?? "";
                if (cfg.NameToIndex.TryGetValue(leaf.Key, out byte nIdx)) leaf.PlantIndex = nIdx;
                break;
            case "feature_count":
                leaf.Kind = L3UnlockKind.FeatureCount;
                leaf.Key = el.GetProperty("feature").GetString() ?? "";
                break;
            case "event":
                leaf.Kind = L3UnlockKind.Event;
                leaf.Key = el.GetProperty("event").GetString() ?? "";
                break;
            default:
                leaf.Kind = L3UnlockKind.And;
                break;
        }

        return leaf;
    }

    private static L3UnlockNode ParseAnimalRequirement(JsonElement el, L3LevelConfig cfg)
    {
        string type = el.TryGetProperty("type", out var tEl) ? tEl.GetString() ?? "AND" : "AND";
        if (string.Equals(type, "AND", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "OR", StringComparison.OrdinalIgnoreCase))
        {
            var node = new L3UnlockNode
            {
                Kind = string.Equals(type, "OR", StringComparison.OrdinalIgnoreCase) ? L3UnlockKind.Or : L3UnlockKind.And
            };
            var children = new List<L3UnlockNode>();
            if (el.TryGetProperty("conditions", out var conds))
            {
                foreach (var c in conds.EnumerateArray())
                {
                    children.Add(ParseAnimalRequirement(c, cfg));
                }
            }

            node.Children = children.ToArray();
            return node;
        }

        var leaf = new L3UnlockNode
        {
            Op = el.TryGetProperty("operator", out var oEl) ? oEl.GetString() ?? ">=" : ">=",
            Value = el.TryGetProperty("threshold", out var th) ? th.GetDouble() : 0
        };

        switch (type)
        {
            case "coverage":
            case "group_coverage":
                leaf.Kind = L3UnlockKind.Coverage;
                leaf.Key = ReadSpeciesKey(el);
                if (cfg.NameToIndex.TryGetValue(leaf.Key, out byte cIdx)) leaf.PlantIndex = cIdx;
                break;
            case "count":
                leaf.Kind = L3UnlockKind.Count;
                leaf.Key = ReadSpeciesKey(el);
                if (cfg.NameToIndex.TryGetValue(leaf.Key, out byte nIdx)) leaf.PlantIndex = nIdx;
                break;
            case "dominance":
                leaf.Kind = L3UnlockKind.Coverage;
                leaf.Key = "__dominance__";
                break;
            default:
                leaf.Kind = L3UnlockKind.And;
                break;
        }

        return leaf;
    }

    private static string ReadSpeciesKey(JsonElement el)
    {
        if (el.TryGetProperty("species", out var s))
        {
            if (s.ValueKind == JsonValueKind.String) return s.GetString() ?? "";
            if (s.ValueKind == JsonValueKind.Array)
            {
                var parts = new List<string>();
                foreach (var x in s.EnumerateArray()) parts.Add(x.GetString() ?? "");
                return string.Join("|", parts);
            }
        }

        if (el.TryGetProperty("species_group", out var g))
        {
            if (g.ValueKind == JsonValueKind.String) return g.GetString() ?? "";
            if (g.ValueKind == JsonValueKind.Array)
            {
                var parts = new List<string>();
                foreach (var x in g.EnumerateArray()) parts.Add(x.GetString() ?? "");
                return string.Join("|", parts);
            }
        }

        return "";
    }

    private static L3Season ParseSeason(string? s) => s?.ToLowerInvariant() switch
    {
        "summer" => L3Season.Summer,
        "autumn" or "fall" => L3Season.Autumn,
        "winter" => L3Season.Winter,
        _ => L3Season.Spring
    };

    private static L3SpreadType ParseSpread(string? s) => s switch
    {
        "VonNeumann" => L3SpreadType.VonNeumann,
        "Moore" => L3SpreadType.Moore,
        "Row" => L3SpreadType.Row,
        "Column" => L3SpreadType.Column,
        "CrossHatch" => L3SpreadType.CrossHatch,
        _ => L3SpreadType.None
    };

    internal static string NormalizeKey(string s)
    {
        var chars = s.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars).ToLowerInvariant();
    }

    internal static bool Compare(double left, string op, double right) => op switch
    {
        ">=" => left >= right,
        "<=" => left <= right,
        "<" => left < right,
        "==" or "=" => Math.Abs(left - right) < 1e-12,
        _ => left > right
    };
}

internal static class L3Pathing
{
    public static string Resolve(string fileName, string[] extraRoots)
    {
        foreach (var root in extraRoots)
        {
            if (string.IsNullOrWhiteSpace(root)) continue;
            string p = Path.Combine(root, fileName);
            if (File.Exists(p)) return p;
        }

        return fileName;
    }
}