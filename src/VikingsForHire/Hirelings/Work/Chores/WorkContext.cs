using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Data;

namespace VikingsForHire.Hirelings.Work.Chores
{
    /// <summary>What a survey knows about the worker (Steward, Farmer, Cook) and its base, built once per survey and shared by every chore.</summary>
    internal sealed class WorkContext
    {
        public readonly Hireling Hireling;
        public readonly Vector3 Home;
        public readonly float Radius;
        public readonly int Level;
        public readonly JobData Job;
        public readonly List<Container> Chests;

        /// <summary>Every chest in the area, skipped ones too: for counting room (a skip is only about taking from it).</summary>
        public readonly List<Container> AllChests;
        public readonly int KeepMin;
        private readonly Dictionary<string, int> _available = new();
        private Dictionary<string, int>? _carried;
        private readonly Dictionary<string, int> _baseReserve;

        /// <summary>An extra amount of an item every worker leaves in the chests (the farm's seed reserve, for the Farmer's produce planting and the Cook).</summary>
        public System.Func<string, int>? ExtraReserve { get; set; }

        public WorkContext(Hireling h)
        {
            Hireling = h;
            Home = h.Home;
            Radius = h.Radius;
            Level = h.Level;
            Job = DataStore.Current.Jobs.TryGetValue(h.Job, out JobData? j) ? j : new JobData();
            _baseReserve = DataStore.Current.Jobs.TryGetValue(JobType.Smelter, out JobData? s) ? s.KeepInStorage : new Dictionary<string, int>();
            AllChests = ChestFinder.Find(Home, Radius);
            Chests = AllChests.Where(c => !Reservations.IsSkipped(c)).ToList();
            KeepMin = VfhConfig.ChestReserve;
        }

        public Vector3 Position => Hireling.transform.position;

        private long? _owner;

        /// <summary>Whether the board's owner could use something here under the wards (as for doors and chests).</summary>
        public bool BoardOwnerMayUse(Vector3 at)
        {
            _owner ??= Nav.DoorRules.BoardOwner(Hireling.BoardId);
            long owner = _owner.Value;
            if (owner == 0L)
                return PrivateArea.CheckAccess(at, 0f, flash: false);
            foreach (PrivateArea area in PrivateArea.m_allAreas)
            {
                if (area == null || !area.IsEnabled() || !area.IsInside(at, 0f))
                    continue;
                if (area.m_piece.GetCreator() != owner && !area.IsPermitted(owner))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// How many of an item the chests can give: each chest keeps its minimum (KeepMinimumInChest), and the total
        /// keeps the data file's reserve for that item (keepInStorage, e.g. Wood 50).
        /// </summary>
        public int Available(string prefab)
        {
            if (_available.TryGetValue(prefab, out int n))
                return n;
            int above = 0, total = 0;
            foreach (Container c in Chests.Where(c => !c.IsInUse()))
            {
                int inChest = c.GetInventory().GetAllItems().Where(i => i.m_dropPrefab != null && i.m_dropPrefab.name == prefab).Sum(i => i.m_stack);
                total += inChest;
                above += System.Math.Max(0, inChest - KeepMin);
            }
            // The base's reserves (the Steward's keepInStorage, e.g. Wood 50) hold for every worker, plus this job's own and any extra.
            int keep = System.Math.Max(Job.KeepInStorage.TryGetValue(prefab, out int own) ? own : 0, _baseReserve.TryGetValue(prefab, out int b) ? b : 0)
                       + (ExtraReserve?.Invoke(prefab) ?? 0);
            if (keep > 0)
                above = System.Math.Min(above, System.Math.Max(0, total - keep));
            _available[prefab] = above;
            return above;
        }

        public Dictionary<string, int> Carried => _carried ??= Hireling.CargoInventory!.GetAllItems().Where(i => i.m_dropPrefab != null)
            .GroupBy(i => i.m_dropPrefab.name).ToDictionary(g => g.Key, g => g.Sum(i => i.m_stack));

        public int FreeSlots => System.Math.Max(0, Hireling.CargoSlots - Hireling.CargoInventory!.NrOfItems());

        /// <summary>The nearest chest holding more of any of these items than it must keep.</summary>
        public Container? NearestChestWith(IEnumerable<string> prefabs)
        {
            var want = new HashSet<string>(prefabs);
            return Chests.Where(c => !c.IsInUse() && c.GetInventory().GetAllItems()
                    .Where(i => i.m_dropPrefab != null && want.Contains(i.m_dropPrefab.name))
                    .GroupBy(i => i.m_dropPrefab.name).Any(g => g.Sum(i => i.m_stack) > KeepMin))
                .OrderBy(c => Vector3.Distance(Position, c.transform.position)).FirstOrDefault();
        }
    }
}
