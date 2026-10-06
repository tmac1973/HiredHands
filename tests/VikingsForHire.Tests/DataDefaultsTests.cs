using System.Linq;
using VikingsForHire.Core;
using VikingsForHire.Core.Data;
using Xunit;

namespace VikingsForHire.Tests
{
    public class DataDefaultsTests
    {
        [Fact]
        public void SettingsMissingFromAnOlderFileComeFromTheDefaults()
        {
            // A file from before minBoardLevel / workRadiusMultiplier existed.
            string yaml = DataYaml.Serialize(DefaultData.Create())
                .Replace("    minBoardLevel: 2\n", "").Replace("    workRadiusMultiplier: 2\n", "");
            Assert.DoesNotContain("workRadiusMultiplier: 2", yaml);
            VfhData data = DataYaml.Deserialize(yaml, out var filled);
            Assert.Equal(2, data.Jobs[JobType.Miner].MinBoardLevel);
            Assert.Equal(2f, data.Jobs[JobType.Miner].WorkRadiusMultiplier);
            Assert.Equal(2f, data.Jobs[JobType.Woodcutter].WorkRadiusMultiplier);
            Assert.Contains("jobs.Miner.minBoardLevel", filled);
        }

        [Fact]
        public void TheWoodReserveReachesOlderFiles()
        {
            string full = DataYaml.Serialize(DefaultData.Create());
            int smelter = full.IndexOf("  Smelter:");
            int start = full.IndexOf("    keepInStorage:", smelter);
            Assert.True(smelter > 0 && start > smelter, "default file should list the smelter's keepInStorage");
            int end = full.IndexOf("\n", full.IndexOf("Wood: 50", start));
            string yaml = full.Remove(start, end - start + 1);
            VfhData data = DataYaml.Deserialize(yaml, out var filled);
            Assert.Equal(50, data.Jobs[JobType.Smelter].KeepInStorage["Wood"]);
            Assert.Contains("jobs.Smelter.keepInStorage", filled);
        }

        [Fact]
        public void NavLinksReachOlderFiles()
        {
            string full = DataYaml.Serialize(DefaultData.Create());
            int start = full.IndexOf("navLinks:");
            Assert.True(start > 0, "default file should have navLinks");
            string yaml = full.Substring(0, full.LastIndexOf('\n', start) + 1);
            VfhData data = DataYaml.Deserialize(yaml, out var filled);
            Assert.Empty(data.NavLinks.Include);
            Assert.Empty(data.NavLinks.Exclude);
            Assert.Contains("navLinks", filled);
        }

        [Fact]
        public void ChoreLevelsReachOlderFiles()
        {
            string full = DataYaml.Serialize(DefaultData.Create());
            int start = full.IndexOf("    choreLevels:", full.IndexOf("  Smelter:"));
            Assert.True(start > 0, "default file should have the Steward's choreLevels");
            int end = full.IndexOf("    keepInStorage:", start);
            Assert.True(end > start, "choreLevels should come before keepInStorage");
            string yaml = full.Remove(start, end - start);
            VfhData data = DataYaml.Deserialize(yaml, out var filled);
            Assert.Equal(2, data.Jobs[JobType.Smelter].ChoreLevels["smelter"]);
            Assert.Contains("jobs.Smelter.choreLevels", filled);
        }

        [Fact]
        public void LaterChoresGetTheirDefaultLevelNotOne()
        {
            // A file from before fermenters, shields and tidying existed: choreLevels without them.
            VfhData early = DefaultData.Create();
            foreach (string k in new[] { "fermenters", "shields", "tidy" })
                early.Jobs[JobType.Smelter].ChoreLevels.Remove(k);
            VfhData data = DataYaml.Deserialize(DataYaml.Serialize(early), out var filled);
            Assert.Equal(4, data.Jobs[JobType.Smelter].ChoreLevels["fermenters"]);
            Assert.Equal(7, data.Jobs[JobType.Smelter].ChoreLevels["shields"]);
            Assert.Contains("jobs.Smelter.choreLevels.shields", filled);
            // A level the owner set is kept.
            VfhData owner = DefaultData.Create();
            owner.Jobs[JobType.Smelter].ChoreLevels["repairs"] = 1;
            Assert.Equal(1, DataYaml.Deserialize(DataYaml.Serialize(owner), out _).Jobs[JobType.Smelter].ChoreLevels["repairs"]);
        }

        [Fact]
        public void PreChoreFilesGainTheMills()
        {
            // A 0.3 file: no choreLevels, and stations without the windmill and spinning wheel.
            VfhData old = DefaultData.Create();
            old.Jobs[JobType.Smelter].Stations.RemoveAll(s => s == "windmill" || s == "piece_spinningwheel");
            foreach (WeaponSetData g in old.Jobs[JobType.Smelter].Gear)
                g.Main = "Club";
            string full = DataYaml.Serialize(old);
            int start = full.IndexOf("    choreLevels:", full.IndexOf("  Smelter:"));
            int end = full.IndexOf("    keepInStorage:", start);
            VfhData data = DataYaml.Deserialize(full.Remove(start, end - start), out var filled);
            Assert.Contains("windmill", data.Jobs[JobType.Smelter].Stations);
            Assert.Contains("piece_spinningwheel", data.Jobs[JobType.Smelter].Stations);
            Assert.Empty(DataValidator.Validate(data));
            Assert.All(data.Jobs[JobType.Smelter].Gear, g => Assert.Equal("VFH_Broom", g.Main));

            // A 0.3 file whose owner armed the Steward keeps that gear.
            string armed = DataYaml.Serialize(old).Replace("main: Club", "main: SwordIron");
            int s2 = armed.IndexOf("    choreLevels:", armed.IndexOf("  Smelter:"));
            int e2 = armed.IndexOf("    keepInStorage:", s2);
            Assert.All(DataYaml.Deserialize(armed.Remove(s2, e2 - s2), out _).Jobs[JobType.Smelter].Gear, g => Assert.Equal("SwordIron", g.Main));

            // A 0.4 file whose owner took the windmill out keeps it out.
            VfhData owner = DefaultData.Create();
            owner.Jobs[JobType.Smelter].Stations.Remove("windmill");
            VfhData back = DataYaml.Deserialize(DataYaml.Serialize(owner), out _);
            Assert.DoesNotContain("windmill", back.Jobs[JobType.Smelter].Stations);
        }

        [Fact]
        public void WhatTheFileSaysIsKept()
        {
            string yaml = DataYaml.Serialize(DefaultData.Create()).Replace("    minBoardLevel: 2\n", "    minBoardLevel: 4\n");
            VfhData data = DataYaml.Deserialize(yaml, out var filled);
            Assert.Equal(4, data.Jobs[JobType.Miner].MinBoardLevel);
            Assert.Empty(filled);
        }

        [Fact]
        public void AFullDefaultFileFillsNothing()
        {
            DataYaml.Deserialize(DataYaml.Serialize(DefaultData.Create()), out var filled);
            Assert.Empty(filled);
        }
    }
}
