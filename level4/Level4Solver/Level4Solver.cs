using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace EntelectHackathon.Level4;

internal sealed class L4Solver
{
    private readonly L4Engine _engine;
    private readonly L4LevelConfig _cfg;
    private readonly int[] _actionsPerTick;
    private readonly List<L4Planting> _plan = new(16000);

    public const byte Grass = 1;
    public const byte Rose = 2;
    public const byte BlueMoss = 3;
    public const byte Crimson = 4;
    public const byte Sunflower = 5;
    public const byte Lavender = 6;
    public const byte Orange = 7;
    public const byte SilverFern = 8;
    public const byte Glowcap = 9;
    public const byte PurpleCanopy = 10;
    public const byte StoneReed = 11;
    public const byte Oak = 12;
    public const byte Emberroot = 13;
    public const byte Whiteveil = 14;
    public const byte Moonpetal = 15;
    public const byte Ironthorn = 16;
    public const byte CrystalCactus = 17;
    public const byte MireBloom = 18;
    public const byte Razorgrass = 19;
    public const byte Skyvine = 20;
    public const byte GhostOrchid = 21;
    public const byte AmberFern = 22;
    public const byte Thornheart = 23;
    public const byte Sporewood = 24;
    public const byte Sunshard = 25;
    public const byte Ashroot = 26;
    public const byte LivingTopiary = 27;
    public const byte Bloodbloom = 28;
    public const byte Starcap = 29;
    public const byte Phoenix = 30;
    public const byte Worldtree = 31;

    // Fixed tree placement coordinates to ensure vital synergies and permanent shade
    private static readonly (int R, int C)[] OakSpots =
    [
        (15, 8), (30, 8), (45, 8), (60, 8), (75, 8), (90, 8),
        (105, 8), (120, 8), (135, 8), (150, 8), (165, 8), (180, 8)
    ];

    private static readonly (int R, int C)[] PurpleSpots =
    [
        (15, 18), (30, 18), (45, 18), (60, 18), (75, 18), (90, 18),
        (105, 18), (120, 18), (135, 18), (150, 18)
    ];

    private static readonly (int R, int C)[] SporewoodSpots =
    [
        (20, 26), (50, 26), (80, 26), (110, 26), (140, 26), (170, 26)
    ];

    private static readonly (int R, int C)[] WorldtreeSpots =
    [
        (35, 28), (65, 28), (95, 28), (125, 28), (155, 28), (185, 28)
    ];

    // Grid of Emberroot trees in cols 275..295 to create solid burnt soil blanket
    private static readonly (int R, int C)[] EmberrootSpots = GenerateEmberrootSpots();

    private static (int R, int C)[] GenerateEmberrootSpots()
    {
        var list = new List<(int R, int C)>(185);
        int[] cols = [275, 280, 285, 290, 295];
        for (int r = 10; r <= 190; r += 5)
        {
            foreach (int c in cols)
            {
                list.Add((r, c));
            }
        }
        return list.ToArray();
    }

    public L4Solver(L4Engine engine)
    {
        _engine = engine;
        _cfg = engine.Config;
        _actionsPerTick = new int[engine.TotalTicks];
    }

    public List<L4Planting> GeneratePlan()
    {
        _plan.Clear();
        Array.Clear(_actionsPerTick);
        _engine.Reset();

        var batch = new List<L4Planting>(20);
        int tEnd = _engine.TotalTicks;

        for (int tick = 0; tick < tEnd; tick++)
        {
            batch.Clear();

            int unlockedCount = 0;
            for (int u = 1; u < 32; u++) if (_engine.IsUnlocked((byte)u)) unlockedCount++;

            if (unlockedCount < 31 && tick < 580)
            {
                FillTechPhase(tick, batch);
            }
            else
            {
                FillAssemblyPhase(tick, batch);
            }

            foreach (var a in batch)
            {
                _plan.Add(a);
                _actionsPerTick[tick]++;
            }

            if (tick < 20 || tick % 50 == 0 || tick == tEnd - 1)
            {
                var speciesSummary = string.Join(" ", batch.GroupBy(b => b.PlantIndex).Select(g => $"{_cfg.PlantsByIndex[g.Key].Name.Split(' ')[0]}:{g.Count()}"));
                Console.WriteLine($"Tick {tick,3}: Unl={unlockedCount,2} Dead={_engine.DeadMatterCount,5} Burnt={_engine.BurntCount,4} Grass={_engine.SpeciesCount(Grass),5} Rose={_engine.SpeciesCount(Rose),5} Crim={_engine.SpeciesCount(Crimson),5} Moss={_engine.SpeciesCount(BlueMoss),4} Fern={_engine.SpeciesCount(SilverFern),4} Glow={_engine.SpeciesCount(Glowcap),4} White={_engine.SpeciesCount(Whiteveil),4} Purple={_engine.SpeciesCount(PurpleCanopy),2} | Actions: {speciesSummary}");
            }

            _engine.Step(CollectionsMarshal.AsSpan(batch));
        }

        return _plan;
    }

