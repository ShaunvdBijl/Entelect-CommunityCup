using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace EntelectHackathon.Level3;

internal sealed class L3Solver
{
    private readonly L3Engine _engine;
    private readonly L3LevelConfig _cfg;
    private readonly int[] _actionsPerTick;
    private readonly List<L3Planting> _plan = new(16000);

    private const byte Grass = 1;
    private const byte Rose = 2;
    private const byte BlueMoss = 3;
    private const byte Crimson = 4;
    private const byte Sunflower = 5;
    private const byte Lavender = 6;
    private const byte Orange = 7;
    private const byte SilverFern = 8;
    private const byte Glowcap = 9;
    private const byte PurpleCanopy = 10;
    private const byte StoneReed = 11;
    private const byte Oak = 12;
    private const byte Emberroot = 13;
    private const byte Whiteveil = 14;
    private const byte Moonpetal = 15;
    private const byte Ironthorn = 16;
    private const byte CrystalCactus = 17;
    private const byte MireBloom = 18;
    private const byte Razorgrass = 19;
    private const byte Skyvine = 20;
    private const byte GhostOrchid = 21;
    private const byte AmberFern = 22;
    private const byte Ashroot = 26;
    private const byte Phoenix = 30;

    public L3Solver(L3Engine engine)
    {
        _engine = engine;
        _cfg = engine.Config;
        _actionsPerTick = new int[engine.TotalTicks];
    }

    public List<L3Planting> GeneratePlan()
    {
        _plan.Clear();
        Array.Clear(_actionsPerTick);
        _engine.Reset();

        var batch = new List<L3Planting>(20);
        int tEnd = _engine.TotalTicks;
        int assemblyStart = Math.Max(0, tEnd - 90);

        for (int tick = 0; tick < tEnd; tick++)
        {
            batch.Clear();
            if (tick < assemblyStart)
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

            _engine.Step(CollectionsMarshalSpan(batch));
        }

        return _plan;
    }

    private static ReadOnlySpan<L3Planting> CollectionsMarshalSpan(List<L3Planting> batch)
    {
        return CollectionsMarshal.AsSpan(batch);
    }

