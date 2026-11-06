using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Obvs.Tests
{
    // Each package declares its public API in PublicAPI.Shipped.txt and PublicAPI.Unshipped.txt, which the public API
    // analyzer checks the code against, so a change to what apps can call fails the build until it's declared
    public class TestPackages
    {
        private static readonly string Root = FindRoot();

        public static TheoryData<string> Packages => new TheoryData<string>(
            "Obvs", "Obvs.ActiveMQ", "Obvs.AmazonSqs", "Obvs.AzureSB", "Obvs.Kafka", "Obvs.Mcp", "Obvs.NATS", "Obvs.NetMQ", "Obvs.RabbitMQ",
            "Obvs.Serialization.Json", "Obvs.Serialization.MessagePack", "Obvs.Serialization.ProtoBuf", "Obvs.Testing");

        [Theory]
        [MemberData(nameof(Packages))]
        public void ShouldDeclareThePublicApiOfEveryPackage(string package)
        {
            Assert.True(File.Exists(Path.Combine(Root, package, "PublicAPI.Shipped.txt")), $"{package} has no PublicAPI.Shipped.txt");
            Assert.True(File.Exists(Path.Combine(Root, package, "PublicAPI.Unshipped.txt")), $"{package} has no PublicAPI.Unshipped.txt");
        }

        // nuget.org shows the README the package ships, from its folder
        [Theory]
        [MemberData(nameof(Packages))]
        public void ShouldShipAReadme(string package)
        {
            Assert.True(File.Exists(Path.Combine(Root, package, "README.md")), $"{package} has no README.md");
            var props = File.ReadAllText(Path.Combine(Root, "Directory.Build.props"));
            Assert.Contains("<PackageReadmeFile>README.md</PackageReadmeFile>", props);
        }

        // nuget.org can't follow a link relative to the repo
        [Theory]
        [MemberData(nameof(Packages))]
        public void ShouldLinkToAbsoluteUrlsInItsReadme(string package)
        {
            var readme = Path.Combine(Root, package, "README.md");
            Assert.True(File.Exists(readme), $"{package} has no README.md");
            var relative = Regex.Matches(File.ReadAllText(readme), @"\]\((?!https://)([^)]*)\)").Cast<Match>().Select(match => match.Groups[1].Value);
            Assert.Empty(relative);
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