    private static bool CanReplaceGrass(byte p) => p == Grass;

    // QUARANTINE PROTOCOL: Grass is strictly confined to cols 195..210!
    // QUARANTINE PROTOCOL: Grass is strictly confined to cols 190..215.
    // Only cull Grass if it has already met all unlock requirements (>3050).
    private void RegulateGrassEscapees(ref int remaining, List<L4Planting> batch)
    {
        if (remaining <= 0) return;
        if (_engine.SpeciesCount(Grass) < 3050) return;

        int budget = Math.Min(remaining, 4);
        while (budget > 0 && remaining > 0)
        {
            byte westReplacer = _engine.IsUnlocked(Sunflower) ? Sunflower : Rose;
            int idx = _engine.FindReplaceFast(westReplacer, 0, 190, CanReplaceGrass);

            if (idx < 0)
            {
                byte eastReplacer = _engine.IsUnlocked(SilverFern) ? SilverFern : westReplacer;
                idx = _engine.FindReplaceFast(eastReplacer, 215, _engine.Cols, CanReplaceGrass);
            }

            if (idx < 0) break;

            int r = idx / _engine.Cols;
            int c = idx - r * _engine.Cols;
            byte rep = c < 190 ? (_engine.IsUnlocked(Sunflower) ? Sunflower : Rose)
                               : (_engine.IsUnlocked(SilverFern) ? SilverFern : Rose);
            batch.Add(new L4Planting(_engine.Tick, r, c, rep));
            remaining--;
            budget--;
        }
    }

