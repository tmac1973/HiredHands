namespace VikingsForHire.Hirelings
{
    /// <summary>One thing a hireling can be doing. Each tick the highest-priority behaviour that wants control runs.</summary>
    internal interface IHirelingBehaviour
    {
        string Name { get; }
        int Priority { get; }
        bool Wants(HirelingAI ai);
        void Tick(HirelingAI ai, float dt);
    }
}
