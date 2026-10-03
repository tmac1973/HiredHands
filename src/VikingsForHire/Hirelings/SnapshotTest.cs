using System.Collections;
using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings
{
    /// <summary>
    /// Snapshot → bytes → destroy → respawn 3m away → compare every stored value. Proves a hireling survives the trip that
    /// portals, ships, respawns and return-home will all use.
    /// </summary>
    internal static class SnapshotTest
    {
        /// <summary>"true", "false: …diffs" or "error: …" from the last run.</summary>
        public static string LastResult { get; private set; } = "not run";

        // Values that legitimately differ on a fresh instance (physics sync) are left out of the comparison.
        // Health is compared as the character's actual health instead: Character drops the stored value when it's full.
        private static readonly HashSet<int> Ignore = new[] { "vel", "body_vel", "body_avel", "BodyVelocity", "BodyAngularVelocity", "relPos", "relRot", "noise", "attachJoint", "health" }
            .Select(n => n.GetStableHashCode()).ToHashSet();

        public static IEnumerator Run(Hireling original)
        {
            ZDO zdo = original.Zdo!;
            string hid = original.Hid;
            Vector3 pos = original.transform.position + original.transform.right * 3f;
            Quaternion rot = original.transform.rotation;

            HirelingSnapshot before = HirelingSnapshot.FromZdo(zdo);
            byte[] bytes = before.ToBytes();
            HirelingSnapshot decoded = HirelingSnapshot.FromBytes(bytes);
            VfhLog.I(LogCat.Hireling, "snapshot.capture", ("hid", hid), ("values", before.ValueCount), ("bytes", bytes.Length));

            ZNetView view = original.GetComponent<ZNetView>();
            view.ClaimOwnership();
            ZNetScene.instance.Destroy(original.gameObject);

            ZDO spawned = decoded.Spawn(pos, rot);
            float waited = 0f;
            while (ZNetScene.instance.FindInstance(spawned) == null && waited < 5f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            if (ZNetScene.instance.FindInstance(spawned) == null)
            {
                LastResult = "error: respawned hireling never appeared";
                VfhLog.E(LogCat.Hireling, "snapshot.respawn_missing", ("hid", hid));
                yield break;
            }
            // Let Awake/Start run (gear, stats) before comparing.
            yield return new WaitForSeconds(1f);

            HirelingSnapshot after = HirelingSnapshot.FromZdo(spawned);
            List<string> diffs = before.Differences(after, Ignore);

            Hireling? rebuilt = ZNetScene.instance.FindInstance(spawned)?.GetComponent<Hireling>();
            float max = rebuilt != null ? rebuilt.Humanoid.GetMaxHealth() : 0f;
            float healthBefore = before.Floats.TryGetValue("health".GetStableHashCode(), out float hb) ? hb : max;
            float healthAfter = rebuilt != null ? rebuilt.Humanoid.GetHealth() : -1f;
            if (Mathf.Abs(healthBefore - healthAfter) > 0.5f)
                diffs.Add($"health={healthBefore}/{healthAfter}");
            LastResult = diffs.Count == 0 ? "true" : "false: " + string.Join("; ", diffs);
            if (diffs.Count == 0)
                VfhLog.I(LogCat.Hireling, "snapshot.roundtrip", ("hid", hid), ("ok", true), ("values", after.ValueCount));
            else
                VfhLog.W(LogCat.Hireling, "snapshot.roundtrip", ("hid", hid), ("ok", false), ("diffs", string.Join("; ", diffs)));
        }
    }
}
