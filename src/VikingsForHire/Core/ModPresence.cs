using System.Collections.Generic;

namespace VikingsForHire.Core
{
    /// <summary>
    /// Which other mods are loaded, by BepInEx GUID: filled at the main menu (Compat.ModPresenceSetup), read by the job
    /// rules (withMods). Tests can pretend a mod is there or not.
    /// </summary>
    public static class ModPresence
    {
        private static HashSet<string> _loaded = new();
        private static readonly Dictionary<string, bool> Pretended = new();

        public static bool IsLoaded(string guid) => Pretended.TryGetValue(guid, out bool p) ? p : _loaded.Contains(guid);

        public static void SetLoaded(IEnumerable<string> guids) => _loaded = new HashSet<string>(guids);

        /// <summary>Test override: true or false pretends, null goes back to the real answer.</summary>
        public static void Pretend(string guid, bool? loaded)
        {
            if (loaded is bool b)
                Pretended[guid] = b;
            else
                Pretended.Remove(guid);
        }
    }
}
