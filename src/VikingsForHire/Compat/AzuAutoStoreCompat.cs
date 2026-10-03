using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;

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

        private static MethodInfo? _tryAdd;

        /// <summary>
        /// Azu only registers a chest in Container.Awake, and only if it already has a creator. Test fixtures set the
        /// creator after spawning, so they call this to have Azu pick the chest up. Returns false without Azu.
        /// </summary>
        public static bool Register(Container container)
        {
            if (!IsLoaded)
                return false;
            _tryAdd ??= AccessTools.Method("AzuAutoStore.Patches.ContainerAwakePatch:TryAddContainer");
            return _tryAdd != null && _tryAdd.Invoke(null, new object[] { container }) is true;
        }
    }
}
