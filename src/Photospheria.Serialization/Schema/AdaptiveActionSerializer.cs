using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Photospheria.Core.Model;

namespace Photospheria.Serialization.Schema;

public enum SerializationKeyMode
{
    Both,
    IndexOnly,
    PlantIndexOnly
}

public static class AdaptiveActionSerializer
{
    public static SerializationKeyMode DetectKeyModeFromSchema(string schemaContent)
    {
        if (string.IsNullOrWhiteSpace(schemaContent))
        {
            return SerializationKeyMode.Both;
        }

        try
        {
            using var doc = JsonDocument.Parse(schemaContent);
            string raw = schemaContent.ToLowerInvariant();

            bool hasIndex = raw.Contains("\"index\"");
            bool hasPlantIndex = raw.Contains("\"plant_index\"");

            if (hasIndex && !hasPlantIndex)
            {
                return SerializationKeyMode.IndexOnly;
            }
            if (hasPlantIndex && !hasIndex)
            {
                return SerializationKeyMode.PlantIndexOnly;
            }

            return SerializationKeyMode.Both;
        }
        catch
        {
            return SerializationKeyMode.Both;
        }
    }

    public static string Serialize(
        IEnumerable<PlantingAction> actions,
        SerializationKeyMode mode = SerializationKeyMode.Both,
        bool indented = true)
    {
        var groupedByTick = actions
            .GroupBy(a => a.Tick)
            .OrderBy(g => g.Key);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = indented }))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("actions");

            foreach (var group in groupedByTick)
            {
                writer.WriteStartObject();
                writer.WriteNumber("tick", group.Key);
                writer.WriteStartArray("plants");

                foreach (var action in group)
                {
                    writer.WriteStartObject();

                    switch (mode)
                    {
                        case SerializationKeyMode.Both:
                            writer.WriteNumber("index", action.PlantIndex);
                            writer.WriteNumber("plant_index", action.PlantIndex);
                            break;
                        case SerializationKeyMode.IndexOnly:
                            writer.WriteNumber("index", action.PlantIndex);
                            break;
                        case SerializationKeyMode.PlantIndexOnly:
                            writer.WriteNumber("plant_index", action.PlantIndex);
                            break;
                    }

                    writer.WriteNumber("row", action.Row);
                    writer.WriteNumber("col", action.Col);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray(); // plants
                writer.WriteEndObject(); // tick entry
            }

            writer.WriteEndArray(); // actions
            writer.WriteEndObject(); // root
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static void SaveToFile(
        string filePath,
        IEnumerable<PlantingAction> actions,
        string? schemaPath = null,
        bool indented = true)
    {
        SerializationKeyMode mode = SerializationKeyMode.Both;

        if (!string.IsNullOrEmpty(schemaPath) && File.Exists(schemaPath))
        {
            string schemaJson = File.ReadAllText(schemaPath);
            mode = DetectKeyModeFromSchema(schemaJson);
        }

        string json = Serialize(actions, mode, indented);
        File.WriteAllText(filePath, json, Encoding.UTF8);
    }
}
