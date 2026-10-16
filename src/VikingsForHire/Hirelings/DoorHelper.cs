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
        // Pens: no opening a door while a tame stands this close to it (it would walk out), for at most AnimalWaitSeconds;
        // with tames about, a door is shut as soon as the hireling is QuickCloseDistance past it.
        private const float AnimalNearDoor = 3f;
        private const float AnimalWaitSeconds = 20f;
        private const float AnimalsAbout = 6f;
        private const float QuickCloseDistance = 0.8f;
        // Which side of each door it opened it from (the sign of its position along the door's forward), so a quick close
        // waits until it's through to the other side, never shutting the door in its own face.
        private readonly Dictionary<Door, float> _openedSide = new();

        private static float SideOf(Door d, Vector3 at) => Mathf.Sign(Vector3.Dot(at - d.transform.position, d.transform.forward));
        private Door? _waitingAt;
        private float _waitingSince;
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
            if (_detour != null && (_detour.m_nview == null || !_detour.m_nview.IsValid() || Nav.DoorRules.AvoidedBy(_detour, _ai.Hireling)))
                _detour = null;
            if (_detour == null)
            {
                float straight = Vector3.Distance(me, goal);
                _detour = Doors(me, DetourRange)
                    .Where(d => !Nav.DoorRules.AvoidedBy(d, _ai.Hireling) && (IsOpen(d) || Usable(d)) && Vector3.Distance(d.transform.position, goal) < straight)
                    .OrderBy(d => Vector3.Distance(me, d.transform.position) + Vector3.Distance(d.transform.position, goal))
                    .FirstOrDefault();
                if (_detour == null)
                    return null;
                VfhLog.D(LogCat.AI, "door.detour", ("hid", _ai.Hireling.Hid), ("door", _detour.transform.position), ("goal", goal), ("open", IsOpen(_detour)));
            }
            Vector3 near = Side(_detour, me, 1.2f);
            if (Utils.DistanceXZ(me, near) > 0.8f)
                return near;
            if (!IsOpen(_detour) && !MayOpen(_detour))
                return near; // wait by the door until the animals move off
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

        /// <summary>A tamed animal (not a hireling or a player) within this distance of a door.</summary>
        public static bool AnimalsNear(Door d, float distance) =>
            Character.GetAllCharacters().Any(c => c != null && !c.IsPlayer() && c.IsTamed() && !c.IsDead() && c.GetComponent<Hireling>() == null &&
                                                  Vector3.Distance(c.transform.position, d.transform.position) <= distance);

        // Not while an animal stands by the door (a pen's gate: it would walk out), unless it's been waiting too long.
        internal bool MayOpen(Door d)
        {
            if (!AnimalsNear(d, AnimalNearDoor))
            {
                if (_waitingAt == d)
                    _waitingAt = null;
                return true;
            }
            if (_waitingAt != d)
            {
                _waitingAt = d;
                _waitingSince = Time.time;
                VfhLog.I(LogCat.AI, "door.waited_for_animals", ("hid", _ai.Hireling.Hid), ("door", d.transform.position));
            }
            if (Time.time - _waitingSince < AnimalWaitSeconds)
                return false;
            VfhLog.I(LogCat.AI, "door.animals_still_there", ("hid", _ai.Hireling.Hid), ("door", d.transform.position), ("waited", AnimalWaitSeconds));
            _waitingAt = null;
            return true;
        }

        /// <summary>Times hirelings opened each door, by its instance id (tests).</summary>
        public static readonly Dictionary<int, int> OpenedCount = new();

        /// <summary>Times a hireling opened a door with the one beside it already open (tests: a pair of doors should be one way through).</summary>
        public static int OpenedBesideOpen;
        private const float BesideRange = 3f;

        private void Open(Door d, Vector3 me)
        {
            OpenedCount[d.GetInstanceID()] = (OpenedCount.TryGetValue(d.GetInstanceID(), out int n) ? n : 0) + 1;
            if (Doors(d.transform.position, BesideRange).Any(o => o != d && IsOpen(o)))
                OpenedBesideOpen++;
            _openedSide[d] = SideOf(d, me);
            // Same as a player using it from where we stand: it swings away from us.
            Nav.DoorRules.Open(d, me);
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

            // One door at a time, the one most in its way: two doors side by side (a pen's double doors) were both opened,
            // and the second stayed open while the Rancher worked by the gate. An open door ahead is the way through.
            Door? best = null;
            float bestOff = float.MaxValue;
            bool openAhead = false;
            foreach (Door d in Doors(me, AheadRange))
            {
                Vector3 toDoor = d.transform.position - me;
                toDoor.y = 0f;
                bool ahead = heading.sqrMagnitude < 0.01f || Vector3.Dot(toDoor.normalized, heading.normalized) > 0.2f;
                if (!ahead && d != _detour)
                    continue;
                if (IsOpen(d))
                {
                    openAhead |= Usable(d) || d == _detour;
                    continue;
                }
                if (!Usable(d))
                    continue;
                // How far the door is off its line of travel (the detour door always wins).
                float off = d == _detour ? -1f : heading.sqrMagnitude < 0.01f ? toDoor.magnitude : Vector3.Cross(heading.normalized, toDoor).magnitude;
                if (off < bestOff)
                {
                    best = d;
                    bestOff = off;
                }
            }
            if (best != null && (!openAhead || best == _detour) && MayOpen(best))
            {
                Open(best, me);
                if (best == _detour)
                {
                    _detour = null;
                    StartThrough(best, me);
                }
            }
            CloseBehind(false);
        }

        /// <summary>Closes the doors this hireling opened once it's clear of them (all of them when <paramref name="now"/>).</summary>
        // Another hireling (or this one) still in the doorway: shutting the door on it would block its way through.
        private static bool HirelingInDoorway(Door d) =>
            Hireling.Loaded.Any(h => h != null && Vector3.Distance(h.transform.position, d.transform.position) < PlayerClearance);

        private static bool OtherHirelingInDoorway(Door d, Hireling? me) =>
            Hireling.Loaded.Any(h => h != null && h != me && Vector3.Distance(h.transform.position, d.transform.position) < QuickCloseDistance);

        /// <summary>A door a link route opened: close it behind like the ones opened here.</summary>
        public void MarkOpened(Door d)
        {
            _opened[d] = Time.time;
            _openedSide[d] = SideOf(d, _ai.transform.position);
            OpenedCount[d.GetInstanceID()] = (OpenedCount.TryGetValue(d.GetInstanceID(), out int n) ? n : 0) + 1;
        }

        public void CloseBehind(bool now)
        {
            if (_opened.Count == 0)
                return;
            if (!VfhConfig.HirelingsCloseDoors.Value)
            {
                _opened.Clear();
                return;
            }
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
                // With animals about (a pen), shut it as soon as we're through, before one wanders out.
                // With animals about: shut as soon as it's through to the other side and clear of the doorway.
                bool through = _openedSide.TryGetValue(d, out float from) && SideOf(d, me) != from;
                bool quick = !now && AnimalsNear(d, AnimalsAbout) && through && Utils.DistanceXZ(me, d.transform.position) > QuickCloseDistance;
                bool clear = now || quick || (Vector3.Distance(me, d.transform.position) > CloseDistance && Time.time - kv.Value > CloseAfterSeconds);
                if (!clear || Player.GetClosestPlayer(d.transform.position, PlayerClearance) != null ||
                    (quick ? OtherHirelingInDoorway(d, _ai?.Hireling) : HirelingInDoorway(d)))
                    continue;
                if (quick)
                    VfhLog.D(LogCat.AI, "door.closed_quick", ("hid", _ai?.Hireling.Hid ?? ""), ("door", d.transform.position));
                if (IsOpen(d))
                {
                    d.m_nview.InvokeRPC("UseDoor", false);
                    _openedSide.Remove(d);
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

        private static bool IsOpen(Door d) => Nav.DoorRules.IsOpen(d);

        private bool Usable(Door d) => Nav.DoorRules.Usable(d, Nav.DoorRules.BoardOwner(_ai.Hireling.BoardId), _ai.Hireling);
    }
}
