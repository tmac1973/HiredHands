using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Core.Nav;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings.Nav;

namespace VikingsForHire.Commands
{
    internal static class NavCommands
    {
        // Plan from the nearest hireling to the spot you're looking at, asking the game's map about every leg right away
        // (no budget), and print what it said: why a route through the links was or wasn't found.
        private static void Why()
        {
            Player me = Player.m_localPlayer;
            if (me == null || GameCamera.instance == null)
                return;
            Hirelings.Hireling? h = Hirelings.Hireling.Loaded.Where(x => x != null).OrderBy(x => UnityEngine.Vector3.Distance(x.transform.position, me.transform.position)).FirstOrDefault();
            if (h == null)
            {
                VfhCommand.Print("HiredHands: no hireling loaded");
                return;
            }
            UnityEngine.Transform cam = GameCamera.instance.transform;
            if (!UnityEngine.Physics.Raycast(cam.position, cam.forward, out UnityEngine.RaycastHit hit, 60f, StairSampler.FloorMask, UnityEngine.QueryTriggerInteraction.Ignore))
            {
                VfhCommand.Print("HiredHands: look at the spot (a chest, a floor) within 60 m");
                return;
            }
            UnityEngine.Vector3 goal = hit.point;
            // Looking at or near a chest: its position, as a delivery aims for.
            Container? chest = hit.collider.GetComponentInParent<Container>();
            if (chest == null)
            {
                var near = new List<UnityEngine.Collider>(UnityEngine.Physics.OverlapSphere(hit.point, 2f));
                chest = near.Select(c => c.GetComponentInParent<Container>()).FirstOrDefault(c => c != null);
            }
            if (chest != null)
                goal = chest.transform.position;
            BoardNav? nav = NavLinkRegistry.AreaAt(h.transform.position);
            if (nav == null || NavLinkRegistry.AreaAt(goal) != nav)
            {
                VfhCommand.Print("HiredHands: the hireling and that spot aren't in the same board's area");
                return;
            }
            bool direct = h.Ai.FullRouteTo(goal, 2.5f);
            var legs = new List<string>();
            LegAnswer Ask(NavPoint a, NavPoint b)
            {
                LegAnswer x = LegOracle.AnswerNow(nav, a, b);
                legs.Add($"{a}→{b}: {(x.Walkable ? $"yes {x.Cost:0.0}m" : "no")}");
                return x;
            }
            PlanResult r = LinkPlanner.Plan(nav.Graph, h.transform.position.ToNav(), goal.ToNav(), Ask, ZNet.instance.GetTimeSeconds());
            VfhCommand.Print($"{h.DisplayName} → {goal}: game map full route {(direct ? "yes" : "no")}; links plan: {r.Describe()} ({legs.Count} legs asked)");
            foreach (string l in legs)
                VfhCommand.Print("  " + l);
            string links = string.Join(" | ", nav.Graph.Links.Select(l => $"#{l.Id} {(l.IsLadder ? "ladder" : l.Kind.ToString().ToLowerInvariant())} {l.Prefab} {l.A}→{l.B}"));
            VfhCommand.Print("Links: " + links);
            VfhLog.I(LogCat.Nav, "navlinks.why", ("hid", h.Hid), ("goal", goal), ("chest", chest != null), ("direct", direct), ("plan", r.Describe()),
                ("legs", string.Join(" | ", legs)), ("links", links));
        }

        public static void Register() =>
            CommandManager.Instance.AddConsoleCommand(new VfhCommand("vfh_navlinks",
                "<show|hide|scan|list|why> - the doors and stairs hirelings route through at your bases (show draws them, scan rescans the nearest board, list prints its links, why plans from the nearest hireling to where you're looking and prints every leg)",
                false, Run, new List<string> { "show", "hide", "scan", "list", "why" }));

        private static void Run(string[] args)
        {
            string what = args.FirstOrDefault() ?? "list";
            if (!NavLinkRegistry.Enabled)
            {
                VfhCommand.Print("HiredHands: BaseNavLinks is off (server setting)");
                return;
            }
            BoardNav? nav = Player.m_localPlayer == null ? null : NavLinkRegistry.Nearest(Player.m_localPlayer.transform.position);
            switch (what)
            {
                case "show":
                    NavOverlay.Show(true);
                    VfhCommand.Print("HiredHands: drawing links (door green, stair yellow, ladder cyan, blocked red)");
                    break;
                case "hide":
                    NavOverlay.Show(false);
                    VfhCommand.Print("HiredHands: links hidden");
                    break;
                case "why":
                    Why();
                    break;
                case "scan":
                    if (nav == null)
                    {
                        VfhCommand.Print("HiredHands: no hiring board loaded");
                        return;
                    }
                    NavLinkRegistry.MarkBoard(nav, now: true);
                    VfhCommand.Print($"HiredHands: rescanning board {nav.BoardId}");
                    break;
                default:
                    if (nav == null)
                    {
                        VfhCommand.Print("HiredHands: no hiring board loaded");
                        return;
                    }
                    double now = ZNet.instance.GetTimeSeconds();
                    foreach (NavLink l in nav.Graph.Links)
                        VfhCommand.Print($"  #{l.Id} {(l.IsLadder ? "ladder" : l.Kind.ToString().ToLowerInvariant())} {l.A}→{l.B} {l.Length:0.0}m {l.Prefab}{(l.Blocked(now) ? " BLOCKED" : "")}");
                    VfhCommand.Print($"Board {nav.BoardId}: {nav.Graph.Links.Count(l => l.Kind == NavLinkKind.Door)} doors, " +
                                     $"{nav.Graph.Links.Count(l => l.Kind == NavLinkKind.Stair && !l.IsLadder)} stairs, {nav.Graph.Links.Count(l => l.IsLadder)} ladders, " +
                                     $"{nav.Rejected} rejected, radius {nav.Radius:0} m, scan {nav.Scans}");
                    VfhLog.I(LogCat.Nav, "navlinks.list", ("board", nav.BoardId), ("links", nav.Graph.Links.Count), ("version", nav.Graph.Version));
                    break;
            }
        }
    }
}
