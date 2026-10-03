using VikingsForHire.Core;
using Xunit;

namespace VikingsForHire.Tests
{
    public class SmokeTests
    {
        [Fact]
        public void CoreIsLinked() => Assert.Equal("VikingsForHire.Core", CoreInfo.Name);
    }
}
