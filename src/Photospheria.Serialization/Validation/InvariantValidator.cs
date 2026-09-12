using System.Collections.Generic;
using Photospheria.Core.Model;

namespace Photospheria.Serialization.Validation;

public readonly record struct ValidationResult(bool IsValid, IReadOnlyList<string> Errors);

public static class InvariantValidator
{
    public const int MaxActionsPerTick = 20;

    public static ValidationResult Validate(IEnumerable<PlantingAction> actions, SimulationConfig config)
    {
        var errors = new List<string>();
        var countByTick = new Dictionary<int, int>();

        foreach (var action in actions)
        {
            // 1. Tick check
            if (action.Tick < 0 || action.Tick >= config.TotalTicks)
            {
                errors.Add($"Action at tick {action.Tick} is outside valid tick range [0, {config.TotalTicks - 1}].");
            }
            else
            {
                countByTick[action.Tick] = countByTick.GetValueOrDefault(action.Tick, 0) + 1;
            }

            // 2. Coordinate bounds check
            if (action.Row < 0 || action.Row >= config.Height)
            {
                errors.Add($"Action row {action.Row} is out of bounds [0, {config.Height - 1}].");
            }

            if (action.Col < 0 || action.Col >= config.Width)
            {
                errors.Add($"Action col {action.Col} is out of bounds [0, {config.Width - 1}].");
            }

            // 3. Level 1 species whitelist check
            if (!Level1Catalogue.IsValidLevel1Plant(action.PlantIndex))
            {
                errors.Add($"Plant index {action.PlantIndex} is not in the Level 1 whitelist ({string.Join(", ", Level1Catalogue.WhitelistIndices)}).");
            }
        }

        // 4. Rate limit check: max 20 plants per tick
        foreach (var (tick, count) in countByTick)
        {
            if (count > MaxActionsPerTick)
            {
                errors.Add($"Tick {tick} has {count} planting actions, which exceeds limit of {MaxActionsPerTick}.");
            }
        }

        return new ValidationResult(errors.Count == 0, errors);
    }
}
