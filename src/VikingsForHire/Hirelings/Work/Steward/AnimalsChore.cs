using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Config;
using VikingsForHire.Core.Chores;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

using VikingsForHire.Hirelings.Work.Chores;

namespace VikingsForHire.Hirelings.Work.Steward
{
    /// <summary>
    /// Feeds hungry tamed animals in the work radius: tamed animals only eat food lying on the ground (vanilla
    /// MonsterAI looks for it), so the Steward fetches something each one eats from chests and drops it right in front of
    /// it. Food it drops is marked so AzuAutoStore leaves it alone. Not done with PetPantry (animals eat from chests).
    /// </summary>
    internal sealed class AnimalsChore : IChore
    {
        public const string FeedKey = "vfh_feed";

        private enum Step { None, Fetch, Feed }

        private const float StuckSeconds = 25f;
        private const float EatSeconds = 15f;
        private const float AnimalSkipSeconds = 300f;
        private const float ChestSkipSeconds = 60f;

        /// <summary>Food Stewards have dropped for each animal since load, by its instance id (for the tests).</summary>
        public static readonly Dictionary<int, int> Fed = new();

        private readonly WorkSteps _walk = new();
        private readonly List<(Character Animal, string Food)> _route = new();
        private Step _step;
        private Dictionary<string, int> _fetch = new();
        private Container? _chest;
        private ItemDrop? _dropped;
        private float _droppedAt;

        public ChoreKind Kind => ChoreKind.Animals;
        public string? Missing { get; private set; }
        public float RestAfter { get; private set; }

