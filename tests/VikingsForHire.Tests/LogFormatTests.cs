using System.Linq;
using VikingsForHire.Core.Diagnostics;
using Xunit;

namespace VikingsForHire.Tests
{
    public class LogFormatTests
    {
        [Fact]
        public void LineShape()
        {
            string line = LogFormat.Line(1834.25, 99120, "SP", LogLevel.Debug, LogCat.Work, "deliver.plan",
                new (string, object?)[] { ("hid", "3f2a91c0-aaaa-bbbb"), ("item", "Wood"), ("n", 40), ("dist", 7.4f), ("ok", true) });
            Assert.Equal("[VFH] t=1834.3 f=99120 role=SP lvl=D cat=Work evt=deliver.plan hid=3f2a item=Wood n=40 dist=7.4 ok=true", line);
        }

        [Fact]
        public void QuotesValuesWithSpaces()
        {
            Assert.Equal("\"before bug\"", LogFormat.Value("before bug"));
            Assert.Equal("\"say \\\"hi\\\"\"", LogFormat.Value("say \"hi\""));
            Assert.Equal("\"a=b\"", LogFormat.Value("a=b"));
            Assert.Equal("\"\"", LogFormat.Value(""));
            Assert.Equal("null", LogFormat.Value(null));
        }

        [Fact]
        public void InvariantNumbers()
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal("1.5", LogFormat.Value(1.5f));
        }

        [Fact]
        public void ShortIds()
        {
            Assert.Equal("3f2a", LogFormat.ShortId("3F2A91C0-0000"));
            Assert.Equal("ab", LogFormat.ShortId("ab"));
        }

        [Fact]
        public void FilterLevels()
        {
            var f = new LogFilter(new[] { LogCat.Work }, new[] { LogCat.AI });
            Assert.True(f.Enabled(LogLevel.Info, LogCat.Board));
            Assert.True(f.Enabled(LogLevel.Error, LogCat.Board));
            Assert.False(f.Enabled(LogLevel.Debug, LogCat.Board));
            Assert.True(f.Enabled(LogLevel.Debug, LogCat.Work));
            Assert.False(f.Enabled(LogLevel.Trace, LogCat.Work));
            Assert.True(f.Enabled(LogLevel.Debug, LogCat.AI)); // trace implies debug
            Assert.True(f.Enabled(LogLevel.Trace, LogCat.AI));
        }

        [Fact]
        public void ParseCategories()
        {
            var cats = LogFilter.ParseCategories("work, ai,Bogus", out var unknown);
            Assert.Equal(new[] { LogCat.AI, LogCat.Work }, cats.OrderBy(c => c));
            Assert.Equal(new[] { "Bogus" }, unknown);
            Assert.Equal(System.Enum.GetValues(typeof(LogCat)).Length, LogFilter.ParseCategories("All", out _).Count);
            Assert.Equal("All", LogFilter.FormatCategories(LogFilter.ParseCategories("all", out _)));
            Assert.Equal("Work,AI", LogFilter.FormatCategories(new[] { LogCat.AI, LogCat.Work }).Replace("AI,Work", "Work,AI"));
        }
    }
}
