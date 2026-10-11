using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VikingsForHire.Hirelings.Work.Ranch
{
    /// <summary>Picking things up off the ground into a hireling's cargo: what an animal dropped, eggs.</summary>
    internal static class DropCollect
    {
        /// <summary>Items within radius of a spot that appeared after a time (an animal's drops), nearest first.</summary>
        public static List<ItemDrop> Near(Vector3 spot, float radius, double spawnedWithin)
        {
            return ItemDrop.s_instances.Where(d => d != null && d.m_nview != null && d.m_nview.IsValid() && d.m_itemData?.m_dropPrefab != null &&
                                                   Vector3.Distance(d.transform.position, spot) <= radius && d.GetTimeSinceSpawned() <= spawnedWithin &&
                                                   d.m_nview.GetZDO()?.GetBool(Steward.AnimalsChore.FeedKey) != true)
                .OrderBy(d => Vector3.Distance(d.transform.position, spot)).ToList();
        }

        /// <summary>Into cargo, and off the ground; false (left where it is) when the cargo has no room.</summary>
        public static bool PickUp(Hireling h, ItemDrop drop)
        {
            if (h.CargoInventory == null || drop == null || drop.m_nview == null || !drop.m_nview.IsValid())
                return false;
            if (!h.CargoInventory.CanAddItem(drop.m_itemData) || h.CargoWeightRoom < drop.m_itemData.GetWeight())
                return false;
            if (!drop.m_nview.IsOwner())
                drop.m_nview.ClaimOwnership();
            if (!h.CargoInventory.AddItem(drop.m_itemData.Clone()))
                return false;
            ZNetScene.instance.Destroy(drop.gameObject);
            return true;
        }
    }
}