    private void FillTechPhase(int tick, List<L3Planting> batch)
    {
        int remaining = 20 - batch.Count;

        if (tick < 8)
        {
            Seed(Oak, ref remaining, batch, 0, 18, stride: 4);
        }

        if (_engine.SpeciesCount(Grass) < TargetCount(0.08) || tick < 12)
        {
            Seed(Grass, ref remaining, batch, 40, 110, stride: 3);
        }

        if (_engine.SpeciesCount(Rose) < 24)
        {
            Seed(Rose, ref remaining, batch, 110, 140, stride: 4);
        }

        if (_engine.SpeciesCount(Lavender) < 24)
        {
            Seed(Lavender, ref remaining, batch, 18, 40, stride: 4);
        }

        if (_engine.SpeciesCount(Sunflower) < 30)
        {
            Seed(Sunflower, ref remaining, batch, 110, 150, stride: 5);
        }

        if (_engine.IsUnlocked(CrystalCactus) && _engine.SpeciesCount(CrystalCactus) < TargetCount(0.03))
        {
            Seed(CrystalCactus, ref remaining, batch, 70, 100, stride: 4);
        }

        if (_engine.IsUnlocked(BlueMoss) && _engine.SpeciesCount(BlueMoss) < TargetCount(0.06))
        {
            Seed(BlueMoss, ref remaining, batch, 50, 90, stride: 4);
        }

        if (_engine.IsUnlocked(Crimson) && _engine.SpeciesCount(Crimson) < TargetCount(0.05))
        {
            Seed(Crimson, ref remaining, batch, 90, 110, stride: 3);
        }

        if (_engine.IsUnlocked(StoneReed) && _engine.SpeciesCount(StoneReed) < 80)
        {
            SeedSpecial(StoneReed, ref remaining, batch, rockPath: true, water: false, burnt: false);
        }

        if (_engine.IsUnlocked(MireBloom) && _engine.SpeciesCount(MireBloom) < 80)
        {
            SeedSpecial(MireBloom, ref remaining, batch, rockPath: false, water: true, burnt: false);
        }

        if (_engine.IsUnlocked(Glowcap) && _engine.SpeciesCount(Glowcap) < TargetCount(0.05))
        {
            Seed(Glowcap, ref remaining, batch, 40, 80, stride: 3);
        }

        if (_engine.IsUnlocked(SilverFern) && _engine.SpeciesCount(SilverFern) < TargetCount(0.06))
        {
            Seed(SilverFern, ref remaining, batch, 20, 50, stride: 4);
        }

        if (_engine.IsUnlocked(PurpleCanopy) && _engine.SpeciesCount(PurpleCanopy) < 12)
        {
            Seed(PurpleCanopy, ref remaining, batch, 0, 22, stride: 5);
        }

        if (_engine.IsUnlocked(Whiteveil) && _engine.SpeciesCount(Whiteveil) < TargetCount(0.055))
        {
            Seed(Whiteveil, ref remaining, batch, 40, 70, stride: 3);
        }

        if (_engine.IsUnlocked(Moonpetal) && _engine.SpeciesCount(Moonpetal) < TargetCount(0.04))
        {
            Seed(Moonpetal, ref remaining, batch, 0, 24, stride: 3);
        }

        if (_engine.IsUnlocked(Orange) && _engine.SpeciesCount(Orange) < TargetCount(0.03))
        {
            Seed(Orange, ref remaining, batch, 100, 130, stride: 4);
        }

        if (_engine.IsUnlocked(Emberroot) && _engine.SpeciesCount(Emberroot) < 48)
        {
            Seed(Emberroot, ref remaining, batch, 24, 60, stride: 4);
        }

        if (_engine.IsUnlocked(Ashroot) && _engine.SpeciesCount(Ashroot) < TargetCount(0.045))
        {
            SeedSpecial(Ashroot, ref remaining, batch, rockPath: false, water: false, burnt: true);
        }

        if (_engine.IsUnlocked(Phoenix) && _engine.SpeciesCount(Phoenix) < TargetCount(0.03))
        {
            Seed(Phoenix, ref remaining, batch, 24, 70, stride: 4);
        }

        if (_engine.IsUnlocked(Razorgrass) && _engine.SpeciesCount(Razorgrass) < TargetCount(0.03))
        {
            Seed(Razorgrass, ref remaining, batch, 80, 110, stride: 4);
        }

        if (_engine.IsUnlocked(Skyvine) && _engine.SpeciesCount(Skyvine) < 40)
        {
            Seed(Skyvine, ref remaining, batch, 60, 90, stride: 4);
        }

        if (_engine.IsUnlocked(Ironthorn) && _engine.SpeciesCount(Ironthorn) < TargetCount(0.05))
        {
            Seed(Ironthorn, ref remaining, batch, 110, 140, stride: 4);
        }

        if (_engine.IsUnlocked(AmberFern) && _engine.SpeciesCount(AmberFern) < TargetCount(0.03))
        {
            Seed(AmberFern, ref remaining, batch, 20, 50, stride: 5);
        }

        if (_engine.IsUnlocked(GhostOrchid) && _engine.SpeciesCount(GhostOrchid) < TargetCount(0.02))
        {
            Seed(GhostOrchid, ref remaining, batch, 0, 24, stride: 4);
        }

        if (remaining > 0 && tick < 40)
        {
            Seed(Grass, ref remaining, batch, 40, 120, stride: 2);
        }
    }

