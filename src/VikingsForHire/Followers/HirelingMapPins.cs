using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Entities;
using Jotunn.Managers;
using VikingsForHire.Board;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings;
using VikingsForHire.Net;
using UnityEngine;

namespace VikingsForHire.Followers
{
    /// <summary>
    /// With the Command Stone in hand, the map shows your hirelings: your followers, and the hirelings of the boards you
    /// built. Your game only has the ones near you, so it asks the server every couple of seconds; ones loaded here are
    /// pinned where this game sees them, so a follower's pin keeps up with it. Pins aren't saved and go when the stone
    /// is put away.
    /// </summary>
    internal static class HirelingMapPins
    {
        private const float AskSeconds = 2f;
        private const float MoveSeconds = 0.25f;

        private sealed class Spot
        {
            public string Hid = "";
            public Vector3 Pos;
            public string Name = "";
            public JobType Job;
            public bool Follower;
        }

        private static CustomRPC _rpc = null!;
        private static readonly Dictionary<string, Minimap.PinData> Pins = new();
        private static List<Spot> _spots = new();
        private static float _nextAsk;
        private static float _nextMove;

        /// <summary>Pins shown right now (tests).</summary>
        public static int Count => Pins.Count;

        public static void Register() => _rpc = NetworkManager.Instance.AddRPC("VFH_HirelingPins", OnServer, OnClient);

        /// <summary>Every frame (Plugin.Update), client side.</summary>
        public static void Tick()
        {
            if (ZNet.instance == null || Minimap.instance == null)
                return;
            if (!StoneInput.StoneInHand)
            {
                if (Pins.Count > 0)
                    Clear();
                _nextAsk = 0f;
                return;
            }
            if (Time.time >= _nextAsk)
            {
                _nextAsk = Time.time + AskSeconds;
                long me = Player.m_localPlayer.GetPlayerID();
                if (ZNet.instance.IsServer())
                    _spots = SpotsOf(me);
                else
                {
                    var pkg = new ZPackage();
                    pkg.Write(me);
                    _rpc.SendPackage(ZRoutedRpc.instance.GetServerPeerID(), pkg);
                }
            }
            if (Time.time >= _nextMove)
            {
                _nextMove = Time.time + MoveSeconds;
                Show();
            }
        }

        // Server: the player's followers, and the hirelings of the boards they built (not those walking off or on a
        // trip home, which aren't anywhere to be seen).
        private static List<Spot> SpotsOf(long pid)
        {
            var mine = new HashSet<string>(WorldIndex.AllBoards().Where(b => b.GetLong(ZDOVars.s_creator) == pid).Select(BoardZdo.GetId));
            var spots = new List<Spot>();
            foreach (ZDO zdo in WorldIndex.AllHirelings())
            {
                var mode = (HirelingMode)zdo.GetInt(HirelingZdo.Mode);
                bool follower = mode == HirelingMode.Following && zdo.GetLong(HirelingZdo.Owner) == pid;
                bool home = !follower && mode is HirelingMode.Working or HirelingMode.Idle && mine.Contains(zdo.GetString(HirelingZdo.BoardId));
                if (!follower && !home)
                    continue;
                spots.Add(new Spot
                {
                    Hid = zdo.GetString(HirelingZdo.Hid), Pos = zdo.GetPosition(), Name = zdo.GetString(HirelingZdo.Name),
                    Job = (JobType)zdo.GetInt(HirelingZdo.Job), Follower = follower,
                });
            }
            return spots;
        }

        private static void Clear()
        {
            if (Minimap.instance != null)
                foreach (Minimap.PinData pin in Pins.Values)
                    Minimap.instance.RemovePin(pin);
            Pins.Clear();
            _spots.Clear();
        }

        // One pin per hireling: moved to where it is now, relabelled if that changed, removed when it's gone.
        private static void Show()
        {
            var keep = new HashSet<string>();
            foreach (Spot s in _spots)
            {
                Hireling? live = Hireling.Loaded.FirstOrDefault(h => h != null && h.Hid == s.Hid);
                Vector3 pos = live != null ? live.transform.position : s.Pos;
                string label = s.Follower ? s.Name : s.Name + " (" + Localization.instance.Localize("$vfh_job_" + s.Job.ToString().ToLowerInvariant()) + ")";
                keep.Add(s.Hid);
                if (Pins.TryGetValue(s.Hid, out Minimap.PinData pin) && pin.m_name == label)
                {
                    pin.m_pos = pos;
                    continue;
                }
                if (pin != null)
                    Minimap.instance.RemovePin(pin);
                Pins[s.Hid] = Minimap.instance.AddPin(pos, s.Follower ? Minimap.PinType.Icon3 : Minimap.PinType.Icon2, label, save: false, isChecked: false);
            }
            foreach (string hid in Pins.Keys.Where(k => !keep.Contains(k)).ToList())
            {
                Minimap.instance.RemovePin(Pins[hid]);
                Pins.Remove(hid);
            }
        }

        private static IEnumerator OnServer(long sender, ZPackage pkg)
        {
            VfhLog.Guard(LogCat.Follow, "pins.server_failed", () =>
            {
                long pid = pkg.ReadLong();
                if (FollowerServer.PlayerIdOf(sender) != pid)
                    return;
                List<Spot> spots = SpotsOf(pid);
                var reply = new ZPackage();
                reply.Write(spots.Count);
                foreach (Spot s in spots)
                {
                    reply.Write(s.Hid);
                    reply.Write(s.Pos);
                    reply.Write(s.Name);
                    reply.Write((int)s.Job);
                    reply.Write(s.Follower);
                }
                _rpc.SendPackage(sender, reply);
            });
            yield break;
        }

        private static IEnumerator OnClient(long sender, ZPackage pkg)
        {
            VfhLog.Guard(LogCat.Follow, "pins.client_failed", () =>
            {
                int n = pkg.ReadInt();
                var spots = new List<Spot>(n);
                for (int i = 0; i < n; i++)
                    spots.Add(new Spot { Hid = pkg.ReadString(), Pos = pkg.ReadVector3(), Name = pkg.ReadString(), Job = (JobType)pkg.ReadInt(), Follower = pkg.ReadBool() });
                // The stone may have been put away while the answer was on its way.
                if (StoneInput.StoneInHand)
                    _spots = spots;
            });
            yield break;
        }
    }
}
