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

        private static IEnumerator ChestStack(string[] args)
        {
            string tag = args.ElementAtOrDefault(0) ?? "S";
            int count = int.Parse(args.ElementAtOrDefault(1) ?? "3", CultureInfo.InvariantCulture);
            HiringBoard board = FixturesWork.Board();
            Vector3 at = board.transform.position + board.transform.forward * 6f;
            at.y = ZoneSystem.instance.GetSolidHeight(at);
            GameObject? top = null;
            float y = 0f;
            for (int i = 0; i < count; i++)
            {
                top = FixturesBoard.Spawn("piece_chest_wood", at + Vector3.up * y, Quaternion.LookRotation(-board.transform.forward));
                yield return null;
                y += top.GetComponentsInChildren<Collider>().Where(c => !c.isTrigger).Select(c => c.bounds.size.y).DefaultIfEmpty(0.9f).Max();
            }
            top!.GetComponent<ZNetView>().GetZDO().Set(FixturesWork.TagKey, tag);
            VfhLog.I(LogCat.Test, "fixture.chest_stack", ("tag", tag), ("count", count), ("height", y));
            yield return new WaitForSeconds(0.5f);
        }

        private static IEnumerator FloorOver(string[] args)
        {
            GameObject go = FixturesWork.FindTagged(args.ElementAtOrDefault(0) ?? "") ?? throw new InvalidOperationException("no such tag");
            float height = float.Parse(args.ElementAtOrDefault(1) ?? "2", CultureInfo.InvariantCulture);
            Vector3 at = go.transform.position;
            at.y = ZoneSystem.instance.GetSolidHeight(at) + height;
            GameObject floor = FixturesBoard.Spawn("wood_floor", at, Quaternion.identity);
            floor.GetComponent<ZNetView>().GetZDO().Set(FixturesWork.TagKey, args.ElementAtOrDefault(2) ?? "F");
            VfhLog.I(LogCat.Test, "fixture.floor_over", ("over", args.ElementAtOrDefault(0)), ("pos", at));
            yield return new WaitForSeconds(0.5f);
        }

        public static void Register()
        {
            TestHarness.RegisterCheck("no_spot", "<prefab> - times workers found no usable spot to work a prefab from, since login", args =>
                (Hirelings.Work.Chores.WorkSteps.NoSpot.TryGetValue(args.ElementAtOrDefault(0) ?? "", out int n) ? n : 0).ToString(CultureInfo.InvariantCulture));
            Fixtures.Add("stove", "<prefab> [lit] [hearth|on_stone] [triple] [fuel <n>] [tag] - a cooking station over a campfire, or up on a hearth (lit: fuelled), or an oven with n wood (default 5), beside the nearest board", Stove);
            Fixtures.Add("craftstation", "<prefab> [level] [tag] - a cauldron or mead ketill over a lit campfire, or a prep table, beside the nearest board; level adds that many of its extensions", CraftStation);
            Fixtures.Add("chest_stack", "<tag> [count=3] - wooden chests stacked in a column 6 m from the board, the top one tagged (drawers stacked on drawers)", ChestStack);
            Fixtures.Add("floor_over", "<tag> [height=2] [newtag=F] - a wood floor piece over a tagged object, that high above its base (an upper storey)", FloorOver);
            TestHarness.RegisterCheck("usable_from", "<tag> <beside|above> - whether a worker could use a tagged object standing beside it on its floor, or on the floor above it (floor_over)", args =>
            {
                GameObject go = FixturesWork.FindTagged(args.ElementAtOrDefault(0) ?? "") ?? throw new InvalidOperationException("no such tag");
                Component obj = go.GetComponentInChildren<CookingStation>() as Component ?? go.transform;
                Vector3 at = obj.transform.position;
                Vector3 feet;
                if ((args.ElementAtOrDefault(1) ?? "beside") == "above")
                {
                    Vector3 from = at + Vector3.right * 0.5f + Vector3.up * 4f;
                    feet = Physics.Raycast(from, Vector3.down, out RaycastHit hit, 4f, Hirelings.Nav.StairSampler.FloorMask, QueryTriggerInteraction.Ignore)
                        ? hit.point : throw new InvalidOperationException("no floor above");
                }
                else
                    feet = new Vector3(at.x + 1.6f, ZoneSystem.instance.GetSolidHeight(at + Vector3.right * 1.6f), at.z);
                return Hirelings.Work.Chores.WorkSteps.UsableFrom(feet, obj, at, Hirelings.Work.Chores.WorkSteps.Footprint(obj), Hirelings.Work.Chores.WorkSteps.PlayerReach)
                    ? "true" : "false";
            });
            Fixtures.Add("cook_stats_reset", "- start counting what Cooks craft from zero (crafted_by_cook)", _ => ResetCookStats());
            TestHarness.RegisterCheck("crafted_by_cook", "<item> - how many of an item Cooks have crafted since login or the last cook_stats_reset", args =>
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
            bool hearth = rest.Remove("hearth");
            bool onStone = rest.Remove("on_stone");
            bool triple = rest.Remove("triple");
            float fuel = 5f;
            int fi = rest.IndexOf("fuel");
            if (fi >= 0 && fi + 1 < rest.Count)
            {
                fuel = float.Parse(rest[fi + 1], CultureInfo.InvariantCulture);
                rest.RemoveRange(fi, 2);
            }
            string tag = rest.LastOrDefault() ?? "";
            Vector3 p = NextSpot();
            GameObject? hearthGo = null;
            if (onStone)
            {
                // A campfire on a waist-high block of stone with the spit over it (Tim's second server's kitchen).
                // A 4 x 4 m platform of 2 x 2 m stone floors, stacked until it's about 2 m high: wider than the ring of spots
                // round the spit, so the old pick sent the Cook up onto it (Svanhild, 2.2 m below her spot).
                float stoneTop = p.y;
                for (int layer = 0; layer < 4 && stoneTop - p.y < 1.8f; layer++)
                {
                    float layerTop = stoneTop;
                    foreach (Vector3 off in new[] { new Vector3(-1f, 0f, -1f), new Vector3(1f, 0f, -1f), new Vector3(-1f, 0f, 1f), new Vector3(1f, 0f, 1f) })
                    {
                        GameObject stone = FixturesWork.Spawn("stone_floor_2x2", new Vector3(p.x, stoneTop, p.z) + off, "");
                        FixturesWork.OwnBuilt(stone);
                        if (stone.GetComponent<WearNTear>() is WearNTear wear)
                            wear.enabled = false;
                        yield return null;
                        Bounds b = stone.GetComponentsInChildren<Collider>().Where(c => !c.isTrigger).Select(c => c.bounds)
                            .Aggregate((x, y) => { x.Encapsulate(y); return x; });
                        Vector3 lifted = stone.transform.position + Vector3.up * (stoneTop - b.min.y);
                        stone.transform.position = lifted;
                        stone.GetComponent<ZNetView>().GetZDO().SetPosition(lifted);
                        layerTop = Mathf.Max(layerTop, stoneTop + b.size.y);
                    }
                    stoneTop = layerTop;
                }
                p = new Vector3(p.x, stoneTop, p.z);
                GameObject campfire = FixturesWork.Spawn("fire_pit", p, "");
                campfire.transform.position = p;
                campfire.GetComponent<ZNetView>().GetZDO().SetPosition(p);
                FixturesWork.OwnBuilt(campfire);
                if (lit && campfire.GetComponent<Fireplace>() is Fireplace cf)
                    cf.m_nview.GetZDO().Set(ZDOVars.s_fuel, cf.m_maxFuel);
            }
            if (hearth)
            {
                // Up on a hearth, as people build kitchens (Svanhild's spits were 2.2 m above her floor).
                hearthGo = FixturesWork.Spawn("hearth", p, "");
                FixturesWork.OwnBuilt(hearthGo);
                if (lit && hearthGo.GetComponent<Fireplace>() is Fireplace hf)
                    hf.m_nview.GetZDO().Set(ZDOVars.s_fuel, hf.m_maxFuel);
                yield return null;
                float top = hearthGo.GetComponentsInChildren<Collider>().Where(c => !c.isTrigger).Select(c => c.bounds.max.y).DefaultIfEmpty(p.y).Max();
                p = new Vector3(p.x, top, p.z);
            }
            GameObject go = FixturesWork.Spawn(prefab, p, tag);
            if (hearth || onStone)
            {
                go.transform.position = p;
                go.GetComponent<ZNetView>().GetZDO().SetPosition(p);
            }
            FixturesWork.OwnBuilt(go);
            CookingStation s = go.GetComponentInChildren<CookingStation>();
            if (s != null && s.m_useFuel)
                s.m_nview.GetZDO().Set(ZDOVars.s_fuel, fuel);
            if (s != null && s.m_requireFire && !hearth && !onStone)
            {
                GameObject fire = FixturesWork.Spawn("fire_pit", p, "");
                FixturesWork.OwnBuilt(fire);
                if (lit && fire.GetComponent<Fireplace>() is Fireplace f)
                    f.m_nview.GetZDO().Set(ZDOVars.s_fuel, f.m_maxFuel);
            }
            // Three spits over the one fire (a common Valheim kitchen: Svanhild's): two more, turned, tagged <tag>2 and <tag>3.
            if (triple)
                for (int k = 2; k <= 3; k++)
                {
                    GameObject extra = FixturesWork.Spawn(prefab, p, tag.Length > 0 ? tag + k : "");
                    extra.transform.position = p;
                    extra.transform.rotation = Quaternion.Euler(0f, 60f * (k - 1), 0f);
                    extra.GetComponent<ZNetView>().GetZDO().SetPosition(p);
                    extra.GetComponent<ZNetView>().GetZDO().SetRotation(extra.transform.rotation);
                    FixturesWork.OwnBuilt(extra);
                }
            VfhLog.I(LogCat.Test, "fixture.stove", ("prefab", prefab), ("lit", lit), ("fuel", fuel), ("tag", tag), ("hearth", hearth), ("onStone", onStone), ("triple", triple), ("at", p));
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

        private static System.Collections.IEnumerator ResetCookStats()
        {
            Hirelings.Work.Kitchen.CraftChore.Crafted.Clear();
            yield return null;
        }
    }
}
