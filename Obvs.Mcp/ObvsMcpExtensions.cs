using System;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Obvs.Mcp;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ObvsMcpServiceCollectionExtensions
    {
        /// <summary>
        /// Adds a Model Context Protocol server for the app's bus, so an agent can list its services and their contracts,
        /// send messages, and watch what happens. Map it with <c>app.MapObvsMcp()</c>, which refuses outside the
        /// Development environment unless <see cref="ObvsMcpOptions.AllowOutsideDevelopment"/> is set.
        /// </summary>
        public static IServiceCollection AddObvsMcp(this IServiceCollection services, Action<ObvsMcpOptions> configure = null)
        {
            var options = new ObvsMcpOptions();
            configure?.Invoke(options);
            services.AddSingleton(options);

            services.AddSingleton<ObvsMcpErrors>();
            services.AddSingleton<ILoggerProvider>(serviceProvider => serviceProvider.GetRequiredService<ObvsMcpErrors>());
            services.AddSingleton<ObvsMcpMessages>();
            services.AddSingleton<IHostedService>(serviceProvider => serviceProvider.GetRequiredService<ObvsMcpMessages>());

            var mcp = services.AddMcpServer().WithHttpTransport().WithTools<ObvsMcpInspectionTools>();
            if (options.AllowSending)
            {
                mcp.WithTools<ObvsMcpSendingTools>();
            }
            return services;
        }
    }
}

namespace Microsoft.AspNetCore.Builder
{
    public static class ObvsMcpEndpointRouteBuilderExtensions
    {
        /// <summary>
        /// Serves the Obvs MCP server at <paramref name="pattern"/>. Throws outside the Development environment, unless
        /// <see cref="ObvsMcpOptions.AllowOutsideDevelopment"/> is set.
        /// </summary>
        public static IEndpointConventionBuilder MapObvsMcp(this IEndpointRouteBuilder endpoints, string pattern = "/obvs/mcp")
        {
            var options = endpoints.ServiceProvider.GetService<ObvsMcpOptions>()
                          ?? throw new InvalidOperationException("Call services.AddObvsMcp() before app.MapObvsMcp().");
            var environment = endpoints.ServiceProvider.GetRequiredService<IHostEnvironment>();
            if (!environment.IsDevelopment() && !options.AllowOutsideDevelopment)
            {
                throw new InvalidOperationException(
                    $"The Obvs MCP server runs only in the Development environment, and this is '{environment.EnvironmentName}'. " +
                    "An agent connected to it can send messages that do real work. To run it here anyway, such as in a shared test " +
                    "environment, set AllowOutsideDevelopment in AddObvsMcp(options => ...), and consider AllowSending = false.");
            }
            return endpoints.MapMcp(pattern);
        }
    }
}
