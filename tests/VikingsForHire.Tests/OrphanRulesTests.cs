using VikingsForHire.Core;
using Xunit;

namespace VikingsForHire.Tests
{
    public class OrphanRulesTests
    {
        [Theory]
        [InlineData(800f, 200f)]
        [InlineData(100f, 60f)]
        [InlineData(0f, 60f)]
        [InlineData(10000f, 1200f)]
        [InlineData(-50f, 60f)]
        public void ReturnSecondsClamped(float distance, float expected) =>
            Assert.Equal(expected, OrphanRules.ReturnSeconds(distance, 25f, 60f, 1200f), 3);
    }
}