    private void FillTechPhase(int tick, List<L4Planting> batch)
    {
        int remaining = 20 - batch.Count;

        // Tick 0: Foundation
        if (tick == 0)
        {
            PlanTreeSpot(Oak, OakSpots, ref remaining, batch, 12);
            SeedRowDistributed(Lavender, ref remaining, batch, 20, 50, maxBudget: 8);
            return;
        }

        // Tick 1: Lavender to 20 -> Nectaris! Rose Bush to 8 -> Crimson Vine!
        if (tick == 1)
        {
            SeedRowDistributed(Lavender, ref remaining, batch, 20, 50, maxBudget: 12);
            SeedRowDistributed(Rose, ref remaining, batch, 80, 110, maxBudget: 8);
            return;
        }

        // Tick 2: Grass in center of quarantine zone [200..205]! Crimson Vine in pairs
        if (tick == 2)
        {
            PlanSeed(Grass, ref remaining, batch, 200, 205, maxThisTick: 10, targetTotal: 10);
            SeedCrimsonInPairs(ref remaining, batch, 110, 165, maxBudget: 10, targetTotal: 10);
            return;
        }

        // 1. CULL ANY GRASS ESCAPEES (Only active when Grass exceeds target 3050)
        RegulateGrassEscapees(ref remaining, batch);

        // 2. Feed Grass inside quarantine zone [190..215] until it reaches 2500 (unlocks Loamcrawlers)
        bool razorUnlocked = _engine.IsUnlocked(Razorgrass);
        int grassTarget = razorUnlocked ? 2500 : 3100;
        if (_engine.SpeciesCount(Grass) < grassTarget)
        {
            int grassMax = _engine.SpeciesCount(Grass) < 60 ? 6 : 2;
            PlanSeed(Grass, ref remaining, batch, 190, 215, maxThisTick: grassMax, targetTotal: grassTarget);
        }

        // 3. Permanent Trees (Top Priority)
        if (_engine.IsUnlocked(Oak) && _engine.SpeciesCount(Oak) < 12)
            PlanTreeSpot(Oak, OakSpots, ref remaining, batch, 12);

        if (_engine.IsUnlocked(PurpleCanopy) && _engine.SpeciesCount(PurpleCanopy) < 10)
            PlanTreeSpot(PurpleCanopy, PurpleSpots, ref remaining, batch, 10);

        if (_engine.IsUnlocked(Sporewood) && _engine.SpeciesCount(Sporewood) < 6)
            PlanTreeSpot(Sporewood, SporewoodSpots, ref remaining, batch, 6);

        if (_engine.IsUnlocked(Worldtree) && _engine.SpeciesCount(Worldtree) < 6)
            PlanTreeSpot(Worldtree, WorldtreeSpots, ref remaining, batch, 6);

        if (_engine.IsUnlocked(Emberroot) && _engine.SpeciesCount(Emberroot) < EmberrootSpots.Length)
            PlanTreeSpot(Emberroot, EmberrootSpots, ref remaining, batch, EmberrootSpots.Length);

        // 4. IMMEDIATE SEEDING: Any unlocked non-tree species with < 15 plants gets planted immediately!
        for (byte p = 1; p < 32; p++)
        {
            if (remaining <= 0) break;
            if (!_engine.IsUnlocked(p) || IsTree(p)) continue;

            int count = _engine.SpeciesCount(p);
            if (count < 15)
            {
                int need = Math.Min(remaining, 15 - count);
                SeedSpecies(p, ref remaining, batch, maxBudget: need, canReplace: CanReplaceGrass);
            }
        }

        // 5. TOP TIER GATEWAY ENABLERS:
        // (A) Crimson Vine -> MUST REACH 2400 FOR PURPLE CANOPY!
        if (_engine.IsUnlocked(Crimson) && !_engine.IsUnlocked(PurpleCanopy) && _engine.SpeciesCount(Crimson) < 2600)
        {
            SeedCrimsonInPairs(ref remaining, batch, 110, 165, maxBudget: 14, targetTotal: 2600, canReplace: CanReplaceGrass);
        }

        // (B) Blue Moss -> gates Purple Canopy (600), Silver Fern (1800), Mire Bloom (3000)
        if (_engine.IsUnlocked(BlueMoss) && (!_engine.IsUnlocked(PurpleCanopy) || _engine.SpeciesCount(BlueMoss) < 1850))
        {
            int mossTarget = !_engine.IsUnlocked(PurpleCanopy) ? 650 : 1850;
            if (_engine.SpeciesCount(BlueMoss) < mossTarget)
            {
                int mossBudget = _engine.SpeciesCount(BlueMoss) < 650 ? 8 : 4;
                PlanSeedStaggered(BlueMoss, ref remaining, batch, 50, 80, maxThisTick: mossBudget, targetTotal: mossTarget);
            }
        }

        // (C) Rose Bush -> gates Blue Moss (600), Ironthorn (2400)
        if (!_engine.IsUnlocked(BlueMoss) && _engine.SpeciesCount(Rose) < 650)
        {
            SeedRowDistributed(Rose, ref remaining, batch, 80, 110, maxBudget: 6, canReplace: CanReplaceGrass);
        }

        // (D) Whiteveil Mycelium -> gates Sporewood, Emberroot, Starcap (need 3000)
        if (_engine.IsUnlocked(Whiteveil) && _engine.SpeciesCount(Whiteveil) < 3200)
        {
            int whiteBudget = _engine.SpeciesCount(Whiteveil) < 3050 ? 14 : 4;
            SeedWithBudget(Whiteveil, ref remaining, batch, 230, 300, maxBudget: whiteBudget, canReplace: CanReplaceGrass);
        }

        // (E) Glowcap Fungus -> gates Whiteveil, Ghost Orchid (need 3000)
        if (_engine.IsUnlocked(Glowcap) && !_engine.IsUnlocked(Whiteveil) && _engine.SpeciesCount(Glowcap) < 3200)
        {
            int glowBudget = _engine.SpeciesCount(Glowcap) < 3050 ? 14 : 4;
            SeedWithBudget(Glowcap, ref remaining, batch, 250, 275, maxBudget: glowBudget, canReplace: CanReplaceGrass);
        }

        // (F) Ashroot Bramble -> gates Phoenix Bloom (needs 2400 on burnt soil)
        if (_engine.IsUnlocked(Ashroot) && _engine.SpeciesCount(Ashroot) < 2800)
        {
            int ashBudget = _engine.SpeciesCount(Ashroot) < 2500 ? 14 : 4;
            PlanSeedSpecial(Ashroot, ref remaining, batch, rockPath: false, water: false, burnt: true, maxThisTick: ashBudget, targetTotal: 2800, canReplace: CanReplaceGrass);
        }

        // (G) Silver Fern -> gates Living Topiary (2400), Amber Fern (3600)
        if (_engine.IsUnlocked(SilverFern) && _engine.SpeciesCount(SilverFern) < 3800)
        {
            int fernBudget = _engine.SpeciesCount(SilverFern) < 3600 ? 8 : 4;
            SeedRowDistributed(SilverFern, ref remaining, batch, 215, 250, maxBudget: fernBudget, canReplace: CanReplaceGrass);
        }

        // (H) Moonpetal Lily -> gates Ghost Orchid (1800), Sunshard (2400)
        if (_engine.IsUnlocked(Moonpetal) && _engine.SpeciesCount(Moonpetal) < 2600)
        {
            PlanSeedShaded(Moonpetal, ref remaining, batch, 0, 20, maxThisTick: 4, targetTotal: 2600, canReplace: CanReplaceGrass);
        }

        // (I) Lavender -> gates Moonpetal (2400)
        if (_engine.IsUnlocked(PurpleCanopy) && _engine.SpeciesCount(Lavender) < 2550)
        {
            SeedRowDistributed(Lavender, ref remaining, batch, 20, 50, maxBudget: 6, canReplace: CanReplaceGrass);
        }

        // (J) Ironthorn Shrub -> gates Thornheart (3000)
        if (_engine.IsUnlocked(Ironthorn) && _engine.SpeciesCount(Ironthorn) < 3200)
        {
            PlanSeed(Ironthorn, ref remaining, batch, 215, 250, maxThisTick: 4, targetTotal: 3200, canReplace: CanReplaceGrass);
        }

        // (K) Sunflower: need >= 1800 for Solwings
        if (_engine.SpeciesCount(Sunflower) < 2000)
        {
            PlanSeed(Sunflower, ref remaining, batch, 165, 190, maxThisTick: 3, targetTotal: 2000, canReplace: CanReplaceGrass);
        }

        // (L) Orange Blossom: need > 1200 for Sunshard
        if (_engine.IsUnlocked(Orange) && _engine.SpeciesCount(Orange) < 1400)
        {
            PlanSeed(Orange, ref remaining, batch, 80, 110, maxThisTick: 3, targetTotal: 1400, canReplace: CanReplaceGrass);
        }

        // (M) Ghost Orchid: need > 600 for Starcap
        if (_engine.IsUnlocked(GhostOrchid) && _engine.SpeciesCount(GhostOrchid) < 1000)
        {
            PlanSeedShaded(GhostOrchid, ref remaining, batch, 0, 20, maxThisTick: 3, targetTotal: 1000, canReplace: CanReplaceGrass);
        }

        // (N) Niche terrain species
        if (_engine.IsUnlocked(StoneReed) && _engine.SpeciesCount(StoneReed) < 350)
            PlanSeedSpecial(StoneReed, ref remaining, batch, rockPath: true, water: false, burnt: false, maxThisTick: 2, targetTotal: 350);

        if (_engine.IsUnlocked(MireBloom) && _engine.SpeciesCount(MireBloom) < 350)
            PlanSeedSpecial(MireBloom, ref remaining, batch, rockPath: false, water: true, burnt: false, maxThisTick: 2, targetTotal: 350);

        // (O) Secondary unlocked species to establish population
        if (_engine.IsUnlocked(Skyvine) && _engine.SpeciesCount(Skyvine) < 1500)
            PlanSeed(Skyvine, ref remaining, batch, 110, 165, maxThisTick: 2, targetTotal: 1500, canReplace: CanReplaceGrass);

        if (_engine.IsUnlocked(Razorgrass) && _engine.SpeciesCount(Razorgrass) < 1500)
            PlanSeed(Razorgrass, ref remaining, batch, 215, 250, maxThisTick: 2, targetTotal: 1500, canReplace: CanReplaceGrass);

        if (_engine.IsUnlocked(LivingTopiary) && _engine.SpeciesCount(LivingTopiary) < 200)
            PlanSeedIsolated(LivingTopiary, ref remaining, batch, 165, 190, maxThisTick: 2, targetTotal: 200);

        if (_engine.IsUnlocked(Bloodbloom) && _engine.SpeciesCount(Bloodbloom) < 1500)
            PlanSeed(Bloodbloom, ref remaining, batch, 110, 165, maxThisTick: 2, targetTotal: 1500, canReplace: CanReplaceGrass);

        if (_engine.IsUnlocked(Thornheart) && _engine.SpeciesCount(Thornheart) < 1500)
            PlanSeed(Thornheart, ref remaining, batch, 215, 250, maxThisTick: 2, targetTotal: 1500, canReplace: CanReplaceGrass);

        if (_engine.IsUnlocked(AmberFern) && _engine.SpeciesCount(AmberFern) < 1500)
            PlanSeed(AmberFern, ref remaining, batch, 215, 250, maxThisTick: 2, targetTotal: 1500, canReplace: CanReplaceGrass);

        if (_engine.IsUnlocked(Sunshard) && _engine.SpeciesCount(Sunshard) < 1500)
            PlanSeed(Sunshard, ref remaining, batch, 215, 250, maxThisTick: 2, targetTotal: 1500, canReplace: CanReplaceGrass);

        if (_engine.IsUnlocked(Starcap) && _engine.SpeciesCount(Starcap) < 1500)
            PlanSeed(Starcap, ref remaining, batch, 250, 275, maxThisTick: 2, targetTotal: 1500, canReplace: CanReplaceGrass);

        if (_engine.IsUnlocked(CrystalCactus) && _engine.SpeciesCount(CrystalCactus) < 1500)
            PlanSeed(CrystalCactus, ref remaining, batch, 215, 250, maxThisTick: 2, targetTotal: 1500, canReplace: CanReplaceGrass);

        if (_engine.IsUnlocked(Phoenix) && _engine.SpeciesCount(Phoenix) < 1500)
            PlanSeed(Phoenix, ref remaining, batch, 275, 300, maxThisTick: 2, targetTotal: 1500, canReplace: CanReplaceGrass);

        // Fill leftover budget among lowest unlocked species
        if (remaining > 0)
        {
            var palette = BuildPalette();
            var sorted = palette.OrderBy(p => _engine.SpeciesCount(p)).ToArray();
            foreach (var p in sorted)
            {
                if (remaining <= 0) break;
                SeedSpecies(p, ref remaining, batch, maxBudget: 2, canReplace: CanReplaceGrass);
            }
        }
    }

