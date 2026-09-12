using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Photospheria.Core.Model;

namespace Photospheria.Core.Simulation;

public static class LevelLoader
{
    public static SimulationConfig LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Level configuration file not found: {filePath}");
        }

        string json = File.ReadAllText(filePath);
        return LoadFromJson(json);
    }

    public static SimulationConfig LoadFromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        int rows = root.GetProperty("rows").GetInt32();
        int cols = root.GetProperty("cols").GetInt32();
        int totalTicks = root.GetProperty("ticks").GetInt32();

        byte[,] terrainMap = new byte[rows, cols];
        byte[,] soilMap = new byte[rows, cols];

        if (root.TryGetProperty("cells", out var cellsElement))
        {
            foreach (var cellElement in cellsElement.EnumerateArray())
            {
                int r = cellElement.GetProperty("row").GetInt32();
                int c = cellElement.GetProperty("col").GetInt32();
                byte terrain = cellElement.GetProperty("terrain").GetByte();
                byte soil = cellElement.GetProperty("soil").GetByte();

                if (r >= 0 && r < rows && c >= 0 && c < cols)
                {
                    terrainMap[r, c] = terrain;
                    soilMap[r, c] = soil;
                }
            }
        }

        var seasonSchedule = new List<(int Tick, Season Season)>
        {
            (0, Season.Spring) // Default initial season
        };

        if (root.TryGetProperty("commands", out var commandsElement))
        {
            foreach (var cmd in commandsElement.EnumerateArray())
            {
                if (cmd.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "season")
                {
                    int tick = cmd.GetProperty("tick").GetInt32();
                    string seasonStr = cmd.GetProperty("season").GetString()!;

                    Season season = seasonStr.ToLowerInvariant() switch
                    {
                        "spring" => Season.Spring,
                        "summer" => Season.Summer,
                        "autumn" => Season.Autumn,
                        "winter" => Season.Winter,
                        _ => Season.Spring
                    };

                    seasonSchedule.Add((tick, season));
                }
            }
        }

        seasonSchedule = seasonSchedule.OrderBy(s => s.Tick).ToList();

        Season GetSeasonForTick(int tick)
        {
            Season current = Season.Spring;
            foreach (var entry in seasonSchedule)
            {
                if (entry.Tick <= tick)
                {
                    current = entry.Season;
                }
                else
                {
                    break;
                }
            }
            return current;
        }

        return new SimulationConfig
        {
            Width = cols,
            Height = rows,
            TotalTicks = totalTicks,
            TerrainMap = terrainMap,
            SoilMap = soilMap,
            SeasonProvider = GetSeasonForTick
        };
    }
}
