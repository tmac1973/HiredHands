using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Enhancements;

namespace VikingsForHire.Hirelings
{
    /// <summary>
    /// A hireling's enhancements as the game simulating it sees them: its picks (the copy on its record) with the data
    /// file's numbers. Nothing at all while the server has Enhancements off. Re-read when the picks, the job or the data
    /// change, so callers can ask every frame.
    /// </summary>
    internal sealed class HirelingEnhancements
    {
        private readonly Hireling _h;
        private string _picks = "";
        private JobType _job;
        private Core.Data.VfhData? _data;
        private bool _on;
        private EnhancementEffects _effects = EnhancementEffects.None;

        public HirelingEnhancements(Hireling h) => _h = h;

        public EnhancementEffects Effects
        {
            get
            {
                string picks = _h.Zdo?.GetString(HirelingZdo.Enhancements) ?? "";
                bool on = VfhConfig.Enhancements.Value;
                if (picks != _picks || _h.Job != _job || !ReferenceEquals(DataStore.Current, _data) || on != _on)
                {
                    _picks = picks;
                    _job = _h.Job;
                    _data = DataStore.Current;
                    _on = on;
                    _effects = on ? new EnhancementEffects(_job, EnhancementPicks.Parse(picks), _data) : EnhancementEffects.None;
                }
                return _effects;
            }
        }

        public bool Has(EnhancementId id) => Effects.Has(id);

        public float Value(EnhancementId id, string name) => Effects.Value(id, name);

        /// <summary>What decides the stats: changes when the picks, the switch or the data change.</summary>
        public string Signature => $"{_h.Zdo?.GetString(HirelingZdo.Enhancements)}|{VfhConfig.Enhancements.Value}|{DataStore.Current.GetHashCode()}";
    }
}

namespace VikingsForHire.Hirelings
{
    /// <summary>
    /// Counts of enhancement techniques firing (Shield bash, Volley, Errand runner…), for the test rows' enh_events check,
    /// and a Debug line each time (cat Combat or Work).
    /// </summary>
    internal static class EnhancementEvents
    {
        private static readonly System.Collections.Generic.Dictionary<string, int> Counts = new();

        public static void Fire(Hireling h, string technique, Core.Diagnostics.LogCat cat = Core.Diagnostics.LogCat.Combat, params (string, object?)[] extra)
        {
            Counts[technique] = Count(technique) + 1;
            var fields = new System.Collections.Generic.List<(string, object?)> { ("hid", h.Hid), ("technique", technique) };
            fields.AddRange(extra);
            Diagnostics.VfhLog.D(cat, "enh.fired", fields.ToArray());
        }

        public static int Count(string technique) => Counts.TryGetValue(technique, out int n) ? n : 0;

        public static void Reset() => Counts.Clear();
    }
}
