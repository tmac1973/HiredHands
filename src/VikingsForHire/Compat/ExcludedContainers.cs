using System.Collections.Generic;

namespace VikingsForHire.Compat
{
    /// <summary>
    /// Our prefabs whose containers other storage mods must never store into or pull from: the board's food and coins,
    /// and (from phase 05) a hireling's cargo.
    /// </summary>
    internal static class ExcludedContainers
    {
        public static readonly HashSet<string> Prefabs = new() { Board.BoardZdo.PrefabName, Hirelings.HirelingZdo.PrefabName };

        public static bool IsExcluded(string? prefabName) => prefabName != null && Prefabs.Contains(prefabName);

        public static bool IsExcluded(Container? container)
        {
            if (container == null)
                return false;
            UnityEngine.GameObject root = container.m_rootObjectOverride != null ? container.m_rootObjectOverride.gameObject : container.gameObject;
            return IsExcluded(Utils.GetPrefabName(root));
        }
    }
}
