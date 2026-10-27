using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Obvs.Hosting
{
    /// <summary>
    /// Finds the changes between two of <see cref="ObvsContracts"/>' documents that would break an app built against the
    /// first: a message removed from a channel, or from a message, a property removed, a property's type changed, or a
    /// property newly required. Adding messages, and optional properties, isn't breaking.
    /// </summary>
    public static class ObvsContractComparer
    {
        /// <summary>The changes from <paramref name="before"/> to <paramref name="after"/> that would break an app built against <paramref name="before"/>, each described.</summary>
        /// <param name="before">The committed document</param>
        /// <param name="after">The current document</param>
        public static IReadOnlyList<string> FindBreakingChanges(string before, string after)
        {
            var changes = new List<string>();
            var oldDocument = JsonNode.Parse(before);
            var newDocument = JsonNode.Parse(after);

            foreach (var channel in (oldDocument["channels"] as JsonObject ?? new JsonObject()))
            {
                var newChannel = newDocument["channels"]?[channel.Key];
                foreach (var message in (channel.Value?["messages"] as JsonObject ?? new JsonObject()))
                {
                    var newMessage = newChannel?["messages"]?[message.Key];
                    if (newMessage == null)
                    {
                        changes.Add($"Message {message.Key} was removed from channel {channel.Key}.");
                        continue;
                    }
                    ComparePayloads(changes, message.Key,
                        Resolve(oldDocument, message.Value)?["payload"],
                        Resolve(newDocument, newMessage)?["payload"]);
                }
            }
            return changes;
        }

        private static void ComparePayloads(List<string> changes, string path, JsonNode before, JsonNode after)
        {
            if (before == null || after == null)
            {
                return;
            }

            var oldType = before["type"]?.ToJsonString();
            var newType = after["type"]?.ToJsonString();
            if (oldType != null && newType != null && oldType != newType)
            {
                changes.Add($"{path} changed type from {oldType} to {newType}.");
                return;
            }

            var newProperties = after["properties"] as JsonObject;
            foreach (var property in (before["properties"] as JsonObject ?? new JsonObject()))
            {
                var newProperty = newProperties?[property.Key];
                if (newProperty == null)
                {
                    changes.Add($"{path}.{property.Key} was removed.");
                    continue;
                }
                ComparePayloads(changes, $"{path}.{property.Key}", property.Value, newProperty);
            }

            var wasRequired = Names(before["required"]);
            foreach (var required in Names(after["required"]).Where(name => !wasRequired.Contains(name)))
            {
                changes.Add($"{path}.{required} is now required.");
            }
        }

        private static HashSet<string> Names(JsonNode array)
        {
            return new HashSet<string>((array as JsonArray ?? new JsonArray()).Select(name => (string)name));
        }

        private static JsonNode Resolve(JsonNode document, JsonNode reference)
        {
            var path = (string)reference?["$ref"];
            if (path == null || !path.StartsWith("#/"))
            {
                return reference;
            }
            JsonNode node = document;
            foreach (var part in path.Substring(2).Split('/'))
            {
                node = node?[part];
            }
            return node;
        }
    }
}
