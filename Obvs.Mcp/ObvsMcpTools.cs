using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Obvs.Hosting;
using Obvs.Serialization.SystemTextJson;
using Obvs.Types;

namespace Obvs.Mcp
{
    [McpServerToolType]
    internal sealed class ObvsMcpInspectionTools
    {
        private static readonly JsonSerializerOptions Indented = new JsonSerializerOptions { WriteIndented = true };

        private readonly ObvsContracts _contracts;
        private readonly ObvsMcpMessages _messages;
        private readonly ObvsMcpErrors _errors;

        public ObvsMcpInspectionTools(ObvsContracts contracts, ObvsMcpMessages messages, ObvsMcpErrors errors)
        {
            _contracts = contracts;
            _messages = messages;
            _errors = errors;
        }

        [McpServerTool(Name = "list_services", ReadOnly = true), Description("Lists the bus's services: each one's transport, whether this app is its client or server, and its commands, events, requests and responses.")]
        public string ListServices()
        {
            var document = JsonNode.Parse(_contracts.Describe());
            var services = new JsonArray();
            foreach (var name in _contracts.ServiceNames)
            {
                var service = new JsonObject { ["name"] = name };
                foreach (var kind in new[] { "commands", "events", "requests", "responses" })
                {
                    var channel = document["channels"]?[$"{name}.{kind}"];
                    service["transport"] ??= (string)channel?["x-obvs-transport"];
                    service[kind] = new JsonArray((channel?["messages"] as JsonObject ?? new JsonObject()).Select(message => (JsonNode)message.Key).ToArray());
                }
                var operations = (document["operations"] as JsonObject ?? new JsonObject())
                    .Where(operation => operation.Key.StartsWith(name + ".", StringComparison.Ordinal))
                    .ToDictionary(operation => operation.Key, operation => (string)operation.Value?["action"]);
                service["server"] = operations.Any(o => o.Key.EndsWith(".commands.receive") || o.Key.EndsWith(".requests.receive"));
                service["client"] = operations.Any(o => o.Key.EndsWith(".commands.send") || o.Key.EndsWith(".requests.send"));
                services.Add(service);
            }
            return services.ToJsonString(Indented);
        }

        [McpServerTool(Name = "describe_service", ReadOnly = true), Description("Returns a service's contract: an AsyncAPI 3.0 document with a JSON Schema for each of its messages.")]
        public string DescribeService([Description("The service's name, from list_services")] string service)
        {
            try
            {
                return _contracts.Describe(service);
            }
            catch (ArgumentException exception)
            {
                throw new McpException(exception.Message);
            }
        }

        [McpServerTool(Name = "watch", ReadOnly = true), Description("Returns the bus's recent messages that match, waiting for the first if none has arrived yet: commands and requests this app receives as a server, and events as a client.")]
        public async Task<string> WatchAsync(
            [Description("Only this service's messages")] string service = null,
            [Description("Only messages of this type, such as OrderPlaced")] string messageType = null,
            [Description("How far back to look, in seconds. 60 by default.")] int sinceSeconds = 60,
            [Description("How long to wait for a match, in seconds. 5 by default.")] int waitSeconds = 5,
            [Description("The most messages to return. 20 by default.")] int max = 20,
            CancellationToken cancellationToken = default)
        {
            var found = await _messages.WatchAsync(
                message => (service == null || message.Service == service) && (messageType == null || message.Type == messageType),
                DateTimeOffset.UtcNow.AddSeconds(-sinceSeconds), TimeSpan.FromSeconds(waitSeconds), max, cancellationToken);
            return new JsonArray(found.Select(message => (JsonNode)new JsonObject
            {
                ["time"] = message.Time,
                ["service"] = message.Service,
                ["type"] = message.Type,
                ["message"] = message.Message?.DeepClone(),
            }).ToArray()).ToJsonString(Indented);
        }

        [McpServerTool(Name = "recent_errors", ReadOnly = true), Description("Returns recent errors, newest first: handlers that failed, and errors transports reported, with their trace IDs.")]
        public string RecentErrors([Description("The most errors to return. 20 by default.")] int max = 20)
        {
            return new JsonArray(_errors.Recent(max).Select(error => (JsonNode)new JsonObject
            {
                ["time"] = error.Time,
                ["category"] = error.Category,
                ["message"] = error.Message,
                ["error"] = error.Error,
                ["traceId"] = error.TraceId,
            }).ToArray()).ToJsonString(Indented);
        }
    }

