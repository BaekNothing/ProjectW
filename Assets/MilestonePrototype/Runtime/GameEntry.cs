using ProjectW.Contracts;

namespace ProjectW.HotUpdate
{
    public sealed class GameEntry : IGameEntry
    {
        public void Start(GameStartupContext context)
        {
            ProjectW.MilestonePrototype.ProjectWSaveStore.Configure(context.Storage);
            ProjectW.MilestonePrototype.TaskSystemDataLoader.Configure(context.DataPath, context.Storage);
            var controller = context.Host.AddComponent<ProjectW.MilestonePrototype.MilestonePrototypeController>();
            controller.Initialize(context.PatchVersion, context.PatchDiagnostics);
            context.MarkHealthy();
        }
    }
}
