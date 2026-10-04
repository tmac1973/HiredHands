using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Board;
using VikingsForHire.Config;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings
{
    /// <summary>
    /// Lets a hireling get through doors on its way somewhere: it opens a closed door just ahead of it in the
    /// direction it's heading, heads for a door that leads towards its goal when the pathfinder finds no route at all,
    /// and closes each door it opened once it's through (never in a player's face). Only doors the board's owner could
    /// open under any ward there; never locked (keyed) doors. Wandering hirelings don't use doors: only WalkTo does.
    /// </summary>
    internal sealed class DoorHelper
    {
        private const float CheckSeconds = 0.25f;
        private const float AheadRange = 2.2f;
        private const float DetourRange = 15f;
        private const float CloseDistance = 3f;
        private const float CloseAfterSeconds = 2.5f;
        private const float PlayerClearance = 2.5f;
        private static readonly int Mask = LayerMask.GetMask("piece", "piece_nonsolid", "Default", "static_solid");
        private static readonly Collider[] Hits = new Collider[64];

        private readonly HirelingAI _ai;
        private readonly Dictionary<Door, float> _opened = new();
        private float _nextCheck;
        private Door? _detour;
        // Going through a door it opened because there was no route: straight to the far side before anything else.
        // The walkable map only catches up with an opened door a few seconds later, so until then the pathfinder still
        // says "no route" and the hireling walked away from the open door, which then closed behind it: a loop.
        private Door? _throughDoor;
        private Vector3 _throughPoint;
        private float _throughUntil;
        private const float ThroughSeconds = 6f;
        private const float ThroughDepth = 1.8f;

        public DoorHelper(HirelingAI ai) => _ai = ai;

        public bool HasDetour => _detour != null;

        /// <summary>The far side of a door it's going through right now (walk straight there), if any.</summary>
        public Vector3? Through
        {
            get
            {
                if (_throughDoor == null)
                    return null;
                Vector3 me = _ai.transform.position;
                bool there = Utils.DistanceXZ(me, _throughPoint) < 0.7f;
                if (there || Time.time > _throughUntil || _throughDoor.m_nview == null || !IsOpen(_throughDoor))
                {
                    VfhLog.D(LogCat.AI, "door.through", ("hid", _ai.Hireling.Hid), ("door", _throughDoor.transform.position), ("made_it", there));
                    _throughDoor = null;
                    return null;
                }
                return _throughPoint;
            }
        }

        /// <summary>A door to walk to first because there's no route to the goal without it, if any.</summary>
        public Vector3? Detour(Vector3 goal, bool havePath)
        {
            if (!VfhConfig.HirelingsOpenDoors.Value)
                return null;
            if (_detour != null && (!Usable(_detour) || IsOpen(_detour)))
                _detour = null;
            // Keep a chosen detour until its door is open: the pathfinder finds the door itself easily, which says
            // nothing about the goal beyond it.
            if (_detour == null && !havePath)
            {
                Vector3 me = _ai.transform.position;
                float straight = Vector3.Distance(me, goal);
                _detour = Doors(me, DetourRange)
                    .Where(d => !IsOpen(d) && Usable(d) && Vector3.Distance(d.transform.position, goal) < straight)
                    .OrderBy(d => Vector3.Distance(me, d.transform.position) + Vector3.Distance(d.transform.position, goal))
                    .FirstOrDefault();
                if (_detour != null)
                    VfhLog.D(LogCat.AI, "door.detour", ("hid", _ai.Hireling.Hid), ("door", _detour.transform.position), ("goal", goal));
            }
            if (_detour == null)
                return null;
            // The near side of the doorway.
            Vector3 side = _ai.transform.position - _detour.transform.position;
            side.y = 0f;
            Vector3 normal = Vector3.Dot(side, _detour.transform.forward) >= 0f ? _detour.transform.forward : -_detour.transform.forward;
            return _detour.transform.position + normal * 1.2f;
        }

        /// <summary>
        /// A follower chasing its owner with a wall in the way and no route: the doorway to go through (open or closed),
        /// as the point on our side of it to walk to. Once there, the door is opened if need be and it goes straight
        /// through (<see cref="Through"/>). Null when there's no door that gets it closer.
        /// </summary>
        public Vector3? DoorwayTowards(Vector3 goal)
        {
            if (!VfhConfig.HirelingsOpenDoors.Value || _throughDoor != null)
                return null;
            Vector3 me = _ai.transform.position;
            if (_detour != null && (_detour.m_nview == null || !_detour.m_nview.IsValid()))
                _detour = null;
            if (_detour == null)
            {
                float straight = Vector3.Distance(me, goal);
                _detour = Doors(me, DetourRange)
                    .Where(d => (IsOpen(d) || Usable(d)) && Vector3.Distance(d.transform.position, goal) < straight)
                    .OrderBy(d => Vector3.Distance(me, d.transform.position) + Vector3.Distance(d.transform.position, goal))
                    .FirstOrDefault();
                if (_detour == null)
                    return null;
                VfhLog.D(LogCat.AI, "door.detour", ("hid", _ai.Hireling.Hid), ("door", _detour.transform.position), ("goal", goal), ("open", IsOpen(_detour)));
            }
            Vector3 near = Side(_detour, me, 1.2f);
            if (Utils.DistanceXZ(me, near) > 0.8f)
                return near;
            if (!IsOpen(_detour))
                Open(_detour, me);
            StartThrough(_detour, me);
            _detour = null;
            return null;
        }

        // A point beside the doorway, on the side of <paramref name="from"/> (positive depth) or the other (negative).
        private static Vector3 Side(Door d, Vector3 from, float depth)
        {
            Vector3 side = from - d.transform.position;
            side.y = 0f;
            Vector3 normal = Vector3.Dot(side, d.transform.forward) >= 0f ? d.transform.forward : -d.transform.forward;
            return d.transform.position + normal * depth;
        }

        private void Open(Door d, Vector3 me)
        {
            // Same as a player using it from where we stand: it swings away from us.
            Vector3 userDir = (me - d.transform.position).normalized;
            d.m_nview.InvokeRPC("UseDoor", Vector3.Dot(d.transform.forward, userDir) < 0f);
            _opened[d] = Time.time;
            VfhLog.D(LogCat.AI, "door.open", ("hid", _ai.Hireling.Hid), ("door", d.transform.position));
        }

        private void StartThrough(Door d, Vector3 me)
        {
            _throughDoor = d;
            _throughPoint = Side(d, me, -ThroughDepth);
            _throughUntil = Time.time + ThroughSeconds;
        }

        /// <summary>Called while walking towards <paramref name="goal"/>: open what's in the way, close what's behind.</summary>
        public void Tick(Vector3 goal)
        {
            if (!VfhConfig.HirelingsOpenDoors.Value || Time.time < _nextCheck)
                return;
            _nextCheck = Time.time + CheckSeconds;
            Vector3 me = _ai.transform.position;
            Vector3 heading = goal - me;
            heading.y = 0f;

            foreach (Door d in Doors(me, AheadRange))
            {
                if (IsOpen(d) || !Usable(d))
                    continue;
                Vector3 toDoor = d.transform.position - me;
                toDoor.y = 0f;
                bool ahead = heading.sqrMagnitude < 0.01f || Vector3.Dot(toDoor.normalized, heading.normalized) > 0.2f;
                if (!ahead && d != _detour)
                    continue;
                Open(d, me);
                if (d == _detour)
                {
                    _detour = null;
                    StartThrough(d, me);
                }
            }
            CloseBehind(false);
        }

        /// <summary>Closes the doors this hireling opened once it's clear of them (all of them when <paramref name="now"/>).</summary>
        public void CloseBehind(bool now)
        {
            if (_opened.Count == 0)
                return;
            Vector3 me = _ai != null ? _ai.transform.position : Vector3.zero;
            foreach (var kv in _opened.ToList())
            {
                Door d = kv.Key;
                if (d == null || d.m_nview == null || !d.m_nview.IsValid())
                {
                    _opened.Remove(d!);
                    continue;
                }
                if (!now && d == _throughDoor)
                    continue; // not until it's through
                bool clear = now || (Vector3.Distance(me, d.transform.position) > CloseDistance && Time.time - kv.Value > CloseAfterSeconds);
                if (!clear || Player.GetClosestPlayer(d.transform.position, PlayerClearance) != null)
                    continue;
                if (IsOpen(d))
                {
                    d.m_nview.InvokeRPC("UseDoor", false);
                    VfhLog.D(LogCat.AI, "door.close", ("hid", _ai != null ? _ai.Hireling.Hid : ""), ("door", d.transform.position));
                }
                _opened.Remove(d);
            }
        }

        private static IEnumerable<Door> Doors(Vector3 at, float radius)
        {
            int n = Physics.OverlapSphereNonAlloc(at + Vector3.up, radius, Hits, Mask);
            var seen = new HashSet<Door>();
            for (int i = 0; i < n; i++)
            {
                Door d = Hits[i].GetComponentInParent<Door>();
                if (d != null && seen.Add(d))
                    yield return d;
            }
        }

        private static bool IsOpen(Door d) => d.m_nview != null && d.m_nview.IsValid() && d.m_nview.GetZDO().GetInt(ZDOVars.s_state) != 0;

        // Not locked, can be closed again, and the board's owner has access under any ward covering it.
        private bool Usable(Door d)
        {
            if (d == null || d.m_nview == null || !d.m_nview.IsValid() || d.m_keyItem != null || d.m_canNotBeClosed)
                return false;
            if (!d.m_checkGuardStone)
                return true;
            long owner = BoardOwner();
            if (owner == 0L)
                return PrivateArea.CheckAccess(d.transform.position, 0f, flash: false);
            foreach (PrivateArea area in PrivateArea.m_allAreas)
            {
                if (area == null || !area.IsEnabled() || !area.IsInside(d.transform.position, 0f))
                    continue;
                if (area.m_piece.GetCreator() != owner && !area.IsPermitted(owner))
                    return false;
            }
            return true;
        }

        private long BoardOwner()
        {
            string id = _ai.Hireling.BoardId;
            HiringBoard? board = HiringBoard.Loaded.FirstOrDefault(b => b != null && b.Id == id);
            Piece? piece = board != null ? board.GetComponent<Piece>() : null;
            return piece != null ? piece.GetCreator() : 0L;
        }
    }
}
