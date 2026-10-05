using UnityEngine;
using VikingsForHire.Core.Nav;

namespace VikingsForHire.Hirelings.Nav
{
    internal static class NavConvert
    {
        public static NavPoint ToNav(this Vector3 v) => new(v.x, v.y, v.z);
        public static Vector3 ToUnity(this NavPoint p) => new(p.X, p.Y, p.Z);
    }
}
