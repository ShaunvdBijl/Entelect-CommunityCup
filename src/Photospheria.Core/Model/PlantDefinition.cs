namespace Photospheria.Core.Model;

public enum SpreadType : byte
{
    None = 0,
    VonNeumann = 1,
    Moore = 2,
    Row = 3,
    Column = 4,
    CrossHatch = 5
}

[System.Flags]
public enum PlantWeaknesses : byte
{
    None = 0,
    NoShadeSurvival = 1 << 0,
    NoWinterSpread = 1 << 1,
    NoShadeSpread = 1 << 2
}

public readonly record struct PlantDefinition(
    byte Index,
    string Name,
    ushort TimeToMaturity,
    ushort SpreadRate,
    SpreadType SpreadType,
    byte SpreadRange,
    byte InvasivenessRank,
    PlantWeaknesses Weaknesses,
    byte ShadeRadius = 0,
    bool HasAdjacentShadePenalty = false
);
