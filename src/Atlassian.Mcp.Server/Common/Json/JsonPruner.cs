// <copyright file="JsonPruner.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json.Nodes;

namespace Atlassian.Mcp.Server.Common.Json;

/// <summary>
/// Removes what an AI model does not need from Atlassian API responses, to save context: null
/// values, empty objects and arrays, and links meant for browsers or API navigation.
/// </summary>
public static class JsonPruner
{
    /// <summary>
    /// The property names removed at every level. They hold API navigation links, avatar and icon
    /// details, internal entity IDs, display colors, user time zones, and expansion hints, none of
    /// which help a model act on the data.
    /// </summary>
    private static readonly HashSet<string> NoiseProperties = new(StringComparer.Ordinal)
    {
        "self",
        "avatarUrls",
        "avatarId",
        "iconUrl",
        "entityId",
        "colorName",
        "timeZone",
        "accountType",
        "expand",
        "_expandable",
        "_links",
    };

    /// <summary>
    /// Returns a pruned copy of <paramref name="node"/>.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The pruned copy, or <see langword="null"/> when nothing is left.</returns>
    public static JsonNode? Prune(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject source:
                var target = new JsonObject();
                foreach ((string name, JsonNode? value) in source)
                {
                    if (NoiseProperties.Contains(name))
                    {
                        continue;
                    }

                    JsonNode? pruned = Prune(value);
                    if (pruned is not null)
                    {
                        target[name] = pruned;
                    }
                }

                return target.Count == 0 ? null : target;

            case JsonArray array:
                var items = new JsonArray();
                foreach (JsonNode? item in array)
                {
                    JsonNode? pruned = Prune(item);
                    if (pruned is not null)
                    {
                        items.Add(pruned);
                    }
                }

                return items.Count == 0 ? null : items;

            case null:
                return null;

            default:
                return node.DeepClone();
        }
    }
}
