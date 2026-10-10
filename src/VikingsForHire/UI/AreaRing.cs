using UnityEngine;

namespace VikingsForHire.UI
{
    /// <summary>
    /// A circle drawn on the ground round its object, shown while something keeps asking for it (looking at the object,
    /// an open settings panel): each Show keeps it up for a moment. Follows the ground, and the radius can change.
    /// </summary>
    internal sealed class AreaRing : MonoBehaviour
    {
        private const float HoldSeconds = 0.3f;
        private const int Points = 96;

        private LineRenderer? _line;
        private float _showUntil;
        private float _radius = -1f;
        private float _drawn = -1f;
        private Color _color = new(0.4f, 1f, 0.4f, 0.8f);

        /// <summary>The ring on this object, added the first time.</summary>
        public static AreaRing On(Component owner, Color color)
        {
            AreaRing ring = owner.GetComponent<AreaRing>();
            if (ring == null)
                ring = owner.gameObject.AddComponent<AreaRing>();
            ring._color = color;
            return ring;
        }

        public void Show(float radius)
        {
            _radius = radius;
            _showUntil = Time.time + HoldSeconds;
        }

        private void Update()
        {
            if (Time.time >= _showUntil)
            {
                if (_line != null && _line.enabled)
                    _line.enabled = false;
                return;
            }
            if (_line == null)
            {
                var go = new GameObject("VFH_AreaRing");
                go.transform.SetParent(transform, false);
                _line = go.AddComponent<LineRenderer>();
                _line.useWorldSpace = true;
                _line.loop = true;
                _line.widthMultiplier = 0.08f;
                _line.material = new Material(Shader.Find("Sprites/Default"));
            }
            _line.startColor = _line.endColor = _color;
            _line.enabled = true;
            // Redrawn when the radius changes, and now and then for the ground (terrain edited, or still loading).
            if (Mathf.Abs(_drawn - _radius) <= 0.01f && _line.positionCount > 0 && Time.frameCount % 30 != 0)
                return;
            _drawn = _radius;
            _line.positionCount = Points;
            for (int i = 0; i < Points; i++)
            {
                float a = i * Mathf.PI * 2f / Points;
                Vector3 p = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * _radius;
                p.y = (ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(p) : p.y) + 0.15f;
                _line.SetPosition(i, p);
            }
        }
    }
}
