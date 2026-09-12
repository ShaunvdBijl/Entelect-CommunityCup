using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace EntelectHackathon.Level3;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct L3Cell
{
    public byte PlantIndex;
    public byte Terrain;
    public byte Soil;
    public byte Nutrients;
    public ushort Age;
    public byte Flags;
    public byte Reserved;

    public const byte FlagDeadMatter = 1;
    public const byte FlagShaded = 2;
    public const byte FlagCracked = 4;
    public const byte FlagBurnt = 8;
    public const byte FlagResurrect = 16;

    public readonly bool Occupied => PlantIndex != 0;
    public bool DeadMatter
    {
        readonly get => (Flags & FlagDeadMatter) != 0;
        set => Flags = value ? (byte)(Flags | FlagDeadMatter) : (byte)(Flags & ~FlagDeadMatter);
    }
    public bool Shaded
    {
        readonly get => (Flags & FlagShaded) != 0;
        set => Flags = value ? (byte)(Flags | FlagShaded) : (byte)(Flags & ~FlagShaded);
    }
    public bool Cracked
    {
        readonly get => (Flags & FlagCracked) != 0;
        set => Flags = value ? (byte)(Flags | FlagCracked) : (byte)(Flags & ~FlagCracked);
    }
    public bool Burnt
    {
        readonly get => (Flags & FlagBurnt) != 0 || Soil == 3;
        set
        {
            if (value)
            {
                Flags = (byte)(Flags | FlagBurnt);
                Soil = 3;
            }
        }
    }
}

internal sealed class L3Engine
{
    public readonly L3LevelConfig Config;
    public readonly int Rows;
    public readonly int Cols;
    public readonly int TotalCells;
    public readonly int TotalTicks;

    private L3Cell[] _grid;
    private L3Cell[] _next;
    private readonly int[] _speciesCounts = new int[32];
    private readonly bool[] _unlocked = new bool[32];
    private readonly HashSet<string> _animals = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _events = new(StringComparer.OrdinalIgnoreCase);
    private int _commandCursor;
    private int _deadMatterCount;
    private int _burntCount;
    private int _occupiedCount;
    private int _scan;
    public int Tick { get; private set; }
    public L3Season Season { get; private set; } = L3Season.Spring;

    public L3Engine(L3LevelConfig config)
    {
        Config = config;
        Rows = config.Rows;
        Cols = config.Cols;
        TotalCells = Rows * Cols;
        TotalTicks = config.Ticks;
        _grid = new L3Cell[TotalCells];
        _next = new L3Cell[TotalCells];
        Reset();
    }

