using System;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using UnityEngine;

namespace VikingsForHire.Hirelings.Gear
{
    /// <summary>
    /// Dressing a club-based item in another model: the club's in-hand visual gets the new mesh and materials, pointed the
    /// way the club points from the hand (its head is the far end) and at a real size.
    /// </summary>
    internal static class HeldModel
    {
        /// <summary>The club's in-hand visual: the biggest mesh under its "attach" transform (or anywhere in it).</summary>
        public static MeshFilter? Visual(GameObject prefab)
        {
            Transform? attach = prefab.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "attach");
            IEnumerable<MeshFilter> filters = (attach != null ? attach.GetComponentsInChildren<MeshFilter>(true) : prefab.GetComponentsInChildren<MeshFilter>(true))
                .Where(f => f.sharedMesh != null && f.GetComponent<MeshRenderer>() != null);
            return filters.OrderByDescending(f => f.sharedMesh.bounds.size.magnitude).FirstOrDefault();
        }

        /// <summary>
        /// Puts <paramref name="mesh"/> in the visual's place. Its far end is where its bounds' centre lies along its longest
        /// axis. <paramref name="length"/>: the size to show it at along that axis (null: the mesh is already in metres).
        /// </summary>
        public static Vector3 Point(MeshFilter visual, Mesh mesh, Material[] materials, float? length = null)
        {
            Mesh club = visual.sharedMesh;
            int clubAxis = LongestAxis(club.bounds.size);
            Vector3 along = Axis(clubAxis) * (club.bounds.center[clubAxis] < 0f ? -1f : 1f);
            int meshAxis = LongestAxis(mesh.bounds.size);
            Vector3 meshDir = Axis(meshAxis) * (mesh.bounds.center[meshAxis] < 0f ? -1f : 1f);
            Transform t = visual.transform;
            visual.sharedMesh = mesh;
            visual.GetComponent<MeshRenderer>().sharedMaterials = materials;
            t.localRotation *= Quaternion.FromToRotation(meshDir, along);
            float scale = length is float l ? l / Mathf.Max(0.01f, mesh.bounds.size[meshAxis]) : 1f;
            t.localScale = t.localScale / Mathf.Max(0.0001f, MaxScale(t)) * scale;
            return along;
        }

        /// <summary>A building piece's material, preferring one named like a hint.</summary>
        public static Material? PieceMaterial(string piece, params string[] hints)
        {
            GameObject? go = PrefabManager.Cache.GetPrefab<GameObject>(piece);
            if (go == null)
                return null;
            Material[] all = go.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).ToArray();
            return all.FirstOrDefault(m => hints.Any(h => m.name.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0)) ?? all.FirstOrDefault();
        }

        /// <summary>A prefab's biggest drawn mesh (plain or skinned) with its materials.</summary>
        public static (Mesh Mesh, Material[] Materials)? BiggestMesh(GameObject prefab)
        {
            IEnumerable<(Renderer R, Mesh M)> meshes = prefab.GetComponentsInChildren<MeshFilter>(true).Select(f => ((Renderer)f.GetComponent<MeshRenderer>(), f.sharedMesh))
                .Concat(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(s => ((Renderer)s, s.sharedMesh)));
            (Renderer R, Mesh M) best = meshes.Where(x => x.R != null && x.M != null).OrderByDescending(x => x.M.bounds.size.magnitude).FirstOrDefault();
            return best.R != null ? (best.M, best.R.sharedMaterials) : null;
        }

        private static float MaxScale(Transform t) => Mathf.Max(Mathf.Abs(t.lossyScale.x), Mathf.Max(Mathf.Abs(t.lossyScale.y), Mathf.Abs(t.lossyScale.z)));

        private static int LongestAxis(Vector3 size) => size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;

        private static Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
    }
}
