using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Board
{
    /// <summary>
    /// Every food something makes: a recipe's output, or what a cooking station, oven or fermenter turns things into.
    /// Edible items outside it are raw (berries, mushrooms, meat), including the ones mods add that the data file's raw
    /// list doesn't name (a Witch Eye). Built from the game's recipes and stations, again when either changes.
    /// </summary>
    internal static class PreparedFoods
    {
        private static HashSet<string>? _set;
        private static ObjectDB? _db;
        private static int _recipes = -1;
        private static int _prefabs = -1;

        /// <summary>The prepared foods, or null while the game's recipes aren't loaded (only the raw list applies then).</summary>
        public static HashSet<string>? Get()
        {
            if (ObjectDB.instance == null || ZNetScene.instance == null || ObjectDB.instance.m_recipes.Count == 0)
                return null;
            if (_set != null && _db == ObjectDB.instance && _recipes == ObjectDB.instance.m_recipes.Count && _prefabs == ZNetScene.instance.m_prefabs.Count)
                return _set;
            _db = ObjectDB.instance;
            _recipes = ObjectDB.instance.m_recipes.Count;
            _prefabs = ZNetScene.instance.m_prefabs.Count;
            _set = Build();
            return _set;
        }

        private static HashSet<string> Build()
        {
            var made = new HashSet<string>();
            foreach (Recipe r in ObjectDB.instance.m_recipes)
                if (r != null && r.m_item != null)
                    made.Add(r.m_item.gameObject.name);
            foreach (GameObject go in ZNetScene.instance.m_prefabs)
            {
                if (go == null)
                    continue;
                if (go.GetComponent<CookingStation>() is CookingStation stove)
                    foreach (CookingStation.ItemConversion c in stove.m_conversion)
                        if (c?.m_to != null)
                            made.Add(c.m_to.gameObject.name);
                if (go.GetComponent<Fermenter>() is Fermenter fermenter)
                    foreach (Fermenter.ItemConversion c in fermenter.m_conversion)
                        if (c?.m_to != null)
                            made.Add(c.m_to.gameObject.name);
            }
            var raw = new List<string>();
            foreach (GameObject item in ObjectDB.instance.m_items)
            {
                ItemDrop.ItemData.SharedData? s = item != null ? item.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
                if (s != null && (s.m_food > 0f || s.m_foodStamina > 0f || s.m_foodEitr > 0f) && !made.Contains(item!.name))
                    raw.Add(item.name);
            }
            VfhLog.I(LogCat.Payment, "food.raw_unmade", ("count", raw.Count), ("items", string.Join(",", raw.OrderBy(n => n))));
            return made;
        }
    }
}
