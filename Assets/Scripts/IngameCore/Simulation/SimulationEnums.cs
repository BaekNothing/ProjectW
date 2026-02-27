namespace ProjectW.IngameCore.Simulation
{
    public enum RoutineZone
    {
        Sleep = 0,
        PathSleepWork = 1,
        Work = 2,
        PathWorkEat = 3,
        Eat = 4
    }

    public enum AgentActionType
    {
        Rest,
        Work,
        Eat,
        Move
    }
}
