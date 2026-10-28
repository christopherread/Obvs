using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.Configuration;
using Obvs.Serialization.SystemTextJson;
using Obvs.Types;

namespace Obvs.Hosting
{
    /// <summary>
    /// The bus's services as an <see href="https://www.asyncapi.com/">AsyncAPI</see> 3.0 document: a channel for each
    /// service's commands, events, requests and responses, what this app sends and receives on each, and a JSON Schema
    /// for each message. Resolve it from the container. Commit the document and check it in CI to catch breaking
    /// changes: see <c>ObvsContractSnapshot</c> in Obvs.Testing, and <see cref="ObvsContractComparer"/>.
    /// </summary>
    public sealed class ObvsContracts
    {
        private static readonly (Type Kind, string Channel, bool ServerReceives)[] Kinds =
        {
            (typeof(ICommand), "commands", true), (typeof(IEvent), "events", false),
            (typeof(IRequest), "requests", true), (typeof(IResponse), "responses", false),
        };

        private static readonly JsonSerializerOptions Indented = new JsonSerializerOptions { WriteIndented = true };

        private readonly ObvsBuilder _builder;
        private readonly IServiceProvider _serviceProvider;

        internal ObvsContracts(ObvsBuilder builder, IServiceProvider serviceProvider)
        {
            _builder = builder;
            _serviceProvider = serviceProvider;
        }

        /// <summary>The names of the bus's services.</summary>
        public IReadOnlyList<string> ServiceNames => _builder.ServiceBuilders.Select(service => service.Name).ToArray();

        /// <summary>The AsyncAPI document for all of the bus's services, as indented JSON.</summary>
        public string Describe()
        {
            return Describe(_builder.ServiceBuilders);
        }

        /// <summary>The AsyncAPI document for one of the bus's services, as indented JSON.</summary>
        public string Describe(string serviceName)
        {
            var service = _builder.ServiceBuilders.FirstOrDefault(s => s.Name == serviceName)
                          ?? throw new ArgumentException($"The bus has no service '{serviceName}'. Its services are: {string.Join(", ", ServiceNames)}.", nameof(serviceName));
            return Describe(new[] { service });
        }

        /// <summary>The message types of a service: those it registers with AddMessages, or else those found by scanning.</summary>
        public IReadOnlyList<Type> MessageTypes(string serviceName)
        {
            var service = _builder.ServiceBuilders.FirstOrDefault(s => s.Name == serviceName)
                          ?? throw new ArgumentException($"The bus has no service '{serviceName}'.", nameof(serviceName));
            return MessagesOf(service);
        }

