using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Followers;
using UnityEngine;

namespace VikingsForHire.Testing
{
    /// <summary>Phase 14 (portals, ships) fixtures and checks.</summary>
    internal static class FixturesTravel
    {
        public static void Register()
        {
            Fixtures.Add("portal_pair", "<tag> <distance> - two wood portals with that tag: one 4 m in front of you, one that far away", PortalPair);
            Fixtures.Add("portal_use", "<tag> - step through the nearest portal with that tag, as if you'd walked into it", PortalUse);
            TestHarness.RegisterCheck("portal_linked", "<tag> - whether the nearest portal with that tag is connected to its partner", args =>
                Nearest(args.ElementAtOrDefault(0) ?? "") is TeleportWorld p &&
                p.GetComponent<ZNetView>().GetZDO().GetConnectionZDOID(ZDOExtraData.ConnectionType.Portal) != ZDOID.None ? "true" : "false");
            TestHarness.RegisterCheck("transit_count", "- followers on their way through a portal with you", _ => TeleportTravel.InTransit.ToString());
        }

        private static IEnumerator PortalPair(string[] args)
        {
            string tag = args.ElementAtOrDefault(0) ?? "vfhtest";
            float distance = float.Parse(args.ElementAtOrDefault(1) ?? "60", CultureInfo.InvariantCulture);
            Transform me = Player.m_localPlayer.transform;
            Vector3 forward = Vector3.ProjectOnPlane(me.forward, Vector3.up).normalized;
            foreach (Vector3 at in new[] { me.position + forward * 4f, me.position + forward * distance })
            {
                Vector3 p = at;
                p.y = ZoneSystem.instance.GetGroundHeight(p);
                GameObject portal = FixturesBoard.Spawn("portal_wood", p, Quaternion.LookRotation(-forward));
                portal.GetComponent<ZNetView>().GetZDO().Set(ZDOVars.s_tag, tag);
            }
            VfhLog.I(LogCat.Test, "fixture.portal_pair", ("tag", tag), ("distance", distance));
            yield return null;
        }

        private static IEnumerator PortalUse(string[] args)
        {
            string tag = args.ElementAtOrDefault(0) ?? "vfhtest";
            TeleportWorld portal = Nearest(tag) ?? throw new InvalidOperationException($"no portal tagged {tag} nearby");
            Player me = Player.m_localPlayer;
            if (!me.IsTeleportable(portal.m_allowAllItems))
                throw new InvalidOperationException("you're carrying something a portal won't take (ore, metal): put it in a chest first");
            // Vanilla refuses a second teleport within 2 s of the last one.
            yield return new WaitForSeconds(2.5f);
            Vector3 before = me.transform.position;
            portal.Teleport(me);
            VfhLog.I(LogCat.Test, "fixture.portal_use", ("tag", tag), ("from", portal.transform.position));
            for (float waited = 0f; waited < 20f && (me.IsTeleporting() || Vector3.Distance(me.transform.position, before) < 10f); waited += 0.5f)
                yield return new WaitForSeconds(0.5f);
            if (Vector3.Distance(me.transform.position, before) < 10f)
                throw new InvalidOperationException("the portal didn't take you anywhere");
        }

        private static TeleportWorld? Nearest(string tag)
        {
            Vector3 me = Player.m_localPlayer.transform.position;
            return UnityEngine.Object.FindObjectsByType<TeleportWorld>(FindObjectsSortMode.None)
                .Where(p => p.GetComponent<ZNetView>()?.GetZDO()?.GetString(ZDOVars.s_tag) == tag)
                .OrderBy(p => Vector3.Distance(p.transform.position, me)).FirstOrDefault();
        }
    }
}
