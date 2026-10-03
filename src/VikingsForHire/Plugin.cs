using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Utils;
using VikingsForHire.Commands;
using VikingsForHire.Config;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
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
        public const string Guid = "Spronglehump.VikingsForHire";
        public const string Name = "VikingsForHire";
        public const string Version = "0.1.0";

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

            Harmony = new Harmony(Guid);
            Harmony.PatchAll(typeof(Plugin).Assembly);

            DebugCommands.Register();
            TestHarness.Register();
            Fixtures.Register();

            SessionInfo.LogHeader("plugin load");
            Log.LogInfo($"{Name} {Version} loaded");
        }

        private void Update() => VfhLog.Guard(LogCat.Data, "data.tick_failed", DataStore.Tick);

        private void OnDestroy() => VfhLog.Shutdown();
    }
}
