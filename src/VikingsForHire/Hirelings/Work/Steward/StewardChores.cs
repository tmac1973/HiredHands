using System.Collections.Generic;
using VikingsForHire.Core.Chores;
using VikingsForHire.Hirelings.Work.Chores;

namespace VikingsForHire.Hirelings.Work.Steward
{
    /// <summary>The Steward's chores, in tie-break order.</summary>
    internal static class StewardChores
    {
        public static IEnumerable<IChore> All() => new IChore[]
        {
            new FiresChore(),
            new ProducersChore(ChoreKind.Beehives),
            new StationsChore(ChoreKind.Stations),
            new StationsChore(ChoreKind.Kilns),
            new StationsChore(ChoreKind.Mills),
            new ProducersChore(ChoreKind.Sap),
            new AnimalsChore(),
            new RepairsChore(),
            new FermentersChore(),
            new ShieldsChore(),
            new BoardChore(),
            new TidyChore(),
        };
    }
}