        private static IEnumerable<(Character Animal, string[] Foods)> Hungry(WorkContext ctx)
        {
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c.IsPlayer() || !c.IsTamed() || c.IsDead() || c.GetComponent<Hireling>() != null)
                    continue;
                if (Utils.DistanceXZ(c.transform.position, ctx.Home) > ctx.Radius || !ctx.BoardOwnerMayUse(c.transform.position))
                    continue;
                Tameable? t = c.GetComponent<Tameable>();
                MonsterAI? ai = c.GetComponent<MonsterAI>();
                if (t == null || ai == null || ai.m_consumeItems == null || ai.m_consumeItems.Count == 0 || !t.IsHungry())
                    continue;
                yield return (c, ai.m_consumeItems.Where(i => i != null).Select(i => i.gameObject.name).ToArray());
            }
        }

        private static string? FoodFor(WorkContext ctx, string[] foods) =>
            foods.FirstOrDefault(f => (ctx.Carried.TryGetValue(f, out int c) ? c : 0) > 0) ?? foods.FirstOrDefault(f => ctx.Available(f) > 0);

        public IEnumerable<ChoreJob> Candidates(WorkContext ctx)
        {
            Missing = null;
            var jobs = new List<ChoreJob>();
            foreach ((Character animal, string[] foods) in Hungry(ctx))
            {
                if (Reservations.IsReservedByOther(animal, ctx.Hireling.Hid) || Reservations.IsSkipped(animal))
                    continue;
                if (FoodFor(ctx, foods) == null)
                {
                    Missing ??= ActivityText.Make("$vfh_need_food", animal.m_name);
                    continue;
                }
                float u = ChoreUrgency.Animal(true);
                jobs.Add(new ChoreJob
                {
                    Kind = Kind, Target = animal, Urgency = u,
                    Score = ChoreUrgency.Score(u, Vector3.Distance(ctx.Position, animal.transform.position), ctx.Radius),
                    Label = ActivityText.Make("$vfh_steward_feed", animal.m_name),
                });
            }
            return jobs;
        }

        public void Begin(ChoreJob job, WorkContext ctx)
        {
            Hireling h = ctx.Hireling;
            RestAfter = 0f;
            _walk.Reset();
            _route.Clear();
            _dropped = null;
            var first = (Character)job.Target;
            // Every hungry animal it can feed this trip, nearest first, one food item each, as far as cargo allows.
            var need = new Dictionary<string, int>();
            int slots = ctx.FreeSlots;
            foreach ((Character animal, string[] foods) in Hungry(ctx).OrderBy(a => a.Animal == first ? -1f : Vector3.Distance(first.transform.position, a.Animal.transform.position)))
            {
                if (Reservations.IsReservedByOther(animal, h.Hid) || Reservations.IsSkipped(animal) || FoodFor(ctx, foods) is not string food)
                    continue;
                int carried = ctx.Carried.TryGetValue(food, out int c) ? c : 0;
                bool newStack = !need.ContainsKey(food) && carried == 0;
                if (newStack && slots <= 0)
                    continue;
                if (newStack)
                    slots--;
                need[food] = (need.TryGetValue(food, out int n) ? n : 0) + 1;
                _route.Add((animal, food));
                Reservations.TryReserve(animal, h.Hid);
            }
            _fetch = need.Select(f => (f.Key, Fetch: Mathf.Max(0, f.Value - (ctx.Carried.TryGetValue(f.Key, out int c) ? c : 0))))
                .Where(f => f.Fetch > 0).ToDictionary(f => f.Key, f => f.Fetch);
            _chest = _fetch.Count > 0 ? ctx.NearestChestWith(_fetch.Keys) : null;
            _step = _chest != null ? Step.Fetch : Step.Feed;
            RestAfter = _fetch.Count > 0 && _chest == null ? 3f : 0f; // its chest is open: try again in a moment
        }

        public ChoreProgress Tick(HirelingAI ai, float dt)
        {
            Hireling h = ai.Hireling;
            if (_walk.SinceProgress > StuckSeconds)
            {
                VfhLog.I(LogCat.Smelter, "steward.feed_stuck", ("hid", h.Hid), ("step", _step), ("pos", h.transform.position));
                if (_route.Count > 0 && _route[0].Animal != null)
                    Reservations.Skip(_route[0].Animal, AnimalSkipSeconds);
                return End(h, 5f, ChoreProgress.Failed);
            }
            if (_step == Step.Fetch)
            {
                Container? chest = _chest;
                if (chest == null)
                    return End(h, 0f);
                if (!_walk.Approach(ai, dt, chest, chest.transform.position))
                    return ChoreProgress.Running;
                ai.Halt();
                ai.Face(chest.transform.position);
                if (WorkSteps.TakeFromChest(chest, h, _fetch, VfhConfig.ChestReserve) == 0)
                {
                    Reservations.Skip(chest, ChestSkipSeconds);
                    return End(h, 1f, ChoreProgress.Failed);
                }
                _step = Step.Feed;
                _walk.Reset();
                return ChoreProgress.Running;
            }
            return Feed(ai, h, dt);
        }

        private ChoreProgress Feed(HirelingAI ai, Hireling h, float dt)
        {
            while (_route.Count > 0 && (_route[0].Animal == null || _route[0].Animal.IsDead() || _dropped == null && !IsHungry(_route[0].Animal)))
                Next(h);
            if (_route.Count == 0)
                return End(h, 0f);
            (Character animal, string food) = _route[0];

            if (_dropped == null)
            {
                Vector3 front = animal.transform.position + animal.transform.forward * 0.8f;
                // Close enough to drop it at its feet (big animals keep you further from their middle).
                if (!_walk.Approach(ai, dt, animal, animal.transform.position))
                    return ChoreProgress.Running;
                ai.Halt();
                ai.Face(animal.transform.position);
                string shared = WorkSteps.SharedName(food);
                GameObject? prefab = ObjectDB.instance.GetItemPrefab(food);
                if (prefab == null || h.CargoInventory!.CountItems(shared) <= 0)
                {
                    Next(h);
                    return ChoreProgress.Running;
                }
                h.CargoInventory.RemoveItem(shared, 1);
                ItemDrop.ItemData item = prefab.GetComponent<ItemDrop>().m_itemData.Clone();
                item.m_stack = 1;
                item.m_dropPrefab = prefab;
                _dropped = ItemDrop.DropItem(item, 1, front + Vector3.up * 0.3f, Quaternion.identity);
                _dropped?.m_nview?.GetZDO()?.Set(FeedKey, true);
                _droppedAt = Time.time;
                int id = animal.GetInstanceID();
                Fed[id] = (Fed.TryGetValue(id, out int n) ? n : 0) + 1;
                VfhLog.D(LogCat.Smelter, "steward.feed", ("hid", h.Hid), ("animal", animal.m_name), ("item", food));
                return ChoreProgress.Running;
            }

            // Wait for it to eat; if it won't, take the food back and leave that animal for a while.
            ai.Halt();
            ai.Face(animal.transform.position);
            _walk.Reset(); // waiting counts as progress
            if (!IsHungry(animal) || _dropped == null)
            {
                VfhLog.D(LogCat.Smelter, "steward.fed", ("hid", h.Hid), ("animal", animal.m_name), ("secs", System.Math.Round(Time.time - _droppedAt, 1)));
                Next(h);
                return ChoreProgress.Running;
            }
            if (Time.time - _droppedAt > EatSeconds)
            {
                if (_dropped != null && _dropped.m_nview != null && _dropped.m_nview.IsValid())
                {
                    if (!_dropped.m_nview.IsOwner())
                        _dropped.m_nview.ClaimOwnership();
                    h.CargoInventory!.AddItem(_dropped.m_itemData.Clone());
                    ZNetScene.instance.Destroy(_dropped.gameObject);
                }
                VfhLog.D(LogCat.Smelter, "steward.feed_refused", ("hid", h.Hid), ("animal", animal.m_name));
                Reservations.Skip(animal, AnimalSkipSeconds);
                Next(h);
            }
            return ChoreProgress.Running;
        }

        private static bool IsHungry(Character c) => c.GetComponent<Tameable>() is Tameable t && t.IsHungry();

        private void Next(Hireling h)
        {
            if (_route.Count > 0)
            {
                if (_route[0].Animal != null)
                    Reservations.Release(_route[0].Animal, h.Hid);
                _route.RemoveAt(0);
            }
            _dropped = null;
            _walk.Reset();
        }

        private ChoreProgress End(Hireling h, float rest, ChoreProgress result = ChoreProgress.Done)
        {
            foreach ((Character animal, _) in _route.Where(r => r.Animal != null))
                Reservations.Release(animal, h.Hid);
            _route.Clear();
            _dropped = null;
            _step = Step.None;
            _chest = null;
            RestAfter = rest;
            return result;
        }

        public void Abort(Hireling h) => End(h, 0f);
    }
}
