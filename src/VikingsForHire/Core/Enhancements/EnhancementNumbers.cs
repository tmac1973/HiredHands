using System.Collections.Generic;
using VikingsForHire.Core.Data;

namespace VikingsForHire.Core.Enhancements
{
    /// <summary>An enhancement's number from the data file ("<Name>.<number>" under its job), else the shipped default.</summary>
    public static class EnhancementNumbers
    {
        private static Dictionary<JobType, Dictionary<string, float>>? _defaults;

        public static string Key(EnhancementId id, string name) => $"{id}.{name}";

        public static float Get(VfhData? data, JobType job, EnhancementId id, string name)
        {
            string key = Key(id, name);
            if (data != null && data.Enhancements.TryGetValue(job, out Dictionary<string, float>? values) && values.TryGetValue(key, out float v))
                return v;
            _defaults ??= EnhancementDefaults.Create();
            return _defaults.TryGetValue(job, out Dictionary<string, float>? d) && d.TryGetValue(key, out float dv) ? dv : 0f;
        }
    }
}
