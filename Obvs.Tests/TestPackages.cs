using System;
using System.IO;
using Xunit;

namespace Obvs.Tests
{
    // Each package declares its public API in PublicAPI.Shipped.txt and PublicAPI.Unshipped.txt, which the public API
    // analyzer checks the code against, so a change to what apps can call fails the build until it's declared
    public class TestPackages
    {
        private static readonly string Root = FindRoot();

        [Theory]
        [InlineData("Obvs")]
        [InlineData("Obvs.ActiveMQ")]
        [InlineData("Obvs.AmazonSqs")]
        [InlineData("Obvs.AzureSB")]
        [InlineData("Obvs.Kafka")]
        [InlineData("Obvs.Mcp")]
        [InlineData("Obvs.NATS")]
        [InlineData("Obvs.NetMQ")]
        [InlineData("Obvs.RabbitMQ")]
        [InlineData("Obvs.Serialization.Json")]
        [InlineData("Obvs.Serialization.MessagePack")]
        [InlineData("Obvs.Serialization.ProtoBuf")]
        [InlineData("Obvs.Testing")]
        public void ShouldDeclareThePublicApiOfEveryPackage(string package)
        {
            Assert.True(File.Exists(Path.Combine(Root, package, "PublicAPI.Shipped.txt")), $"{package} has no PublicAPI.Shipped.txt");
            Assert.True(File.Exists(Path.Combine(Root, package, "PublicAPI.Unshipped.txt")), $"{package} has no PublicAPI.Unshipped.txt");
        }

        [Fact]
        public void ShouldNotDependOnSystemReflectionEmitLightweight()
        {
            // Obvs.ActiveMQ took it for code it no longer has; every framework Obvs targets has what's left of it
            Assert.DoesNotContain("System.Reflection.Emit.Lightweight", File.ReadAllText(Path.Combine(Root, "Obvs.ActiveMQ", "Obvs.ActiveMQ.csproj")));
            Assert.DoesNotContain("System.Reflection.Emit.Lightweight", File.ReadAllText(Path.Combine(Root, "Directory.Packages.props")));
        }

        private static string FindRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Obvs.sln")))
            {
                directory = directory.Parent;
            }
            return directory?.FullName ?? throw new InvalidOperationException("Couldn't find the repo's root, with Obvs.sln, above " + AppContext.BaseDirectory);
        }
    }
}
