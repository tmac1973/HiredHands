using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings.Work.Chores
{
    /// <summary>
    /// Walking up to things for a chore (one per chore: it remembers its spot and progress) and the small shared steps:
    /// taking items from a chest, picking up dropped items.
    /// </summary>
    internal sealed class WorkSteps
    {
        public const float Reach = 2.2f;

        private float _bestDistance = float.MaxValue;
        private float _bestRoute = float.MaxValue;
        private float _progressAt;
        private Component? _spotFor;
        private Vector3 _spotTarget;
        private Vector3 _spot;
        private bool _spotReachable;
        private float _spotAt;
        private float _noSpotLoggedAt = -999f;

        /// <summary>Seconds without getting closer to (or working at) the current thing.</summary>
        public float SinceProgress => Time.time - _progressAt;

        public void Reset()
        {
            _bestDistance = float.MaxValue;
            _bestRoute = float.MaxValue;
            _spotFor = null;
            _progressAt = Time.time;
        }

        /// <summary>
        /// Walks to a spot just outside <paramref name="obj"/>'s footprint on our side of <paramref name="target"/>
        /// (a switch or a chest): the target itself is inside the object, where the pathfinder can't go. True when close
        /// enough to use it.
        /// </summary>
        public bool Approach(HirelingAI ai, float dt, Component obj, Vector3 target, float reach = PlayerReach)
        {
            float half = Footprint(obj);
            float dist = Utils.DistanceXZ(ai.transform.position, target);
            if (dist < _bestDistance - 0.3f)
            {
                _bestDistance = dist;
                _progressAt = Time.time;
            }
            // Or getting on along the route there (round a hill, away from it in a straight line).
            if (_spotFor == obj && ai.RouteLeft(_spot) is float left && left < _bestRoute - 0.3f)
            {
                _bestRoute = left;
                _progressAt = Time.time;
            }
            if (UsableFrom(ai.transform.position, obj, target, half, reach))
            {
                _progressAt = Time.time; // working at it counts as progress
                return true;
            }
            // Pick again now and then while no spot was reachable: routes up stairs are worked out over the next frames
            // (the first ask only queues them), so the first pick misses every spot upstairs.
            if (_spotFor != obj || _spotTarget != target || (!_spotReachable && Time.time - _spotAt > RepickSeconds))
            {
                if (_spotFor != obj || _spotTarget != target)
                    _bestRoute = float.MaxValue; // another thing: another route (picking again for the same one keeps it)
                _spotFor = obj;
                _spotTarget = target;
                _spotAt = Time.time;
                _spot = PickSpot(ai, obj, target, half, reach, out _spotReachable);
                if (!_spotReachable && Time.time - _noSpotLoggedAt > 10f)
                {
                    _noSpotLoggedAt = Time.time;
                    VfhLog.D(LogCat.Work, "work.no_spot", ("hid", ai.Hireling.Hid), ("obj", Utils.GetPrefabName(obj.gameObject)), ("at", target),
                        ("from", ai.transform.position), ("meanwhile", _spot));
                }
            }
            ai.WalkTo(dt, _spot, 0.5f, run: false);
            return false;
        }

        // Close enough to use it from where the feet are. Height against the object's base, not the switch (a smelter's ore
        // input is 2 m up): on its floor, not below. Or, like a player's reach, within 3 m of its nearest surface (a wall's
        // upper row, a sconce up high).
        private static bool UsableFrom(Vector3 feet, Component obj, Vector3 target, float half, float reach) =>
            (Utils.DistanceXZ(feet, target) <= half + Reach && Mathf.Abs(feet.y - obj.transform.position.y) < 1.8f) || WithinReach(feet, obj, reach);

        // A spot just outside the object it can use it from: the side facing us if we can get there (the game's map or the
        // base's links), else another side we can reach (nearest first). Not the ground below a chest upstairs: close by,
        // but out of reach through the floor. With none (yet: routes upstairs are still being worked out), the nearest
        // spot it can get to, to close in meanwhile; else the facing side anyway (WalkTo then walks straight at it).
        private static Vector3 PickSpot(HirelingAI ai, Component obj, Vector3 target, float half, float reach, out bool reachable)
        {
            reachable = true;
            Vector3 away = ai.transform.position - target;
            away.y = 0f;
            Vector3 facing = away.sqrMagnitude > 0.01f ? away.normalized : obj.transform.forward;
            var spots = Enumerable.Range(0, 8).Select(i => Quaternion.Euler(0f, i * 45f, 0f) * facing)
                .Select(d => target + d * (half + 1f)).ToList();
            Vector3? closer = null;
            foreach (Vector3 spot in spots.OrderBy(p => Vector3.Distance(p, ai.transform.position)))
            {
                Vector3 grounded = spot;
                grounded.y = Floor(spot, target.y);
                if (!ai.CanReach(grounded))
                    continue;
                if (UsableFrom(grounded, obj, target, half, reach))
                    return grounded;
                closer ??= grounded;
            }
            reachable = false;
            return closer ?? spots[0];
        }

        // The floor under a spot at about the target's height (an upper floor, not the ground under the building).
        private static float Floor(Vector3 p, float near)
        {
            if (Physics.Raycast(new Vector3(p.x, near + 1.5f, p.z), Vector3.down, out RaycastHit hit, 4f, Nav.StairSampler.FloorMask, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return ZoneSystem.instance.GetSolidHeight(p);
        }

        private const float RepickSeconds = 1f;

        public const float PlayerReach = 3f;

        /// <summary>A player's hammer reach: 5 m from the eye to what it's aimed at.</summary>
        public const float HammerReach = 5f;

        private static readonly int SightMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");

        /// <summary>
        /// Whether something is within <paramref name="reach"/> of the hireling's eye (its nearest surface). Within a close
        /// 3 m no other building piece may be in the way (a floor or wall: a player can't reach a chest upstairs through the
        /// floor); beyond it the line from the eye must be clear of anything, as a player's aim would be.
        /// </summary>
        public static bool WithinReach(HirelingAI ai, Component obj, float reach = PlayerReach) => WithinReach(ai.transform.position, obj, reach);

        public static bool WithinReach(Vector3 feet, Component obj, float reach)
        {
            Vector3 eye = feet + Vector3.up * 1.5f;
            foreach (Collider c in obj.GetComponentsInChildren<Collider>())
            {
                if (!c.enabled || c.isTrigger)
                    continue;
                // ClosestPoint needs a convex collider; others fall back to their bounds.
                Vector3 p = c is MeshCollider { convex: false } ? c.bounds.ClosestPoint(eye) : c.ClosestPoint(eye);
                float d = Vector3.Distance(eye, p);
                // Both to its nearest point and to its middle: the nearest point of a chest on a floor is its bottom edge,
                // and a line to that can slip through the crack between a wall's top and the floor.
                if (d <= PlayerReach && !PieceBetween(eye, p, obj) && !PieceBetween(eye, c.bounds.center, obj))
                    return true;
                if (d <= reach && InSight(eye, p, obj))
                    return true;
            }
            return false;
        }

        // Another building piece (not terrain, not the thing itself or its own parts) between the eye and a point on it.
        private static bool PieceBetween(Vector3 eye, Vector3 point, Component obj)
        {
            Vector3 to = point - eye;
            float length = to.magnitude;
            if (length < 0.05f)
                return false;
            Piece? own = obj.GetComponentInParent<Piece>();
            foreach (RaycastHit hit in Physics.RaycastAll(eye, to / length, length - 0.05f, SightMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(obj.transform))
                    continue;
                Piece? piece = hit.collider.GetComponentInParent<Piece>();
                if (piece != null && piece != own)
                    return true;
            }
            return false;
        }

        // Nothing but the thing itself between the eye and a point on it.
        private static bool InSight(Vector3 eye, Vector3 point, Component obj)
        {
            Vector3 to = point - eye;
            float length = to.magnitude;
            if (length < 0.05f)
                return true;
            if (!Physics.Raycast(eye, to / length, out RaycastHit hit, length - 0.05f, SightMask, QueryTriggerInteraction.Ignore))
                return true;
            return hit.collider.transform.IsChildOf(obj.transform);
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

        /// <summary>
        /// Adds items to cargo; whatever doesn't fit is dropped at the hireling's feet. Counted before and after, since the
        /// game's AddItem can add part of an amount (topping up stacks) and still report failure.
        /// </summary>
        public static void AddToCargo(Hireling h, GameObject prefab, int amount)
        {
            if (h.CargoInventory == null || amount <= 0)
                return;
            string shared = prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_name;
            int before = h.CargoInventory.CountItems(shared);
            h.CargoInventory.AddItem(prefab, amount);
            int left = amount - (h.CargoInventory.CountItems(shared) - before);
            if (left > 0)
                ItemDrop.DropItem(prefab.GetComponent<ItemDrop>().m_itemData.Clone(), left, h.transform.position + h.transform.forward * 0.5f + Vector3.up * 0.3f, Quaternion.identity);
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