        private string Describe(IEnumerable<ObvsServiceBuilder> services)
        {
            var channels = new JsonObject();
            var operations = new JsonObject();
            var messages = new JsonObject();
            var schemaOptions = new JsonSerializerOptions(SystemTextJsonMessageDefaults.Options) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

            foreach (var service in services)
            {
                var serviceMessages = MessagesOf(service);
                var responsesId = Id(service.Name) + ".responses";
                foreach (var (kind, channelName, serverReceives) in Kinds)
                {
                    var ofKind = serviceMessages.Where(kind.IsAssignableFrom).OrderBy(type => type.Name, StringComparer.Ordinal).ToArray();
                    if (ofKind.Length == 0)
                    {
                        continue;
                    }

                    var channelId = Id(service.Name) + "." + channelName;
                    var channelMessages = new JsonObject();
                    var operationMessages = new JsonArray();
                    var replyMessages = new JsonArray();
                    foreach (var type in ofKind)
                    {
                        var messageId = Id(type.FullName ?? type.Name);
                        var message = messages[messageId] = new JsonObject
                        {
                            ["name"] = type.Name,
                            ["title"] = type.Name,
                            ["contentType"] = "application/json",
                            ["payload"] = JsonSchemaExporter.GetJsonSchemaAsNode(schemaOptions, type),
                        };
                        channelMessages[type.Name] = new JsonObject { ["$ref"] = "#/components/messages/" + messageId };
                        operationMessages.Add(new JsonObject { ["$ref"] = $"#/channels/{channelId}/messages/{type.Name}" });

                        // A typed request names its response, and the request operations reply with it if it's one of
                        // this service's responses
                        var response = ResponseOf(type);
                        if (response != null)
                        {
                            message["x-obvs-response"] = response.Name;
                            var reply = $"#/channels/{responsesId}/messages/{response.Name}";
                            if (serviceMessages.Contains(response) && !replyMessages.Any(m => (string)m["$ref"] == reply))
                            {
                                replyMessages.Add(new JsonObject { ["$ref"] = reply });
                            }
                        }
                    }

                    channels[channelId] = new JsonObject
                    {
                        ["address"] = null,
                        ["title"] = $"{service.Name} {channelName}",
                        ["messages"] = channelMessages,
                        ["x-obvs-service"] = service.Name,
                        ["x-obvs-kind"] = channelName,
                        ["x-obvs-transport"] = service.TransportName,
                    };

                    // What this app does on the channel: a server receives commands and requests, and sends events and
                    // responses; a client does the opposite
                    foreach (var (role, receives) in new[] { (service.IsServer, serverReceives), (service.IsClient, !serverReceives) })
                    {
                        if (!role)
                        {
                            continue;
                        }
                        var action = receives ? "receive" : "send";
                        var operation = operations[$"{channelId}.{action}"] = new JsonObject
                        {
                            ["action"] = action,
                            ["channel"] = new JsonObject { ["$ref"] = "#/channels/" + channelId },
                            ["messages"] = operationMessages.DeepClone(),
                        };
                        if (replyMessages.Count > 0)
                        {
                            operation["reply"] = new JsonObject
                            {
                                ["channel"] = new JsonObject { ["$ref"] = "#/channels/" + responsesId },
                                ["messages"] = replyMessages.DeepClone(),
                            };
                        }
                    }
                }
            }

            var document = new JsonObject
            {
                ["asyncapi"] = "3.0.0",
                ["info"] = new JsonObject { ["title"] = ApplicationName(), ["version"] = ApplicationVersion() },
                ["channels"] = channels,
                ["operations"] = operations,
                ["components"] = new JsonObject { ["messages"] = messages },
            };
            return document.ToJsonString(Indented);
        }

        private static IReadOnlyList<Type> MessagesOf(ObvsServiceBuilder service)
        {
            if (service.Messages.Count > 0)
            {
                return service.Messages.ToArray();
            }
            try
            {
                var scan = typeof(Configuration.MessageTypes).GetMethod(nameof(Configuration.MessageTypes.Get))
                    .MakeGenericMethod(typeof(IMessage), service.ServiceMessageType);
                return ((IEnumerable<Type>)scan.Invoke(null, new object[] { null, null })).ToArray();
            }
            catch (TargetInvocationException)
            {
                // Scanning failed, as it does on a type that isn't public: the bus reports that when it starts
                return Array.Empty<Type>();
            }
        }

        // The response a request declares with IRequest<TResponse>, or null
        private static Type ResponseOf(Type request)
        {
            return request.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))
                .Select(i => i.GetGenericArguments()[0])
                .OrderBy(type => type.Name, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private string ApplicationName()
        {
            return _serviceProvider.GetService<IHostEnvironment>()?.ApplicationName ?? Assembly.GetEntryAssembly()?.GetName().Name ?? "Obvs";
        }

        private static string ApplicationVersion()
        {
            return Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";
        }

        // AsyncAPI's ids allow letters, digits, '.', '-' and '_'
        private static string Id(string name)
        {
            var id = new StringBuilder(name.Length);
            foreach (var c in name)
            {
                id.Append(char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_' ? c : '_');
            }
            return id.ToString();
        }
    }
}
