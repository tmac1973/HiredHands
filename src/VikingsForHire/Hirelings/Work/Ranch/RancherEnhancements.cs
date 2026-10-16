using System;
using System.Collections.Generic;
using HarmonyLib;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings.Work.Chores;
using UnityEngine;
using static VikingsForHire.Core.Enhancements.EnhancementId;
using Random = UnityEngine.Random;

namespace VikingsForHire.Hirelings.Work.Ranch
{
    /// <summary>
    /// The Rancher's enhancements (plan/enhancements phase 11): feeding from further over the fence (Gentle hand), food
    /// that lasts longer (Hearty feed), quicker pregnancies (Breeder) and starrier young (Star breeder) for animals it
    /// feeds, more from each slaughter (Butcher, Skinner, Master rancher), a bigger herd kept (Big herd), its herd defended
    /// (Herd guard), escaped animals led back (Herder) and wild ones of its kinds tamed (Wrangler).
    /// </summary>
    internal static class RancherEnhancements
    {
        /// <summary>On an animal it just dropped food for: the share longer that meal lasts (Hearty feed).</summary>
        public const string HeartyKey = "vfh_hearty";

        /// <summary>On an animal it fed: the share sooner its pregnancy runs (Breeder), until its next feeding.</summary>
        public const string BreederKey = "vfh_breeder";

        /// <summary>On an animal it fed: the chance its young get one more star (Star breeder).</summary>
        public const string StarKey = "vfh_starbreeder";

        /// <summary>Gentle hand: how far it reaches to feed (a player's reach plus this).</summary>
        public static float FeedReach(Hireling h, float normal) => normal + h.Enh.Value(GentleHand, "range");

        /// <summary>Master rancher: its slaughter steps' pace.</summary>
        public static float StepMult(Hireling h) => 1f - h.Enh.Value(MasterRancher, "speed");

        /// <summary>Big herd: how many more of each kind it keeps above the pool before slaughtering.</summary>
        public static int ExtraPool(Hireling h) => Mathf.RoundToInt(h.Enh.Value(BigHerd, "extra"));

        /// <summary>Herd guard: a Rancher with it hits harder.</summary>
        public static float HerdGuardMult(Hireling h, Character? target) =>
            h.Job == JobType.Rancher && h.Enh.Has(HerdGuard) ? 1f + h.Enh.Value(HerdGuard, "damage") : 1f;

        /// <summary>Food it just dropped for an animal, and the animal: marks them for Hearty feed, Breeder and Star breeder.</summary>
        public static void Fed(Hireling h, ItemDrop? food, Character animal)
        {
            if (!h.Enh.Effects.AnyPicks)
                return;
            ZDO? z = animal.m_nview != null && animal.m_nview.IsValid() ? animal.m_nview.GetZDO() : null;
            if (z == null)
                return;
            // On the animal, not the food: the food is gone by the time the game says it was eaten.
            z.Set(HeartyKey, h.Enh.Has(HeartyFeed) ? h.Enh.Value(HeartyFeed, "share") : 0f);
            z.Set(BreederKey, h.Enh.Has(Breeder) ? h.Enh.Value(Breeder, "share") : 0f);
            if (h.Enh.Has(StarBreeder))
                z.Set(StarKey, h.Enh.Value(StarBreeder, "chance"));
        }

        /// <summary>
        /// What a slaughter left, picked up: one more meat now and then (Butcher, plus Master rancher's own chance) and one
        /// more of its other drops (Skinner, plus Master rancher's). Added straight to cargo.
        /// </summary>
        public static void Slaughtered(Hireling h, List<string> picked)
        {
            if (!h.Enh.Effects.AnyPicks || picked.Count == 0)
                return;
            float master = h.Enh.Value(MasterRancher, "chance");
            string? meat = picked.Find(p => p.Contains("Meat"));
            string? other = picked.Find(p => !p.Contains("Meat"));
            int extra = 0;
            if (meat != null && Random.value < h.Enh.Value(Butcher, "chance") + master && ObjectDB.instance.GetItemPrefab(meat) is GameObject m)
            {
                WorkSteps.AddToCargo(h, m, 1);
                extra++;
            }
            if (other != null && Random.value < h.Enh.Value(Skinner, "chance") + master && ObjectDB.instance.GetItemPrefab(other) is GameObject o)
            {
                WorkSteps.AddToCargo(h, o, 1);
                extra++;
            }
            if (extra > 0)
                EnhancementEvents.Fire(h, "Butcher", LogCat.Work, ("extra", extra));
        }

