using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Net;

namespace VikingsForHire.Hirelings
{
    /// <summary>Applies a HirelingOp to the hireling's ZDO (on its owner); the live hireling reacts within a second.</summary>
    internal static class HirelingOps
    {
        public static OpResult Apply(ZDO zdo, HirelingOp op)
        {
            if (op.Mode.HasValue) zdo.Set(HirelingZdo.Mode, (int)op.Mode.Value);
            if (op.Stance.HasValue) zdo.Set(HirelingZdo.Stance, (int)op.Stance.Value);
            if (op.Radius.HasValue) zdo.Set(HirelingZdo.Radius, op.Radius.Value);
            if (op.Level.HasValue) zdo.Set(HirelingZdo.Level, op.Level.Value);
            if (op.LeavingSince.HasValue) zdo.Set(HirelingZdo.LeavingSince, op.LeavingSince.Value);
            if (op.Status != null) zdo.Set(HirelingZdo.Status, op.Status);
            if (op.Owner.HasValue) zdo.Set(HirelingZdo.Owner, op.Owner.Value);
            if (op.OwnerName != null) zdo.Set(HirelingZdo.OwnerName, op.OwnerName);
            if (op.FollowMode.HasValue) zdo.Set(HirelingZdo.FollowMode, (int)op.FollowMode.Value);
            if (op.StayPos is (float x, float y, float z)) zdo.Set(HirelingZdo.StayPos, new UnityEngine.Vector3(x, y, z));
            if (op.DeliverPending.HasValue) zdo.Set(HirelingZdo.DeliverPending, op.DeliverPending.Value);
            if (op.Post is (float px, float py, float pz, float yaw))
            {
                zdo.Set(HirelingZdo.Post, new UnityEngine.Vector3(px, py, pz));
                zdo.Set(HirelingZdo.PostYaw, yaw);
                zdo.Set(HirelingZdo.Posted, true);
            }
            if (op.ClearPost == true) zdo.Set(HirelingZdo.Posted, false);
            VfhLog.D(LogCat.Hireling, "hireling.fields", ("hid", zdo.GetString(HirelingZdo.Hid)), ("op", op.ToString()));
            return new OpResult(OpOutcome.Ok);
        }
    }
}