    public void Reset()
    {
        Tick = 0;
        Season = L3Season.Spring;
        _commandCursor = 0;
        _deadMatterCount = 0;
        _burntCount = 0;
        _occupiedCount = 0;
        _animals.Clear();
        _events.Clear();
        Array.Clear(_speciesCounts);
        Array.Clear(_unlocked);
        foreach (byte p in Config.StartingPlants)
        {
            if (p < _unlocked.Length) _unlocked[p] = true;
        }

        for (int i = 0; i < TotalCells; i++)
        {
            _grid[i] = new L3Cell
            {
                PlantIndex = 0,
                Terrain = Config.Terrain[i],
                Soil = Config.Soil[i],
                Nutrients = 100,
                Age = 0,
                Flags = 0
            };
            if (Config.Soil[i] == 3)
            {
                _grid[i].Flags = L3Cell.FlagBurnt;
                _burntCount++;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Idx(int r, int c) => r * Cols + c;

    public ref L3Cell Cell(int r, int c) => ref _grid[Idx(r, c)];

    public int SpeciesCount(byte plant) => _speciesCounts[plant];
    public int OccupiedCount => _occupiedCount;
    public int DeadMatterCount => _deadMatterCount;
    public int BurntCount => _burntCount;
    public bool IsUnlocked(byte plant) => plant < _unlocked.Length && _unlocked[plant];
    public bool HasEvent(string name) => _events.Contains(name);
    public bool HasAnimal(string name) => _animals.Contains(name);
    public IReadOnlyCollection<string> ActiveAnimals => _animals;

    public bool TryPlant(byte plant, int r, int c)
    {
        if ((uint)r >= (uint)Rows || (uint)c >= (uint)Cols) return false;
        if (!_unlocked[plant]) return false;
        int i = Idx(r, c);
        ref var cell = ref _grid[i];
        if (!CanOccupy(in Config.PlantsByIndex[plant], r, c, planting: true)) return false;

        byte prev = cell.PlantIndex;
        if (prev != 0) _speciesCounts[prev]--;
        else _occupiedCount++;
        cell.PlantIndex = plant;
        cell.Age = 0;
        _speciesCounts[plant]++;
        return true;
    }

    public void Step(ReadOnlySpan<L3Planting> plantings)
    {
        ApplyCommandsForTick(Tick);

        int applied = 0;
        for (int i = 0; i < plantings.Length && applied < 20; i++)
        {
            var a = plantings[i];
            if (TryPlant(a.PlantIndex, a.Row, a.Col))
            {
                applied++;
            }
        }

        AgeAndSpecials();
        ApplyShadeAndBurnt();
        ApplyWeaknessCulls();
        Spread();
        MetabolizeAndStarve();
        RefreshUnlocksAndAnimals();

        Tick++;
    }

    public void RunScheduled(List<L3Planting> all)
    {
        Reset();
        all.Sort((a, b) => a.Tick.CompareTo(b.Tick));
        int p = 0;
        var buffer = new L3Planting[20];
        while (Tick < TotalTicks)
        {
            int n = 0;
            while (p < all.Count && all[p].Tick == Tick && n < 20)
            {
                buffer[n++] = all[p++];
            }

            while (p < all.Count && all[p].Tick == Tick) p++;
            Step(buffer.AsSpan(0, n));
        }
    }

    public (double Entropy, double Coverage, double Longevity, double Final, int Distinct) Score(double alpha = 1.0)
    {
        int distinct = 0;
        for (int i = 1; i < 32; i++)
        {
            if (_speciesCounts[i] > 0) distinct++;
        }

        double entropy = 0;
        if (distinct > 1 && _occupiedCount > 0)
        {
            double logK = Math.Log(distinct);
            for (int i = 1; i < 32; i++)
            {
                if (_speciesCounts[i] <= 0) continue;
                double p = (double)_speciesCounts[i] / _occupiedCount;
                entropy -= p * (Math.Log(p) / logK);
            }
        }

        double coverage = TotalCells > 0 ? (double)_occupiedCount / TotalCells : 0;
        double longevitySum = 0;
        for (int i = 0; i < TotalCells; i++)
        {
            if (_grid[i].Occupied && TotalTicks > 0)
            {
                longevitySum += (double)_grid[i].Age / TotalTicks;
            }
        }

        double longevity = TotalCells > 0 ? longevitySum / TotalCells : 0;
        double main = entropy * Math.Pow(coverage, alpha);
        double final = 0.8 * main + 0.2 * longevity;
        return (entropy, coverage, longevity, final, distinct);
    }

    public int FindEmpty(byte plant, int zoneLoCol, int zoneHiCol, Func<int, int, bool>? extra = null)
    {
        var def = Config.PlantsByIndex[plant];
        int start = _scan;
        for (int k = 0; k < TotalCells; k++)
        {
            int i = start + k;
            if (i >= TotalCells) i -= TotalCells;
            int r = i / Cols;
            int c = i - r * Cols;
            if (c < zoneLoCol || c >= zoneHiCol) continue;
            ref var cell = ref _grid[i];
            if (cell.Occupied) continue;
            if (!CanOccupy(in def, r, c, planting: true)) continue;
            if (extra != null && !extra(r, c)) continue;
            _scan = (i + 1) % TotalCells;
            return i;
        }

        return -1;
    }

    public bool AdjacentToTerrain(int r, int c, byte terrain)
    {
        return HasTerrain(r - 1, c, terrain) || HasTerrain(r + 1, c, terrain) ||
               HasTerrain(r, c - 1, terrain) || HasTerrain(r, c + 1, terrain);
    }

    public bool AdjacentToPathOrStone(int r, int c)
    {
        return AdjacentToTerrain(r, c, (byte)L3Terrain.Path) || AdjacentToTerrain(r, c, (byte)L3Terrain.Stone);
    }

    private bool HasTerrain(int r, int c, byte terrain)
    {
        if ((uint)r >= (uint)Rows || (uint)c >= (uint)Cols) return false;
        byte t = _grid[Idx(r, c)].Terrain;
        if (terrain == (byte)L3Terrain.Crack)
        {
            return _grid[Idx(r, c)].Cracked || t == (byte)L3Terrain.Crack;
        }

        return t == terrain;
    }

    private void ApplyCommandsForTick(int tick)
    {
        for (int i = _commandCursor; i < Config.Commands.Length; i++)
        {
            var cmd = Config.Commands[i];
            if (cmd.Tick > tick)
            {
                _commandCursor = i;
                break;
            }

            if (cmd.Tick < tick) continue;
            if (cmd.IsEvent)
            {
                _events.Add(cmd.EventName);
                if (cmd.EventName.Contains("Earthquake", StringComparison.OrdinalIgnoreCase))
                {
                    SpawnCracks();
                }
            }
            else
            {
                Season = cmd.Season;
            }

            _commandCursor = i + 1;
        }
    }

    private void SpawnCracks()
    {
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                if (((r * 31) + (c * 17)) % 41 != 0) continue;
                int i = Idx(r, c);
                ref var cell = ref _grid[i];
                if (cell.Terrain != (byte)L3Terrain.Habitable) continue;
                cell.Cracked = true;
                cell.Terrain = (byte)L3Terrain.Crack;
                if (cell.Occupied)
                {
                    var def = Config.PlantsByIndex[cell.PlantIndex];
                    if (!def.Has(L3PlantFlags.CrackSpread))
                    {
                        Kill(ref cell);
                    }
                }
            }
        }
    }

    private void AgeAndSpecials()
    {
        for (int i = 0; i < TotalCells; i++)
        {
            ref var cell = ref _grid[i];
            if (!cell.Occupied) continue;
            var def = Config.PlantsByIndex[cell.PlantIndex];
            int add = 1;
            if (def.BurntMaturityBoost != 0 && cell.Burnt) add += def.BurntMaturityBoost;
            if (def.AdjacentMaturityBoost != 0 || def.AdjacentMaturityPenalty != 0)
            {
                if (AnyOccupiedNeighbor(i))
                {
                    add += def.AdjacentMaturityBoost;
                    add -= def.AdjacentMaturityPenalty;
                }
            }

            if (add < 1) add = 1;
            int age = cell.Age + add;
            cell.Age = (ushort)Math.Min(ushort.MaxValue, age);
        }
    }

    private void ApplyShadeAndBurnt()
    {
        for (int i = 0; i < TotalCells; i++) _grid[i].Shaded = false;

        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                int i = Idx(r, c);
                ref readonly var cell = ref _grid[i];
                if (!cell.Occupied) continue;
                var def = Config.PlantsByIndex[cell.PlantIndex];
                int need = EffectiveMaturity(in def, in cell);
                if (cell.Age < need) continue;

                if (def.ShadeRadius > 0)
                {
                    StampRadius(r, c, def.ShadeRadius, shade: true, burnt: false);
                }

                if (def.BurntRadius > 0)
                {
                    StampRadius(r, c, def.BurntRadius, shade: false, burnt: true);
                }
            }
        }
    }

    private void StampRadius(int r, int c, int radius, bool shade, bool burnt)
    {
        int r0 = Math.Max(0, r - radius);
        int r1 = Math.Min(Rows - 1, r + radius);
        int c0 = Math.Max(0, c - radius);
        int c1 = Math.Min(Cols - 1, c + radius);
        for (int nr = r0; nr <= r1; nr++)
        {
            for (int nc = c0; nc <= c1; nc++)
            {
                ref var t = ref _grid[Idx(nr, nc)];
                if (shade) t.Shaded = true;
                if (burnt && !t.Burnt)
                {
                    t.Burnt = true;
                    _burntCount++;
                }
            }
        }
    }

    private void ApplyWeaknessCulls()
    {
        for (int i = 0; i < TotalCells; i++)
        {
            ref var cell = ref _grid[i];
            if (!cell.Occupied) continue;
            var def = Config.PlantsByIndex[cell.PlantIndex];
            int r = i / Cols;
            int c = i - r * Cols;

            if (def.Has(L3PlantFlags.NoShadeSurvival) && cell.Shaded)
            {
                Kill(ref cell);
                continue;
            }

            if (def.Has(L3PlantFlags.ShadeRequired) && !cell.Shaded && cell.Age >= EffectiveMaturity(in def, in cell))
            {
                Kill(ref cell);
                continue;
            }

            if (def.DieIfNeighborsGreaterThan > 0)
            {
                int n = CountMooreOccupied(r, c);
                if (n > def.DieIfNeighborsGreaterThan)
                {
                    Kill(ref cell);
                    continue;
                }
            }

            if (def.Has(L3PlantFlags.DieIfIsolated) && !AnyVonNeumannOccupied(r, c))
            {
                Kill(ref cell);
                continue;
            }

            if (def.Has(L3PlantFlags.NoAdjacentPlants) && AnyVonNeumannOccupied(r, c))
            {
                Kill(ref cell);
            }
        }
    }

    private void Spread()
    {
        Array.Copy(_grid, _next, TotalCells);

        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                int i = Idx(r, c);
                ref readonly var src = ref _grid[i];
                if (!src.Occupied) continue;
                var def = Config.PlantsByIndex[src.PlantIndex];
                int mat = EffectiveMaturity(in def, in src);
                if (src.Age < mat) continue;
                if (def.SpreadRate == 0 || src.Age % def.SpreadRate != 0) continue;
                if (Season == L3Season.Winter && def.Has(L3PlantFlags.NoWinterSpread)) continue;
                if (src.Shaded && def.Has(L3PlantFlags.NoShadeSpread)) continue;

                ResolveSpread(r, c, in def);
            }
        }

        RecalcCountsFromNext();
        var tmp = _grid;
        _grid = _next;
        _next = tmp;
    }

    private void ResolveSpread(int r, int c, in L3PlantDef def)
    {
        int range = def.SpreadRange;
        switch (def.SpreadType)
        {
            case L3SpreadType.VonNeumann:
                for (int d = 1; d <= range; d++)
                {
                    TrySpread(r - d, c, in def);
                    TrySpread(r + d, c, in def);
                    TrySpread(r, c - d, in def);
                    TrySpread(r, c + d, in def);
                }
                break;
            case L3SpreadType.Row:
                for (int d = 1; d <= range; d++)
                {
                    TrySpread(r, c - d, in def);
                    TrySpread(r, c + d, in def);
                }
                break;
            case L3SpreadType.Column:
                for (int d = 1; d <= range; d++)
                {
                    TrySpread(r - d, c, in def);
                    TrySpread(r + d, c, in def);
                }
                break;
            case L3SpreadType.Moore:
                for (int dr = -range; dr <= range; dr++)
                {
                    for (int dc = -range; dc <= range; dc++)
                    {
                        if (dr == 0 && dc == 0) continue;
                        TrySpread(r + dr, c + dc, in def);
                    }
                }
                break;
            case L3SpreadType.CrossHatch:
                for (int d = 1; d <= range; d++)
                {
                    TrySpread(r + d, c + d, in def);
                    TrySpread(r + d, c - d, in def);
                    TrySpread(r - d, c + d, in def);
                    TrySpread(r - d, c - d, in def);
                }
                break;
        }
    }

    private void TrySpread(int tr, int tc, in L3PlantDef def)
    {
        if ((uint)tr >= (uint)Rows || (uint)tc >= (uint)Cols) return;
        int i = Idx(tr, tc);
        ref readonly var current = ref _grid[i];
        if (current.Occupied)
        {
            var tdef = Config.PlantsByIndex[current.PlantIndex];
            if (current.Age >= EffectiveMaturity(in tdef, in current) && !tdef.Has(L3PlantFlags.Coexist))
            {
                return;
            }
        }

        if (current.Shaded && def.Has(L3PlantFlags.NoShadeSpread)) return;
        if (!CanOccupy(in def, tr, tc, planting: false)) return;

        ref var next = ref _next[i];
        if (next.Occupied)
        {
            var existing = Config.PlantsByIndex[next.PlantIndex];
            if (existing.Invasiveness > def.Invasiveness) return;
            if (existing.Invasiveness == def.Invasiveness && next.PlantIndex != 0 && next.Age >= current.Age)
            {
                if (next.PlantIndex != def.Index) return;
            }
        }

        next.PlantIndex = def.Index;
        next.Age = 0;
    }

    private void MetabolizeAndStarve()
    {
        _deadMatterCount = 0;
        for (int i = 0; i < TotalCells; i++)
        {
            ref var cell = ref _grid[i];
            if (cell.Occupied)
            {
                var def = Config.PlantsByIndex[cell.PlantIndex];
                int drain = 1;
                if (def.Has(L3PlantFlags.NutrientRegen) || _animals.Contains("Loamcrawlers"))
                {
                    if (def.Has(L3PlantFlags.NutrientRegen)) drain = 0;
                    else if (cell.DeadMatter) drain = 0;
                }

                int n = cell.Nutrients - drain;
                if (n < 0) n = 0;
                cell.Nutrients = (byte)n;
                if (cell.Nutrients == 0)
                {
                    bool resurrect = def.Has(L3PlantFlags.Resurrect);
                    Kill(ref cell);
                    if (resurrect)
                    {
                        cell.PlantIndex = def.Index;
                        cell.Age = 0;
                        cell.Nutrients = 50;
                        _speciesCounts[def.Index]++;
                        _occupiedCount++;
                    }
                }
            }
            else if (cell.DeadMatter && cell.Nutrients < 100)
            {
                int add = _animals.Contains("Loamcrawlers") ? 2 : 1;
                int n = cell.Nutrients + add;
                if (n > 100) n = 100;
                cell.Nutrients = (byte)n;
            }

            if (cell.DeadMatter) _deadMatterCount++;
        }
    }

    private void RefreshUnlocksAndAnimals()
    {
        if (Config.AnimalsEnabled)
        {
            foreach (var animal in Config.Animals)
            {
                if (EvalNode(animal.Requirement))
                {
                    _animals.Add(animal.Name);
                }
            }
        }

        foreach (var kv in Config.UnlockByPlant)
        {
            if (_unlocked[kv.Key]) continue;
            if (EvalNode(kv.Value)) _unlocked[kv.Key] = true;
        }
    }

    private bool EvalNode(L3UnlockNode node)
    {
        switch (node.Kind)
        {
            case L3UnlockKind.And:
                foreach (var c in node.Children) if (!EvalNode(c)) return false;
                return true;
            case L3UnlockKind.Or:
                foreach (var c in node.Children) if (EvalNode(c)) return true;
                return false;
            case L3UnlockKind.SpeciesPresent:
                return _animals.Contains(node.Key);
            case L3UnlockKind.SpeciesAbsent:
                return !_animals.Contains(node.Key);
            case L3UnlockKind.Event:
                return _events.Contains(node.Key);
            case L3UnlockKind.Coverage:
                return L3Loader.Compare(CoverageOf(node), node.Op, node.Value);
            case L3UnlockKind.Count:
                return L3Loader.Compare(CountOf(node), node.Op, node.Value);
            case L3UnlockKind.FeatureCount:
                {
                    double feat = 0;
                    string keyLower = node.Key.ToLowerInvariant();
                    if (keyLower.Contains("dead"))
                    {
                        feat = node.Value <= 1.0 ? (double)_deadMatterCount / TotalCells : _deadMatterCount;
                    }
                    else if (keyLower.Contains("burnt"))
                    {
                        feat = node.Value <= 1.0 ? (double)_burntCount / TotalCells : _burntCount;
                    }
                    return L3Loader.Compare(feat, node.Op, node.Value);
                }
            default:
                return true;
        }
    }

    private double CoverageOf(L3UnlockNode node)
    {
        if (node.Key == "__dominance__")
        {
            int max = 0;
            for (int i = 1; i < 32; i++) if (_speciesCounts[i] > max) max = _speciesCounts[i];
            return _occupiedCount > 0 ? (double)max / _occupiedCount : 0;
        }

        return (double)CountOf(node) / TotalCells;
    }

    private double CountOf(L3UnlockNode node)
    {
        if (node.PlantIndex != 0) return _speciesCounts[node.PlantIndex];
        if (string.IsNullOrEmpty(node.Key)) return 0;

        int sum = 0;
        foreach (var part in node.Key.Split('|'))
        {
            string trimmed = part.Trim();
            if (Config.NameToIndex.TryGetValue(trimmed, out byte idx))
            {
                sum += _speciesCounts[idx];
                continue;
            }

            if (Config.Groups.TryGetValue(trimmed, out var group) ||
                Config.Groups.TryGetValue(L3Loader.NormalizeKey(trimmed), out group))
            {
                foreach (byte g in group) sum += _speciesCounts[g];
            }
        }

        return sum;
    }

    private void RecalcCountsFromNext()
    {
        Array.Clear(_speciesCounts);
        _occupiedCount = 0;
        for (int i = 0; i < TotalCells; i++)
        {
            byte p = _next[i].PlantIndex;
            if (p == 0) continue;
            _speciesCounts[p]++;
            _occupiedCount++;
        }
    }

    private void Kill(ref L3Cell cell)
    {
        if (cell.PlantIndex != 0)
        {
            _speciesCounts[cell.PlantIndex]--;
            _occupiedCount--;
        }

        cell.PlantIndex = 0;
        cell.Age = 0;
        cell.DeadMatter = true;
    }

    private bool CanOccupy(in L3PlantDef def, int r, int c, bool planting)
    {
        if ((uint)r >= (uint)Rows || (uint)c >= (uint)Cols) return false;
        int i = Idx(r, c);
        ref readonly var cell = ref _grid[i];
        byte t = cell.Terrain;
        bool cracked = cell.Cracked || t == (byte)L3Terrain.Crack;
        if (t == (byte)L3Terrain.Path || t == (byte)L3Terrain.Stone || t == (byte)L3Terrain.Water)
        {
            return false;
        }

        if (cracked && !def.Has(L3PlantFlags.CrackSpread))
        {
            return false;
        }

        bool burnt = cell.Burnt || cell.Soil == 3;
        if (burnt && !def.Has(L3PlantFlags.BurntSoilOk))
        {
            return false;
        }

        if (def.Has(L3PlantFlags.MustBurntSoil) && !burnt) return false;
        if (def.PreferredSoilMask != 0 && (def.PreferredSoilMask & (1 << cell.Soil)) == 0 && !burnt) return false;
        if (def.Has(L3PlantFlags.DeadMatterOnlySpread) && !planting && !cell.DeadMatter) return false;

        if (cell.Nutrients == 0) return false;

        if (def.Has(L3PlantFlags.MustRockOrPath) && !AdjacentToPathOrStone(r, c)) return false;
        if (def.Has(L3PlantFlags.MustWater) && !AdjacentToTerrain(r, c, (byte)L3Terrain.Water)) return false;
        if (def.Has(L3PlantFlags.NoAdjacentPlants) && AnyVonNeumannOccupied(r, c)) return false;

        return true;
    }

    private static int EffectiveMaturity(in L3PlantDef def, in L3Cell cell)
    {
        int m = def.TimeToMaturity;
        if (def.BurntMaturityBoost != 0 && cell.Burnt) m = Math.Max(1, m - def.BurntMaturityBoost);
        return m;
    }

    private bool AnyOccupiedNeighbor(int i)
    {
        int r = i / Cols;
        int c = i - r * Cols;
        return AnyVonNeumannOccupied(r, c) ||
               OccupiedAt(r - 1, c - 1) || OccupiedAt(r - 1, c + 1) ||
               OccupiedAt(r + 1, c - 1) || OccupiedAt(r + 1, c + 1);
    }

    private int CountMooreOccupied(int r, int c)
    {
        int n = 0;
        for (int dr = -1; dr <= 1; dr++)
        {
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0) continue;
                if (OccupiedAt(r + dr, c + dc)) n++;
            }
        }

        return n;
    }

    private bool AnyVonNeumannOccupied(int r, int c)
    {
        return OccupiedAt(r - 1, c) || OccupiedAt(r + 1, c) || OccupiedAt(r, c - 1) || OccupiedAt(r, c + 1);
    }

    private bool OccupiedAt(int r, int c)
    {
        if ((uint)r >= (uint)Rows || (uint)c >= (uint)Cols) return false;
        return _grid[Idx(r, c)].Occupied;
    }
}