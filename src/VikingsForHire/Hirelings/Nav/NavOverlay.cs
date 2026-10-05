using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Core.Nav;

namespace VikingsForHire.Hirelings.Nav
{
    /// <summary>
    /// `vfh_navlinks show`: draws each loaded board's links in the world (door green, stair yellow, ladder cyan,
    /// blocked red, a white tick at every end). Only on this screen; nothing is networked or saved.
    /// </summary>
    internal static class NavOverlay
    {
        private const float RefreshSeconds = 0.5f;
        private static readonly Color DoorColor = new(0.3f, 1f, 0.3f);
        private static readonly Color StairColor = new(1f, 0.9f, 0.2f);
        private static readonly Color LadderColor = new(0.2f, 0.9f, 1f);
        private static readonly Color BlockedColor = new(1f, 0.25f, 0.25f);

        private static readonly Dictionary<string, (GameObject Root, string Key)> Shown = new();
        private static Material? _material;
        private static float _next;
        private static GameObject? _routes;

        public static bool On { get; private set; }

        public static void Show(bool on)
        {
            On = on;
            if (!on)
                RemoveAll();
            _next = 0f;
        }

        public static void Tick()
        {
            if (!On || Time.time < _next)
                return;
            _next = Time.time + RefreshSeconds;
            double now = ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0.0;
            foreach (BoardNav nav in NavLinkRegistry.All)
            {
                // Redraw only when the links or their blocked state changed.
                string key = nav.Graph.Version + ":" + string.Join(",", nav.Graph.Links.Where(l => l.Blocked(now)).Select(l => l.Id));
                if (Shown.TryGetValue(nav.BoardId, out var shown) && shown.Key == key && shown.Root != null)
                    continue;
                Remove(nav.BoardId);
                Shown[nav.BoardId] = (Draw(nav, now), key);
            }
            DrawRoutes();
        }

        // Each loaded hireling's route ahead, white, redrawn every refresh.
        private static void DrawRoutes()
        {
            if (_routes != null)
                Object.Destroy(_routes);
            _routes = new GameObject("VFH_NavOverlay_routes");
            foreach (Hireling h in Hireling.Loaded)
            {
                if (h == null || h.Ai == null || !h.Ai.Links.HasRoute)
                    continue;
                Vector3[] pts = new[] { h.transform.position }.Concat(h.Ai.Links.Remaining()).Select(p => p + Vector3.up * 0.3f).ToArray();
                if (pts.Length > 1)
                    Line(_routes.transform, pts, Color.white, 0.04f);
            }
        }

        public static void Remove(string boardId)
        {
            if (Shown.TryGetValue(boardId, out var shown) && shown.Root != null)
                Object.Destroy(shown.Root);
            Shown.Remove(boardId);
        }

        public static void RemoveAll()
        {
            if (_routes != null)
                Object.Destroy(_routes);
            _routes = null;
            foreach (string id in Shown.Keys.ToList())
                Remove(id);
        }

        private static GameObject Draw(BoardNav nav, double now)
        {
            var root = new GameObject("VFH_NavOverlay_" + nav.BoardId);
            foreach (NavLink l in nav.Graph.Links)
            {
                Color c = l.Blocked(now) ? BlockedColor : l.Kind == NavLinkKind.Door ? DoorColor : l.IsLadder ? LadderColor : StairColor;
                IEnumerable<NavPoint> pts = l.Waypoints.Count > 1 ? l.Waypoints : new[] { l.A, l.B };
                Line(root.transform, pts.Select(p => p.ToUnity() + Vector3.up * 0.15f).ToArray(), c);
            }
            foreach (NavNode n in nav.Graph.Endpoints)
            {
                Vector3 p = n.Point.ToUnity();
                Line(root.transform, new[] { p, p + Vector3.up }, Color.white);
            }
            return root;
        }

        public static LineRenderer Line(Transform parent, Vector3[] points, Color color, float width = 0.05f)
        {
            var go = new GameObject("line");
            go.transform.SetParent(parent, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            _material ??= new Material(Shader.Find("Sprites/Default"));
            lr.material = _material;
            lr.useWorldSpace = true;
            lr.startWidth = lr.endWidth = width;
            lr.startColor = lr.endColor = color;
            lr.positionCount = points.Length;
            lr.SetPositions(points);
            return lr;
        }
    }
}
