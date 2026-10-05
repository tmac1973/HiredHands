using VikingsForHire.Core.Chores;
using Xunit;

namespace VikingsForHire.Tests
{
    public class ChoreUrgencyTests
    {
        [Fact]
        public void Fires()
        {
            Assert.Equal(0f, ChoreUrgency.Fire(6f, 10f, 0.5f));
            Assert.InRange(ChoreUrgency.Fire(1f, 10f, 0.5f), 0.89f, 0.91f);
            Assert.Equal(1f, ChoreUrgency.Fire(0f, 10f, 0.5f));
            Assert.Equal(0f, ChoreUrgency.Fire(0f, 0f, 0.5f));
        }

        [Fact]
        public void Producers()
        {
            Assert.Equal(0f, ChoreUrgency.Producer(1, 4));
            Assert.InRange(ChoreUrgency.Producer(2, 4), 0.49f, 0.51f);
            Assert.InRange(ChoreUrgency.Producer(4, 4), 0.69f, 0.71f);
        }

        [Fact]
        public void Repairs()
        {
            Assert.Equal(0f, ChoreUrgency.Repair(0.96f, 0.95f));
            Assert.InRange(ChoreUrgency.Repair(0.1f, 0.95f), 0.83f, 0.85f);
        }

        [Fact]
        public void Stations()
        {
            Assert.Equal(0f, ChoreUrgency.Station(0.8f, 0.8f, false, 0.5f));
            Assert.InRange(ChoreUrgency.Station(0f, 0.8f, false, 0.5f), 0.89f, 0.91f);
            Assert.Equal(0.6f, ChoreUrgency.Station(0.8f, 0.8f, true, 0.5f));
        }

        [Fact]
        public void DistanceOrdersEqualJobsButNotUrgentOnes()
        {
            float near = ChoreUrgency.Score(0.7f, 5f, 30f);
            float far = ChoreUrgency.Score(0.7f, 25f, 30f);
            Assert.True(near > far);
            float urgentFar = ChoreUrgency.Score(1f, 30f, 30f);
            float idleNear = ChoreUrgency.Score(0f, 0f, 30f);
            Assert.True(urgentFar > idleNear);
            Assert.True(urgentFar > ChoreUrgency.Score(0.7f, 0f, 30f));
        }
    }
}
