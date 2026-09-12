using System.Collections.Frozen;
using System.Collections.Generic;

namespace Photospheria.Core.Model;

public static class Level1Catalogue
{
    public const byte GrassIndex = 1;
    public const byte RoseBushIndex = 2;
    public const byte SunflowerIndex = 5;
    public const byte LavenderIndex = 6;
    public const byte OakTreeIndex = 12;

    public static readonly PlantDefinition Grass = new(
        Index: GrassIndex,
        Name: "Grass",
        TimeToMaturity: 1,
        SpreadRate: 2,
        SpreadType: SpreadType.VonNeumann,
        SpreadRange: 1,
        InvasivenessRank: 1,
        Weaknesses: PlantWeaknesses.NoShadeSurvival
    );

    public static readonly PlantDefinition RoseBush = new(
        Index: RoseBushIndex,
        Name: "Rose Bush",
        TimeToMaturity: 10,
        SpreadRate: 2,
        SpreadType: SpreadType.Row,
        SpreadRange: 1,
        InvasivenessRank: 2,
        Weaknesses: PlantWeaknesses.NoWinterSpread
    );

    public static readonly PlantDefinition DwarfSunflower = new(
        Index: SunflowerIndex,
        Name: "Dwarf Sunflower",
        TimeToMaturity: 5,
        SpreadRate: 4,
        SpreadType: SpreadType.CrossHatch,
        SpreadRange: 2,
        InvasivenessRank: 4,
        Weaknesses: PlantWeaknesses.NoShadeSpread,
        HasAdjacentShadePenalty: true
    );

    public static readonly PlantDefinition Lavender = new(
        Index: LavenderIndex,
        Name: "Lavender",
        TimeToMaturity: 4,
        SpreadRate: 3,
        SpreadType: SpreadType.CrossHatch,
        SpreadRange: 1,
        InvasivenessRank: 2,
        Weaknesses: PlantWeaknesses.NoWinterSpread
    );

    public static readonly PlantDefinition OakTree = new(
        Index: OakTreeIndex,
        Name: "Oak Tree",
        TimeToMaturity: 20,
        SpreadRate: 7,
        SpreadType: SpreadType.Moore,
        SpreadRange: 2,
        InvasivenessRank: 10,
        Weaknesses: PlantWeaknesses.None,
        ShadeRadius: 4
    );

    public static readonly byte[] WhitelistIndices = [GrassIndex, RoseBushIndex, SunflowerIndex, LavenderIndex, OakTreeIndex];

    private static readonly PlantDefinition[] LookupTable;

    static Level1Catalogue()
    {
        LookupTable = new PlantDefinition[16];
        LookupTable[GrassIndex] = Grass;
        LookupTable[RoseBushIndex] = RoseBush;
        LookupTable[SunflowerIndex] = DwarfSunflower;
        LookupTable[LavenderIndex] = Lavender;
        LookupTable[OakTreeIndex] = OakTree;
    }

    public static bool IsValidLevel1Plant(byte plantIndex)
    {
        return plantIndex switch
        {
            GrassIndex or RoseBushIndex or SunflowerIndex or LavenderIndex or OakTreeIndex => true,
            _ => false
        };
    }

    public static ref readonly PlantDefinition Get(byte plantIndex)
    {
        if (plantIndex < LookupTable.Length)
        {
            return ref LookupTable[plantIndex];
        }
        throw new System.ArgumentOutOfRangeException(nameof(plantIndex), $"Unknown plant index: {plantIndex}");
    }
}
