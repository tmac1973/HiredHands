using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using VikingsForHire.Core;
using Xunit;

namespace VikingsForHire.Tests
{
    /// <summary>Every $vfh_ token in the source has an English string, so no raw token ever shows in game.</summary>
    public class LocalizationCoverageTests
    {
        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "VikingsForHire.sln")))
                dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
        }

        private static HashSet<string> EnglishKeys()
        {
            string json = File.ReadAllText(Path.Combine(RepoRoot(), "src", "VikingsForHire", "Localization", "English.json"));
            using JsonDocument doc = JsonDocument.Parse(json);
            return new HashSet<string>(doc.RootElement.EnumerateObject().Select(p => p.Name));
        }

        [Fact]
        public void EveryLiteralTokenHasAString()
        {
            HashSet<string> keys = EnglishKeys();
            var missing = new SortedSet<string>();
            int found = 0;
            foreach (string file in Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src"), "*.cs", SearchOption.AllDirectories))
                foreach (Match m in Regex.Matches(File.ReadAllText(file), @"\$(vfh_[a-z0-9_]+)"))
                {
                    string key = m.Groups[1].Value;
                    if (key.EndsWith("_")) // a prefix completed at runtime (jobs, stances): covered below
                        continue;
                    found++;
                    if (!keys.Contains(key))
                        missing.Add($"{key} ({Path.GetFileName(file)})");
                }
            Assert.True(found > 50, $"only {found} tokens found: is the scan looking in the right place?");
            Assert.True(missing.Count == 0, "Missing from English.json: " + string.Join(", ", missing));
        }

        [Fact]
        public void EveryEnhancementHasANameAndADescriptionWithItsNumbers()
        {
            string json = File.ReadAllText(Path.Combine(RepoRoot(), "src", "VikingsForHire", "Localization", "English.json"));
            using JsonDocument doc = JsonDocument.Parse(json);
            var strings = doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? "");
            var data = VikingsForHire.Core.Data.DefaultData.Create();
            int n = 0;
            foreach ((VikingsForHire.Core.JobType job, VikingsForHire.Core.Enhancements.EnhancementId id) in VikingsForHire.Core.Enhancements.EnhancementCatalog.All())
            {
                string name = VikingsForHire.Core.Enhancements.EnhancementText.NameKey(job, id).TrimStart('$');
                string desc = VikingsForHire.Core.Enhancements.EnhancementText.DescKey(job, id).TrimStart('$');
                Assert.True(strings.ContainsKey(name), name);
                Assert.True(strings.ContainsKey(desc), desc);
                string[] args = VikingsForHire.Core.Enhancements.EnhancementText.Args(data, job, id);
                int placeholders = Regex.Matches(strings[desc], @"\$\d").Count;
                Assert.True(placeholders == args.Length, $"{desc}: {placeholders} placeholders, {args.Length} numbers");
                foreach (string number in VikingsForHire.Core.Enhancements.EnhancementText.NumberNames(id))
                    Assert.True(data.Enhancements[job].ContainsKey($"{id}.{number}"), $"{job}.{id}.{number} has no default");
                n++;
            }
            Assert.Equal(126, n);
        }

        [Fact]
        public void NoKeyIsDefinedTwice()
        {
            string json = File.ReadAllText(Path.Combine(RepoRoot(), "src", "VikingsForHire", "Localization", "English.json"));
            var dupes = Regex.Matches(json, "^\\s*\"(vfh_[a-z0-9_]+)\"\\s*:", RegexOptions.Multiline)
                .Cast<Match>().GroupBy(m => m.Groups[1].Value).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.True(dupes.Count == 0, "Defined more than once in English.json: " + string.Join(", ", dupes));
        }

        [Fact]
        public void EveryJobAndStanceHasAString()
        {
            HashSet<string> keys = EnglishKeys();
            var missing = Enum.GetNames(typeof(JobType)).Select(j => "vfh_job_" + j.ToLowerInvariant())
                .Concat(Enum.GetNames(typeof(Stance)).Select(s => "vfh_stance_" + s.ToLowerInvariant()))
                .Where(k => !keys.Contains(k)).ToList();
            Assert.True(missing.Count == 0, "Missing from English.json: " + string.Join(", ", missing));
        }
    }
}
