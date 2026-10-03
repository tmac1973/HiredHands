using System;
using System.Collections;
using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;
using VikingsForHire.Board;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings;
using UnityEngine;

namespace VikingsForHire.Net
{
    internal enum TargetKind : byte
    {
        Board = 1,
        Hireling = 2,
    }

    /// <summary>The answer to a submitted op, delivered back to whoever submitted it.</summary>
    internal readonly struct OpResult
    {
        public readonly OpOutcome Outcome;
        public readonly string Message;

        public OpResult(OpOutcome outcome, string message = "")
        {
            Outcome = outcome;
            Message = message;
        }

        public bool Ok => Outcome == OpOutcome.Ok;
    }

    /// <summary>
    /// Every change to a board's roster or a hireling's state goes through here, so it's applied exactly once by the one
    /// machine allowed to write that object: its ZDO owner. Clients send ops to the server; the server forwards each op
    /// to the object's owner, or, when nobody has the object loaded, takes ownership and applies it to the ZDO itself
    /// (that's how a hireling dying far away still updates its board). The result goes back to the submitter.
    /// </summary>
    internal static class MutationService
    {
        public const string ApplyRpc = "VFH_ApplyOp";
        private const int MaxHops = 3;

        private sealed class Envelope
        {
            public TargetKind Kind;
            public string TargetId = "";
            public int RequestId;
            public long Origin;
            public int Hops;
            public byte[] Payload = Array.Empty<byte>();

            public ZPackage ToPackage()
            {
                var pkg = new ZPackage();
                pkg.Write((byte)Kind);
                pkg.Write(TargetId);
                pkg.Write(RequestId);
                pkg.Write(Origin);
                pkg.Write(Hops);
                pkg.Write(Payload);
                return pkg;
            }

            public static Envelope From(ZPackage pkg) => new()
            {
                Kind = (TargetKind)pkg.ReadByte(),
                TargetId = pkg.ReadString(),
                RequestId = pkg.ReadInt(),
                Origin = pkg.ReadLong(),
                Hops = pkg.ReadInt(),
                Payload = pkg.ReadByteArray(),
            };

            public string Describe()
            {
                try
                {
                    var reader = new HirelingSnapshot.PackageReader(new ZPackage(Payload));
                    return Kind == TargetKind.Board ? RosterOp.Read(reader).ToString() : HirelingOp.Read(reader).ToString();
                }
                catch (Exception)
                {
                    return "?";
                }
            }
        }

        private static CustomRPC _submitRpc = null!;
        private static CustomRPC _resultRpc = null!;
        private static readonly Dictionary<int, Action<OpResult>> Callbacks = new();
        private static int _nextRequest = 1;

        public static void Register()
        {
            _submitRpc = NetworkManager.Instance.AddRPC("VFH_SubmitOp", OnServerSubmit, OnClientSubmitIgnored);
            _resultRpc = NetworkManager.Instance.AddRPC("VFH_OpResult", OnResultAtServer, OnResultAtClient);
        }

        /// <summary>Called by HiringBoard and Hireling in Awake so the server can forward ops to them.</summary>
        public static void RegisterApply(ZNetView nview) =>
            nview.Register<ZPackage>(ApplyRpc, (sender, pkg) =>
                VfhLog.Guard(LogCat.Net, "op.apply_rpc_failed", () => OnApplyRouted(nview, Envelope.From(pkg))));

        public static void SubmitBoard(string boardId, RosterOp op, Action<OpResult>? onResult = null, bool allowLocal = true) =>
            Submit(TargetKind.Board, boardId, Pack(op.Write), onResult, allowLocal);

        public static void SubmitHireling(string hid, HirelingOp op, Action<OpResult>? onResult = null, bool allowLocal = true) =>
            Submit(TargetKind.Hireling, hid, Pack(op.Write), onResult, allowLocal);

        private static byte[] Pack(Action<IPackageWriter> write)
        {
            var pkg = new ZPackage();
            write(new HirelingSnapshot.PackageWriter(pkg));
            return pkg.GetArray();
        }

        private static void Submit(TargetKind kind, string targetId, byte[] payload, Action<OpResult>? onResult, bool allowLocal)
        {
            if (ZNet.instance == null || targetId.Length == 0)
            {
                onResult?.Invoke(new OpResult(OpOutcome.NotFound));
                return;
            }
            var env = new Envelope
            {
                Kind = kind, TargetId = targetId, RequestId = _nextRequest++, Origin = ZDOMan.GetSessionID(), Payload = payload,
            };
            if (onResult != null)
                Callbacks[env.RequestId] = onResult;
            VfhLog.D(LogCat.Net, "op.submit", (kind == TargetKind.Board ? "board" : "hid", targetId), ("op", env.Describe()), ("req", env.RequestId));

            // Fast path: we already own a loaded instance of the target.
            if (allowLocal && LocalOwned(kind, targetId) is ZDO local)
            {
                Finish(env, Apply(local, env), "local");
                return;
            }
            if (ZNet.instance.IsServer())
                Route(env);
            else
                _submitRpc.SendPackage(ZRoutedRpc.instance.GetServerPeerID(), env.ToPackage());
        }

