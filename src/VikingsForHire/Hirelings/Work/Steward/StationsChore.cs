using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Compat;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Chores;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

using VikingsForHire.Hirelings.Work.Chores;

namespace VikingsForHire.Hirelings.Work.Steward
{
    /// <summary>
    /// Keeps the Steward's stations of one kind (Stations: smelters, kilns, furnaces, refineries; Mills: windmills,
    /// spinning wheels) stocked from chests and, without AzuAutoStore, emptied. Each station is gated by its own level.
    /// A job is one step, as the old smelter behaviour did it: fetch from a chest, load one station, or collect output;
    /// every survey plans afresh from the stations' live state (SmelterPlanner), so outside changes never confuse it.
    /// </summary>
    internal sealed class StationsChore : IChore
    {
        private enum Step { None, Fetch, Load, Collect }

        private const int VariantLoad = 0;
        private const int VariantCollect = 1;
        private const float ItemSeconds = 0.25f;
        private const float StuckSeconds = 20f;
        private const float OutputPickupRadius = 4f;
        private const float ChestSkipSeconds = 60f;
        private const float SettleSeconds = 2f;

        private readonly WorkSteps _walk = new();
        private readonly HashSet<Smelter> _claimed = new();
        private readonly List<LoadTask> _loads = new();
        private Step _step;
        private SmelterPlan? _plan;
        private Dictionary<string, Smelter> _byId = new();
        private Container? _chest;
        private Dictionary<string, int> _fetch = new();
        private Smelter? _station;
        private float _nextItemAt;
        private int _loadedOfTask;
        private bool _emptied;
        private Hireling? _h;

        public StationsChore(ChoreKind kind) => Kind = kind;

        public ChoreKind Kind { get; }
        public string? Missing { get; private set; }
        public float RestAfter { get; private set; }

        /// <summary>This kind's stations in the radius that a Steward of this level may tend.</summary>
        private List<Smelter> Stations(WorkContext ctx) =>
            StationSurvey.Find(ctx.Home, ctx.Radius)
                .Where(s => ChoreRules.KindOfStation(Utils.GetPrefabName(s.gameObject)) == Kind &&
                            ChoreRules.Unlocked(ctx.Job, ctx.Level, Utils.GetPrefabName(s.gameObject)) &&
                            !Reservations.IsReservedByOther(s, ctx.Hireling.Hid) && !Reservations.IsSkipped(s))
                .ToList();

