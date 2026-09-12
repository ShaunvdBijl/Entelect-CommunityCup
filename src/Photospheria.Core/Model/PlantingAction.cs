namespace Photospheria.Core.Model;

public readonly record struct PlantingAction(int Tick, int Row, int Col, byte PlantIndex);