    [McpServerToolType]
    internal sealed class ObvsMcpSendingTools
    {
        private static readonly JsonSerializerOptions Indented = new JsonSerializerOptions { WriteIndented = true };

        private readonly ObvsContracts _contracts;
        private readonly IServiceBus _bus;

        public ObvsMcpSendingTools(ObvsContracts contracts, IServiceBus bus)
        {
            _contracts = contracts;
            _bus = bus;
        }

        [McpServerTool(Name = "send_command", Destructive = true), Description("Sends a command, given as JSON matching its schema from describe_service.")]
        public async Task<string> SendCommandAsync(
            [Description("The command's type, such as PlaceOrder")] string messageType,
            [Description("The command as JSON")] JsonElement message,
            CancellationToken cancellationToken = default)
        {
            var (command, service) = Create<ICommand>(messageType, message);
            await _bus.SendAsync(command, cancellationToken);
            return $"Sent {messageType} to service '{service}'.";
        }

        [McpServerTool(Name = "publish_event", Destructive = true), Description("Publishes an event, given as JSON matching its schema from describe_service.")]
        public async Task<string> PublishEventAsync(
            [Description("The event's type, such as OrderPlaced")] string messageType,
            [Description("The event as JSON")] JsonElement message,
            CancellationToken cancellationToken = default)
        {
            var (ev, service) = Create<IEvent>(messageType, message);
            await _bus.PublishAsync(ev, cancellationToken);
            return $"Published {messageType} on service '{service}'.";
        }

        [McpServerTool(Name = "request", Destructive = true), Description("Sends a request, given as JSON matching its schema from describe_service, and returns the first response.")]
        public async Task<string> RequestAsync(
            [Description("The request's type, such as GetQuote")] string messageType,
            [Description("The request as JSON")] JsonElement message,
            [Description("How long to wait for the response, in seconds. 10 by default.")] int timeoutSeconds = 10,
            CancellationToken cancellationToken = default)
        {
            var (request, service) = Create<IRequest>(messageType, message);
            IResponse response;
            try
            {
                response = await _bus.GetResponses(request).Timeout(TimeSpan.FromSeconds(timeoutSeconds)).FirstAsync().ToTask(cancellationToken);
            }
            catch (TimeoutException)
            {
                throw new McpException($"No response to {messageType} on service '{service}' within {timeoutSeconds} seconds. Check that a server for the service is running, and recent_errors.");
            }
            return new JsonObject
            {
                ["type"] = response.GetType().Name,
                ["message"] = JsonSerializer.SerializeToNode(response, response.GetType(), SystemTextJsonMessageDefaults.Options),
            }.ToJsonString(Indented);
        }

        // The message, validated by deserializing it as its type, and the service it belongs to
        private (T Message, string Service) Create<T>(string messageType, JsonElement json) where T : class, IMessage
        {
            var candidates = _contracts.ServiceNames
                .SelectMany(service => _contracts.MessageTypes(service).Select(type => (type, service)))
                .Where(pair => typeof(T).IsAssignableFrom(pair.type))
                .ToArray();
            var matches = candidates.Where(pair => pair.type.Name == messageType || pair.type.FullName == messageType).ToArray();
            var kind = typeof(T).Name.Substring(1).ToLowerInvariant();
            if (matches.Length == 0)
            {
                throw new McpException($"The bus has no {kind} {messageType}. Its {kind}s are: {string.Join(", ", candidates.Select(pair => pair.type.Name).Distinct())}.");
            }
            if (matches.Length > 1)
            {
                throw new McpException($"More than one {kind} is called {messageType}: {string.Join(", ", matches.Select(pair => pair.type.FullName))}. Give its full name.");
            }

            var (type, owner) = matches[0];
            try
            {
                var message = (T)JsonSerializer.Deserialize(json.GetRawText(), type, SystemTextJsonMessageDefaults.Options);
                return (message ?? throw new McpException($"The {messageType} given was null."), owner);
            }
            catch (JsonException exception)
            {
                throw new McpException($"That isn't a {messageType}: {exception.Message} Its schema is in describe_service(\"{owner}\").");
            }
        }
    }
}
