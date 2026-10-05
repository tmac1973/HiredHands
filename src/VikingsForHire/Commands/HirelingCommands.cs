using System;
using System.Linq;
using Jotunn.Managers;
using VikingsForHire.Board;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings;
using UnityEngine;

namespace VikingsForHire.Commands
{
    internal static class HirelingCommands
    {
        private const float BoardRange = 100f;

        /// <summary>Hids from the most recent vfh_spawn, so tests can check exactly what they spawned.</summary>
        public static readonly System.Collections.Generic.List<string> LastSpawned = new();

        public static void Register()
        {
            var jobs = Enum.GetNames(typeof(JobType)).ToList();
            CommandManager.Instance.AddConsoleCommand(new VfhCommand("vfh_spawn", "<job> <level> [count] - spawn hirelings where you're looking, linked to the nearest board",
                true, args => Spawn(args), jobs));
            CommandManager.Instance.AddConsoleCommand(new VfhCommand("vfh_snapshot_test", "- snapshot the nearest hireling, destroy it, rebuild it 3m away and compare every value",
                true, _ =>
                {
                    Hireling h = NearestOrThrow();
                    Plugin.Instance.StartCoroutine(SnapshotTest.Run(h));
                    VfhCommand.Print($"HiredHands: snapshot test running on {h.DisplayName}, see the log (evt=snapshot.roundtrip)");
                }));
            CommandManager.Instance.AddConsoleCommand(new VfhCommand("vfh_kill_hirelings", "[radius=50] - kill hirelings near you", true, args =>
                VfhCommand.Print($"HiredHands: killed {Kill(args.Length > 0 && float.TryParse(args[0], out float r) ? r : 50f)} hirelings")));
            CommandManager.Instance.AddConsoleCommand(new VfhCommand("vfh_deliver", "- the nearest hireling takes what it carries to the chests now (as when its cargo is full)", true, _ =>
            {
                Hireling h = NearestOrThrow();
                h.Zdo!.Set(HirelingZdo.DeliverPending, true);
                VfhLog.I(LogCat.Hireling, "hireling.deliver_now", ("hid", h.Hid));
                VfhCommand.Print($"HiredHands: {h.DisplayName} is delivering");
            }));
            DebugCommands.DumpStateSections.Add(Dump);
        }

        public static int Spawn(string[] args, Vector3? at = null)
        {
            if (args.Length < 2 || !Enum.TryParse(args[0], true, out JobType job) || !int.TryParse(args[1], out int level))
            {
                VfhCommand.Print("Usage: vfh_spawn <Woodcutter|Miner|Smelter|GuardMelee|GuardRanged> <level> [count]");
                return 0;
            }
            int count = args.Length > 2 && int.TryParse(args[2], out int c) ? Mathf.Clamp(c, 1, 20) : 1;
            Player player = Player.m_localPlayer;
            Vector3 point = at ?? LookPoint(player);
            HiringBoard? board = HiringBoard.Nearest(point, BoardRange);
            LastSpawned.Clear();
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = count == 1 ? Vector3.zero : Quaternion.Euler(0f, i * 360f / count, 0f) * Vector3.forward * 2f;
                HirelingRecord record = HirelingFactory.NewRecord(job, level, board, point);
                HirelingFactory.Spawn(record, point + offset, Quaternion.LookRotation(-player.transform.forward), "vfh_spawn");
                LastSpawned.Add(record.Hid);
            }
            VfhCommand.Print($"HiredHands: spawned {count} {job} L{level}{(board != null ? $" for board {board.Id}" : " (no board nearby)")}");
            return count;
        }

        public static int Kill(float radius)
        {
            Vector3 origin = Player.m_localPlayer.transform.position;
            var victims = Hireling.Loaded.Where(h => h != null && Vector3.Distance(h.transform.position, origin) <= radius).ToList();
            foreach (Hireling h in victims)
            {
                var hit = new HitData { m_point = h.transform.position };
                hit.m_damage.m_damage = 1e7f;
                VfhLog.I(LogCat.Hireling, "hireling.killed", ("hid", h.Hid), ("name", h.DisplayName), ("by", "vfh_kill_hirelings"));
                h.Humanoid.Damage(hit);
            }
            return victims.Count;
        }

        public static Hireling NearestOrThrow() =>
            Hireling.Nearest(Player.m_localPlayer.transform.position, 30f) ?? throw new InvalidOperationException("no hireling within 30m");

        private static Vector3 LookPoint(Player player)
        {
            Transform cam = GameCamera.instance != null ? GameCamera.instance.transform : player.transform;
            int mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");
            if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, 50f, mask))
                return hit.point;
            Vector3 p = player.transform.position + player.transform.forward * 3f;
            p.y = ZoneSystem.instance.GetGroundHeight(p);
            return p;
        }

        private static void Dump()
        {
            var all = Hireling.Loaded.Where(h => h != null && h.Zdo != null).ToList();
            VfhLog.I(LogCat.Hireling, "dump.hirelings", ("loaded", all.Count));
            foreach (Hireling h in all)
            {
                ZDO z = h.Zdo!;
                Inventory? cargo = h.CargoInventory;
                VfhLog.I(LogCat.Hireling, "dump.hireling", ("hid", h.Hid), ("board", h.BoardId), ("name", h.DisplayName), ("job", h.Job),
                    ("level", h.Level), ("mode", h.Mode), ("stance", (Stance)z.GetInt(HirelingZdo.Stance)), ("owner", z.GetOwner()),
                    ("pos", h.transform.position), ("home", h.Home), ("health", h.Humanoid.GetHealth()), ("max", h.Humanoid.GetMaxHealth()),
                    ("charLevel", h.Humanoid.GetLevel()), ("tamed", h.Humanoid.IsTamed()), ("behaviour", h.Ai.CurrentBehaviour),
                    ("gear", GearApplier.Describe(h.Humanoid)), ("cargo", cargo == null ? "" : string.Join(",", cargo.GetAllItems().Select(i => $"{GearApplier.Name(i)}x{i.m_stack}"))),
                    ("zdo", z.m_uid.ToString()));
            }
        }
    }
}
