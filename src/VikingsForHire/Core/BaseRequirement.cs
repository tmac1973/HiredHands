using System.Collections.Generic;

namespace VikingsForHire.Core
{
    public enum BaseMissingKind
    {
        Workbench,
        Bed,
        Pieces,
        BoardTooClose,
        WorldBoardLimit,
    }

    /// <summary>One unmet requirement. Token is the localization key, Have/Need fill its numbers.</summary>
    public readonly record struct BaseMissing(BaseMissingKind Kind, float Have, float Need)
    {
        public string Token => Kind switch
        {
            BaseMissingKind.Workbench => "$vfh_base_need_workbench",
            BaseMissingKind.Bed => "$vfh_base_need_bed",
            BaseMissingKind.Pieces => "$vfh_base_need_pieces",
            BaseMissingKind.BoardTooClose => "$vfh_base_board_too_close",
            _ => "$vfh_base_world_board_limit",
        };
    }

    public readonly record struct BaseRules(int Workbenches, int Beds, int Pieces, float MinBoardDistance, int MaxBoardsPerWorld);

    public readonly record struct BaseCounts(int Workbenches, int Beds, int Pieces, float? NearestBoardDistance, int WorldBoardCount);

    public sealed class BaseCheckResult
    {
        public IReadOnlyList<BaseMissing> Missing { get; }
        public bool Ok => Missing.Count == 0;

        public BaseCheckResult(IReadOnlyList<BaseMissing> missing) => Missing = missing;
    }

    public static class BaseRequirement
    {
        public static BaseCheckResult Evaluate(BaseCounts counts, BaseRules rules)
        {
            var missing = new List<BaseMissing>();
            if (counts.Workbenches < rules.Workbenches)
                missing.Add(new BaseMissing(BaseMissingKind.Workbench, counts.Workbenches, rules.Workbenches));
            if (counts.Beds < rules.Beds)
                missing.Add(new BaseMissing(BaseMissingKind.Bed, counts.Beds, rules.Beds));
            if (counts.Pieces < rules.Pieces)
                missing.Add(new BaseMissing(BaseMissingKind.Pieces, counts.Pieces, rules.Pieces));
            if (counts.NearestBoardDistance is float d && d < rules.MinBoardDistance)
                missing.Add(new BaseMissing(BaseMissingKind.BoardTooClose, d, rules.MinBoardDistance));
            if (rules.MaxBoardsPerWorld > 0 && counts.WorldBoardCount >= rules.MaxBoardsPerWorld)
                missing.Add(new BaseMissing(BaseMissingKind.WorldBoardLimit, counts.WorldBoardCount, rules.MaxBoardsPerWorld));
            return new BaseCheckResult(missing);
        }
    }
}
