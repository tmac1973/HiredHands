using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using UnityEngine;
using VikingsForHire.Board;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings.Work.Kitchen;

namespace VikingsForHire.Testing
{
    /// <summary>Kitchen stations for the Cook's tests, and checks on them.</summary>
    internal static class FixturesKitchen
    {
        private static int _placed;

        public static void Register()
        {
            Fixtures.Add("stove", "<prefab> [lit] [fuel <n>] [tag] - a cooking station over a campfire (lit: fuelled) or an oven with n wood (default 5), beside the nearest board", Stove);
            Fixtures.Add("craftstation", "<prefab> [level] [tag] - a cauldron or mead ketill over a lit campfire, or a prep table, beside the nearest board; level adds that many of its extensions", CraftStation);
            TestHarness.RegisterCheck("crafted_by_cook", "<item> - how many of an item Cooks have crafted since login", args =>
                (CraftChore.Crafted.TryGetValue(args.ElementAtOrDefault(0) ?? "", out int n) ? n : 0).ToString(CultureInfo.InvariantCulture));
            TestHarness.RegisterCheck("status_has", "<posted> <text> - true when the status line of the hireling from your last contract contains the text", args =>
            {
                Hirelings.Hireling h = FixturesWork.Posted();
                string shown = Hirelings.Work.Chores.ActivityText.Show(h.Zdo?.GetString(Hirelings.HirelingZdo.Activity) ?? "");
                return (shown.IndexOf(string.Join(" ", args.Skip(1)), StringComparison.OrdinalIgnoreCase) >= 0).ToString().ToLowerInvariant();
            });
            TestHarness.RegisterCheck("stove_slots", "<tag> <empty|cooking|done|burnt> - how many of a tagged stove's slots are in that state", args =>
            {
                CookingStation s = Tagged(args.ElementAtOrDefault(0));
                string want = args.ElementAtOrDefault(1) ?? "cooking";
                int n = 0;
                for (int i = 0; i < s.m_slots.Length; i++)
                {
                    s.GetSlot(i, out string item, out _, out CookingStation.Status st, out _);
                    bool match = want switch
                    {
                        "empty" => item.Length == 0,
                        "done" => item.Length > 0 && st == CookingStation.Status.Done,
                        "burnt" => item.Length > 0 && st == CookingStation.Status.Burnt,
                        _ => item.Length > 0 && st == CookingStation.Status.NotDone,
                    };
                    if (match)
                        n++;
                }
                return n.ToString(CultureInfo.InvariantCulture);
            });
            TestHarness.RegisterCheck("stove_fuel", "<tag> - a tagged oven's fuel", args =>
                Tagged(args.ElementAtOrDefault(0)).GetFuel().ToString("0.0", CultureInfo.InvariantCulture));
            TestHarness.RegisterCheck("taken_off", "- items Cooks have taken off stoves since login", _ =>
                (StovesChore.TakenOff.TryGetValue("any", out int n) ? n : 0).ToString(CultureInfo.InvariantCulture));
        }

        private static CookingStation Tagged(string? tag) =>
            FixturesWork.FindTagged(tag ?? "")?.GetComponentInChildren<CookingStation>() ?? throw new InvalidOperationException("no such tagged stove");

        internal static Vector3 NextSpot()
        {
            HiringBoard board = FixturesWork.Board();
            int k = _placed++;
            Vector3 p = board.transform.position - board.transform.forward * 6f + board.transform.right * ((k % 4) - 1.5f) * 3.5f;
            p.y = ZoneSystem.instance.GetGroundHeight(p);
            return p;
        }

        private static IEnumerator Stove(string[] args)
        {
            string prefab = args.ElementAtOrDefault(0) ?? "piece_cookingstation";
            var rest = args.Skip(1).ToList();
            bool lit = rest.Remove("lit");
            float fuel = 5f;
            int fi = rest.IndexOf("fuel");
            if (fi >= 0 && fi + 1 < rest.Count)
            {
                fuel = float.Parse(rest[fi + 1], CultureInfo.InvariantCulture);
                rest.RemoveRange(fi, 2);
            }
            string tag = rest.LastOrDefault() ?? "";
            Vector3 p = NextSpot();
            GameObject go = FixturesWork.Spawn(prefab, p, tag);
            FixturesWork.OwnBuilt(go);
            CookingStation s = go.GetComponentInChildren<CookingStation>();
            if (s != null && s.m_useFuel)
                s.m_nview.GetZDO().Set(ZDOVars.s_fuel, fuel);
            if (s != null && s.m_requireFire)
            {
                GameObject fire = FixturesWork.Spawn("fire_pit", p, "");
                FixturesWork.OwnBuilt(fire);
                if (lit && fire.GetComponent<Fireplace>() is Fireplace f)
                    f.m_nview.GetZDO().Set(ZDOVars.s_fuel, f.m_maxFuel);
            }
            VfhLog.I(LogCat.Test, "fixture.stove", ("prefab", prefab), ("lit", lit), ("fuel", fuel), ("tag", tag));
            yield return new WaitForSeconds(1f);
        }

        private static IEnumerator CraftStation(string[] args)
        {
            string prefab = args.ElementAtOrDefault(0) ?? "piece_cauldron";
            int level = int.TryParse(args.ElementAtOrDefault(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int l) ? l : 1;
            string tag = args.Skip(1).LastOrDefault(a => !int.TryParse(a, out _)) ?? "";
            Vector3 p = NextSpot();
            GameObject go = FixturesWork.Spawn(prefab, p, tag);
            FixturesWork.OwnBuilt(go);
            CraftingStation s = go.GetComponentInChildren<CraftingStation>() ?? throw new InvalidOperationException($"{prefab} isn't a crafting station");
            if (s.m_craftRequireFire)
            {
                GameObject fire = FixturesWork.Spawn("fire_pit", p, "");
                FixturesWork.OwnBuilt(fire);
                if (fire.GetComponent<Fireplace>() is Fireplace f)
                    f.m_nview.GetZDO().Set(ZDOVars.s_fuel, f.m_maxFuel);
            }
            // Its extensions (upgrades), set round it within their range.
            var extensions = ZNetScene.instance.m_prefabs.Where(x => x != null && x.GetComponent<StationExtension>() is StationExtension e &&
                                                                     e.m_craftingStation != null && e.m_craftingStation.m_name == s.m_name).Take(level - 1).ToList();
            for (int i = 0; i < extensions.Count; i++)
            {
                Vector3 at = p + Quaternion.Euler(0f, 60f * i, 0f) * Vector3.forward * 2.5f;
                FixturesWork.OwnBuilt(FixturesWork.Spawn(extensions[i].name, at, ""));
            }
            VfhLog.I(LogCat.Test, "fixture.craftstation", ("prefab", prefab), ("extensions", extensions.Count), ("tag", tag));
            yield return new WaitForSeconds(1f);
        }
    }
}