        /// <summary>Hearty feed: food marked by a Rancher with it feeds the animal for longer.</summary>
        [HarmonyPatch(typeof(Tameable), nameof(Tameable.OnConsumedItem))]
        private static class HeartyPatch
        {
            private static void Postfix(Tameable __instance, ItemDrop item)
            {
                try
                {
                    if (__instance.m_nview == null || !__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner() || ZNet.instance == null)
                        return;
                    float share = __instance.m_nview.GetZDO().GetFloat(HeartyKey);
                    if (share <= 0f)
                        return;
                    __instance.m_nview.GetZDO().Set(HeartyKey, 0f); // the one meal
                    long later = TimeSpan.FromSeconds(__instance.m_fedDuration * share).Ticks;
                    __instance.m_nview.GetZDO().Set(ZDOVars.s_tameLastFeeding, ZNet.instance.GetTime().Ticks + later);
                    EnhancementEvents.Fire(null, "HeartyFeed", LogCat.Work, ("animal", __instance.name), ("seconds", __instance.m_fedDuration * share));
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("RancherEnhancements.Hearty", e);
                }
            }
        }

        /// <summary>Breeder: an animal its Rancher fed is due sooner once pregnant.</summary>
        [HarmonyPatch(typeof(Procreation), nameof(Procreation.MakePregnant))]
        private static class BreederPatch
        {
            private static void Postfix(Procreation __instance)
            {
                try
                {
                    ZDO? z = __instance.m_nview != null && __instance.m_nview.IsValid() ? __instance.m_nview.GetZDO() : null;
                    float share = z?.GetFloat(BreederKey) ?? 0f;
                    if (z == null || share <= 0f)
                        return;
                    long back = TimeSpan.FromSeconds(__instance.m_pregnancyDuration * share).Ticks;
                    z.Set(ZDOVars.s_pregnant, z.GetLong(ZDOVars.s_pregnant) - back);
                    EnhancementEvents.Fire(null, "Breeder", LogCat.Work, ("animal", __instance.name));
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("RancherEnhancements.Breeder", e);
                }
            }
        }

        [ThreadStatic] private static float _starChance;

        /// <summary>Star breeder: while an animal its Rancher fed gives birth, the young may get one more star (up to 2).</summary>
        [HarmonyPatch(typeof(Procreation), "Procreate")]
        private static class StarPatch
        {
            private static void Prefix(Procreation __instance)
            {
                _starChance = __instance.m_nview != null && __instance.m_nview.IsValid() ? __instance.m_nview.GetZDO().GetFloat(StarKey) : 0f;
            }

            private static void Finalizer() => _starChance = 0f;
        }

        [HarmonyPatch(typeof(Character), nameof(Character.SetLevel))]
        private static class StarLevelPatch
        {
            private static void Prefix(ref int level)
            {
                if (_starChance <= 0f)
                    return;
                float chance = _starChance;
                _starChance = 0f; // the one birth only
                if (level < 3 && Random.value < chance)
                {
                    level++;
                    EnhancementEvents.Fire(null, "StarBreeder", LogCat.Work, ("level", level));
                }
            }
        }

