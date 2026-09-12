using System.Runtime.InteropServices;

namespace Photospheria.Core.Model;

/// <summary>
/// Unmanaged 8-byte flat cell state struct representing a discrete terrain cell in Photospheria.
/// Designed for high-speed cache efficiency and zero-allocation double-buffering.
/// Nutrients use fixed-point integer scaling: 1 unit = 0.5 nutrient points (100.0 = 200 units).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CellState
{
    public const ushort MaxNutrientUnits = 200; // 100.0 points
    public const ushort StandardNutrientConsumption = 2; // 1.0 point
    public const ushort DeadMatterNutrientConsumption = 1; // 0.5 points
    public const ushort DeadMatterRegenerationRate = 2; // 1.0 point

    public byte PlantIndex;      // 0 = Empty, 1 = Grass, 2 = Rose Bush, 5 = Sunflower, 6 = Lavender, 12 = Oak Tree
    public byte SoilType;        // 0 = Dirt, 1 = Mud, 2 = Clay, 3 = Burnt
    public ushort Age;           // Ticks since plant was placed or spawned
    public ushort NutrientUnits; // Fixed-point: 200 = 100.0 points (1 unit = 0.5 points)
    public CellFlags Flags;      // HasDeadMatter, IsShaded
    public byte TerrainType;     // 0 = Soil, 1 = Non-soil/Path, 2 = Non-soil/Stone

    public readonly bool IsOccupied => PlantIndex != 0;
    public readonly bool IsHabitableSoil => TerrainType == 0 && (SoilType == 0 || SoilType == 1);

    public bool HasDeadMatter
    {
        readonly get => (Flags & CellFlags.HasDeadMatter) != 0;
        set
        {
            if (value) Flags |= CellFlags.HasDeadMatter;
            else Flags &= ~CellFlags.HasDeadMatter;
        }
    }

    public bool IsShaded
    {
        readonly get => (Flags & CellFlags.IsShaded) != 0;
        set
        {
            if (value) Flags |= CellFlags.IsShaded;
            else Flags &= ~CellFlags.IsShaded;
        }
    }

    public readonly float Nutrients => NutrientUnits * 0.5f;

    public static CellState CreateDefault(byte soilType = 0, byte terrainType = 0)
    {
        return new CellState
        {
            PlantIndex = 0,
            SoilType = soilType,
            TerrainType = terrainType,
            Age = 0,
            NutrientUnits = MaxNutrientUnits,
            Flags = CellFlags.None
        };
    }

    /// <summary>
    /// Updates nutrient level for this tick.
    /// Occupied without dead matter: -1.0 (2 units).
    /// Occupied with dead matter: -0.5 (1 unit).
    /// Empty with dead matter: +1.0 (2 units) up to 100.0 (200 units).
    /// </summary>
    public void MetabolizeNutrients()
    {
        if (IsOccupied)
        {
            ushort consumption = HasDeadMatter ? DeadMatterNutrientConsumption : StandardNutrientConsumption;
            if (NutrientUnits > consumption)
            {
                NutrientUnits -= consumption;
            }
            else
            {
                NutrientUnits = 0;
            }
        }
        else if (HasDeadMatter)
        {
            NutrientUnits = (ushort)Math.Min(MaxNutrientUnits, NutrientUnits + DeadMatterRegenerationRate);
        }
    }

    /// <summary>
    /// Checks for starvation at 0 nutrients. If starved, culls plant and sets dead matter flag.
    /// Returns true if the plant died this tick.
    /// </summary>
    public bool CheckAndCullStarved()
    {
        if (IsOccupied && NutrientUnits == 0)
        {
            PlantIndex = 0;
            Age = 0;
            HasDeadMatter = true;
            return true;
        }
        return false;
    }
}
