using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Utils;

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

        private void Awake()
        {
            Log = Logger;
            Harmony = new Harmony(Guid);
            Log.LogInfo($"{Name} {Version} loaded");
        }
    }
}
