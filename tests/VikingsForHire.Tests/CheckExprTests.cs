using VikingsForHire.Core.Testing;
using Xunit;

namespace VikingsForHire.Tests
{
    public class CheckExprTests
    {
        [Theory]
        [InlineData("40", "==", "40", true)]
        [InlineData("40", "==", "40.0", true)]
        [InlineData("2.5", ">=", "2", true)]
        [InlineData("2", ">", "2", false)]
        [InlineData("3", "<", "10", true)]
        [InlineData("0.30000001", "==", "0.3", true)]
        [InlineData("true", "==", "True", true)]
        [InlineData("false", "!=", "true", true)]
        [InlineData("Wood", "==", "wood", true)]
        [InlineData("Wood", "!=", "Stone", true)]
        public void Compares(string actual, string op, string expected, bool result) =>
            Assert.Equal(result, CheckExpr.Compare(actual, op, expected));

        [Fact]
        public void RejectsUnknownOperator() => Assert.Throws<System.ArgumentException>(() => CheckExpr.Compare("1", "=~", "1"));

        [Fact]
        public void RejectsOrderingOnBools() => Assert.Throws<System.ArgumentException>(() => CheckExpr.Compare("true", ">", "false"));
    }
}
