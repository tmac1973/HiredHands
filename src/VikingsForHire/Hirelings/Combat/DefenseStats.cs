namespace VikingsForHire.Hirelings.Combat
{
    /// <summary>What a hireling's defence has done since it loaded: for test checks and the balance log.</summary>
    internal sealed class DefenseStats
    {
        public int Reads { get; set; }
        public int Misses { get; set; }
        public int Blocks { get; set; }
        public int Parries { get; set; }
        public int Dodges { get; set; }
        public int DodgedHits { get; set; }

        public int Get(string counter) => counter switch
        {
            "reads" => Reads,
            "misses" => Misses,
            "blocks" => Blocks,
            "parries" => Parries,
            "dodges" => Dodges,
            "dodged_hits" => DodgedHits,
            _ => throw new System.ArgumentException($"no defense counter {counter}"),
        };
    }
}