        public IEnumerable<ChoreJob> Candidates(WorkContext ctx)
        {
            Missing = null;
            Hireling h = ctx.Hireling;
            List<Smelter> stations = Stations(ctx);
            _byId = stations.ToDictionary(StationSurvey.Id);
            List<StationState> states = stations.Select(s => StationSurvey.State(s, ctx.Position)).ToList();
            // PauseWhenStorageFull: inputs whose product has no room left aren't loaded (a kiln stops when the coal chests are full).
            string? paused = null;
            foreach (StationState st in states)
            {
                Smelter s = _byId[st.Id];
                var noRoom = st.Inputs.Where(i => StorageRoom.NoRoom(ctx.AllChests, ProductOf(s, i))).ToList();
                if (noRoom.Count == 0)
                    continue;
                st.Inputs = st.Inputs.Except(noRoom).ToList();
                paused ??= ActivityText.Make("$vfh_paused_full", s.m_name, WorkSteps.SharedName(ProductOf(s, noRoom[0])));
            }
            var wanted = new HashSet<string>(states.SelectMany(s => s.Inputs).Concat(states.Select(s => s.FuelItem)).Where(p => p.Length > 0));
            Dictionary<string, int> stock = wanted.ToDictionary(p => p, ctx.Available);
            float threshold = VfhConfig.SmelterRefillThreshold.Value;
            _plan = SmelterPlanner.Plan(states, stock, ctx.Carried, threshold, ctx.FreeSlots, WorkSteps.MaxStack);
            VfhLog.D(LogCat.Smelter, "smelter.plan", ("hid", h.Hid), ("kind", Kind), ("stations", states.Count), ("chests", ctx.Chests.Count),
                ("loads", string.Join(",", _plan.Loads.Select(l => $"{_byId[l.StationId].name.Replace("(Clone)", "")}:{l.Prefab}x{l.Amount}"))),
                ("fetch", string.Join(",", _plan.Fetch.Select(f => $"{f.Key}x{f.Value}"))), ("starved", _plan.Starved.Count));

            // Let go of stations this plan no longer serves, so another Steward can take them.
            var serving = new HashSet<Smelter>(_plan.Loads.Select(l => _byId[l.StationId]));
            foreach (Smelter s in _claimed.Where(s => !serving.Contains(s)).ToList())
            {
                Reservations.Release(s, h.Hid);
                _claimed.Remove(s);
            }

            var jobs = new List<ChoreJob>();
            if (!_plan.Idle)
            {
                Smelter first = _byId[_plan.Loads[0].StationId];
                StationState st = states.First(s => s.Id == _plan.Loads[0].StationId);
                jobs.Add(Job(first, ChoreUrgency.Station(st.OreRatio, st.FuelRatio, false, threshold), VariantLoad, ctx, "$vfh_steward_load"));
            }
            // With AzuAutoStore, Azu picks up what stations drop, but a station that holds its output inside (spinning
            // wheel, eitr refinery) still needs emptying; then Azu takes it from there.
            if ((AzuAutoStoreCompat.IsLoaded || ctx.FreeSlots > 0) && stations.FirstOrDefault(s => OutputWaiting(s, AzuAutoStoreCompat.IsLoaded) && !OutputsFull(s, ctx)) is Smelter full)
                jobs.Add(Job(full, ChoreUrgency.Station(1f, 1f, true, threshold), VariantCollect, ctx, "$vfh_status_collecting"));

            if (jobs.Count == 0 && paused != null)
                Missing = paused;
            else if (jobs.Count == 0 && _plan.Starved.Count > 0 && _byId.TryGetValue(_plan.Starved[0], out Smelter starved))
            {
                StationState st = states.First(s => s.Id == _plan.Starved[0]);
                int Have(string p) => ctx.Available(p) + (ctx.Carried.TryGetValue(p, out int c) ? c : 0);
                // Ore there (in a chest or already queued) but no fuel: say the fuel; else the first input.
                bool hasInput = st.Queue > 0 || st.Inputs.Any(i => Have(i) > 0);
                string need = hasInput && st.FuelItem.Length > 0 ? st.FuelItem : st.Inputs.FirstOrDefault() ?? st.FuelItem;
                Missing = ActivityText.Make("$vfh_need_item", starved.m_name, WorkSteps.SharedName(need));
            }
            return jobs;
        }

        // PauseWhenStorageFull: nowhere to put anything it makes, so what it holds stays in it.
        private static bool OutputsFull(Smelter s, WorkContext ctx) =>
            StationSurvey.Outputs(s).All(o => StorageRoom.NoRoom(ctx.AllChests, o));

        private static string ProductOf(Smelter s, string input) =>
            s.m_conversion.FirstOrDefault(c => c.m_from != null && c.m_from.gameObject.name == input && c.m_to != null)?.m_to.gameObject.name ?? "";

        private ChoreJob Job(Smelter s, float urgency, int variant, WorkContext ctx, string label) => new()
        {
            Kind = Kind, Target = s, Urgency = urgency, Variant = variant,
            Score = ChoreUrgency.Score(urgency, Vector3.Distance(ctx.Position, s.transform.position), ctx.Radius),
            Label = ActivityText.Make(label, s.m_name),
        };

