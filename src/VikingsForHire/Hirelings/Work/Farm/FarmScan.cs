using System.Collections.Generic;
using UnityEngine;

namespace VikingsForHire.Hirelings.Work.Farm
{
    /// <summary>Loaded crops near a point, found by their prefab without a physics query.</summary>
    internal static class FarmScan
    {
        /// <summary>Growing saplings and ripe or regrowing pickables of catalog crops within the radius.</summary>
        public static void Nearby(Vector3 center, float radius, List<Plant> plants, List<Pickable> pickables)
        {
            plants.Clear();
            pickables.Clear();
            if (ZNetScene.instance == null)
                return;
            float r2 = radius * radius;
            foreach (KeyValuePair<ZDO, ZNetView> kv in ZNetScene.instance.m_instances)
            {
                ZNetView v = kv.Value;
                if (v == null || kv.Key == null)
                    continue;
                int hash = kv.Key.GetPrefab();
                bool sapling = CropCatalog.IsSaplingHash(hash);
                if (!sapling && !CropCatalog.IsGrownHash(hash))
                    continue;
                Vector3 p = kv.Key.GetPosition();
                float dx = p.x - center.x, dz = p.z - center.z;
                if (dx * dx + dz * dz > r2)
                    continue;
                if (sapling)
                {
                    if (v.GetComponent<Plant>() is Plant plant)
                        plants.Add(plant);
                }
                else if (v.GetComponent<Pickable>() is Pickable pick)
                {
                    pickables.Add(pick);
                }
            }
        }
    }
}