    private void FillAssemblyPhase(int tick, List<L3Planting> batch)
    {
        byte[] palette = BuildPalette();
        if (palette.Length == 0) return;

        int remaining = 20;
        int total = Math.Max(1, _engine.OccupiedCount);
        int target = Math.Max(1, total / palette.Length + TargetCount(0.01) / palette.Length);

        var order = palette.OrderBy(p => _engine.SpeciesCount(p)).ToArray();
        foreach (byte plant in order)
        {
            if (remaining <= 0) break;
            int want = Math.Max(1, target - _engine.SpeciesCount(plant) / 8);
            want = Math.Min(want, remaining);
            int planted;
            if (plant == StoneReed)
            {
                planted = SeedSpecial(plant, ref remaining, batch, true, false, false);
            }
            else if (plant == MireBloom)
            {
                planted = SeedSpecial(plant, ref remaining, batch, false, true, false);
            }
            else if (plant == Ashroot)
            {
                planted = SeedSpecial(plant, ref remaining, batch, false, false, true);
            }
            else
            {
                int zone0 = ZoneLo(plant);
                int zone1 = ZoneHi(plant);
                planted = Seed(plant, ref remaining, batch, zone0, zone1, stride: 1);
            }

            _ = planted;
            _ = want;
        }

        while (remaining > 0)
        {
            byte plant = order[tick % order.Length];
            int before = remaining;
            Seed(plant, ref remaining, batch, 0, _engine.Cols, stride: 1);
            if (remaining == before) break;
        }
    }

    private byte[] BuildPalette()
    {
        byte[] candidates =
        [
            Grass, Rose, Sunflower, Lavender, Oak, BlueMoss, Crimson, SilverFern, Glowcap,
            PurpleCanopy, StoneReed, CrystalCactus, MireBloom, Emberroot, Whiteveil,
            Moonpetal, Orange, Ashroot, Phoenix, Razorgrass, Skyvine, Ironthorn, AmberFern, GhostOrchid
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
        Oak or PurpleCanopy or Moonpetal or GhostOrchid => 0,
        Lavender or SilverFern or AmberFern or Emberroot => 18,
        Grass or BlueMoss or Glowcap or Whiteveil or CrystalCactus => 40,
        Crimson or Skyvine => 88,
        _ => 110
    };

    private int ZoneHi(byte plant) => plant switch
    {
        Oak or PurpleCanopy or Moonpetal or GhostOrchid => 24,
        Lavender or SilverFern or AmberFern or Emberroot => 50,
        Grass or BlueMoss or Glowcap or Whiteveil or CrystalCactus => 90,
        Crimson or Skyvine => 112,
        _ => _engine.Cols
    };

    private int TargetCount(double coverage) => Math.Max(1, (int)(_engine.TotalCells * coverage));

    private int Seed(byte plant, ref int remaining, List<L3Planting> batch, int colLo, int colHi, int stride)
    {
        if (remaining <= 0 || !_engine.IsUnlocked(plant)) return 0;
        int planted = 0;
        int guard = 0;
        while (remaining > 0 && planted < 20 && guard++ < _engine.TotalCells)
        {
            int idx = _engine.FindEmpty(plant, colLo, colHi);
            if (idx < 0) break;
            int r = idx / _engine.Cols;
            int c = idx - r * _engine.Cols;
            if (stride > 1 && ((r + c) % stride != 0) && planted > 0)
            {
                continue;
            }

            batch.Add(new L3Planting(_engine.Tick, r, c, plant));
            remaining--;
            planted++;
        }

        return planted;
    }

    private int SeedSpecial(byte plant, ref int remaining, List<L3Planting> batch, bool rockPath, bool water, bool burnt)
    {
        if (remaining <= 0 || !_engine.IsUnlocked(plant)) return 0;
        int planted = 0;
        int idx = 0;
        while (remaining > 0 && planted < 8 && idx < _engine.TotalCells)
        {
            int found = _engine.FindEmpty(plant, 0, _engine.Cols, (r, c) =>
            {
                if (rockPath && !_engine.AdjacentToPathOrStone(r, c)) return false;
                if (water && !_engine.AdjacentToTerrain(r, c, (byte)L3Terrain.Water)) return false;
                if (burnt && !_engine.Cell(r, c).Burnt) return false;
                return true;
            });
            if (found < 0) break;
            int r = found / _engine.Cols;
            int c = found - r * _engine.Cols;
            batch.Add(new L3Planting(_engine.Tick, r, c, plant));
            remaining--;
            planted++;
            idx++;
        }

        return planted;
    }
}