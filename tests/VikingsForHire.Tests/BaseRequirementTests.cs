using System.Linq;
using VikingsForHire.Core;
using Xunit;

namespace VikingsForHire.Tests
{
    public class BaseRequirementTests
    {
        private static readonly BaseRules Rules = new(1, 1, 40, 100f, 0);

        [Fact]
        public void QualifyingBasePasses() =>
            Assert.True(BaseRequirement.Evaluate(new BaseCounts(1, 2, 55, null, 0), Rules).Ok);

        [Fact]
        public void ListsEveryMissingRequirement()
        {
            BaseCheckResult r = BaseRequirement.Evaluate(new BaseCounts(0, 0, 31, 64f, 0), Rules);
            Assert.Equal(new[] { BaseMissingKind.Workbench, BaseMissingKind.Bed, BaseMissingKind.Pieces, BaseMissingKind.BoardTooClose },
                r.Missing.Select(m => m.Kind));
            BaseMissing pieces = r.Missing.Single(m => m.Kind == BaseMissingKind.Pieces);
            Assert.Equal(31f, pieces.Have);
            Assert.Equal(40f, pieces.Need);
            Assert.Equal("$vfh_base_need_pieces", pieces.Token);
        }

        [Fact]
        public void WorldLimitOnlyWhenSet()
        {
            Assert.True(BaseRequirement.Evaluate(new BaseCounts(1, 1, 40, null, 50), Rules).Ok);
            BaseCheckResult r = BaseRequirement.Evaluate(new BaseCounts(1, 1, 40, null, 1), Rules with { MaxBoardsPerWorld = 1 });
            Assert.Equal(BaseMissingKind.WorldBoardLimit, Assert.Single(r.Missing).Kind);
        }

        [Fact]
        public void BoardExactlyAtMinDistanceIsAllowed() =>
            Assert.True(BaseRequirement.Evaluate(new BaseCounts(1, 1, 40, 100f, 1), Rules).Ok);
    }
}