    private void FillAssemblyPhase(int tick, List<L4Planting> batch)
    {
        byte[] palette = BuildPalette();
        if (palette.Length == 0) return;

        int remaining = 20;
        int totalOccupied = Math.Max(1, _engine.OccupiedCount);
        int targetPerSpecies = Math.Max(1, totalOccupied / palette.Length);

        // Replacement predicate: replace non-tree plants that exceed target
        bool CanReplaceAssembly(byte plant)
        {
            if (IsTree(plant)) return false;
            if (plant is StoneReed or MireBloom) return false;
            return _engine.SpeciesCount(plant) > targetPerSpecies * 1.15;
        }

        // Sort palette ascending by count to always lift the lowest species
        var order = palette.OrderBy(p => _engine.SpeciesCount(p)).ToArray();

        foreach (byte plant in order)
        {
            if (remaining <= 0) break;
            int count = _engine.SpeciesCount(plant);
            if (count >= targetPerSpecies * 1.05) continue;

            SeedSpecies(plant, ref remaining, batch, maxBudget: 2, canReplace: CanReplaceAssembly);
        }

        // If budget still remains, keep feeding the lowest count species
        while (remaining > 0)
        {
            order = palette.OrderBy(p => _engine.SpeciesCount(p)).ToArray();
            byte plant = order[0];
            int before = remaining;
            SeedSpecies(plant, ref remaining, batch, maxBudget: 1, canReplace: CanReplaceAssembly);
            if (remaining == before) break;
        }
    }