        public void Begin(ChoreJob job, WorkContext ctx)
        {
            Hireling h = ctx.Hireling;
            _h = h;
            RestAfter = 0f;
            _walk.Reset();
            _nextItemAt = 0f;
            if (job.Variant == VariantCollect)
            {
                _step = Step.Collect;
                _station = (Smelter)job.Target;
                _emptied = false;
                return;
            }
            SmelterPlan plan = _plan!;
            h.LeftoverSince = 0f; // loading: what it carries is wanted after all
            // Claim the stations this plan serves, so a second Steward picks others.
            var serving = new HashSet<Smelter>(plan.Loads.Select(l => _byId[l.StationId]));
            foreach (Smelter s in serving)
                if (Reservations.TryReserve(s, h.Hid))
                    _claimed.Add(s);

            if (plan.Fetch.Count > 0 && ctx.NearestChestWith(plan.Fetch.Keys) is Container chest)
            {
                _step = Step.Fetch;
                _chest = chest;
                _fetch = plan.Fetch.ToDictionary(f => f.Key, f => f.Value);
                h.SetActivity("$vfh_status_fetching");
                return;
            }
            LoadTask first = plan.Loads[0];
            _step = Step.Load;
            _station = _byId[first.StationId];
            _loads.Clear();
            // Ore before fuel, never more than the station has room for right now.
            StationState live = StationSurvey.State(_station, h.transform.position);
            foreach (LoadTask t in plan.Loads.Where(l => l.StationId == first.StationId).OrderBy(l => l.IsFuel))
            {
                int room = t.IsFuel ? live.FuelSpace : live.OreSpace;
                int have = h.CargoInventory!.CountItems(WorkSteps.SharedName(t.Prefab));
                int n = Mathf.Min(t.Amount, room, have);
                if (n > 0)
                    _loads.Add(new LoadTask(t.StationId, t.Prefab, n, t.IsFuel));
            }
            if (_loads.Count == 0)
                _step = Step.None;
        }

        public ChoreProgress Tick(HirelingAI ai, float dt)
        {
            Hireling h = ai.Hireling;
            if (_walk.SinceProgress > StuckSeconds)
            {
                VfhLog.I(LogCat.Smelter, "smelter.stuck", ("hid", h.Hid), ("step", _step), ("station", _station != null ? Utils.GetPrefabName(_station.gameObject) : ""),
                    ("chest", _chest != null ? _chest.transform.position.ToString() : ""), ("pos", h.transform.position));
                if (_chest != null)
                    Reservations.Skip(_chest, ChestSkipSeconds);
                return End(5f, ChoreProgress.Failed);
            }
            return _step switch
            {
                Step.Fetch => Fetch(ai, h, dt),
                Step.Load => Load(ai, h, dt),
                Step.Collect => Collect(ai, h, dt),
                _ => End(0f),
            };
        }

        private ChoreProgress Fetch(HirelingAI ai, Hireling h, float dt)
        {
            Container? chest = _chest;
            if (chest == null)
                return End(0f);
            if (!_walk.Approach(ai, dt, chest, chest.transform.position))
                return ChoreProgress.Running;
            ai.Halt();
            ai.Face(chest.transform.position);
            if (WorkSteps.TakeFromChest(chest, h, _fetch, VfhConfig.ChestReserve) == 0)
                Reservations.Skip(chest, ChestSkipSeconds); // in use, or nothing we can take after all
            return End(0f);
        }

        private ChoreProgress Load(HirelingAI ai, Hireling h, float dt)
        {
            Smelter? s = _station;
            if (s == null || s.m_nview == null || !s.m_nview.IsValid() || _loads.Count == 0)
                return End(0f);
            LoadTask task = _loads[0];
            Switch? sw = task.IsFuel ? s.m_addWoodSwitch : s.m_addOreSwitch;
            Vector3 at = sw != null ? sw.transform.position : s.transform.position;
            if (!_walk.Approach(ai, dt, s, at))
                return ChoreProgress.Running;
            ai.Halt();
            ai.Face(at);
            if (Time.time < _nextItemAt)
                return ChoreProgress.Running;
            _nextItemAt = Time.time + ItemSeconds;

            string shared = WorkSteps.SharedName(task.Prefab);
            if (h.CargoInventory!.CountItems(shared) <= 0)
            {
                _loads.RemoveAt(0);
                _loadedOfTask = 0;
                return _loads.Count == 0 ? End(SettleSeconds) : ChoreProgress.Running;
            }
            h.CargoInventory.RemoveItem(shared, 1);
            if (task.IsFuel)
                s.m_nview.InvokeRPC("RPC_AddFuel");
            else
                s.m_nview.InvokeRPC("RPC_AddOre", task.Prefab, false);
            _loadedOfTask++;
            int left = task.Amount - 1;
            if (left > 0)
            {
                _loads[0] = new LoadTask(task.StationId, task.Prefab, left, task.IsFuel);
                return ChoreProgress.Running;
            }
            _loads.RemoveAt(0);
            VfhLog.I(LogCat.Smelter, "smelter.loaded", ("hid", h.Hid), ("station", Utils.GetPrefabName(s.gameObject)), ("pos", s.transform.position),
                ("item", task.Prefab), ("fuel", task.IsFuel), ("n", _loadedOfTask));
            _loadedOfTask = 0;
            // Give the station's owner time to apply the RPCs before the next survey reads its fill, so a remote
            // station isn't topped up twice from a stale count (vanilla doesn't refuse ore past max).
            return _loads.Count == 0 ? End(SettleSeconds) : ChoreProgress.Running;
        }

