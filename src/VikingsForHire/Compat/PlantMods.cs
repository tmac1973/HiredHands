using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace VikingsForHire.Compat
{
    /// <summary>
    /// PlantEverything (bushes, mushrooms… planted as pickables) and PlantEasily (rows, area harvest), both optional:
    /// detected at runtime, settings read from their config so the Farmer's rows match the player's.
    /// </summary>
    internal static class PlantMods
    {
        public const string PlantEverythingGuid = "advize.PlantEverything";
        public const string PlantEasilyGuid = "advize.PlantEasily";
        public const float DefaultHarvestRadius = 3f;

        public static bool IsPlantEverything => Chainloader.PluginInfos.ContainsKey(PlantEverythingGuid);
        public static bool IsPlantEasily => Chainloader.PluginInfos.ContainsKey(PlantEasilyGuid);

        /// <summary>Distance between plants in a row: PlantEasily's (grow radius × 2 + its extra spacing), else grow radius × 2.</summary>
        public static float CropSpacing(float growRadius) => growRadius * 2f + (Setting("Grid", "ExtraCropSpacing") ?? 0f);

        /// <summary>How far around it a Farmer harvests at each stop: PlantEasily's harvest radius, else 3 m.</summary>
        public static float HarvestRadius => Setting("Harvesting", "HarvestRadius") is float r && r > 0f ? r : DefaultHarvestRadius;

        // A PlantEasily float setting; null when it isn't loaded or doesn't bind it here (it doesn't on a dedicated server).
        private static float? Setting(string section, string key)
        {
            if (!Chainloader.PluginInfos.TryGetValue(PlantEasilyGuid, out BepInEx.PluginInfo info) || info.Instance == null)
                return null;
            return info.Instance.Config.TryGetEntry(section, key, out ConfigEntry<float> entry) ? entry.Value : null;
        }
    }
}
