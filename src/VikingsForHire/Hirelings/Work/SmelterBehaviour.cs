using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Compat;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings.Work
{
    /// <summary>
    /// Keeps smelters, kilns and furnaces in the work radius stocked from chests. Each cycle it surveys the stations'
    /// live state and plans afresh (SmelterPlanner), then does one step: fetch from a chest, load one station, or, when
    /// AzuAutoStore isn't installed, collect finished output. Nothing is remembered between cycles except which
    /// stations it has claimed, so two smelters split the stations and outside changes (players, Azu) never confuse it.
    /// </summary>
    internal sealed class SmelterBehaviour : IHirelingBehaviour
    {
        private enum Step { None, Fetch, Load, Collect }

        private const float Reach = 2.2f;
        private const float ItemSeconds = 0.25f;
        private const float IdleRescan = 30f;
        private const float StepGiveUp = 45f;
        private const float OutputPickupRadius = 4f;
        private const float ChestSkipSeconds = 60f;
        private const float SettleSeconds = 2f;

        private Step _step;
        private float _nextSurvey;
        private float _stepStarted;
        private float _nextItemAt;
        private Container? _chest;
        private Dictionary<string, int> _fetch = new();
        private Smelter? _station;
        private readonly List<LoadTask> _loads = new();
        private readonly HashSet<Smelter> _claimed = new();
        private bool _emptied;

        public string Name => "Smelter";
        public int Priority => 200;

        public bool Wants(HirelingAI ai)
        {
            Hireling h = ai.Hireling;
            if (h.Mode != HirelingMode.Working)
            {
                Reset(h, "not working");
                return false;
            }
            if (_step != Step.None)
                return true;
            if (Time.time < _nextSurvey)
                return false;
            Decide(h);
            return _step != Step.None;
        }

        public void Tick(HirelingAI ai, float dt)
        {
            Hireling h = ai.Hireling;
            if (Time.time - _stepStarted > StepGiveUp)
            {
                VfhLog.D(LogCat.Smelter, "smelter.step_timeout", ("hid", h.Hid), ("step", _step), ("station", _station != null ? _station.name : ""));
                End(h, ChestSkipSeconds);
                return;
            }
            switch (_step)
            {
                case Step.Fetch:
                    Fetch(ai, h, dt);
                    break;
                case Step.Load:
                    Load(ai, h, dt);
                    break;
                case Step.Collect:
                    Collect(ai, h, dt);
                    break;
                default:
                    End(h, 0f);
                    break;
            }
        }

        private void Decide(Hireling h)
        {
            Inventory cargo = h.CargoInventory!;
            List<Smelter> stations = StationSurvey.Find(h.Home, h.Radius)
                .Where(s => !Reservations.IsReservedByOther(s, h.Hid)).ToList();
            var byId = stations.ToDictionary(StationSurvey.Id);
            List<StationState> states = stations.Select(s => StationSurvey.State(s, h.transform.position)).ToList();

            var wanted = new HashSet<string>(states.SelectMany(s => s.Inputs).Concat(states.Select(s => s.FuelItem)).Where(p => p.Length > 0));
            int keepMin = VfhConfig.KeepMinimumInChest.Value;
            List<Container> chests = ChestFinder.Find(h.Home, h.Radius).Where(c => !Reservations.IsSkipped(c)).ToList();
            var stock = new Dictionary<string, int>();
            foreach (Container c in chests)
                foreach (var g in c.GetInventory().GetAllItems().Where(i => i.m_dropPrefab != null && wanted.Contains(i.m_dropPrefab.name)).GroupBy(i => i.m_dropPrefab.name))
                    stock[g.Key] = (stock.TryGetValue(g.Key, out int n) ? n : 0) + System.Math.Max(0, g.Sum(i => i.m_stack) - keepMin);
            Dictionary<string, int> carried = cargo.GetAllItems().Where(i => i.m_dropPrefab != null)
                .GroupBy(i => i.m_dropPrefab.name).ToDictionary(g => g.Key, g => g.Sum(i => i.m_stack));
            int freeSlots = System.Math.Max(0, h.CargoSlots - cargo.NrOfItems());

            SmelterPlan plan = SmelterPlanner.Plan(states, stock, carried, VfhConfig.SmelterRefillThreshold.Value, freeSlots, MaxStack);
            VfhLog.D(LogCat.Smelter, "smelter.plan", ("hid", h.Hid), ("stations", states.Count), ("chests", chests.Count),
                ("loads", string.Join(",", plan.Loads.Select(l => $"{byId[l.StationId].name.Replace("(Clone)", "")}:{l.Prefab}x{l.Amount}"))),
                ("fetch", string.Join(",", plan.Fetch.Select(f => $"{f.Key}x{f.Value}"))), ("starved", plan.Starved.Count));

            // Claim the stations this plan serves, so a second smelter picks others.
            var serving = new HashSet<Smelter>(plan.Loads.Select(l => byId[l.StationId]));
            foreach (Smelter s in _claimed.Where(s => !serving.Contains(s)).ToList())
            {
                Reservations.Release(s, h.Hid);
                _claimed.Remove(s);
            }
            foreach (Smelter s in serving)
                if (Reservations.TryReserve(s, h.Hid))
                    _claimed.Add(s);

            if (!plan.Idle)
            {
                h.LeftoverSince = 0f;
                if (plan.Fetch.Count > 0 && NearestChestWith(h, chests, plan.Fetch.Keys, keepMin) is Container chest)
                {
                    Begin(Step.Fetch, h, "$vfh_status_fetching");
                    _chest = chest;
                    _fetch = plan.Fetch.ToDictionary(f => f.Key, f => f.Value);
                    return;
                }
                LoadTask first = plan.Loads[0];
                Begin(Step.Load, h, "$vfh_status_loading");
                _station = byId[first.StationId];
                _loads.Clear();
                // Ore before fuel, never more than the station has room for right now.
                StationState live = StationSurvey.State(_station, h.transform.position);
                foreach (LoadTask t in plan.Loads.Where(l => l.StationId == first.StationId).OrderBy(l => l.IsFuel))
                {
                    int room = t.IsFuel ? live.FuelSpace : live.OreSpace;
                    int have = cargo.CountItems(SharedName(t.Prefab));
                    int n = Mathf.Min(t.Amount, room, have);
                    if (n > 0)
                        _loads.Add(new LoadTask(t.StationId, t.Prefab, n, t.IsFuel));
                }
                if (_loads.Count == 0)
                    End(h, 0f);
                return;
            }

            if (!AzuAutoStoreCompat.IsLoaded && cargo.NrOfItems() < h.CargoSlots && stations.FirstOrDefault(OutputWaiting) is Smelter full)
            {
                Begin(Step.Collect, h, "$vfh_status_collecting");
                _station = full;
                _emptied = false;
                return;
            }

            // Nothing to do: idle near the board and look again in a while.
            bool leftovers = cargo.GetAllItems().Any(i => i.m_dropPrefab != null && wanted.Contains(i.m_dropPrefab.name));
            if (leftovers && h.LeftoverSince <= 0f)
                h.LeftoverSince = Time.time;
            else if (!leftovers)
                h.LeftoverSince = 0f;
            h.SetActivity(plan.Starved.Count > 0 ? "$vfh_status_no_input" : "$vfh_status_no_work");
            VfhLog.D(LogCat.Smelter, "smelter.idle", ("hid", h.Hid), ("stations", states.Count), ("starved", plan.Starved.Count), ("leftovers", leftovers));
            _nextSurvey = Time.time + IdleRescan;
        }

        private void Fetch(HirelingAI ai, Hireling h, float dt)
        {
            Container? chest = _chest;
            if (chest == null)
            {
                End(h, 0f);
                return;
            }
            if (Vector3.Distance(ai.transform.position, chest.transform.position) > Reach)
            {
                ai.WalkTo(dt, chest.transform.position, Reach * 0.8f, run: false);
                return;
            }
            ai.Halt();
            ai.Face(chest.transform.position);
            int keepMin = VfhConfig.KeepMinimumInChest.Value;
            int moved = 0;
            foreach (var f in _fetch.ToList())
                moved += ContainerAccess.Take(chest, h.CargoInventory!, f.Key, f.Value, keepMin, h.Hid);
            if (moved == 0)
                Reservations.Skip(chest, ChestSkipSeconds); // in use, or nothing we can take after all
            End(h, 0f);
        }

        private void Load(HirelingAI ai, Hireling h, float dt)
        {
            Smelter? s = _station;
            if (s == null || s.m_nview == null || !s.m_nview.IsValid() || _loads.Count == 0)
            {
                End(h, 0f);
                return;
            }
            LoadTask task = _loads[0];
            Switch? sw = task.IsFuel ? s.m_addWoodSwitch : s.m_addOreSwitch;
            Vector3 at = sw != null ? sw.transform.position : s.transform.position;
            if (Utils.DistanceXZ(ai.transform.position, at) > Reach)
            {
                ai.WalkTo(dt, at, Reach * 0.8f, run: false);
                return;
            }
            ai.Halt();
            ai.Face(at);
            if (Time.time < _nextItemAt)
                return;
            _nextItemAt = Time.time + ItemSeconds;

            string shared = SharedName(task.Prefab);
            if (h.CargoInventory!.CountItems(shared) <= 0)
            {
                _loads.RemoveAt(0);
                return;
            }
            h.CargoInventory.RemoveItem(shared, 1);
            if (task.IsFuel)
                s.m_nview.InvokeRPC("RPC_AddFuel");
            else
                s.m_nview.InvokeRPC("RPC_AddOre", task.Prefab, false);
            int left = task.Amount - 1;
            if (left > 0)
            {
                _loads[0] = new LoadTask(task.StationId, task.Prefab, left, task.IsFuel);
                return;
            }
            _loads.RemoveAt(0);
            VfhLog.I(LogCat.Smelter, "smelter.loaded", ("hid", h.Hid), ("station", Utils.GetPrefabName(s.gameObject)), ("pos", s.transform.position),
                ("item", task.Prefab), ("fuel", task.IsFuel), ("n", task.Amount - left));
            // Give the station's owner time to apply the RPCs before the next survey reads its fill, so a remote
            // station isn't topped up twice from a stale count (vanilla doesn't refuse ore past max).
            if (_loads.Count == 0)
                End(h, SettleSeconds);
        }

        private void Collect(HirelingAI ai, Hireling h, float dt)
        {
            Smelter? s = _station;
            if (s == null || s.m_nview == null || !s.m_nview.IsValid())
            {
                End(h, 0f);
                return;
            }
            Vector3 at = !_emptied && s.m_emptyOreSwitch != null && StationSurvey.ProcessedWaiting(s) > 0
                ? s.m_emptyOreSwitch.transform.position
                : s.m_outputPoint != null ? s.m_outputPoint.position : s.transform.position;
            if (Utils.DistanceXZ(ai.transform.position, at) > Reach)
            {
                ai.WalkTo(dt, at, Reach * 0.8f, run: false);
                return;
            }
            ai.Halt();
            ai.Face(at);
            if (!_emptied && StationSurvey.ProcessedWaiting(s) > 0)
            {
                s.m_nview.InvokeRPC("RPC_EmptyProcessed");
                _emptied = true;
                _nextItemAt = Time.time + 0.6f; // let the owner spawn the stack
                return;
            }
            if (Time.time < _nextItemAt)
                return;
            int picked = 0;
            foreach (ItemDrop d in OutputDrops(s).ToList())
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
            VfhLog.I(LogCat.Smelter, "smelter.collected", ("hid", h.Hid), ("station", Utils.GetPrefabName(s.gameObject)), ("n", picked));
            End(h, 0f);
        }

        private static bool OutputWaiting(Smelter s) => StationSurvey.ProcessedWaiting(s) > 0 && s.m_emptyOreSwitch != null || OutputDrops(s).Any();

        private static IEnumerable<ItemDrop> OutputDrops(Smelter s)
        {
            HashSet<string> outputs = StationSurvey.Outputs(s);
            Vector3 at = s.m_outputPoint != null ? s.m_outputPoint.position : s.transform.position;
            return ItemDrop.s_instances.Where(d => d != null && d.m_nview != null && d.m_nview.IsValid() && d.m_itemData?.m_dropPrefab != null &&
                                                   outputs.Contains(d.m_itemData.m_dropPrefab.name) && !d.m_itemData.m_customData.ContainsKey(DropPile.Tag) &&
                                                   Vector3.Distance(d.transform.position, at) < OutputPickupRadius);
        }

        private static Container? NearestChestWith(Hireling h, List<Container> chests, IEnumerable<string> prefabs, int keepMin)
        {
            var want = new HashSet<string>(prefabs);
            return chests.Where(c => !c.IsInUse() && c.GetInventory().GetAllItems()
                    .Where(i => i.m_dropPrefab != null && want.Contains(i.m_dropPrefab.name))
                    .GroupBy(i => i.m_dropPrefab.name).Any(g => g.Sum(i => i.m_stack) > keepMin))
                .OrderBy(c => Vector3.Distance(h.transform.position, c.transform.position)).FirstOrDefault();
        }

        private void Begin(Step step, Hireling h, string status)
        {
            _step = step;
            _stepStarted = Time.time;
            _nextItemAt = 0f;
            h.SetActivity(status);
            VfhLog.D(LogCat.Smelter, "smelter.step", ("hid", h.Hid), ("step", step));
        }

        // Step finished: survey again right away (or after a pause when it was given up on).
        private void End(Hireling h, float pause)
        {
            _step = Step.None;
            _chest = null;
            _station = null;
            _loads.Clear();
            _nextSurvey = Time.time + pause;
        }

        private void Reset(Hireling h, string why)
        {
            if (_step == Step.None && _claimed.Count == 0)
                return;
            foreach (Smelter s in _claimed)
                Reservations.Release(s, h.Hid);
            _claimed.Clear();
            End(h, 0f);
        }

        private static int MaxStack(string prefab) =>
            ObjectDB.instance.GetItemPrefab(prefab)?.GetComponent<ItemDrop>().m_itemData.m_shared.m_maxStackSize ?? 1;

        private static string SharedName(string prefab) =>
            ObjectDB.instance.GetItemPrefab(prefab)?.GetComponent<ItemDrop>().m_itemData.m_shared.m_name ?? prefab;
    }
}
