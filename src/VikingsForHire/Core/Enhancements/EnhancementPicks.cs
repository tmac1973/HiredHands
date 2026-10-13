using System;
using System.Collections.Generic;
using System.Text;

namespace VikingsForHire.Core.Enhancements
{
    /// <summary>
    /// A hireling's choices, one per level 2-8: 'A', 'B' or '-' (not chosen yet). Stored as a 7-character string on its
    /// contract and its own record ("A--B---"); anything unreadable counts as not chosen.
    /// </summary>
    public readonly struct EnhancementPicks : IEquatable<EnhancementPicks>
    {
        public const string Empty = "-------";
        private readonly string? _s;

        private EnhancementPicks(string s) => _s = s;

        private string S => _s ?? Empty;

        public static EnhancementPicks Parse(string? text)
        {
            var sb = new StringBuilder(Empty);
            if (text != null)
                for (int i = 0; i < EnhancementCatalog.Levels && i < text.Length; i++)
                    if (text[i] == 'A' || text[i] == 'B')
                        sb[i] = text[i];
            return new EnhancementPicks(sb.ToString());
        }

        /// <summary>'A', 'B' or '-' at a level 2-8.</summary>
        public char Get(int level)
        {
            Check(level);
            return S[level - EnhancementCatalog.FirstLevel];
        }

        public bool Chosen(int level) => Get(level) != '-';

        public EnhancementPicks With(int level, char choice)
        {
            Check(level);
            if (choice != 'A' && choice != 'B' && choice != '-')
                throw new ArgumentOutOfRangeException(nameof(choice), choice, "A, B or -");
            char[] c = S.ToCharArray();
            c[level - EnhancementCatalog.FirstLevel] = choice;
            return new EnhancementPicks(new string(c));
        }

        public bool Any => S != Empty;

        /// <summary>The names chosen, for a job's catalog.</summary>
        public IEnumerable<EnhancementId> Ids(JobType job)
        {
            for (int level = EnhancementCatalog.FirstLevel; level <= EnhancementCatalog.LastLevel; level++)
                if (Chosen(level))
                    yield return EnhancementCatalog.Option(job, level, Get(level));
        }

        public bool Has(JobType job, EnhancementId id)
        {
            int level = EnhancementCatalog.LevelOf(job, id);
            if (level == 0 || !Chosen(level))
                return false;
            return EnhancementCatalog.Option(job, level, Get(level)) == id;
        }

        private static void Check(int level)
        {
            if (level < EnhancementCatalog.FirstLevel || level > EnhancementCatalog.LastLevel)
                throw new ArgumentOutOfRangeException(nameof(level), level, "levels 2-8");
        }

        public override string ToString() => S;
        public bool Equals(EnhancementPicks other) => S == other.S;
        public override bool Equals(object? obj) => obj is EnhancementPicks p && Equals(p);
        public override int GetHashCode() => S.GetHashCode();
    }
}