    private void SeedSpecies(byte plant, ref int remaining, List<L4Planting> batch, int maxBudget, Func<byte, bool>? canReplace)
    {
        if (remaining <= 0 || maxBudget <= 0 || !_engine.IsUnlocked(plant)) return;

        if (plant == StoneReed)
        {
            SeedSpecialWithBudget(plant, ref remaining, batch, true, false, false, maxBudget, canReplace);
        }
        else if (plant == MireBloom)
        {
            SeedSpecialWithBudget(plant, ref remaining, batch, false, true, false, maxBudget, canReplace);
        }
        else if (plant == Ashroot)
        {
            SeedSpecialWithBudget(plant, ref remaining, batch, false, false, true, maxBudget, canReplace);
        }
        else if (plant is Moonpetal or GhostOrchid)
        {
            SeedShadedWithBudget(plant, ref remaining, batch, 0, 30, maxBudget, canReplace);
        }
        else if (plant == LivingTopiary)
        {
            SeedIsolatedWithBudget(plant, ref remaining, batch, 165, 195, maxBudget);
        }
        else if (plant == Crimson)
        {
            SeedCrimsonInPairs(ref remaining, batch, 125, 165, maxBudget, int.MaxValue, canReplace);
        }
        else if (plant is Rose or SilverFern)
        {
            int z0 = ZoneLo(plant);
            int z1 = ZoneHi(plant);
            SeedRowDistributed(plant, ref remaining, batch, z0, z1, maxBudget, canReplace);
        }
        else
        {
            int z0 = ZoneLo(plant);
            int z1 = ZoneHi(plant);
            SeedWithBudget(plant, ref remaining, batch, z0, z1, maxBudget, canReplace);
        }
    }

    private static bool IsTree(byte plant) =>
        plant is Oak or PurpleCanopy or Sporewood or Worldtree or Emberroot;

