using System.Collections.Generic;

namespace VikingsForHire.Hirelings.Work.Chores
{
    /// <summary>
    /// Every reason the chores gave during one survey (a chore's <see cref="IChore.Missing"/> keeps only its first): the
    /// hireling's Shift+E problems list shows them all. Surveys run one at a time on the game simulating the hireling.
    /// </summary>
    internal static class ChoreNotes
    {
        private static readonly List<string> Notes = new();

        public static void Clear() => Notes.Clear();

        /// <summary>Note a reason and hand it back.</summary>
        public static string Add(string why)
        {
            if (!Notes.Contains(why))
                Notes.Add(why);
            return why;
        }

        /// <summary>Note a reason and keep the first one (for <c>Missing = ChoreNotes.First(Missing, ...)</c>; <c>??=</c> would skip noting the rest).</summary>
        public static string First(string? current, string why)
        {
            Add(why);
            return current ?? why;
        }

        public static IReadOnlyList<string> All => Notes;
    }
}
