using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.AmazonSqs;
using Obvs.Hosting;
using Obvs.Kafka;

namespace Obvs.Samples
{
    // The transports' docs. They need brokers or cloud accounts, so they're compiled, not run.
    public static class TransportSamples
    {
        public static void Kafka(HostApplicationBuilder builder)
        {
            // snippet: kafka-setup
            builder.Services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseKafka(builder.Configuration.GetSection("Obvs:Orders:Kafka"))));
            // end-snippet

            // snippet: kafka-instance-id
            builder.Services.AddObvs(obvs => obvs
                .UseInstanceId(Environment.GetEnvironmentVariable("POD_NAME"))
                .AddService<IOrderMessage>("orders", service => service
                    .UseKafka(builder.Configuration.GetSection("Obvs:Orders:Kafka"))));
            // end-snippet
        }

        public static void KafkaOAuth(HostApplicationBuilder builder, ObvsServiceBuilder service)
        {
            // snippet: kafka-oauth
            service.UseKafka(builder.Configuration.GetSection("Obvs:Orders:Kafka"), options =>
                options.OAuthBearerTokenProvider = async cancellationToken =>
                {
                    var token = await GetTokenAsync(cancellationToken);
                    return new KafkaOAuthBearerToken(token.Value, token.ExpiresOn);
                });
            // end-snippet
        }

        public static void KafkaPlatforms(HostApplicationBuilder builder, ObvsServiceBuilder service, IdentityProvider identityProvider)
        {
            // snippet: kafka-from-configuration
            service.UseKafka(builder.Configuration.GetSection("Obvs:Orders:Kafka"));
            // end-snippet

            // snippet: kafka-confluent-oauth
            service.UseKafka(builder.Configuration.GetSection("Obvs:Orders:Kafka"), options =>
                options.OAuthBearerTokenProvider = async cancellationToken =>
                {
                    // For example a client credentials grant against your identity provider
                    var token = await identityProvider.GetTokenAsync(cancellationToken);
                    return new KafkaOAuthBearerToken(token.AccessToken, token.ExpiresAt)
                    {
                        Extensions = new Dictionary<string, string>
                        {
                            ["logicalCluster"] = "lkc-abc12",
                            ["identityPoolId"] = "pool-AbCd",
                        },
                    };
                });
            // end-snippet
        }

        public static void KafkaEventHubs(HostApplicationBuilder builder, ObvsServiceBuilder service)
        {
            // snippet: kafka-event-hubs-oauth
            var credential = new DefaultAzureCredential();
            var scope = new TokenRequestContext(new[] { "https://contoso.servicebus.windows.net/.default" });

            service.UseKafka(builder.Configuration.GetSection("Obvs:Orders:Kafka"), options =>
                options.OAuthBearerTokenProvider = async cancellationToken =>
                {
                    var token = await credential.GetTokenAsync(scope, cancellationToken);
                    return new KafkaOAuthBearerToken(token.Token, token.ExpiresOn);
                });
            // end-snippet
        }

        public static void AmazonSqs(HostApplicationBuilder builder, string accessKey, string secretKey)
        {
            // snippet: sqs-setup
            builder.Services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseAmazonSqs(builder.Configuration.GetSection("Obvs:Orders:AmazonSqs"))));
            // end-snippet

            builder.Services.AddObvs(obvs => obvs.AddService<IOrderMessage>("orders", service =>
            {
                // snippet: sqs-region
                service.UseAmazonSqs(options => options.Region = "eu-west-1");
                // end-snippet

                // snippet: sqs-concurrency
                service
                    .UseAmazonSqs(builder.Configuration.GetSection("Obvs:Orders:AmazonSqs"))
                    .HandleCommand<PlaceOrder>(PlaceOrderAsync, handler => handler.MaxConcurrency = 8);
                // end-snippet

                // snippet: sqs-credentials
                service.UseAmazonSqs(builder.Configuration.GetSection("Obvs:Orders:AmazonSqs"));
                builder.Services.Configure<AmazonSqsOptions>("orders", options => options.Credentials = new BasicAWSCredentials(accessKey, secretKey));
                // end-snippet

                // snippet: sqs-localstack
                service.UseAmazonSqs(options =>
                {
                    options.Region = "us-east-1";
                    options.ServiceUrl = "http://localhost:4566";
                    options.Credentials = new BasicAWSCredentials("test", "test");
                });
                // end-snippet
            }));
        }

        public static void NetMq(HostApplicationBuilder builder)
        {
            // snippet: netmq-setup
            builder.Services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseNetMq(builder.Configuration.GetSection("Obvs:Orders:NetMq"))));
            // end-snippet

            builder.Services.AddObvs(obvs => obvs.AddService<IOrderMessage>("orders", service =>
            {
                // snippet: netmq-in-code
                service.UseNetMq(options =>
                {
                    options.Address = "tcp://orders-host";
                    options.Port = 5555;
                });
                // end-snippet
            }));
        }

        private static Task PlaceOrderAsync(PlaceOrder command, MessageContext context, CancellationToken ct) => Task.CompletedTask;

        // Stands for however you get a token
        private static Task<(string Value, DateTimeOffset ExpiresOn)> GetTokenAsync(CancellationToken ct) => Task.FromResult(("token", DateTimeOffset.UtcNow.AddHours(1)));

        // Stands for your identity provider's client
        public abstract class IdentityProvider
        {
            public abstract Task<(string AccessToken, DateTimeOffset ExpiresAt)> GetTokenAsync(CancellationToken cancellationToken);
        }
    }
}
