using System.Linq;

namespace VikingsForHire.Hirelings.Work.Steward
{
    /// <summary>
    /// Status lines with arguments, stored in the hireling's ZDO as "token|arg|arg" (each part a localization token or
    /// plain text) and turned into text by whoever shows them, in their own game's language.
    /// </summary>
    internal static class ActivityText
    {
        public static string Make(string token, params string[] args) => args.Length == 0 ? token : token + "|" + string.Join("|", args);

        public static string Show(string stored)
        {
            if (stored.IndexOf('|') < 0)
                return stored;
            string[] parts = stored.Split('|');
            string[] args = parts.Skip(1).Select(a => Localization.instance.Localize(a)).ToArray();
            return Localization.instance.Localize(parts[0], args);
        }
    }
}
