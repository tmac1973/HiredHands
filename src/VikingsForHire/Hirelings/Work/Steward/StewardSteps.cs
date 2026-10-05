using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VikingsForHire.Hirelings.Work.Steward
{
    /// <summary>
    /// Walking up to things for a chore (one per chore: it remembers its spot and progress) and the small shared steps:
    /// taking items from a chest, picking up dropped items.
    /// </summary>
    internal sealed class StewardSteps
    {
        public const float Reach = 2.2f;

        private float _bestDistance = float.MaxValue;
        private float _progressAt;
        private Component? _spotFor;
        private Vector3 _spotTarget;
        private Vector3 _spot;

        /// <summary>Seconds without getting closer to (or working at) the current thing.</summary>
        public float SinceProgress => Time.time - _progressAt;

        public void Reset()
        {
            _bestDistance = float.MaxValue;
            _spotFor = null;
            _progressAt = Time.time;
        }

        /// <summary>
        /// Walks to a spot just outside <paramref name="obj"/>'s footprint on our side of <paramref name="target"/>
        /// (a switch or a chest): the target itself is inside the object, where the pathfinder can't go. True when close
        /// enough to use it.
        /// </summary>
        public bool Approach(HirelingAI ai, float dt, Component obj, Vector3 target)
        {
            float half = Footprint(obj);
            float dist = Utils.DistanceXZ(ai.transform.position, target);
            if (dist < _bestDistance - 0.3f)
            {
                _bestDistance = dist;
                _progressAt = Time.time;
            }
            // Height against the object's base, not the switch (a smelter's ore input is 2 m up): on its floor, not below.
            // Or, like a player's reach, within 3 m of its nearest surface (a wall's upper row, a sconce up high).
            if ((dist <= half + Reach && Mathf.Abs(ai.transform.position.y - obj.transform.position.y) < 1.8f) || WithinReach(ai, obj))
            {
                _progressAt = Time.time; // working at it counts as progress
                return true;
            }
            if (_spotFor != obj || _spotTarget != target)
            {
                _spotFor = obj;
                _spotTarget = target;
                _spot = PickSpot(ai, obj, target, half);
            }
            ai.WalkTo(dt, _spot, 0.5f, run: false);
            return false;
        }

        // A spot just outside the object: the side facing us if we can get there (the game's map or the base's links),
        // else another side we can reach (nearest first), else the facing side anyway (WalkTo then walks straight at it).
        private static Vector3 PickSpot(HirelingAI ai, Component obj, Vector3 target, float half)
        {
            Vector3 away = ai.transform.position - target;
            away.y = 0f;
            Vector3 facing = away.sqrMagnitude > 0.01f ? away.normalized : obj.transform.forward;
            var spots = Enumerable.Range(0, 8).Select(i => Quaternion.Euler(0f, i * 45f, 0f) * facing)
                .Select(d => target + d * (half + 1f)).ToList();
            foreach (Vector3 spot in spots.OrderBy(p => Vector3.Distance(p, ai.transform.position)))
            {
                Vector3 grounded = spot;
                grounded.y = Floor(spot, target.y);
                if (ai.CanReach(grounded))
                    return grounded;
            }
            return spots[0];
        }

        // The floor under a spot at about the target's height (an upper floor, not the ground under the building).
        private static float Floor(Vector3 p, float near)
        {
            if (Physics.Raycast(new Vector3(p.x, near + 1.5f, p.z), Vector3.down, out RaycastHit hit, 4f, Nav.StairSampler.FloorMask, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return ZoneSystem.instance.GetSolidHeight(p);
        }

        private const float PlayerReach = 3f;

        private static bool WithinReach(HirelingAI ai, Component obj)
        {
            Vector3 eye = ai.transform.position + Vector3.up * 1.5f;
            foreach (Collider c in obj.GetComponentsInChildren<Collider>())
            {
                if (!c.enabled || c.isTrigger)
                    continue;
                // ClosestPoint needs a convex collider; others fall back to their bounds.
                Vector3 p = c is MeshCollider { convex: false } ? c.bounds.ClosestPoint(eye) : c.ClosestPoint(eye);
                if (Vector3.Distance(eye, p) <= PlayerReach)
                    return true;
            }
            return false;
        }

        // Half the widest horizontal extent of the object's solid colliders.
        public static float Footprint(Component obj)
        {
            Bounds? all = null;
            foreach (Collider c in obj.GetComponentsInChildren<Collider>())
            {
                if (!c.enabled || c.isTrigger)
                    continue;
                if (all is Bounds b)
                {
                    b.Encapsulate(c.bounds);
                    all = b;
                }
                else
                {
                    all = c.bounds;
                }
            }
            return all is Bounds x ? Mathf.Clamp(Mathf.Max(x.extents.x, x.extents.z), 0.3f, 3f) : 0.5f;
        }

        /// <summary>Takes what it can of each item from one chest into cargo (the chest keeps its minimum). Returns how many moved.</summary>
        public static int TakeFromChest(Container chest, Hireling h, IReadOnlyDictionary<string, int> want, int keepMin)
        {
            int moved = 0;
            var inChest = new HashSet<string>(chest.GetInventory().GetAllItems().Where(i => i.m_dropPrefab != null).Select(i => i.m_dropPrefab.name));
            foreach (KeyValuePair<string, int> f in want.Where(f => inChest.Contains(f.Key)))
                moved += ContainerAccess.Take(chest, h.CargoInventory!, f.Key, f.Value, keepMin, h.Hid);
            return moved;
        }

        /// <summary>Picks up dropped items of these kinds near a point into cargo, as far as cargo allows. Returns how many.</summary>
        public static int PickUpDrops(Hireling h, Vector3 around, ICollection<string> prefabs, float radius, System.Func<ItemDrop, bool>? only = null)
        {
            int picked = 0;
            foreach (ItemDrop d in ItemDrop.s_instances.Where(d => d != null && d.m_nview != null && d.m_nview.IsValid() && d.m_itemData?.m_dropPrefab != null &&
                                                                 prefabs.Contains(d.m_itemData.m_dropPrefab.name) &&
                                                                 !d.m_itemData.m_customData.ContainsKey(DropPile.Tag) &&
                                                                 Vector3.Distance(d.transform.position, around) < radius &&
                                                                 (only == null || only(d))).ToList())
            {
                if (h.CargoInventory!.NrOfItems() >= h.CargoSlots && !h.CargoInventory.CanAddItem(d.m_itemData))
                    break;
                if (!d.m_nview.IsOwner())
                    d.m_nview.ClaimOwnership();
                ItemDrop.ItemData item = d.m_itemData.Clone();
                if (!h.CargoInventory.AddItem(item))
                    break;
                picked += item.m_stack;
                ZNetScene.instance.Destroy(d.gameObject);
            }
            return picked;
        }

        public static int MaxStack(string prefab) =>
            ObjectDB.instance.GetItemPrefab(prefab)?.GetComponent<ItemDrop>().m_itemData.m_shared.m_maxStackSize ?? 1;

        public static string SharedName(string prefab) =>
            ObjectDB.instance.GetItemPrefab(prefab)?.GetComponent<ItemDrop>().m_itemData.m_shared.m_name ?? prefab;
    }
}
