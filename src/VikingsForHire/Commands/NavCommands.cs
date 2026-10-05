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
        public static void Register() =>
            CommandManager.Instance.AddConsoleCommand(new VfhCommand("vfh_navlinks",
                "<show|hide|scan|list> - the doors and stairs hirelings route through at your bases (show draws them, scan rescans the nearest board, list prints its links)",
                false, Run, new List<string> { "show", "hide", "scan", "list" }));

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