    private byte[] BuildPalette()
    {
        byte[] candidates =
        [
            Grass, Rose, BlueMoss, Crimson, Sunflower, Lavender, Orange, SilverFern,
            Glowcap, PurpleCanopy, StoneReed, Oak, Emberroot, Whiteveil, Moonpetal,
            Ironthorn, CrystalCactus, MireBloom, Razorgrass, Skyvine, GhostOrchid,
            AmberFern, Thornheart, Sporewood, Sunshard, Ashroot, LivingTopiary,
            Bloodbloom, Starcap, Phoenix, Worldtree
        ];

        var list = new List<byte>(candidates.Length);
        foreach (byte p in candidates)
        {
            if (_engine.IsUnlocked(p)) list.Add(p);
        }

        return list.ToArray();
    }

    private int ZoneLo(byte plant) => plant switch
    {
        Oak or PurpleCanopy or Sporewood or Worldtree or Moonpetal or GhostOrchid => 0,
        Lavender => 20,
        BlueMoss => 50,
        Rose or Orange => 80,
        Crimson or Skyvine or Bloodbloom => 110,
        Sunflower or LivingTopiary => 165,
        Grass => 190,
        SilverFern or Ironthorn or Razorgrass or CrystalCactus or Thornheart or Sunshard or AmberFern => 215,
        Whiteveil or Glowcap or Starcap => 250,
        Emberroot or Ashroot or Phoenix => 275,
        _ => 20
    };

    private int ZoneHi(byte plant) => plant switch
    {
        Oak or PurpleCanopy or Sporewood or Worldtree or Moonpetal or GhostOrchid => 20,
        Lavender => 50,
        BlueMoss => 80,
        Rose or Orange => 110,
        Crimson or Skyvine or Bloodbloom => 165,
        Sunflower or LivingTopiary => 190,
        Grass => 215,
        SilverFern or Ironthorn or Razorgrass or CrystalCactus or Thornheart or Sunshard or AmberFern => 250,
        Whiteveil or Glowcap or Starcap => 275,
        Emberroot or Ashroot or Phoenix => _engine.Cols,
        _ => _engine.Cols
    };

    private void PlanTreeSpot(byte tree, (int R, int C)[] spots, ref int remaining, List<L4Planting> batch, int targetCount)
    {
        if (remaining <= 0 || !_engine.IsUnlocked(tree)) return;
        for (int i = 0; i < spots.Length && remaining > 0; i++)
        {
            if (_engine.SpeciesCount(tree) >= targetCount) break;
            var (r, c) = spots[i];
            var cell = _engine.Cell(r, c);
            if (cell.PlantIndex == tree) continue;

            int bestC = c;
            if (!_engine.CanOccupyPlanting(tree, r, bestC))
            {
                for (int dc = -2; dc <= 2; dc++)
                {
                    int tc = c + dc;
                    if (tc >= 0 && tc < _engine.Cols && _engine.CanOccupyPlanting(tree, r, tc))
                    {
                        bestC = tc;
                        break;
                    }
                }
            }

            if (_engine.CanOccupyPlanting(tree, r, bestC))
            {
                batch.Add(new L4Planting(_engine.Tick, r, bestC, tree));
                remaining--;
            }
        }
    }

    private void PlanSeed(byte plant, ref int remaining, List<L4Planting> batch, int colLo, int colHi, int maxThisTick, int targetTotal, Func<byte, bool>? canReplace = null)
    {
        if (remaining <= 0 || !_engine.IsUnlocked(plant)) return;
        if (_engine.SpeciesCount(plant) >= targetTotal) return;
        if (plant is Rose or SilverFern)
        {
            SeedRowDistributed(plant, ref remaining, batch, colLo, colHi, maxThisTick, canReplace);
        }
        else
        {
            SeedWithBudget(plant, ref remaining, batch, colLo, colHi, maxThisTick, canReplace);
        }
    }

    private int SeedRowDistributed(byte plant, ref int remaining, List<L4Planting> batch, int colLo, int colHi, int maxBudget, Func<byte, bool>? canReplace = null)
    {
        if (remaining <= 0 || maxBudget <= 0 || !_engine.IsUnlocked(plant)) return 0;
        int limit = Math.Min(remaining, maxBudget);
        int planted = 0;
        while (remaining > 0 && planted < limit)
        {
            int idx = -1;
            if (canReplace != null)
            {
                idx = _engine.FindReplaceFast(plant, colLo, colHi, canReplace);
            }
            if (idx < 0)
            {
                idx = _engine.FindEmptyRowDistributed(plant, colLo, colHi);
            }
            if (idx < 0) break;

            int r = idx / _engine.Cols;
            int c = idx - r * _engine.Cols;

            batch.Add(new L4Planting(_engine.Tick, r, c, plant));
            remaining--;
            planted++;
        }

        return planted;
    }

