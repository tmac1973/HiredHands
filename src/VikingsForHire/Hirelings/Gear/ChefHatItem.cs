using System.Collections.Generic;
using System.Linq;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using VikingsForHire.Core.Data;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings.Gear
{
    /// <summary>
    /// The Cook's chef's hat: a cap (the game has no chef's hat) whose look is replaced by a white toque built from simple
    /// shapes, a band round the head and a pleated puff on top. A helmet's "attach" child is placed at the head joint with
    /// no offset, so the toque is put in that joint's frame: upright by the player skeleton's head, and sized and seated by
    /// where vanilla caps sit. Never crafted by players.
    /// </summary>
    internal static class ChefHatItem
    {
        public const string PrefabName = DefaultData.ChefHatItem;
        private const string BasePrefab = "HelmetHat10";
        // Caps and helmets whose drawn mesh shows where the top of the head is, in the head joint's frame.
        private static readonly string[] HeadSources = { "HelmetHat10", "HelmetHat5", "HelmetHat3", "HelmetLeather", "HelmetBronze", "HelmetIron" };
        private const float BandHeight = 0.09f, PuffHeight = 0.16f, PuffWidth = 1.3f, Pleats = 8, PleatDepth = 0.06f;
        // The band's bottom edge, as a fraction of the way up a cap (its lowest point is at the back of the neck; this is
        // the brow), and the band's radius against the cap's (a cap is roomier than a band). Tuned by eye in game.
        private const float Seat = 0.3f, Snug = 0.85f;
        private static readonly Color Cloth = new(0.96f, 0.95f, 0.92f);

        private static bool _created;

        public static void Register() =>
            PrefabManager.OnVanillaPrefabsAvailable += () => VfhLog.Guard(LogCat.Hireling, "chefhat.register_failed", Create);

        private static void Create()
        {
            if (_created)
                return;
            _created = true;
            var item = new CustomItem(PrefabName, BasePrefab, new ItemConfig { Name = "$vfh_chefhat", Description = "$vfh_chefhat_desc" });
            ItemManager.Instance.AddItem(item);
            Vector3 up = HeadUp();
            if (Head(up) is not var (rim, radius) || BaseMaterial(item.ItemPrefab) is not Material baseMaterial)
            {
                VfhLog.W(LogCat.Hireling, "chefhat.model", ("source", BasePrefab + " (no cap model found)"));
                return;
            }

            // The cap's own look goes; a new "attach" holds the toque, so the game seats it like any helmet.
            int layer = item.ItemPrefab.transform.Cast<Transform>().FirstOrDefault(IsAttach)?.gameObject.layer ?? 0;
            foreach (Transform old in item.ItemPrefab.transform.Cast<Transform>().Where(IsAttach).ToList())
                Object.DestroyImmediate(old.gameObject);
            var attach = new GameObject("attach") { layer = layer };
            attach.transform.SetParent(item.ItemPrefab.transform, false);
            var toque = new GameObject("vfh_toque") { layer = layer };
            toque.transform.SetParent(attach.transform, false);
            toque.transform.localPosition = rim;
            toque.transform.localRotation = Quaternion.FromToRotation(Vector3.up, up);
            toque.AddComponent<MeshFilter>().sharedMesh = Build(radius);
            toque.AddComponent<MeshRenderer>().sharedMaterial = White(baseMaterial);
            VfhLog.I(LogCat.Hireling, "chefhat.model", ("up", up), ("rim", rim), ("radius", radius), ("material", baseMaterial.name), ("shader", baseMaterial.shader.name));
        }

        private static bool IsAttach(Transform t) => t.name == "attach" || t.name == "attach_skin";

        /// <summary>Straight up in the head joint's frame, from the player skeleton as it stands in its prefab.</summary>
        private static Vector3 HeadUp()
        {
            GameObject? player = PrefabManager.Cache.GetPrefab<GameObject>("Player");
            Transform? head = player != null ? player.GetComponent<VisEquipment>()?.m_helmet : null;
            return head != null ? head.InverseTransformDirection(player!.transform.up).normalized : Vector3.up;
        }

        /// <summary>
        /// Where the toque's band starts (its bottom edge's centre, in the joint's frame) and its radius: from the first cap
        /// or helmet drawn as plain meshes under its "attach".
        /// </summary>
        private static (Vector3 Rim, float Radius)? Head(Vector3 up)
        {
            foreach (string name in HeadSources)
            {
                GameObject? prefab = PrefabManager.Cache.GetPrefab<GameObject>(name);
                Transform? attach = prefab != null ? prefab.transform.Cast<Transform>().FirstOrDefault(t => t.name == "attach") : null;
                if (attach == null)
                    continue;
                List<Vector3> points = attach.GetComponentsInChildren<MeshFilter>(true)
                    .Where(f => f.sharedMesh != null && f.GetComponent<MeshRenderer>() != null)
                    .SelectMany(f => Corners(f.sharedMesh.bounds).Select(c => ToJoint(attach, f.transform.TransformPoint(c))))
                    .ToList();
                if (points.Count == 0)
                    continue;
                float low = points.Min(p => Vector3.Dot(p, up)), high = points.Max(p => Vector3.Dot(p, up));
                Vector3 centre = points.Aggregate(Vector3.zero, (a, p) => a + p) / points.Count;
                Vector3 across = centre - up * Vector3.Dot(centre, up);
                float radius = points.Max(p => (p - up * Vector3.Dot(p, up) - across).magnitude) / Mathf.Sqrt(2f); // box corners to a circle
                VfhLog.D(LogCat.Hireling, "chefhat.head", ("source", name), ("scale", attach.localScale), ("low", low), ("high", high), ("radius", radius));
                return (across + up * Mathf.Lerp(low, high, Seat), radius * Snug);
            }
            return null;
        }

        /// <summary>
        /// A point of a prefab's attach in the head joint's frame: the game puts an attach at the joint with no offset or
        /// turn but keeps its scale, so only the scale carries over.
        /// </summary>
        private static Vector3 ToJoint(Transform attach, Vector3 world) => Vector3.Scale(attach.localScale, attach.InverseTransformPoint(world));

        private static IEnumerable<Vector3> Corners(Bounds b)
        {
            for (int i = 0; i < 8; i++)
                yield return b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
        }

        /// <summary>The cap's own cloth: a mesh's material under its attach (the prefab also holds particle effects).</summary>
        private static Material? BaseMaterial(GameObject prefab) =>
            prefab.transform.Cast<Transform>().Where(IsAttach)
                .SelectMany(a => a.GetComponentsInChildren<Renderer>(true))
                .Where(r => r is MeshRenderer || r is SkinnedMeshRenderer)
                .SelectMany(r => r.sharedMaterials).FirstOrDefault(m => m != null);

        /// <summary>The cap's material, cleaned to plain white cloth.</summary>
        private static Material White(Material from)
        {
            var m = new Material(from) { name = "vfh_toque" };
            foreach (string tex in new[] { "_MainTex", "_BumpMap", "_MetallicGlossMap", "_EmissionMap", "_StyleTex" })
                if (m.HasProperty(tex))
                    m.SetTexture(tex, tex == "_MainTex" ? Texture2D.whiteTexture : null);
            if (m.HasProperty("_Color"))
                m.SetColor("_Color", Cloth);
            if (m.HasProperty("_Glossiness"))
                m.SetFloat("_Glossiness", 0.1f);
            if (m.HasProperty("_Metallic"))
                m.SetFloat("_Metallic", 0f);
            m.DisableKeyword("_EMISSION");
            return m;
        }

        // Along +y from the band's bottom edge: the band, then the puff, wider and pleated.
        private static Mesh Build(float radius)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float r = radius;
            MeshBuilder.Tube(verts, uvs, tris, 0f, BandHeight, new Vector2(r, r), new Vector2(r * 1.04f, r * 1.04f), 0f, false, false);
            Puff(verts, uvs, tris, BandHeight + PuffHeight * 0.35f, r * PuffWidth, PuffHeight * 0.65f);
            var mesh = new Mesh { name = "vfh_toque" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A squashed sphere centred <paramref name="y"/> up, its sides pleated like gathered cloth; its lower half tucks into the band.</summary>
        private static void Puff(List<Vector3> v, List<Vector2> uv, List<int> tri, float y, float width, float height)
        {
            const int rings = 8, sides = 24;
            int start = v.Count;
            for (int ring = 0; ring <= rings; ring++)
            {
                float lat = Mathf.PI * ring / rings; // 0 at the bottom, pi at the top
                float across = Mathf.Sin(lat), up = -Mathf.Cos(lat);
                for (int i = 0; i <= sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides;
                    float pleat = 1f + PleatDepth * Mathf.Cos(a * Pleats) * across;
                    v.Add(new Vector3(Mathf.Cos(a) * width * across * pleat, y + up * height, Mathf.Sin(a) * width * across * pleat));
                    uv.Add(new Vector2((float)i / sides, (float)ring / rings));
                }
            }
            int row = sides + 1;
            for (int ring = 0; ring < rings; ring++)
                for (int i = 0; i < sides; i++)
                {
                    int a0 = start + ring * row + i, a1 = a0 + 1, b0 = a0 + row, b1 = b0 + 1;
                    tri.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
                }
        }
    }
}
