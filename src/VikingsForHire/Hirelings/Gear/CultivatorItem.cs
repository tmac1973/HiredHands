using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings.Gear
{
    /// <summary>
    /// The Farmer's cultivator: a club (same stats, the Farmer's weapon at every level) wearing the vanilla cultivator's
    /// model, so it can swing it at a Greyling. Never crafted by players.
    /// </summary>
    internal static class CultivatorItem
    {
        public const string PrefabName = "VFH_Cultivator";
        private const string BasePrefab = "Club";
        private const string LookPrefab = "Cultivator";
        private const float Length = 1.5f;

        private static bool _created;

        public static void Register() =>
            PrefabManager.OnVanillaPrefabsAvailable += () => VfhLog.Guard(LogCat.Hireling, "cultivator.register_failed", Create);

        private static void Create()
        {
            if (_created)
                return;
            _created = true;
            var item = new CustomItem(PrefabName, BasePrefab, new ItemConfig { Name = "$vfh_cultivator", Description = "$vfh_cultivator_desc" });
            ItemManager.Instance.AddItem(item);
            MeshFilter? visual = HeldModel.Visual(item.ItemPrefab);
            GameObject? look = PrefabManager.Cache.GetPrefab<GameObject>(LookPrefab);
            if (visual == null || look == null || HeldModel.BiggestMesh(look) is not var (mesh, materials))
            {
                VfhLog.W(LogCat.Hireling, "cultivator.model", ("source", "club (no cultivator model found)"));
                return;
            }
            // The cultivator's own model runs from the tines to the handle: hold it near the handle's end, tines out.
            HeldModel.Point(visual, mesh, materials, Length, reverse: true, grip: 0.15f);
            VfhLog.I(LogCat.Hireling, "cultivator.model", ("mesh", mesh.name));
        }
    }
}
