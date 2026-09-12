using System.Collections.Generic;
using System.Text.Json;
using Photospheria.Core.Model;
using Photospheria.Serialization.Schema;
using Photospheria.Serialization.Validation;
using Xunit;

namespace Photospheria.Tests;

public class SerializationAndSafeguardsTests
{
    [Fact]
    public void InvariantValidator_ValidPlan_ReturnsSuccess()
    {
        var config = new SimulationConfig { Width = 100, Height = 100, TotalTicks = 200 };
        var actions = new List<PlantingAction>
        {
            new PlantingAction(0, 10, 10, Level1Catalogue.GrassIndex),
            new PlantingAction(0, 10, 11, Level1Catalogue.RoseBushIndex),
            new PlantingAction(50, 50, 50, Level1Catalogue.OakTreeIndex)
        };

        var result = InvariantValidator.Validate(actions, config);
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void InvariantValidator_Exceeds20ActionsPerTick_Fails()
    {
        var config = new SimulationConfig { Width = 100, Height = 100, TotalTicks = 200 };
        var actions = new List<PlantingAction>();
        for (int i = 0; i < 25; i++)
        {
            actions.Add(new PlantingAction(0, i, 0, Level1Catalogue.GrassIndex));
        }

        var result = InvariantValidator.Validate(actions, config);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("exceeds limit of 20"));
    }

    [Fact]
    public void InvariantValidator_DisallowedSpecies_Fails()
    {
        var config = new SimulationConfig { Width = 100, Height = 100, TotalTicks = 200 };
        var actions = new List<PlantingAction>
        {
            new PlantingAction(0, 0, 0, 3) // Blue Moss (index 3) is locked in Level 1!
        };

        var result = InvariantValidator.Validate(actions, config);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("whitelist"));
    }

    [Fact]
    public void InvariantValidator_OutOfBoundsCoordinates_Fails()
    {
        var config = new SimulationConfig { Width = 100, Height = 100, TotalTicks = 200 };
        var actions = new List<PlantingAction>
        {
            new PlantingAction(0, 100, 50, Level1Catalogue.GrassIndex) // Row 100 is out of bounds (0-99)
        };

        var result = InvariantValidator.Validate(actions, config);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("row", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AdaptiveActionSerializer_DualMode_SerializesBothIndexFields()
    {
        var actions = new List<PlantingAction>
        {
            new PlantingAction(1, 10, 20, Level1Catalogue.LavenderIndex)
        };

        string json = AdaptiveActionSerializer.Serialize(actions, SerializationKeyMode.Both);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("actions", out var actionsProp));
        var tickEntry = actionsProp[0];
        Assert.Equal(1, tickEntry.GetProperty("tick").GetInt32());
        var plantEntry = tickEntry.GetProperty("plants")[0];

        Assert.Equal(6, plantEntry.GetProperty("index").GetInt32());
        Assert.Equal(6, plantEntry.GetProperty("plant_index").GetInt32());
        Assert.Equal(10, plantEntry.GetProperty("row").GetInt32());
        Assert.Equal(20, plantEntry.GetProperty("col").GetInt32());
    }

    [Fact]
    public void AdaptiveActionSerializer_SchemaDetection_SelectsCorrectField()
    {
        // Schema specifying "index"
        string schemaWithIndex = """
        {
            "$schema": "http://json-schema.org/draft-07/schema#",
            "properties": {
                "actions": {
                    "items": {
                        "properties": {
                            "plants": {
                                "items": {
                                    "required": ["index", "row", "col"]
                                }
                            }
                        }
                    }
                }
            }
        }
        """;

        var mode = AdaptiveActionSerializer.DetectKeyModeFromSchema(schemaWithIndex);
        Assert.Equal(SerializationKeyMode.IndexOnly, mode);
    }
}