        private ChoreProgress Collect(HirelingAI ai, Hireling h, float dt)
        {
            Smelter? s = _station;
            if (s == null || s.m_nview == null || !s.m_nview.IsValid())
                return End(0f);
            Vector3 at = !_emptied && s.m_emptyOreSwitch != null && StationSurvey.ProcessedWaiting(s) > 0
                ? s.m_emptyOreSwitch.transform.position
                : s.m_outputPoint != null ? s.m_outputPoint.position : s.transform.position;
            if (!_walk.Approach(ai, dt, s, at))
                return ChoreProgress.Running;
            ai.Halt();
            ai.Face(at);
            if (!_emptied && StationSurvey.ProcessedWaiting(s) > 0)
            {
                s.m_nview.InvokeRPC("RPC_EmptyProcessed");
                _emptied = true;
                _nextItemAt = Time.time + 0.6f; // let the owner spawn the stack
                return ChoreProgress.Running;
            }
            if (Time.time < _nextItemAt)
                return ChoreProgress.Running;
            if (AzuAutoStoreCompat.IsLoaded)
                return End(0f); // emptied: AzuAutoStore puts the stack away
            Vector3 point = s.m_outputPoint != null ? s.m_outputPoint.position : s.transform.position;
            int picked = WorkSteps.PickUpDrops(h, point, StationSurvey.Outputs(s), OutputPickupRadius);
            VfhLog.I(LogCat.Smelter, "smelter.collected", ("hid", h.Hid), ("station", Utils.GetPrefabName(s.gameObject)), ("n", picked));
            return End(0f);
        }

        // Output to collect: held inside (needs emptying) or, unless AzuAutoStore picks drops up, lying at the output point.
        private static bool OutputWaiting(Smelter s, bool azu)
        {
            if (StationSurvey.ProcessedWaiting(s) > 0 && s.m_emptyOreSwitch != null)
                return true;
            if (azu)
                return false;
            HashSet<string> outputs = StationSurvey.Outputs(s);
            Vector3 at = s.m_outputPoint != null ? s.m_outputPoint.position : s.transform.position;
            return ItemDrop.s_instances.Any(d => d != null && d.m_nview != null && d.m_nview.IsValid() && d.m_itemData?.m_dropPrefab != null &&
                                                 outputs.Contains(d.m_itemData.m_dropPrefab.name) && !d.m_itemData.m_customData.ContainsKey(DropPile.Tag) &&
                                                 Vector3.Distance(d.transform.position, at) < OutputPickupRadius);
        }

        private ChoreProgress End(float rest, ChoreProgress result = ChoreProgress.Done)
        {
            _step = Step.None;
            _chest = null;
            _station = null;
            _loads.Clear();
            _loadedOfTask = 0;
            RestAfter = rest;
            return result;
        }

        public void Abort(Hireling h)
        {
            foreach (Smelter s in _claimed)
                Reservations.Release(s, h.Hid);
            _claimed.Clear();
            End(0f);
        }
    }
}
