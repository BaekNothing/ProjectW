using ProjectW.Contracts;

namespace ProjectW.HotUpdate
{
    public sealed class GameEntry : IGameEntry
    {
        public void Start(GameStartupContext context)
        {
            context.Host.AddComponent<ProjectW.MilestonePrototype.MilestonePrototypeController>();
            context.MarkHealthy();
        }
    }
}
