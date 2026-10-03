using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Board;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings.Work
{
    /// <summary>Decides whether a hireling should go and empty its cargo now.</summary>
    internal interface IDeliveryPolicy
    {
        bool NeedsDelivery(Hireling h);
    }

    /// <summary>Gatherers deliver when full, when there's nothing left to harvest, or after carrying for 10 minutes.</summary>
    internal sealed class GathererDeliveryPolicy : IDeliveryPolicy
    {
        private const float MaxCarrySeconds = 600f;

        public bool NeedsDelivery(Hireling h) =>
            h.CargoFull || h.NoTargets || (h.CarryingSince > 0f && Time.time - h.CarryingSince > MaxCarrySeconds);
    }

    /// <summary>Jobs that don't gather only deliver when asked to (released followers, returning hirelings).</summary>
    internal sealed class OnRequestDeliveryPolicy : IDeliveryPolicy
    {
        public bool NeedsDelivery(Hireling h) => false;
    }

    /// <summary>
    /// Empties the cargo item type by item type: each goes only to chests in the radius that already hold it, nearest
    /// first; what doesn't fit is dropped in front of the board. The plan is redone at each chest, so chests that
    /// changed meanwhile (a player, AzuAutoStore) are handled.
    /// </summary>
    internal sealed class DeliverBehaviour : IHirelingBehaviour
    {
        private const float Reach = 2.5f;
        private const float ReplanSeconds = 3f;

        private readonly IDeliveryPolicy _policy;
        private DepositPlan? _plan;
        private Dictionary<string, Container> _chests = new();
        private float _planAt = -999f;
        private bool _active;

        public DeliverBehaviour(IDeliveryPolicy policy) => _policy = policy;

        public string Name => "Deliver";
        public int Priority => 300;

        public bool Wants(HirelingAI ai)
        {
            Hireling h = ai.Hireling;
            bool want = h.Mode == HirelingMode.Working && h.CargoInventory != null && h.CargoInventory.NrOfItems() > 0 &&
                        (_active || h.DeliverPending || _policy.NeedsDelivery(h));
            if (want && !_active)
            {
                _active = true;
                _plan = null;
                VfhLog.D(LogCat.Deliver, "deliver.start", ("hid", h.Hid), ("full", h.CargoFull), ("noTargets", h.NoTargets), ("requested", h.DeliverPending),
                    ("cargo", Cargo(h)));
            }
            if (!want && _active)
                Finish(h, "nothing left");
            return want;
        }

        public void Tick(HirelingAI ai, float dt)
        {
            Hireling h = ai.Hireling;
            h.SetActivity("$vfh_status_delivering");
            if (_plan == null || Time.time - _planAt > ReplanSeconds)
                Plan(h);

            DepositStep? next = NearestStep(h);
            if (next is DepositStep step && _chests.TryGetValue(step.ChestId, out Container chest) && chest != null)
            {
                if (Vector3.Distance(ai.transform.position, chest.transform.position) > Reach)
                {
                    ai.WalkTo(dt, chest.transform.position, Reach * 0.8f, run: false);
                    return;
                }
                ai.Halt();
                foreach (DepositStep s in _plan!.Steps.Where(s => s.ChestId == step.ChestId))
                    ContainerAccess.Deposit(chest, h.CargoInventory!, s.Prefab, s.Amount, h.Hid);
                _plan = null; // re-plan from what's left
                return;
            }

            // Nothing fits in any chest: leave it in front of the board (or here if the board isn't loaded).
            HiringBoard? board = HiringBoard.Loaded.Find(b => b != null && b.Id == h.BoardId);
            Vector3 pile = board != null ? DropPile.Position(board) : ai.transform.position;
            if (board != null && Vector3.Distance(ai.transform.position, pile) > Reach)
            {
                h.SetActivity("$vfh_status_dropping_at_board");
                ai.WalkTo(dt, pile, Reach * 0.8f, run: false);
                return;
            }
            DropPile.DropAll(h.CargoInventory!, pile, "no chest has room", h.Hid);
            Finish(h, "dropped the rest");
        }

        private void Plan(Hireling h)
        {
            _planAt = Time.time;
            List<Container> chests = ChestFinder.Find(h.Home, h.Radius);
            _chests = new Dictionary<string, Container>();
            var infos = new List<ChestInfo>();
            foreach (Container c in chests)
            {
                string id = c.GetInstanceID().ToString();
                _chests[id] = c;
                Inventory inv = c.GetInventory();
                var stacks = inv.GetAllItems().Where(i => i.m_dropPrefab != null).Select(i => (i.m_dropPrefab.name, i.m_stack)).ToList();
                infos.Add(new ChestInfo(id, Vector3.Distance(h.transform.position, c.transform.position), stacks, inv.GetWidth() * inv.GetHeight() - inv.NrOfItems()));
            }
            Dictionary<string, int> cargo = h.CargoInventory!.GetAllItems().Where(i => i.m_dropPrefab != null)
                .GroupBy(i => i.m_dropPrefab.name).ToDictionary(g => g.Key, g => g.Sum(i => i.m_stack));
            _plan = DepositPlanner.Plan(cargo, infos, prefab => ObjectDB.instance.GetItemPrefab(prefab)?.GetComponent<ItemDrop>().m_itemData.m_shared.m_maxStackSize ?? 1);
            VfhLog.D(LogCat.Deliver, "deliver.plan", ("hid", h.Hid), ("chests", chests.Count), ("steps", string.Join(",", _plan.Steps.Select(s => $"{s.Prefab}x{s.Amount}"))),
                ("toPile", string.Join(",", _plan.ToDropPile.Select(p => $"{p.Key}x{p.Value}"))));
        }

        private DepositStep? NearestStep(Hireling h)
        {
            if (_plan == null || _plan.Steps.Count == 0)
                return null;
            return _plan.Steps.Where(s => _chests.TryGetValue(s.ChestId, out Container c) && c != null && !c.IsInUse())
                .OrderBy(s => Vector3.Distance(h.transform.position, _chests[s.ChestId].transform.position))
                .Cast<DepositStep?>().FirstOrDefault();
        }

        private void Finish(Hireling h, string why)
        {
            _active = false;
            _plan = null;
            h.OnDelivered();
            VfhLog.D(LogCat.Deliver, "deliver.done", ("hid", h.Hid), ("why", why), ("left", Cargo(h)));
        }

        private static string Cargo(Hireling h) =>
            h.CargoInventory == null ? "" : string.Join(",", h.CargoInventory.GetAllItems().Select(i => $"{GearApplier.Name(i)}x{i.m_stack}"));
    }
}
