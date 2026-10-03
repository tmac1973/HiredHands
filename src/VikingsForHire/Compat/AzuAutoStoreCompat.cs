using BepInEx.Bootstrap;

namespace VikingsForHire.Compat
{
    /// <summary>
    /// With AzuAutoStore installed, finished bars and coal are picked up by Azu, so smelter hirelings never collect
    /// output themselves (they'd chase items that vanish).
    /// </summary>
    internal static class AzuAutoStoreCompat
    {
        public const string Guid = "Azumatt.AzuAutoStore";

        public static bool IsLoaded => Chainloader.PluginInfos.ContainsKey(Guid);
    }
}
