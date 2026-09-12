using Photospheria.Core.Model;
using Xunit;

namespace Photospheria.Tests;

public class CellStateTests
{
    [Fact]
    public void CellState_InitialState_HasFullNutrientsAndNoDeadMatter()
    {
        var cell = CellState.CreateDefault();

        Assert.Equal(0, cell.PlantIndex);
        Assert.Equal(0, cell.Age);
        Assert.Equal(200, cell.NutrientUnits); // 100.0 fixed-point
        Assert.Equal(100.0f, cell.Nutrients);
        Assert.False(cell.HasDeadMatter);
        Assert.False(cell.IsShaded);
        Assert.False(cell.IsOccupied);
    }

    [Fact]
    public void CellState_NutrientDepletion_ConsumesCorrectUnits()
    {
        var cell = CellState.CreateDefault();
        cell.PlantIndex = 1; // Grass

        // Occupied cell without dead matter consumes 1.0 (2 units) per tick
        cell.MetabolizeNutrients();
        Assert.Equal(198, cell.NutrientUnits);
        Assert.Equal(99.0f, cell.Nutrients);

        // Set dead matter flag
        cell.HasDeadMatter = true;
        // Occupied cell with dead matter consumes 0.5 (1 unit) per tick
        cell.MetabolizeNutrients();
        Assert.Equal(197, cell.NutrientUnits);
        Assert.Equal(98.5f, cell.Nutrients);
    }

    [Fact]
    public void CellState_DeadMatterRegeneration_RegainsNutrientsUpToMax()
    {
        var cell = CellState.CreateDefault();
        cell.NutrientUnits = 196; // 98.0
        cell.HasDeadMatter = true;
        cell.PlantIndex = 0; // Empty

        // Unoccupied cell with dead matter regains 1.0 (2 units) per tick capped at 200 (100.0)
        cell.MetabolizeNutrients();
        Assert.Equal(198, cell.NutrientUnits);

        cell.MetabolizeNutrients();
        Assert.Equal(200, cell.NutrientUnits);

        // Cap check
        cell.MetabolizeNutrients();
        Assert.Equal(200, cell.NutrientUnits);
    }

    [Fact]
    public void CellState_StarvationCull_CreatesDeadMatter()
    {
        var cell = CellState.CreateDefault();
        cell.PlantIndex = 1; // Grass
        cell.NutrientUnits = 1; // 0.5 nutrient left

        // Metabolism drops nutrient to 0
        cell.MetabolizeNutrients();
        Assert.Equal(0, cell.NutrientUnits);
        Assert.True(cell.CheckAndCullStarved());
        Assert.Equal(0, cell.PlantIndex);
        Assert.True(cell.HasDeadMatter);
    }
}
