using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Config;
using VikingsForHire.Core.Chores;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings.Work.Steward
{
    /// <summary>
    /// Repairs damaged pieces in the work radius under vanilla's hammer rules: its crafting station in range (unless the
    /// world has no-workbench), ward access, free. Worst first, one piece per job, and never while enemies are about: not
    /// until none has been seen near the base for a while after a fight.
    /// </summary>
    internal sealed class RepairsChore : IChore
    {
        private const float EnemyRange = 30f;
        private const float StuckSeconds = 20f;

        private readonly StewardSteps _walk = new();
        private WearNTear? _piece;
        private float _lastEnemySeen = -999f;
        private float _waitLoggedAt = -999f;
        private static readonly List<string> _monsterNames = new();

        public ChoreKind Kind => ChoreKind.Repairs;
        public string? Missing { get; private set; }
        public float RestAfter { get; private set; }

        // Monsters about: any enemy of the Steward's (monsters only: the players' side counts deer and wild boar as
        // enemies too, and they never attack). Listed once per survey.
        private static List<Vector3> Monsters(StewardContext ctx)
        {
            Humanoid me = ctx.Hireling.Humanoid;
            var found = new List<Vector3>();
            _monsterNames.Clear();
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c.IsDead() || c == me || !BaseAI.IsEnemy(me, c) || c.GetFaction() == Character.Faction.AnimalsVeg ||
                    c.GetBaseAI() is not MonsterAI)
                    continue;
                if (Utils.DistanceXZ(c.transform.position, ctx.Home) < ctx.Radius + EnemyRange)
                {
                    found.Add(c.transform.position);
                    _monsterNames.Add($"{Utils.GetPrefabName(c.gameObject)}@{Vector3.Distance(c.transform.position, ctx.Position):0}m");
                }
            }
            return found;
        }

        // Not while a fight is on: no monster within 30 m of the Steward or of the piece, for a while after the last one.
        private bool Quiet(StewardContext ctx, List<Vector3> monsters, Vector3 piece)
        {
            if (monsters.Any(m => Vector3.Distance(m, ctx.Position) < EnemyRange || Vector3.Distance(m, piece) < EnemyRange))
            {
                _lastEnemySeen = Time.time;
                if (Time.time - _waitLoggedAt > 10f)
                {
                    _waitLoggedAt = Time.time;
                    VfhLog.D(LogCat.Smelter, "steward.repair_wait", ("hid", ctx.Hireling.Hid), ("monsters", string.Join(" ", _monsterNames)));
                }
            }
            return Time.time - _lastEnemySeen >= VfhConfig.StewardRepairQuietSeconds.Value;
        }

        // Vanilla: a piece made at a station can only be repaired with that kind of station in range.
        private static CraftingStation? MissingStation(Piece p, Vector3 from) =>
            p.m_craftingStation != null && !ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoWorkbench) &&
            CraftingStation.HaveBuildStationInRange(p.m_craftingStation.m_name, from) == null
                ? p.m_craftingStation : null;

        public IEnumerable<ChoreJob> Candidates(StewardContext ctx)
        {
            Missing = null;
            float below = VfhConfig.StewardRepairBelow.Value;
            var jobs = new List<ChoreJob>();
            var pieces = new List<Piece>();
            Piece.GetAllPiecesInRadius(ctx.Home, ctx.Radius, pieces);
            List<(Piece Piece, WearNTear Wnt, float Health)> damaged = pieces
                .Where(p => p != null && p.GetCreator() != 0L)
                .Select(p => (Piece: p, Wnt: p.GetComponent<WearNTear>()))
                .Where(x => x.Wnt != null && x.Wnt.m_nview != null && x.Wnt.m_nview.IsValid())
                .Select(x => (x.Piece, x.Wnt, Health: x.Wnt.GetHealthPercentage()))
                .Where(x => x.Health < below)
                .ToList();
            if (damaged.Count == 0)
                return jobs;
            List<Vector3> monsters = Monsters(ctx);
            foreach ((Piece p, WearNTear wnt, float health) in damaged)
            {
                if (!Quiet(ctx, monsters, p.transform.position))
                {
                    Missing ??= "$vfh_steward_wait_enemies";
                    continue;
                }
                if (Reservations.IsReservedByOther(wnt, ctx.Hireling.Hid) || Reservations.IsSkipped(wnt) || !ctx.BoardOwnerMayUse(p.transform.position))
                    continue;
                if (MissingStation(p, p.transform.position) is CraftingStation station)
                {
                    Missing ??= ActivityText.Make("$vfh_need_station", p.m_name, station.m_name);
                    continue;
                }
                float u = ChoreUrgency.Repair(health, below);
                jobs.Add(new ChoreJob
                {
                    Kind = Kind, Target = wnt, Urgency = u,
                    Score = ChoreUrgency.Score(u, Vector3.Distance(ctx.Position, p.transform.position), ctx.Radius),
                    Label = ActivityText.Make("$vfh_steward_repair", p.m_name),
                });
            }
            return jobs;
        }

        public void Begin(ChoreJob job, StewardContext ctx)
        {
            RestAfter = 1f; // a short pause between repairs, so it looks like work
            _walk.Reset();
            _piece = (WearNTear)job.Target;
            Reservations.TryReserve(_piece, ctx.Hireling.Hid);
        }

        public ChoreProgress Tick(HirelingAI ai, float dt)
        {
            Hireling h = ai.Hireling;
            WearNTear? wnt = _piece;
            if (wnt == null || wnt.m_nview == null || !wnt.m_nview.IsValid())
                return End(h, ChoreProgress.Done);
            if (_walk.SinceProgress > StuckSeconds)
            {
                VfhLog.I(LogCat.Smelter, "steward.repair_stuck", ("hid", h.Hid), ("piece", Utils.GetPrefabName(wnt.gameObject)), ("pos", h.transform.position));
                return End(h, ChoreProgress.Failed);
            }
            if (!_walk.Approach(ai, dt, wnt, wnt.transform.position))
                return ChoreProgress.Running;
            ai.Halt();
            ai.Face(wnt.transform.position + Vector3.up);
            Piece p = wnt.GetComponent<Piece>();
            // Vanilla checks the station from where the repairer stands.
            if (p != null && MissingStation(p, h.transform.position) != null)
                return End(h, ChoreProgress.Failed);
            float before = wnt.GetHealthPercentage();
            if (wnt.Repair() && p != null)
                p.m_placeEffect.Create(p.transform.position, p.transform.rotation);
            VfhLog.D(LogCat.Smelter, "steward.repair", ("hid", h.Hid), ("piece", Utils.GetPrefabName(wnt.gameObject)), ("before", System.Math.Round(before, 2)));
            return End(h, ChoreProgress.Done);
        }

        private ChoreProgress End(Hireling h, ChoreProgress result)
        {
            if (_piece != null)
                Reservations.Release(_piece, h.Hid);
            _piece = null;
            if (result == ChoreProgress.Failed)
                RestAfter = 5f;
            return result;
        }

        public void Abort(Hireling h) => End(h, ChoreProgress.Done);
    }
}
