using ProjectW.Contracts;

namespace ProjectW.HotUpdate
{
    public sealed class GameEntry : IGameEntry
    {
        public void Start(GameStartupContext context)
        {
            MilestonePrototype.ProjectWSaveStore.Configure(context.Storage);
            MilestonePrototype.TaskSystemDataLoader.Configure(context.DataPath, context.Storage);

            var controller = context.Host.AddComponent<MilestonePrototype.ProjectWUiController>();
            controller.Initialize(context.PatchVersion, context.PatchDiagnostics);
            context.MarkHealthy();
        }
    }
}
