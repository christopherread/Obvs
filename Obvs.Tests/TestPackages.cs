using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
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

        public static TheoryData<string> PackagesOnObvs => new TheoryData<string>(
            "Obvs.ActiveMQ", "Obvs.AmazonSqs", "Obvs.AzureSB", "Obvs.Kafka", "Obvs.Mcp", "Obvs.NATS", "Obvs.NetMQ", "Obvs.RabbitMQ",
            "Obvs.Serialization.Json", "Obvs.Serialization.MessagePack", "Obvs.Serialization.ProtoBuf", "Obvs.Testing");

        // An app that installs only a transport still gets the analyzers and the [ObvsMessages] generator Obvs ships.
        // NuGet's default for a ProjectReference keeps its analyzers private, packing exclude="Build,Analyzers".
        [Theory]
        [MemberData(nameof(PackagesOnObvs))]
        public void ShouldPassOnTheAnalyzersOfObvs(string package)
        {
            var reference = XDocument.Load(Path.Combine(Root, package, package + ".csproj")).Descendants("ProjectReference")
                .Single(item => item.Attribute("Include")?.Value.Replace('\\', '/') == "../Obvs/Obvs.csproj");
            var privateAssets = reference.Attribute("PrivateAssets")?.Value;
            Assert.True(privateAssets != null, $"{package} keeps the analyzers of Obvs private: its ProjectReference to Obvs needs PrivateAssets without analyzers");
            Assert.DoesNotContain("analyzers", privateAssets, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("all", privateAssets, StringComparison.OrdinalIgnoreCase);
            Assert.Null(reference.Attribute("IncludeAssets"));
            Assert.Null(reference.Attribute("ExcludeAssets"));
        }

        // CI packs and publishes each package from master and tags, where packing a project that's gone fails the release
        [Fact]
        public void ShouldPackEveryPackageAndNothingElse()
        {
            var workflow = File.ReadAllText(Path.Combine(Root, ".github", "workflows", "dotnet.yml"));
            var packed = Regex.Matches(workflow, @"^\s*dotnet pack .* (\S+)\s*$", RegexOptions.Multiline).Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .OrderBy(package => package, StringComparer.Ordinal);
            var packages = Packages.Select(row => row.Data).OrderBy(package => package, StringComparer.Ordinal);
            Assert.Equal(packages, packed);
        }

        [Fact]
        public void ShouldNotDependOnSystemReflectionEmitLightweight()
        {
            // Obvs.ActiveMQ took it for code it no longer has; every framework Obvs targets has what's left of it
            Assert.DoesNotContain("System.Reflection.Emit.Lightweight", File.ReadAllText(Path.Combine(Root, "Obvs.ActiveMQ", "Obvs.ActiveMQ.csproj")));
            Assert.DoesNotContain("System.Reflection.Emit.Lightweight", File.ReadAllText(Path.Combine(Root, "Directory.Packages.props")));
        }

        // Apps configure a transport with its Use method and options. Its sources and publishers stay public, with the types
        // their constructors take, for apps that build endpoints from them. What v6 built endpoints with is internal.
        public static TheoryData<string, string[]> TransportTypes => new TheoryData<string, string[]>
        {
            { "Obvs.ActiveMQ", new[]
            {
                "Obvs.ActiveMQ.AcknowledgementMode", "Obvs.ActiveMQ.ActiveMqOptions", "Obvs.ActiveMQ.MessagePublisher<TMessage>",
                "Obvs.ActiveMQ.MessageSource<TMessage>", "Obvs.ActiveMQ.TextMessagePublisher<TMessage>", "Obvs.ActiveMQ.TextMessageSource<TMessage>",
                "Obvs.Hosting.ActiveMqTransportExtensions",
            } },
            { "Obvs.Kafka", new[]
            {
                "Obvs.Hosting.KafkaTransportExtensions", "Obvs.Kafka.Configuration.KafkaConfiguration", "Obvs.Kafka.Configuration.KafkaProducerConfiguration",
                "Obvs.Kafka.Configuration.KafkaSourceConfiguration", "Obvs.Kafka.KafkaOAuthBearerToken", "Obvs.Kafka.KafkaOptions",
                "Obvs.Kafka.MessagePublisher<TMessage>", "Obvs.Kafka.MessageSource<TMessage>",
            } },
            { "Obvs.NATS", new[]
            {
                "Obvs.Hosting.NatsJetStreamTransportExtensions", "Obvs.Hosting.NatsTransportExtensions", "Obvs.NATS.MessagePublisher<TMessage>",
                "Obvs.NATS.MessageSource<TMessage>", "Obvs.NATS.NatsJetStreamOptions", "Obvs.NATS.NatsOptions",
            } },
            { "Obvs.NetMQ", new[]
            {
                "Obvs.Hosting.NetMqTransportExtensions", "Obvs.NetMQ.Configuration.SocketType", "Obvs.NetMQ.MessagePublisher<TMessage>",
                "Obvs.NetMQ.MessageSource<TMessage>", "Obvs.NetMQ.NetMqOptions",
            } },
            { "Obvs.RabbitMQ", new[]
            {
                "Obvs.Hosting.RabbitMqTransportExtensions", "Obvs.RabbitMQ.MessagePublisher<TMessage>", "Obvs.RabbitMQ.MessageSource<TMessage>",
                "Obvs.RabbitMQ.RabbitMqOptions",
            } },
        };

        [Theory]
        [MemberData(nameof(TransportTypes))]
        public void ShouldExposeOnlyItsUseMethodOptionsSourcesAndPublishers(string package, string[] types)
        {
            Assert.Equal(types.OrderBy(type => type, StringComparer.Ordinal), DeclaredTypes(package));
        }

        public static TheoryData<string> Transports => new TheoryData<string>("Obvs.ActiveMQ", "Obvs.Kafka", "Obvs.NATS", "Obvs.NetMQ", "Obvs.RabbitMQ");

        // Every public member a transport ships is documented, its sources and publishers included
        [Theory]
        [MemberData(nameof(Transports))]
        public void ShouldDocumentEveryPublicMember(string package)
        {
            var exempt = Directory.EnumerateFiles(Path.Combine(Root, package), "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                .Where(file => Regex.IsMatch(File.ReadAllText(file), @"#pragma warning disable[^\n]*CS1591"))
                .Select(file => Path.GetRelativePath(Root, file));
            Assert.Empty(exempt);
        }

        // The types a package declares in its public API files: the lines that name a type, not a member
        private static string[] DeclaredTypes(string package)
        {
            return new[] { "PublicAPI.Shipped.txt", "PublicAPI.Unshipped.txt" }
                .SelectMany(file => File.ReadAllLines(Path.Combine(Root, package, file)))
                .Where(line => line.Length > 0 && !line.StartsWith("#") && !line.StartsWith("*REMOVED*") && !line.Contains("->") && !line.Contains("("))
                .OrderBy(type => type, StringComparer.Ordinal)
                .ToArray();
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
