using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Utils;
using VikingsForHire.Board;
using VikingsForHire.Commands;
using VikingsForHire.Compat;
using VikingsForHire.Config;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.L10n;
using VikingsForHire.Testing;

namespace VikingsForHire
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInDependency("com.ValheimModding.YamlDotNetDetector")]
    // Soft dependencies load first so the compat patches can find their targets.
    [BepInDependency("Azumatt.AzuAutoStore", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Azumatt.AzuCraftyBoxes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Spronglehump.PullMats", BepInDependency.DependencyFlags.SoftDependency)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "Spronglehump.HiredHands";
        public const string Name = "HiredHands";
        public const string Version = "0.7.2";

        internal static ManualLogSource Log = null!;
        internal static Harmony Harmony = null!;
        internal static Plugin Instance = null!;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            VfhLog.Init(Logger);
            VfhConfig.Bind(Config);
            DataStore.Init();

            Strings.Register();

            Harmony = new Harmony(Guid);
            Harmony.PatchAll(typeof(Plugin).Assembly);
            CompatPatcher.Apply(Harmony);

            BoardPiece.Register();
            Followers.CommandStoneItem.Register();
            Board.HiringCharter.Register();
            Hirelings.BroomItem.Register();
            Hirelings.Gear.CultivatorItem.Register();
            Hirelings.Gear.LadleItem.Register();
            Hirelings.Work.Trees.TreePatchPiece.Register();
            Hirelings.Work.Farm.CropCatalog.Register();
            Hirelings.Work.Farm.FarmState.FreeSpots = Hirelings.Work.Farm.PlantChore.FreeSpots;
            Hirelings.Work.Kitchen.KitchenCatalog.Register();
            Net.FollowerServer.Register();
            Telemetry.BalanceLog.Register();
            Board.LowFunds.Register();
            BoardRegistry.Register();
            Net.MutationService.Register();
            Net.BoardServer.Register();
            Board.CharterPacking.Register();
            Hirelings.HirelingPrefab.Register();

            DebugCommands.Register();
            BoardCommands.Register();
            HirelingCommands.Register();
            NavCommands.Register();
            RosterCommands.Register();
            TestHarness.Register();
            Fixtures.Register();
            FixturesBoard.Register();
            FixturesUpgrade.Register();
            FixturesHireling.Register();
            FixturesRoster.Register();
            FixturesCombat.Register();
            FixturesWork.Register();
            FixturesMining.Register();
            FixturesSmelter.Register();
            FixturesFollow.Register();
            FixturesTravel.Register();
            FixturesNav.Register();
            FixturesFarm.Register();
            FixturesKitchen.Register();

            SessionInfo.LogHeader("plugin load");
            Log.LogInfo($"{Name} {Version} loaded");
        }

        private void Update()
        {
            PerfCounters.FrameTick();
            VfhLog.Guard(LogCat.Data, "data.tick_failed", DataStore.Tick);
            VfhLog.Guard(LogCat.Board, "upgrade.tick_failed", BoardUpgrade.Tick);
            VfhLog.Guard(LogCat.Roster, "server.tick_failed", Net.BoardServer.Tick);
            VfhLog.Guard(LogCat.Follow, "follow.tick_failed", Net.FollowerServer.Tick);
            VfhLog.Guard(LogCat.Follow, "orphan.tick_failed", Net.OrphanMonitor.Tick);
            VfhLog.Guard(LogCat.Roster, "reconcile.tick_failed", Net.RosterReconciler.Tick);
            VfhLog.Guard(LogCat.Core, "balance.tick_failed", Telemetry.BalanceLog.Tick);
            Followers.StoneInput.Tick();
            VfhLog.Guard(LogCat.Follow, "travel.tick_failed", Followers.TeleportTravel.Tick);
            VfhLog.Guard(LogCat.Follow, "ship.tick_failed", Followers.ShipStowage.Tick);
            VfhLog.Guard(LogCat.UI, "hud.tick_failed", Followers.FollowerHud.Tick);
            VfhLog.Guard(LogCat.Board, "funds.tick_failed", Board.LowFunds.Tick);
            VfhLog.Guard(LogCat.Hireling, "graves.tick_failed", Hirelings.Graves.Tick);
            VfhLog.Guard(LogCat.Nav, "navlinks.tick_failed", Hirelings.Nav.NavLinkRegistry.Tick);
        }

        private void OnDestroy() => VfhLog.Shutdown();
    }
}