        private static ZDO? LocalOwned(TargetKind kind, string id)
        {
            if (kind == TargetKind.Board)
            {
                foreach (HiringBoard b in HiringBoard.Loaded)
                    if (b != null && b.Zdo != null && b.Id == id && b.Zdo.IsOwner())
                        return b.Zdo;
            }
            else
            {
                foreach (Hireling h in Hireling.Loaded)
                    if (h != null && h.Zdo != null && h.Hid == id && h.IsOwner)
                        return h.Zdo;
            }
            return null;
        }

        // Server: forward to the owner, or apply to the ZDO directly when nobody has it loaded.
        private static void Route(Envelope env)
        {
            ZDO? zdo = env.Kind == TargetKind.Board ? WorldIndex.Board(env.TargetId) : WorldIndex.Hireling(env.TargetId);
            if (zdo == null)
            {
                VfhLog.W(LogCat.Net, "op.target_missing", ("kind", env.Kind), ("target", env.TargetId), ("op", env.Describe()));
                Reply(env, new OpResult(OpOutcome.NotFound));
                return;
            }
            long me = ZDOMan.GetSessionID();
            long owner = zdo.GetOwner();
            if (owner != 0L && owner != me && ZNet.instance.GetPeer(owner) != null && env.Hops < MaxHops)
            {
                env.Hops++;
                ZRoutedRpc.instance.InvokeRoutedRPC(owner, zdo.m_uid, ApplyRpc, env.ToPackage());
                VfhLog.D(LogCat.Net, "op.forward", ("to", owner), ("zdo", zdo.m_uid.ToString()), ("op", env.Describe()), ("req", env.RequestId), ("hops", env.Hops));
                return;
            }
            if (owner != me)
                zdo.SetOwner(me);
            Finish(env, Apply(zdo, env), "server");
        }

        // Owner (forwarded to us). If ownership moved on meanwhile, hand it back to the server to route again.
        private static void OnApplyRouted(ZNetView nview, Envelope env)
        {
            if (!nview.IsValid())
                return;
            if (!nview.IsOwner())
            {
                VfhLog.D(LogCat.Net, "op.bounce", ("req", env.RequestId), ("hops", env.Hops));
                if (ZNet.instance.IsServer())
                    Route(env);
                else
                    _submitRpc.SendPackage(ZRoutedRpc.instance.GetServerPeerID(), env.ToPackage());
                return;
            }
            Finish(env, Apply(nview.GetZDO(), env), "owner");
        }

        private static OpResult Apply(ZDO zdo, Envelope env)
        {
            OpResult result = new(OpOutcome.BadValue);
            VfhLog.Guard(LogCat.Net, "op.apply_failed", () =>
            {
                var reader = new HirelingSnapshot.PackageReader(new ZPackage(env.Payload));
                result = env.Kind == TargetKind.Board
                    ? BoardRosterOps.Apply(zdo, RosterOp.Read(reader))
                    : HirelingOps.Apply(zdo, HirelingOp.Read(reader));
            }, ("req", env.RequestId));
            return result;
        }

        private static void Finish(Envelope env, OpResult result, string where)
        {
            VfhLog.D(LogCat.Net, "op.apply", ("where", where), (env.Kind == TargetKind.Board ? "board" : "hid", env.TargetId),
                ("op", env.Describe()), ("outcome", result.Outcome), ("req", env.RequestId));
            Reply(env, result);
        }

        private static void Reply(Envelope env, OpResult result)
        {
            if (env.Origin == ZDOMan.GetSessionID())
            {
                Deliver(env.RequestId, result);
                return;
            }
            var pkg = new ZPackage();
            pkg.Write(env.RequestId);
            pkg.Write((int)result.Outcome);
            pkg.Write(result.Message);
            _resultRpc.SendPackage(env.Origin, pkg);
        }

        private static void Deliver(int requestId, OpResult result)
        {
            if (Callbacks.TryGetValue(requestId, out Action<OpResult> cb))
            {
                Callbacks.Remove(requestId);
                VfhLog.Guard(LogCat.Net, "op.callback_failed", () => cb(result), ("req", requestId));
            }
        }

        private static IEnumerator OnServerSubmit(long sender, ZPackage package)
        {
            Envelope env = Envelope.From(package);
            VfhLog.D(LogCat.Net, "op.received", ("from", sender), ("op", env.Describe()), ("req", env.RequestId));
            VfhLog.Guard(LogCat.Net, "op.route_failed", () => Route(env));
            yield break;
        }

        private static IEnumerator OnClientSubmitIgnored(long sender, ZPackage package)
        {
            yield break;
        }

        // A result addressed to the server itself (host submitted through a forward).
        private static IEnumerator OnResultAtServer(long sender, ZPackage package)
        {
            int id = package.ReadInt();
            Deliver(id, new OpResult((OpOutcome)package.ReadInt(), package.ReadString()));
            yield break;
        }

        private static IEnumerator OnResultAtClient(long sender, ZPackage package)
        {
            int id = package.ReadInt();
            Deliver(id, new OpResult((OpOutcome)package.ReadInt(), package.ReadString()));
            yield break;
        }
    }
}
