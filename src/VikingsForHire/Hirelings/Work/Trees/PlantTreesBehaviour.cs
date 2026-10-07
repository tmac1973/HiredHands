using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings.Nav;
using VikingsForHire.Hirelings.Work.Chores;

namespace VikingsForHire.Hirelings.Work.Trees
{
    /// <summary>
    /// A woodcutter keeping the tree patches in its work radius planted: at most once a minute it looks for a patch with
    /// free spots and seeds in the chests for a kind its axe can fell, fetches up to 10 seeds and plants them, then goes
    /// back to felling. Grown trees in a patch are felled like any other.
    /// </summary>
    internal sealed class PlantTreesBehaviour : IHirelingBehaviour
    {
        private const float CheckSeconds = 60f;
        private const int MaxPerTrip = 10;
        private const float StuckSeconds = 20f;
        private const float PlantEvery = 0.8f;

        private readonly WorkSteps _walk = new();
        private float _nextCheck;
        private bool _active;
        private TreeKind? _kind;
        private readonly List<Vector3> _spots = new();
        private Container? _chest;
        private int _fetch;
        private float _progressAt;
        private float _nextPlant;
        private int _planted;
        private long _owner;

        /// <summary>Saplings woodcutters have planted since login (for the tests).</summary>
        public static int Planted;

        public string Name => "PlantTrees";
        public int Priority => 210; // a short trip now and then, ahead of felling

        public bool Wants(HirelingAI ai)
        {
            Hireling h = ai.Hireling;
            if (h.Mode != HirelingMode.Working || !h.WorksAtHome || !VfhConfig.WoodcuttersPlantTrees.Value || h.CargoInventory == null)
                return Stop(h);
            if (_active)
                return true;
            if (Time.time < _nextCheck)
                return false;
            _nextCheck = Time.time + CheckSeconds;
            return Plan(h);
        }

        private bool Plan(Hireling h)
        {
            var ctx = new WorkContext(h);
            _owner = DoorRules.BoardOwner(h.BoardId);
            foreach (TreePatch patch in TreePatch.Within(h.Home, h.Radius))
            {
                IEnumerable<TreeKind> kinds = TreeCatalog.All.Where(k => k.ToolTier <= h.ToolTier && (patch.Kind.Length == 0 || patch.Kind == k.SaplingName));
                foreach (TreeKind kind in kinds.OrderByDescending(k => ctx.Available(k.Seed)))
                {
                    int carried = ctx.Carried.TryGetValue(kind.Seed, out int c) ? c : 0;
                    int seeds = ctx.Available(kind.Seed) + carried;
                    if (seeds < kind.SeedAmount)
                        continue;
                    List<Vector3> spots = TreeGrid.Spots(patch, kind, _owner, Mathf.Min(MaxPerTrip, seeds / kind.SeedAmount));
                    if (spots.Count == 0)
                        continue;
                    _kind = kind;
                    _spots.Clear();
                    _spots.AddRange(spots);
                    _fetch = Mathf.Max(0, spots.Count * kind.SeedAmount - carried);
                    _chest = _fetch > 0 ? ctx.NearestChestWith(new[] { kind.Seed }) : null;
                    _planted = 0;
                    _active = true;
                    _walk.Reset();
                    _progressAt = Time.time;
                    h.FetchingSupplies = true;
                    VfhLog.D(LogCat.Work, "woodcutter.plant_plan", ("hid", h.Hid), ("kind", kind.SaplingName), ("spots", spots.Count), ("fetch", _fetch));
                    return true;
                }
            }
            return false;
        }

        public void Tick(HirelingAI ai, float dt)
        {
            Hireling h = ai.Hireling;
            TreeKind? kind = _kind;
            if (kind == null)
            {
                Stop(h);
                return;
            }
            h.SetActivity("$vfh_status_planting_trees");
            if (_chest != null)
            {
                if (_walk.SinceProgress > StuckSeconds)
                {
                    Reservations.Skip(_chest, 60f);
                    Stop(h);
                    return;
                }
                if (!_walk.Approach(ai, dt, _chest, _chest.transform.position))
                    return;
                ai.Halt();
                WorkSteps.TakeFromChest(_chest, h, new Dictionary<string, int> { [kind.Seed] = _fetch }, VfhConfig.ChestReserve);
                _chest = null;
                _progressAt = Time.time;
                return;
            }
            string seed = WorkSteps.SharedName(kind.Seed);
            if (_spots.Count == 0 || h.CargoInventory!.CountItems(seed) < kind.SeedAmount)
            {
                Stop(h);
                return;
            }
            Vector3 spot = _spots[0];
            if (Utils.DistanceXZ(ai.transform.position, spot) > 1.8f)
            {
                if (Time.time - _progressAt > StuckSeconds)
                {
                    TreeGrid.Avoid(spot);
                    _spots.RemoveAt(0);
                    _progressAt = Time.time;
                    return;
                }
                ai.WalkTo(dt, spot, 1.2f, run: false);
                return;
            }
            ai.Halt();
            ai.Face(spot);
            if (Time.time < _nextPlant)
                return;
            _nextPlant = Time.time + PlantEvery;
            _progressAt = Time.time;
            _spots.RemoveAt(0);
            if (!TreeGrid.IsFree(kind, spot, _owner))
                return;
            PlantAt(kind, spot);
            h.CargoInventory.RemoveItem(seed, kind.SeedAmount);
            _planted++;
            Planted++;
        }

        // As the cultivator places a sapling, owned by the board's owner.
        private void PlantAt(TreeKind kind, Vector3 spot)
        {
            TerrainModifier.SetTriggerOnPlaced(true);
            GameObject go;
            try
            {
                go = Object.Instantiate(kind.Sapling, spot, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            }
            finally
            {
                TerrainModifier.SetTriggerOnPlaced(false);
            }
            if (go.GetComponent<Piece>() is Piece piece)
            {
                if (_owner != 0L && piece.m_nview != null && piece.m_nview.IsValid() && piece.GetCreator() == 0L)
                {
                    piece.m_creator = _owner;
                    piece.m_nview.GetZDO().Set(ZDOVars.s_creator, _owner);
                }
                piece.m_placeEffect.Create(spot, go.transform.rotation, go.transform);
            }
        }

        private bool Stop(Hireling h)
        {
            if (_active)
            {
                if (_planted > 0)
                    VfhLog.I(LogCat.Work, "woodcutter.planted", ("hid", h.Hid), ("kind", _kind?.SaplingName ?? ""), ("n", _planted));
                h.FetchingSupplies = false;
            }
            _active = false;
            _kind = null;
            _chest = null;
            _spots.Clear();
            return false;
        }
    }
}
