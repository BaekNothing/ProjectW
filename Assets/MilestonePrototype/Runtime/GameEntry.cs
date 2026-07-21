using ProjectW.Contracts;

namespace ProjectW.HotUpdate
{
    public sealed class GameEntry : IGameEntry
    {
        public void Start(GameStartupContext context)
        {
            var controller = context.Host.AddComponent<ProjectW.MilestonePrototype.MilestonePrototypeController>();
            controller.Initialize(context.PatchVersion);
            context.MarkHealthy();
        }
    }
}
