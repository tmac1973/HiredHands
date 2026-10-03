using System.IO;
using System.Reflection;
using Jotunn.Managers;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.L10n
{
    /// <summary>Loads the embedded $vfh_* strings into Jotunn's localization.</summary>
    internal static class Strings
    {
        public static void Register()
        {
            const string resource = "VikingsForHire.Localization.English.json";
            using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
            if (stream == null)
            {
                VfhLog.E(LogCat.Core, "localization.missing", ("resource", resource));
                return;
            }
            using var reader = new StreamReader(stream);
            LocalizationManager.Instance.GetLocalization().AddJsonFile("English", reader.ReadToEnd());
        }
    }
}
