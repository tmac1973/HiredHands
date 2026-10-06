using System.Collections.Generic;
using UnityEngine;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings.Work
{
    /// <summary>
    /// A Steward parked in Gather Here out in the field picks up loose loot around its spot (monster drops, trophies,
    /// coins…), nearest first, until its cargo is full. Not things a player has only just dropped, the board's pile,
    /// animal food, or anything under a ward the owner can't use. At home it brings them back like any leftover.
    /// </summary>
    internal sealed class FieldLoot
    {
        private const float ScanSeconds = 1f;
        private const float MinAge = 5f;              // let a fight's drops settle, and a player pick up their own
        private const float PlayerDropAge = 20f;      // ... or longer, when it's at a player's feet
        private const float PlayerNear = 2f;
        private const float Reach = 2.5f;             // about a player's reach to pick something up
        private const float ReachUp = 2.5f;
        private const float GiveUp = 12f;

        private ItemDrop? _drop;
        private float _started;
        private float _scanAt;
        private readonly HashSet<ItemDrop> _unreachable = new();

        public bool Wants(HirelingAI ai)
        {
            Hireling h = ai.Hireling;
            if (ai.WorkArea is not { } area || h.CargoFull)
                return false;
            if (_drop != null && _drop.m_nview != null && _drop.m_nview.IsValid())
                return true;
            _drop = null;
            if (Time.time < _scanAt)
                return false;
            _scanAt = Time.time + ScanSeconds;
            _drop = Nearest(ai, area.Center, area.Radius);
            _started = Time.time;
            return _drop != null;
        }

        private ItemDrop? Nearest(HirelingAI ai, Vector3 center, float radius)
        {
            _unreachable.RemoveWhere(d => d == null);
            ItemDrop? best = null;
            float bestSq = float.MaxValue;
            Vector3 me = ai.transform.position;
            foreach (ItemDrop d in ItemDrop.s_instances)
            {
                if (d == null || _unreachable.Contains(d) || d.m_nview == null || !d.m_nview.IsValid() || d.m_itemData?.m_dropPrefab == null)
                    continue;
                Vector3 p = d.transform.position;
                if (Utils.DistanceXZ(p, center) > radius || d.m_itemData.m_customData.ContainsKey(DropPile.Tag) || d.IsPiece())
                    continue;
                ZDO? z = d.m_nview.GetZDO();
                if (z == null || z.GetBool(Steward.AnimalsChore.FeedKey))
                    continue;
                double age = d.GetTimeSinceSpawned();
                if (age < MinAge || (age < PlayerDropAge && Player.GetClosestPlayer(p, PlayerNear) != null))
                    continue;
                if (!PrivateArea.CheckAccess(p, 0f, false, false))
                    continue;
                float sq = (p - me).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = d;
                }
            }
            return best;
        }

        public void Tick(HirelingAI ai, float dt)
        {
            Hireling h = ai.Hireling;
            ItemDrop? drop = _drop;
            if (drop == null || drop.m_nview == null || !drop.m_nview.IsValid())
            {
                _drop = null;
                return;
            }
            bool inReach = Utils.DistanceXZ(ai.transform.position, drop.transform.position) <= Reach &&
                           Mathf.Abs(drop.transform.position.y - ai.transform.position.y) <= ReachUp;
            if (!inReach)
            {
                if (Time.time - _started > GiveUp)
                {
                    VfhLog.I(LogCat.Follow, "loot.unreachable", ("hid", h.Hid), ("item", drop.m_itemData.m_dropPrefab.name),
                        ("pos", drop.transform.position), ("me", ai.transform.position));
                    _unreachable.Add(drop);
                    _drop = null;
                    return;
                }
                ai.WalkTo(dt, drop.transform.position, 1f, run: false);
                return;
            }
            if (!drop.m_nview.IsOwner())
                drop.m_nview.ClaimOwnership();
            ItemDrop.ItemData item = drop.m_itemData.Clone();
            float unit = item.GetWeight(1);
            int fits = unit <= 0f ? item.m_stack : Mathf.Min(item.m_stack, Mathf.FloorToInt(h.CargoWeightRoom / unit));
            if (fits <= 0 || h.CargoInventory == null)
            {
                _unreachable.Add(drop); // too heavy for what's left: leave it
                _drop = null;
                return;
            }
            if (fits < item.m_stack)
            {
                item.m_stack = fits;
                if (h.CargoInventory.AddItem(item))
                {
                    drop.m_itemData.m_stack -= fits;
                    drop.Save();
                    h.OnPickedUp();
                }
                _unreachable.Add(drop);
            }
            else if (h.CargoInventory.AddItem(item))
            {
                h.OnPickedUp();
                ZNetScene.instance.Destroy(drop.gameObject);
                _unreachable.Add(drop); // destroyed at the end of the frame: don't pick it again meanwhile
            }
            else
            {
                _unreachable.Add(drop); // no slot for it
            }
            VfhLog.D(LogCat.Follow, "loot.pickup", ("hid", h.Hid), ("item", GearApplier.Name(item)), ("n", fits));
            _drop = null;
            _scanAt = 0f;
        }
    }
}
