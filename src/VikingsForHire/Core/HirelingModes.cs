namespace VikingsForHire.Core
{
    // Persisted in ZDOs as ints: never renumber. Value 2 is reserved (an old "Stay" mode; Stay is a FollowMode).
    public enum HirelingMode
    {
        Working = 0,
        Following = 1,
        Idle = 3,
        Leaving = 4,
        /// <summary>On its way back to its board (phase 15): hidden, parked at home until its return time.</summary>
        Returning = 5,
    }

    public enum FollowMode
    {
        Follow = 0,
        Stay = 1,
        GatherHere = 2, // gatherers work around the stay spot; others hold it like Stay
    }
}
