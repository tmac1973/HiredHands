using BepInEx.Configuration;
using UnityEngine;
using VikingsForHire.Config;

namespace VikingsForHire.Hirelings.Work
{
    /// <summary>
    /// The key held with Use (E) to mark a chest, door or fire for workers, one setting each, so it can be moved off a key
    /// another mod uses there (MissingPieces' drawers take Shift+E; Tim moved AzuCraftyBoxes to Alt+E). Shift reads the
    /// game's own alt-use (which follows a remapped Shift and the gamepad); any other key is read directly.
    /// </summary>
    internal static class MarkKeys
    {
        public static bool Held(ConfigEntry<KeyCode> key, bool alt)
        {
            if (!VfhConfig.ChestModeOnShiftUse.Value)
                return false;
            return key.Value switch
            {
                KeyCode.None => false,
                KeyCode.LeftShift or KeyCode.RightShift => alt,
                _ => Input.GetKey(key.Value),
            };
        }

        /// <summary>Whether marking this kind of thing is on at all (for the hover line).</summary>
        public static bool On(ConfigEntry<KeyCode> key) => VfhConfig.ChestModeOnShiftUse.Value && key.Value != KeyCode.None;

        /// <summary>The hover's key hint: "[Shift + E] ".</summary>
        public static string Hint(ConfigEntry<KeyCode> key) => $"\n[<color=yellow><b>{Name(key.Value)} + $KEY_Use</b></color>] ";

        private static string Name(KeyCode k) => k switch
        {
            KeyCode.LeftShift or KeyCode.RightShift => "Shift",
            KeyCode.LeftAlt or KeyCode.RightAlt => "Alt",
            KeyCode.LeftControl or KeyCode.RightControl => "Ctrl",
            _ => k.ToString(),
        };
    }
}