    private void PlanSeedStaggered(byte plant, ref int remaining, List<L4Planting> batch, int colLo, int colHi, int maxThisTick, int targetTotal)
    {
        if (remaining <= 0 || !_engine.IsUnlocked(plant)) return;
        if (_engine.SpeciesCount(plant) >= targetTotal) return;
        int limit = Math.Min(remaining, maxThisTick);
        int planted = 0;
        while (remaining > 0 && planted < limit)
        {
            int idx = _engine.FindEmptyFast(plant, colLo, colHi, (r, c) => (r + c) % 2 == 0);
            if (idx < 0) break;
            int r = idx / _engine.Cols;
            int c = idx - r * _engine.Cols;

            batch.Add(new L4Planting(_engine.Tick, r, c, plant));
            remaining--;
            planted++;
        }
    }

    private void PlanSeedSpecial(byte plant, ref int remaining, List<L4Planting> batch, bool rockPath, bool water, bool burnt, int maxThisTick, int targetTotal, Func<byte, bool>? canReplace = null)
    {
        if (remaining <= 0 || !_engine.IsUnlocked(plant)) return;
        if (_engine.SpeciesCount(plant) >= targetTotal) return;
        SeedSpecialWithBudget(plant, ref remaining, batch, rockPath, water, burnt, maxThisTick, canReplace);
    }

    private void PlanSeedShaded(byte plant, ref int remaining, List<L4Planting> batch, int colLo, int colHi, int maxThisTick, int targetTotal, Func<byte, bool>? canReplace = null)
    {
        if (remaining <= 0 || !_engine.IsUnlocked(plant)) return;
        if (_engine.SpeciesCount(plant) >= targetTotal) return;
        SeedShadedWithBudget(plant, ref remaining, batch, colLo, colHi, maxThisTick, canReplace);
    }

    private void PlanSeedIsolated(byte plant, ref int remaining, List<L4Planting> batch, int colLo, int colHi, int maxThisTick, int targetTotal)
    {
        if (remaining <= 0 || !_engine.IsUnlocked(plant)) return;
        if (_engine.SpeciesCount(plant) >= targetTotal) return;
        SeedIsolatedWithBudget(plant, ref remaining, batch, colLo, colHi, maxThisTick);
    }

    private int SeedCrimsonInPairs(ref int remaining, List<L4Planting> batch, int colLo, int colHi, int maxBudget, int targetTotal, Func<byte, bool>? canReplace = null)
    {
        if (remaining <= 0 || maxBudget <= 0 || !_engine.IsUnlocked(Crimson)) return 0;
        if (_engine.SpeciesCount(Crimson) >= targetTotal) return 0;
        int limit = Math.Min(remaining, maxBudget);
        int planted = 0;

        for (int step = 0; step < 500 && remaining > 0 && planted < limit; step++)
        {
            int idx = -1;
            if (canReplace != null)
            {
                idx = _engine.FindReplaceFast(Crimson, colLo, colHi, canReplace);
            }
            if (idx < 0)
            {
                idx = _engine.FindEmptyColDistributed(Crimson, colLo, colHi);
            }
            if (idx < 0) break;

            int r = idx / _engine.Cols;
            int c = idx - r * _engine.Cols;

            if (_engine.AnyVonNeumannOccupied(r, c))
            {
                batch.Add(new L4Planting(_engine.Tick, r, c, Crimson));
                remaining--;
                planted++;
                continue;
            }

            if (remaining < 2 || planted + 1 >= limit) continue;

            int nr = -1, nc = -1;
            int[] dr = [1, -1, 0, 0];
            int[] dc = [0, 0, 1, -1];
            for (int d = 0; d < 4; d++)
            {
                int tr = r + dr[d];
                int tc = c + dc[d];
                if (tr >= 0 && tr < _engine.Rows && tc >= colLo && tc < colHi)
                {
                    ref readonly var ncell = ref _engine.Cell(tr, tc);
                    bool canUse = ncell.Nutrients >= 25 &&
                                  (!ncell.Occupied || (canReplace != null && canReplace(ncell.PlantIndex)))
                                  && _engine.CanOccupyPlanting(Crimson, tr, tc);
                    if (canUse)
                    {
                        nr = tr;
                        nc = tc;
                        break;
                    }
                }
            }

            if (nr >= 0)
            {
                batch.Add(new L4Planting(_engine.Tick, r, c, Crimson));
                batch.Add(new L4Planting(_engine.Tick, nr, nc, Crimson));
                remaining -= 2;
                planted += 2;
            }
        }

        return planted;
    }

