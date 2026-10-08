using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VikingsForHire.Hirelings.Work.Trees
{
    /// <summary>
    /// The tree patch sign: the patch's radius and what to plant live on its ZDO. Shift+E opens its settings; looking at
    /// it draws the patch's circle on the ground.
    /// </summary>
    internal sealed class TreePatch : MonoBehaviour, Hoverable, Interactable
    {
        public const string RadiusKey = "vfh_patch_radius";
        public const string KindKey = "vfh_patch_kind";
        public const float DefaultRadius = 8f, MinRadius = 3f, MaxRadius = 20f;
        private const float InteractRange = 5f;

        public static readonly List<TreePatch> Loaded = new();

        private ZNetView? _nview;
        private LineRenderer? _ring;
        private float _showRingUntil;
        private float _ringRadius = -1f;

        public ZDO? Zdo => _nview != null && _nview.IsValid() ? _nview.GetZDO() : null;
        public float Radius => Mathf.Clamp(Zdo?.GetFloat(RadiusKey, DefaultRadius) ?? DefaultRadius, MinRadius, MaxRadius);

        /// <summary>The sapling prefab to plant, or empty for any kind that grows here.</summary>
        public string Kind => Zdo?.GetString(KindKey) ?? "";

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            Loaded.Add(this);
        }

        private void OnDestroy() => Loaded.Remove(this);

        public void Set(float radius, string kind)
        {
            if (Zdo == null)
                return;
            if (!_nview!.IsOwner())
                _nview.ClaimOwnership();
            Zdo.Set(RadiusKey, Mathf.Clamp(radius, MinRadius, MaxRadius));
            Zdo.Set(KindKey, kind);
        }

        private (int Saplings, int Trees) _count;
        private float _countedAt = -999f;

        /// <summary>Saplings and grown trees inside the patch (counted at most once a second: the hover asks every frame).</summary>
        public (int Saplings, int Trees) Count()
        {
            if (Time.time - _countedAt < 1f)
                return _count;
            _countedAt = Time.time;
            _count = CountNow();
            return _count;
        }

        private (int Saplings, int Trees) CountNow()
        {
            int saplings = 0, trees = 0;
            float r2 = Radius * Radius;
            if (ZNetScene.instance == null)
                return (0, 0);
            foreach (KeyValuePair<ZDO, ZNetView> kv in ZNetScene.instance.m_instances)
            {
                if (kv.Key == null || kv.Value == null)
                    continue;
                Vector3 p = kv.Key.GetPosition();
                float dx = p.x - transform.position.x, dz = p.z - transform.position.z;
                if (dx * dx + dz * dz > r2)
                    continue;
                if (TreeCatalog.IsSaplingHash(kv.Key.GetPrefab()))
                    saplings++;
                else if (kv.Value.GetComponent<TreeBase>() != null)
                    trees++;
            }
            return (saplings, trees);
        }

        public static string KindName(string sapling)
        {
            if (sapling.Length == 0)
                return Localization.instance.Localize("$vfh_patch_any");
            TreeKind? k = TreeCatalog.BySapling(sapling);
            return k != null ? Localization.instance.Localize(Chores.WorkSteps.SharedName(k.Seed)) : sapling;
        }

        public string GetHoverText()
        {
            _showRingUntil = Time.time + 0.3f;
            (int saplings, int trees) = Count();
            Localization l = Localization.instance;
            return l.Localize("$vfh_patch") + "\n" + l.Localize("$vfh_patch_plants", KindName(Kind), Radius.ToString("0")) + "\n" +
                   l.Localize("$vfh_patch_count", saplings.ToString(), trees.ToString()) + "\n" +
                   l.Localize("[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] $vfh_patch_settings");
        }

        public string GetHoverName() => "$vfh_patch";

        public float GetHoverOffset() => 0f;

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || user != Player.m_localPlayer || Vector3.Distance(user.transform.position, transform.position) > InteractRange)
                return false;
            if (!PrivateArea.CheckAccess(transform.position, 0f, flash: true))
                return true;
            UI.TreePatchPanel.Open(this);
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

        /// <summary>Keep the circle shown (while the settings panel is open).</summary>
        public void ShowRing() => _showRingUntil = Time.time + 0.3f;

        private void Update()
        {
            bool show = Time.time < _showRingUntil;
            if (!show)
            {
                if (_ring != null && _ring.enabled)
                    _ring.enabled = false;
                return;
            }
            if (_ring == null)
            {
                var go = new GameObject("VFH_PatchRing");
                go.transform.SetParent(transform, false);
                _ring = go.AddComponent<LineRenderer>();
                _ring.useWorldSpace = true;
                _ring.loop = true;
                _ring.widthMultiplier = 0.08f;
                _ring.material = new Material(Shader.Find("Sprites/Default"));
                _ring.startColor = _ring.endColor = new Color(0.4f, 1f, 0.4f, 0.8f);
            }
            _ring.enabled = true;
            if (Mathf.Abs(_ringRadius - Radius) > 0.01f || _ring.positionCount == 0 || Time.frameCount % 30 == 0)
            {
                _ringRadius = Radius;
                const int n = 64;
                _ring.positionCount = n;
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / n;
                    Vector3 p = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * _ringRadius;
                    p.y = (ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(p) : p.y) + 0.15f;
                    _ring.SetPosition(i, p);
                }
            }
        }

        /// <summary>The patches whose sign is within a worker's area.</summary>
        public static IEnumerable<TreePatch> Within(Vector3 center, float radius) =>
            Loaded.Where(p => p != null && p.Zdo != null && Utils.DistanceXZ(p.transform.position, center) <= radius);
    }
}