        /// <summary>
        /// Wrangler: now and then a wild animal of a kind its board keeps (one with a breeding pool), inside its area, is
        /// tamed and joins the herd. Never a boss, or something the game can't tame. Returns the one tamed, if any.
        /// </summary>
        public static Character? Wrangle(Hireling h, ICollection<string> kept, ref float nextAt)
        {
            if (!h.Enh.Has(Wrangler) || Time.time < nextAt || kept.Count == 0)
                return null;
            nextAt = Time.time + 10f;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c.IsDead() || c.IsTamed() || c.IsBoss() || c.GetComponent<Tameable>() is not Tameable t ||
                    !kept.Contains(Utils.GetPrefabName(c.gameObject)) || Utils.DistanceXZ(c.transform.position, h.WorkCentre) > h.Radius ||
                    c.m_nview == null || !c.m_nview.IsValid())
                    continue;
                if (!c.m_nview.IsOwner())
                    c.m_nview.ClaimOwnership();
                t.Tame();
                nextAt = Time.time + h.Enh.Value(Wrangler, "cooldown");
                EnhancementEvents.Fire(h, "Wrangler", LogCat.Work, ("animal", c.m_name));
                return c;
            }
            return null;
        }
    }

    /// <summary>
    /// Once a second, for the Ranchers this game runs: Wrangler's taming, and Herder's leading escaped animals back (an
    /// escaped herd animal follows its Rancher for a while; once walled in again it's let go).
    /// </summary>
    internal static class RancherTicker
    {
        private static float _next;
        private static readonly Dictionary<string, float> NextWrangle = new();
        private static readonly Dictionary<ZDOID, (Hireling By, float Until)> Led = new();

        public static void Tick()
        {
            if (Time.time < _next)
                return;
            _next = Time.time + 1f;
            foreach (Hireling h in Hireling.Loaded)
            {
                if (h == null || h.Job != JobType.Rancher || h.Zdo == null || !h.Humanoid.m_nview.IsOwner() || !h.Enh.Effects.AnyPicks)
                    continue;
                if (h.Enh.Has(Wrangler) && Board.BoardOrders.BoardOf(h.BoardId) is Board.HiringBoard board)
                {
                    var kept = new HashSet<string>();
                    foreach (Core.Orders.ProductionOrder o in Board.BoardOrders.For(board).Ranch())
                        if (o.Kind == Core.Orders.OrderKind.Herd)
                            kept.Add(o.Item);
                    float at = NextWrangle.TryGetValue(h.Hid, out float n) ? n : 0f;
                    RancherEnhancements.Wrangle(h, kept, ref at);
                    NextWrangle[h.Hid] = at;
                }
                if (h.Enh.Has(Herder))
                    Herd(h);
            }
            Release();
        }

        private static void Herd(Hireling h)
        {
            float seconds = h.Enh.Value(Herder, "seconds");
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c.IsDead() || !c.IsTamed() || c.m_nview == null || !c.m_nview.IsValid() || Hireling.Of(c) != null ||
                    Utils.DistanceXZ(c.transform.position, h.WorkCentre) > h.Radius + 20f)
                    continue;
                ZDOID id = c.m_nview.GetZDO().m_uid;
                if (Led.ContainsKey(id) || !HerdScan.Escaped(id, c.transform.position) || c.GetBaseAI() is not MonsterAI ai)
                    continue;
                if (!c.m_nview.IsOwner())
                    c.m_nview.ClaimOwnership();
                ai.SetFollowTarget(h.gameObject);
                Led[id] = (h, Time.time + seconds);
                EnhancementEvents.Fire(h, "Herder", LogCat.Work, ("animal", c.m_name));
            }
        }

        // Back in a pen, out of time, or the Rancher gone: stop following.
        private static void Release()
        {
            List<ZDOID>? done = null;
            foreach (KeyValuePair<ZDOID, (Hireling By, float Until)> kv in Led)
            {
                GameObject? go = ZNetScene.instance?.FindInstance(kv.Key);
                Character? c = go != null ? go.GetComponent<Character>() : null;
                bool penned = c != null && HerdScan.Enclosed(c.transform.position);
                if (c == null || penned || Time.time >= kv.Value.Until || kv.Value.By == null)
                {
                    if (c != null && c.GetBaseAI() is MonsterAI ai && c.m_nview != null && c.m_nview.IsOwner())
                        ai.SetFollowTarget(null);
                    if (penned && kv.Value.By != null)
                        EnhancementEvents.Fire(kv.Value.By, "HerderHome", LogCat.Work, ("animal", c!.m_name));
                    (done ??= new List<ZDOID>()).Add(kv.Key);
                }
            }
            if (done != null)
                foreach (ZDOID id in done)
                    Led.Remove(id);
        }
    }
}