    private int SeedWithBudget(byte plant, ref int remaining, List<L4Planting> batch, int colLo, int colHi, int maxBudget, Func<byte, bool>? canReplace = null)
    {
        if (remaining <= 0 || maxBudget <= 0 || !_engine.IsUnlocked(plant)) return 0;
        int limit = Math.Min(remaining, maxBudget);
        int planted = 0;
        while (remaining > 0 && planted < limit)
        {
            int idx = -1;
            if (canReplace != null)
            {
                idx = _engine.FindReplaceFast(plant, colLo, colHi, canReplace);
            }
            if (idx < 0)
            {
                idx = _engine.FindEmptyFast(plant, colLo, colHi);
            }
            if (idx < 0) break;

            int r = idx / _engine.Cols;
            int c = idx - r * _engine.Cols;

            batch.Add(new L4Planting(_engine.Tick, r, c, plant));
            remaining--;
            planted++;
        }

        return planted;
    }

    private int SeedShadedWithBudget(byte plant, ref int remaining, List<L4Planting> batch, int colLo, int colHi, int maxBudget, Func<byte, bool>? canReplace = null)
    {
        if (remaining <= 0 || maxBudget <= 0 || !_engine.IsUnlocked(plant)) return 0;
        int limit = Math.Min(remaining, maxBudget);
        int planted = 0;
        while (remaining > 0 && planted < limit)
        {
            int idx = -1;
            if (canReplace != null)
            {
                idx = _engine.FindReplaceFast(plant, colLo, colHi, canReplace, (r, c) => _engine.Cell(r, c).Shaded);
            }
            if (idx < 0)
            {
                idx = _engine.FindEmptyFast(plant, colLo, colHi, (r, c) => _engine.Cell(r, c).Shaded);
            }
            if (idx < 0 && canReplace != null)
            {
                idx = _engine.FindReplaceFast(plant, 0, _engine.Cols, canReplace, (r, c) => _engine.Cell(r, c).Shaded);
            }
            if (idx < 0) break;

            int r = idx / _engine.Cols;
            int c = idx - r * _engine.Cols;

            batch.Add(new L4Planting(_engine.Tick, r, c, plant));
            remaining--;
            planted++;
        }

        return planted;
    }

    private int SeedIsolatedWithBudget(byte plant, ref int remaining, List<L4Planting> batch, int colLo, int colHi, int maxBudget)
    {
        if (remaining <= 0 || maxBudget <= 0 || !_engine.IsUnlocked(plant)) return 0;
        int limit = Math.Min(remaining, maxBudget);
        int planted = 0;
        while (remaining > 0 && planted < limit)
        {
            int idx = _engine.FindEmptyFast(plant, colLo, colHi, (r, c) =>
            {
                int r0 = Math.Max(0, r - 1);
                int r1 = Math.Min(_engine.Rows - 1, r + 1);
                int c0 = Math.Max(0, c - 1);
                int c1 = Math.Min(_engine.Cols - 1, c + 1);
                for (int nr = r0; nr <= r1; nr++)
                {
                    for (int nc = c0; nc <= c1; nc++)
                    {
                        if (nr == r && nc == c) continue;
                        if (_engine.Cell(nr, nc).Occupied) return false;
                    }
                }
                return true;
            });
            if (idx < 0) break;
            int r = idx / _engine.Cols;
            int c = idx - r * _engine.Cols;

            batch.Add(new L4Planting(_engine.Tick, r, c, plant));
            remaining--;
            planted++;
        }

        return planted;
    }

    private int SeedSpecialWithBudget(byte plant, ref int remaining, List<L4Planting> batch, bool rockPath, bool water, bool burnt, int maxBudget, Func<byte, bool>? canReplace = null)
    {
        if (remaining <= 0 || maxBudget <= 0 || !_engine.IsUnlocked(plant)) return 0;
        int limit = Math.Min(remaining, maxBudget);
        int planted = 0;

        bool Filter(int r, int c)
        {
            if (rockPath && !_engine.AdjacentToPathOrStone(r, c)) return false;
            if (water && !_engine.AdjacentToTerrain(r, c, (byte)L4Terrain.Water)) return false;
            if (burnt && !_engine.Cell(r, c).Burnt) return false;
            return true;
        }

        while (remaining > 0 && planted < limit)
        {
            int idx = -1;
            if (canReplace != null)
            {
                idx = _engine.FindReplaceFast(plant, 0, _engine.Cols, canReplace, Filter);
            }
            if (idx < 0)
            {
                idx = _engine.FindEmptyFast(plant, 0, _engine.Cols, Filter);
            }
            if (idx < 0) break;

            int r = idx / _engine.Cols;
            int c = idx - r * _engine.Cols;

            batch.Add(new L4Planting(_engine.Tick, r, c, plant));
            remaining--;
            planted++;
        }

        return planted;
    }
}
