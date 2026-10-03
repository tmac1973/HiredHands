using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Config;
using VikingsForHire.Core;
using UnityEngine;

namespace VikingsForHire.Hirelings
{
    /// <summary>Random viking looks from the same hair/beard/colour options as character creation.</summary>
    internal static class Appearance
    {
        // Vanilla's creator ranges live on a menu-scene component; these match its spread (pale to dark skin,
        // blond to near-black hair) closely enough for NPCs.
        private static readonly Color SkinLight = new(1f, 1f, 1f);
        private static readonly Color SkinDark = new(0.42f, 0.30f, 0.22f);
        private static readonly Color HairBlond = new(1f, 0.85f, 0.55f);
        private static readonly Color HairDark = new(0.35f, 0.18f, 0.08f);

        public static void Fill(HirelingRecord record, System.Random rng)
        {
            record.Model = rng.Next(2);
            List<string> hairs = Customization("Hair");
            List<string> beards = Customization("Beard");
            record.Hair = hairs.Count > 0 ? hairs[rng.Next(hairs.Count)] : "HairNone";
            record.Beard = record.Model == 0 && beards.Count > 0 ? beards[rng.Next(beards.Count)] : "BeardNone";

            Color skin = Color.Lerp(SkinLight, SkinDark, (float)rng.NextDouble());
            Color hair = Color.Lerp(HairBlond, HairDark, (float)rng.NextDouble()) * Mathf.Lerp(0.35f, 1f, (float)rng.NextDouble());
            if (rng.NextDouble() < 0.08)
                hair = new Color(0.75f, 0.75f, 0.72f); // the odd grey-haired veteran
            record.SkinR = skin.r; record.SkinG = skin.g; record.SkinB = skin.b;
            record.HairR = hair.r; record.HairG = hair.g; record.HairB = hair.b;

            List<string> names = record.Model == 0 ? DataStore.Current.Names.Male : DataStore.Current.Names.Female;
            record.Name = names.Count > 0 ? names[rng.Next(names.Count)] : "Viking";
        }

        /// <summary>Owner only: VisEquipment stores these in the ZDO and every other client draws them from there.</summary>
        public static void Apply(Hireling h)
        {
            ZDO zdo = h.Zdo!;
            VisEquipment vis = h.Visuals;
            vis.SetModel(zdo.GetInt(HirelingZdo.Model));
            vis.SetHairItem(zdo.GetString(HirelingZdo.Hair, "HairNone").GetStableHashCode());
            vis.SetBeardItem(zdo.GetString(HirelingZdo.Beard, "BeardNone").GetStableHashCode());
            vis.SetSkinColor(zdo.GetVec3(HirelingZdo.Skin, Vector3.one));
            vis.SetHairColor(zdo.GetVec3(HirelingZdo.HairColor, Vector3.one));
        }

        private static List<string> Customization(string prefix) =>
            ObjectDB.instance == null
                ? new List<string>()
                : ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Customization, prefix).Select(i => i.gameObject.name).ToList();
    }
}
