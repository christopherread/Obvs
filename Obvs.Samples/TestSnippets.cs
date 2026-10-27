using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Obvs.Samples
{
    // Every C# block in the README and docs is either a snippet of this project, marked <!-- snippet: name --> and
    // identical to the region "// snippet: name" ... "// end-snippet" here, or marked <!-- not compiled: why -->.
    // Set OBVS_UPDATE_SNIPPETS=1 to copy the regions into the docs instead of checking them.
    public class TestSnippets
    {
        private static readonly string Root = FindRoot();

        public static TheoryData<string> Documents()
        {
            var documents = new[] { "README.md" }
                .Concat(Directory.GetFiles(Path.Combine(Root, "docs"), "*.md", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(Root, path)))
                // The design doc sketches APIs before they exist
                .Where(path => Path.GetFileName(path) != "v7-design.md")
                .OrderBy(path => path, StringComparer.Ordinal);
            return new TheoryData<string>(documents);
        }

        [Theory]
        [MemberData(nameof(Documents))]
        public void ShouldMatchTheSamples(string document)
        {
            var path = Path.Combine(Root, document);
            var text = File.ReadAllText(path);
            var snippets = Snippets();
            var problems = new List<string>();
            var update = Environment.GetEnvironmentVariable("OBVS_UPDATE_SNIPPETS") == "1";

            var updated = Regex.Replace(text, @"(?<marker><!-- (?<kind>snippet|not compiled): (?<name>[^>]*?) -->\n)?```csharp\n(?<code>.*?)```", match =>
            {
                var line = text.Substring(0, match.Index).Count(c => c == '\n') + 1;
                if (!match.Groups["marker"].Success)
                {
                    problems.Add($"{document}:{line}: a C# block without <!-- snippet: name --> or <!-- not compiled: why --> before it.");
                    return match.Value;
                }
                if (match.Groups["kind"].Value == "not compiled")
                {
                    return match.Value;
                }
                var name = match.Groups["name"].Value;
                if (!snippets.TryGetValue(name, out var code))
                {
                    problems.Add($"{document}:{line}: no region '// snippet: {name}' in Obvs.Samples.");
                    return match.Value;
                }
                if (match.Groups["code"].Value != code)
                {
                    problems.Add($"{document}:{line}: snippet '{name}' differs from Obvs.Samples. Run with OBVS_UPDATE_SNIPPETS=1 to copy it in.");
                }
                return match.Groups["marker"].Value + "```csharp\n" + code + "```";
            }, RegexOptions.Singleline);

            if (update && updated != text)
            {
                File.WriteAllText(path, updated);
                return;
            }
            Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
        }

        // The regions in this project's source, without their indentation
        private static Dictionary<string, string> Snippets()
        {
            var snippets = new Dictionary<string, string>();
            foreach (var file in Directory.GetFiles(Path.Combine(Root, "Obvs.Samples"), "*.cs", SearchOption.AllDirectories)
                         .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
            {
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    var start = Regex.Match(lines[i], @"^\s*// snippet: (\S+)\s*$");
                    if (!start.Success)
                    {
                        continue;
                    }
                    var body = lines.Skip(i + 1).TakeWhile(l => !Regex.IsMatch(l, @"^\s*// end-snippet\s*$")).ToArray();
                    var indent = body.Where(l => l.Trim().Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
                    snippets[start.Groups[1].Value] = string.Concat(body.Select(l => (l.Length >= indent ? l.Substring(indent) : l.TrimStart()).TrimEnd() + "\n"));
                }
            }
            return snippets;
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
