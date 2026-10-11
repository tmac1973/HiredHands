using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings
{
    /// <summary>
    /// Now and then a hireling near you says something, in the trader's speech bubble: about what it's doing right now
    /// (you watching it work, following you, idle, full up, fighting, rain), about its job, or nothing in particular.
    /// Each game picks lines for the hirelings near its own player; nothing goes over the network. HirelingChatter and
    /// ChatterMinutes are each player's own settings.
    /// </summary>
    internal static class HirelingChatter
    {
        private const float Near = 15f;
        private const float WatchNear = 6f;
        private const float WatchSeconds = 4f;
        private const float GapSeconds = 20f;
        private const float ShowSeconds = 6f;
        private static readonly int RoofMask = LayerMask.GetMask("piece", "piece_nonsolid", "static_solid");

        // How many lines each pool has in the strings file ($vfh_say_<pool>_<n>).
        private static readonly Dictionary<string, int> Pools = new()
        {
            ["generic"] = 12, ["woodcutter"] = 8, ["miner"] = 8, ["steward"] = 8, ["farmer"] = 8, ["cook"] = 8, ["guard"] = 8,
            ["archer"] = 8, ["wizard"] = 8, ["rancher"] = 8, ["watching"] = 6, ["following"] = 6, ["idle"] = 5, ["full"] = 4, ["fight"] = 5, ["scared"] = 4, ["wet"] = 4,
        };

        private static readonly Dictionary<string, float> NextAt = new();
        private static readonly Dictionary<string, string> LastLine = new();
        private static float _lastAnyone;
        private static float _stillSince = -1f;

        /// <summary>The last line each hireling said here (tests).</summary>
        public static string Said(string hid) => LastLine.TryGetValue(hid, out string s) ? s : "";

        /// <summary>Every frame (Plugin.Update), on every game with a player.</summary>
        public static void Tick()
        {
            Player me = Player.m_localPlayer;
            if (me == null || Chat.instance == null || !VfhConfig.HirelingChatter.Value)
                return;
            _stillSince = me.GetVelocity().magnitude < 0.3f ? (_stillSince < 0f ? Time.time : _stillSince) : -1f;
            if (Time.time - _lastAnyone < GapSeconds)
                return;
            foreach (Hireling h in Hireling.Loaded)
            {
                if (h == null || h.Zdo == null || h.IsStowed || h.Humanoid.IsDead() || Vector3.Distance(h.transform.position, me.transform.position) > Near)
                    continue;
                if (!NextAt.TryGetValue(h.Hid, out float at))
                {
                    NextAt[h.Hid] = Time.time + Gap() * Random.Range(0.2f, 1f); // not all at once when you arrive
                    continue;
                }
                if (Time.time < at)
                    continue;
                Say(h, me);
                return;
            }
        }

        /// <summary>Say something now (tests, and Tick).</summary>
        public static void Say(Hireling h, Player me)
        {
            string pool = Pick(h, me);
            string key = $"$vfh_say_{pool}_{Random.Range(1, Pools[pool] + 1)}";
            string text = Localization.instance.Localize(key);
            Chat.instance.SetNpcText(h.gameObject, Vector3.up * 2.1f, 20f, ShowSeconds, h.DisplayName, text, false);
            LastLine[h.Hid] = text;
            NextAt[h.Hid] = Time.time + Gap();
            _lastAnyone = Time.time;
            VfhLog.D(LogCat.Hireling, "chatter.said", ("hid", h.Hid), ("pool", pool), ("key", key));
        }

        // What it's doing now first (half the time, when something fits), else its job or something generic.
        private static string Pick(Hireling h, Player me)
        {
            var now = new List<string>();
            string doing = h.Ai.CurrentBehaviour;
            if (doing == "Combat")
                now.Add(h.Job.IsGuard() ? "fight" : "scared");
            else if (doing == "Flee")
                now.Add("scared");
            if (h.CargoFull)
                now.Add("full");
            if (h.Mode == HirelingMode.Following && h.OwnerId == me.GetPlayerID() && doing == "Follow")
                now.Add("following");
            if (doing == "Idle")
                now.Add("idle");
            if (_stillSince >= 0f && Time.time - _stillSince > WatchSeconds && Vector3.Distance(h.transform.position, me.transform.position) <= WatchNear)
                now.Add("watching");
            if (EnvMan.IsWet() && !Physics.Raycast(h.transform.position + Vector3.up * 2f, Vector3.up, 30f, RoofMask))
                now.Add("wet");
            if (now.Count > 0 && Random.value < 0.5f)
                return now[Random.Range(0, now.Count)];
            string? job = h.Job switch
            {
                JobType.Woodcutter => "woodcutter",
                JobType.Miner => "miner",
                JobType.Smelter => "steward",
                JobType.Farmer => "farmer",
                JobType.Cook => "cook",
                JobType.GuardMelee => "guard",
                JobType.GuardRanged => "archer",
                JobType.Wizard => "wizard",
                JobType.Rancher => "rancher",
                _ => null,
            };
            return job != null && Random.value < 0.65f ? job : "generic";
        }

        private static float Gap() => Mathf.Max(0.5f, VfhConfig.ChatterMinutes.Value) * 60f * Random.Range(0.5f, 1.5f);
    }
}
