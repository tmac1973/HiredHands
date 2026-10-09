using System;
using System.Collections.Generic;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings;
using VikingsForHire.Net;
using UnityEngine;

namespace VikingsForHire.Board
{
    /// <summary>Reads/writes a board's roster and applies roster ops. Runs only on the board's ZDO owner.</summary>
    internal static class BoardRosterOps
    {
        public static Roster Read(ZDO zdo)
        {
            byte[]? bytes = zdo.GetByteArray(BoardZdo.Roster);
            if (bytes == null || bytes.Length == 0)
                return new Roster();
            try
            {
                return Roster.Read(new HirelingSnapshot.PackageReader(new ZPackage(bytes)));
            }
            catch (Exception ex)
            {
                VfhLog.Exception(LogCat.Roster, "roster.read_failed", ex, ("board", BoardZdo.GetId(zdo)), ("bytes", bytes.Length));
                return new Roster();
            }
        }

        public static void Write(ZDO zdo, Roster roster)
        {
            var pkg = new ZPackage();
            roster.Write(new HirelingSnapshot.PackageWriter(pkg));
            zdo.Set(BoardZdo.Roster, pkg.GetArray());
        }

        public static OpResult Apply(ZDO zdo, RosterOp op)
        {
            string boardId = BoardZdo.GetId(zdo);
            int boardLevel = BoardZdo.GetLevel(zdo);
            var rules = new LevelRules(DataStore.Current);
            var costs = new CostCalculator(DataStore.Current, VfhConfig.RespawnCostFraction.Value);
            Roster roster = Read(zdo);
            double now = ZNet.instance.GetTimeSeconds();
            OpResult result;

            switch (op.Type)
            {
                case RosterOpType.Post:
                {
                    if (op.Level < 1 || op.Level > rules.MaxHirelingLevel(boardLevel))
                        return Done(zdo, null, boardId, op, new OpResult(OpOutcome.BadLevel, "$vfh_op_badlevel"));
                    if (!rules.JobUnlocked(boardLevel, op.Job))
                        return Done(zdo, null, boardId, op, new OpResult(OpOutcome.BadLevel, "$vfh_op_job_locked"));
                    OpOutcome can = roster.CanPost(op.Job, rules, boardLevel);
                    if (can != OpOutcome.Ok)
                        return Done(zdo, null, boardId, op, new OpResult(can, "$vfh_op_cap"));
                    var wallet = new BoardLedger.Wallet(zdo);
                    string paid = "";
                    if (!op.Free && !BoardLedger.TryPay(wallet, costs.HireCost(op.Job, op.Level), out paid))
                        return Done(zdo, null, boardId, op, new OpResult(OpOutcome.InsufficientFunds, "$vfh_op_funds"));
                    float min = VfhConfig.Get(VfhConfig.ArrivalDelayMinSeconds), max = VfhConfig.Get(VfhConfig.ArrivalDelayMaxSeconds);
                    var entry = new ContractEntry
                    {
                        ContractId = Guid.NewGuid().ToString("N"),
                        Hid = op.Hid,
                        Name = op.Name,
                        Job = op.Job,
                        Level = op.Level,
                        Radius = rules.ClampRadius(boardLevel, op.Job, op.Radius),
                        Stance = StanceRules.IsAllowed(op.Job, op.Stance) ? op.Stance : StanceRules.Default(op.Job),
                        Snapshot = op.Snapshot,
                        ArriveAt = op.Free ? now : now + UnityEngine.Random.Range(min, Mathf.Max(min, max)),
                        Paid = paid,
                    };
                    roster.Add(entry);
                    Telemetry.BalanceLog.Record("hire", ("board", boardId.Length > 4 ? boardId.Substring(0, 4) : boardId), ("boardLvl", boardLevel),
                        ("job", entry.Job), ("lvl", entry.Level), ("paid", paid), ("free", op.Free));
                    VfhLog.I(LogCat.Roster, "contract.posted", ("board", boardId), ("contract", entry.ContractId), ("hid", entry.Hid), ("name", entry.Name),
                        ("job", entry.Job), ("level", entry.Level), ("radius", entry.Radius), ("paid", paid), ("arriveIn", entry.ArriveAt - now), ("free", op.Free));
                    result = new OpResult(OpOutcome.Ok, $"{entry.Name} ($vfh_job_{entry.Job.ToString().ToLowerInvariant()}) $vfh_op_posted");
                    break;
                }
                case RosterOpType.Cancel:
                {
                    ContractEntry? e = roster.CancelPending(op.ContractId);
                    if (e == null)
                        return Done(zdo, null, boardId, op, new OpResult(OpOutcome.NotFound, "$vfh_op_notfound"));
                    if (!e.RespawnPending)
                        BoardLedger.Refund(new BoardLedger.Wallet(zdo), e.Paid);
                    VfhLog.I(LogCat.Roster, "contract.cancelled", ("board", boardId), ("contract", e.ContractId), ("name", e.Name), ("refund", e.RespawnPending ? "" : e.Paid));
                    result = new OpResult(OpOutcome.Ok, "$vfh_op_cancelled");
                    break;
                }
                case RosterOpType.SetPost:
                {
                    OpOutcome o = roster.SetPost(op.Hid, op.Post);
                    if (o != OpOutcome.Ok)
                        return Done(zdo, null, boardId, op, new OpResult(o, "$vfh_op_failed"));
                    MutationService.SubmitHireling(op.Hid, op.Post is GuardPost p
                        ? new HirelingOp { Post = (p.X, p.Y, p.Z, p.Yaw) }
                        : new HirelingOp { ClearPost = true });
                    VfhLog.I(LogCat.Roster, "contract.post", ("board", boardId), ("hid", op.Hid),
                        ("post", op.Post != null ? $"{op.Post.X:0.0},{op.Post.Y:0.0},{op.Post.Z:0.0} yaw {op.Post.Yaw:0}" : "cleared"));
                    result = new OpResult(OpOutcome.Ok, op.Post != null ? "$vfh_post_set" : "$vfh_post_cleared");
                    break;
                }
                case RosterOpType.Edit:
                {
                    JobType editJob = roster.ByHid(op.Hid)?.Job ?? JobType.GuardMelee;
                    OpOutcome o = roster.Edit(op.Hid, rules.ClampRadius(boardLevel, editJob, op.Radius), op.Stance);
                    if (o != OpOutcome.Ok)
                        return Done(zdo, null, boardId, op, new OpResult(o, "$vfh_op_failed"));
                    ContractEntry e = roster.ByHid(op.Hid)!;
                    MutationService.SubmitHireling(op.Hid, new HirelingOp { Stance = e.Stance, Radius = e.Radius });
                    VfhLog.I(LogCat.Roster, "contract.edited", ("board", boardId), ("hid", op.Hid), ("radius", e.Radius), ("stance", e.Stance));
                    result = new OpResult(OpOutcome.Ok, "$vfh_op_edited");
                    break;
                }
                case RosterOpType.Rename:
                {
                    string name = HirelingNames.Clean(op.Name);
                    OpOutcome o = roster.Rename(op.Hid, name);
                    if (o != OpOutcome.Ok)
                        return Done(zdo, null, boardId, op, new OpResult(o, "$vfh_op_failed"));
                    MutationService.SubmitHireling(op.Hid, new HirelingOp { Name = name });
                    VfhLog.I(LogCat.Roster, "contract.renamed", ("board", boardId), ("hid", op.Hid), ("name", name));
                    result = new OpResult(OpOutcome.Ok);
                    break;
                }
                case RosterOpType.Orders:
                {
                    // Orders don't touch the roster: apply to their own key and stop here.
                    Core.Orders.OrderList list = Core.Orders.OrderList.Parse(zdo.GetString(BoardZdo.Orders));
                    bool ok = op.Edit switch
                    {
                        OrderEdit.Add => list.Add(op.OrderItem, (Core.Orders.OrderKind)op.OrderKind, op.OrderTarget),
                        OrderEdit.Remove => list.Remove(op.OrderItem),
                        OrderEdit.Up => list.Move(op.OrderItem, -1),
                        OrderEdit.Down => list.Move(op.OrderItem, 1),
                        OrderEdit.Target => list.SetTarget(op.OrderItem, op.OrderTarget),
                        OrderEdit.Pause => list.SetPaused(op.OrderItem, op.OrderPaused),
                        OrderEdit.Clear => ClearOrders(list),
                        _ => false,
                    };
                    if (!ok)
                        return Done(zdo, null, boardId, op, new OpResult(OpOutcome.BadValue));
                    zdo.Set(BoardZdo.Orders, list.Serialize());
                    VfhLog.I(LogCat.Roster, "orders.edit", ("board", boardId), ("edit", op.Edit), ("item", op.OrderItem), ("target", op.OrderTarget), ("count", list.Orders.Count));
                    return Done(zdo, null, boardId, op, new OpResult(OpOutcome.Ok));
                }
                case RosterOpType.SetGather:
                {
                    OpOutcome o = roster.SetGather(op.Hid, op.SkipItems, op.NoHomeWork);
                    if (o != OpOutcome.Ok)
                        return Done(zdo, null, boardId, op, new OpResult(o, "$vfh_op_failed"));
                    ContractEntry e = roster.ByHid(op.Hid)!;
                    MutationService.SubmitHireling(op.Hid, new HirelingOp { SkipItems = e.SkipItems, NoHomeWork = e.NoHomeWork });
                    VfhLog.I(LogCat.Roster, "contract.gather", ("board", boardId), ("hid", op.Hid), ("skip", e.SkipItems), ("noHomeWork", e.NoHomeWork));
                    result = new OpResult(OpOutcome.Ok);
                    break;
                }
                case RosterOpType.Promote:
                {
                    ContractEntry? e = roster.ByHid(op.Hid);
                    if (e == null)
                        return Done(zdo, null, boardId, op, new OpResult(OpOutcome.NotFound, "$vfh_op_notfound"));
                    if (op.Level <= e.Level || op.Level > rules.MaxHirelingLevel(boardLevel))
                        return Done(zdo, null, boardId, op, new OpResult(OpOutcome.BadLevel, "$vfh_op_badlevel"));
                    Cost cost = costs.PromotionCost(e.Job, e.Level, op.Level);
                    if (!BoardLedger.TryPay(new BoardLedger.Wallet(zdo), cost, out string paid))
                        return Done(zdo, null, boardId, op, new OpResult(OpOutcome.InsufficientFunds, "$vfh_op_funds"));
                    int from = e.Level;
                    roster.Promote(op.Hid, op.Level, rules.MaxHirelingLevel(boardLevel));
                    MutationService.SubmitHireling(op.Hid, new HirelingOp { Level = op.Level });
                    VfhLog.I(LogCat.Roster, "contract.promoted", ("board", boardId), ("hid", op.Hid), ("from", from), ("to", op.Level), ("paid", paid));
                    result = new OpResult(OpOutcome.Ok, "$vfh_op_promoted");
                    break;
                }
                case RosterOpType.Dismiss:
                {
                    OpOutcome o = roster.Dismiss(op.Hid);
                    if (o != OpOutcome.Ok)
                        return Done(zdo, null, boardId, op, new OpResult(o, "$vfh_op_failed"));
                    SendAway(op.Hid, "$vfh_status_dismissed");
                    VfhLog.I(LogCat.Roster, "contract.dismissed", ("board", boardId), ("hid", op.Hid));
                    result = new OpResult(OpOutcome.Ok, "$vfh_op_dismissed");
                    break;
                }
                case RosterOpType.MarkDied:
                {
                    ContractEntry? e = roster.ByHid(op.Hid);
                    string name = e?.Name ?? op.Name;
                    bool permadeath = VfhConfig.PermadeathEnabled.Value;
                    OpOutcome o = roster.MarkDied(op.Hid, permadeath, now, VfhConfig.Get(VfhConfig.RespawnCooldownSeconds));
                    if (o != OpOutcome.Ok)
                        return Done(zdo, null, boardId, op, new OpResult(o));
                    VfhLog.I(LogCat.Roster, "contract.died", ("board", boardId), ("hid", op.Hid), ("name", name), ("permadeath", permadeath));
                    result = new OpResult(OpOutcome.Ok);
                    break;
                }
                case RosterOpType.Remove:
                {
                    bool removed = roster.RemoveEntry(op.Hid);
                    VfhLog.I(LogCat.Roster, "contract.removed", ("board", boardId), ("hid", op.Hid), ("found", removed));
                    result = new OpResult(removed ? OpOutcome.Ok : OpOutcome.NotFound);
                    break;
                }
                default:
                    return new OpResult(OpOutcome.BadValue);
            }

            return Done(zdo, roster, boardId, op, result);
        }

        /// <summary>Sets a hireling to leave: it drops its cargo, walks off and is removed from the roster when gone.</summary>
        public static void SendAway(string hid, string status) =>
            MutationService.SubmitHireling(hid, new HirelingOp
            {
                Mode = HirelingMode.Leaving, LeavingSince = (long)ZNet.instance.GetTimeSeconds(), Status = status,
            });

        private static bool ClearOrders(Core.Orders.OrderList list)
        {
            list.Orders.Clear();
            return true;
        }

        private static OpResult Done(ZDO zdo, Roster? roster, string boardId, RosterOp op, OpResult result)
        {
            if (roster != null)
                Write(zdo, roster);
            if (!result.Ok)
                VfhLog.I(LogCat.Roster, "contract.rejected", ("board", boardId), ("op", op.Type), ("hid", op.Hid), ("outcome", result.Outcome));
            return result;
        }
    }
}
