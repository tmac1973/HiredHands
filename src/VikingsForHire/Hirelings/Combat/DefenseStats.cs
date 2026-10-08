namespace VikingsForHire.Hirelings.Combat
{
    /// <summary>What a hireling's defence has done since it loaded: for test checks and the balance log.</summary>
    internal sealed class DefenseStats
    {
        public int Reads { get; set; }
        public int Misses { get; set; }
        public int Blocks { get; set; }
        public int Parries { get; set; }
        public int ParryRolls { get; set; }
        public int ParryWins { get; set; }
        public int ProjReads { get; set; }
        public int ProjBlocks { get; set; }
        public int Dodges { get; set; }
        public int DodgeRolls { get; set; }
        public int DodgeWins { get; set; }
        public int DodgedHits { get; set; }

        public int Get(string counter) => counter switch
        {
            "reads" => Reads,
            "misses" => Misses,
            "blocks" => Blocks,
            "parries" => Parries,
            "parry_rolls" => ParryRolls,
            "parry_wins" => ParryWins,
            "proj_reads" => ProjReads,
            "proj_blocks" => ProjBlocks,
            "dodges" => Dodges,
            "dodged_hits" => DodgedHits,
            "dodge_rolls" => DodgeRolls,
            "dodge_wins" => DodgeWins,
            _ => throw new System.ArgumentException($"no defense counter {counter}"),
        };
    }
}
