using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectW.MilestonePrototype.Editor
{
    public sealed class EnduranceSimulationWindow : EditorWindow
    {
        private int runCount = 200;
        private int maximumDays = 1000;
        private int restFatigue = 55;
        private int regenerationFatigue = 90;
        private int resourceReserve = 6;
        private int startingResources = 12;
        private int rewardPercent = 100;
        private int payrollInterval = 30;
        private Vector2 scroll;
        private EnduranceBatchResult result;

        [MenuItem("ProjectW/Balance/Endurance Simulator")]
        private static void Open() => GetWindow<EnduranceSimulationWindow>("Endurance Simulator");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Endless resource survival", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Identical policy, consecutive random seeds. A run ends only when resources reach zero or the safety cap is reached.",
                MessageType.Info);
            runCount = EditorGUILayout.IntSlider("Runs", runCount, 10, 2000);
            maximumDays = EditorGUILayout.IntSlider("Safety cap (days)", maximumDays, 100, 5000);
            startingResources = EditorGUILayout.IntSlider("Starting resources", startingResources, 1, 100);
            rewardPercent = EditorGUILayout.IntSlider("Work reward", rewardPercent, 25, 200);
            payrollInterval = EditorGUILayout.IntSlider("Payroll interval", payrollInterval, 10, 60);
            restFatigue = EditorGUILayout.IntSlider("Rest at fatigue", restFatigue, 20, 90);
            regenerationFatigue = EditorGUILayout.IntSlider("Regenerate at fatigue", regenerationFatigue, 55, 100);
            resourceReserve = EditorGUILayout.IntSlider("Resource reserve", resourceReserve, 0, 30);

            if (GUILayout.Button("Run simulation", GUILayout.Height(32)))
                result = EnduranceSimulator.Run(runCount, maximumDays, restFatigue,
                    regenerationFatigue, resourceReserve, startingResources, rewardPercent,
                    payrollInterval);
            if (result == null) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                $"Median D{result.MedianDay}   ·   P10 D{result.P10Day}   ·   P90 D{result.P90Day}   ·   cap reached {result.CappedRuns}/{result.RunCount}",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"Mean completed work {result.MeanCompletedWorks:0.0}   ·   mean failed work {result.MeanFailedWorks:0.0}");
            DrawSurvivalCurve(result);

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(180));
            foreach (EnduranceRunResult run in result.Runs.Take(50))
                EditorGUILayout.LabelField(
                    $"seed {run.Seed,4}  D{run.SurvivedDay,4}  resource {run.Resources,3}  complete {run.CompletedWorks,3}  failed {run.FailedWorks,3}" +
                    (run.ReachedCap ? "  [cap]" : string.Empty));
            EditorGUILayout.EndScrollView();
        }

        private static void DrawSurvivalCurve(EnduranceBatchResult batch)
        {
            Rect rect = GUILayoutUtility.GetRect(100, 180, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(.12f, .12f, .12f, .08f));
            Handles.BeginGUI();
            Handles.color = new Color(.2f, .65f, .9f, 1f);
            Vector3 previous = new Vector3(rect.x, rect.y, 0);
            for (int sample = 0; sample <= 100; sample++)
            {
                int day = batch.MaximumDays * sample / 100;
                int survivors = batch.Runs.Count(run => run.SurvivedDay >= day);
                float rate = survivors / (float)batch.RunCount;
                Vector3 point = new Vector3(
                    Mathf.Lerp(rect.x, rect.xMax, sample / 100f),
                    Mathf.Lerp(rect.yMax, rect.y, rate), 0);
                if (sample > 0) Handles.DrawLine(previous, point);
                previous = point;
            }
            Handles.EndGUI();
            GUI.Label(new Rect(rect.x + 6, rect.y + 4, 160, 20), "surviving runs 100%");
            GUI.Label(new Rect(rect.xMax - 100, rect.yMax - 22, 96, 20), $"D{batch.MaximumDays}");
        }
    }

    public sealed class EnduranceRunResult
    {
        public int Seed;
        public int SurvivedDay;
        public int Resources;
        public int CompletedWorks;
        public int FailedWorks;
        public bool ReachedCap;
    }

    public sealed class EnduranceBatchResult
    {
        public int RunCount;
        public int MaximumDays;
        public int MedianDay;
        public int P10Day;
        public int P90Day;
        public int CappedRuns;
        public float MeanCompletedWorks;
        public float MeanFailedWorks;
        public List<EnduranceRunResult> Runs;
    }

    public static class EnduranceSimulator
    {
        public static EnduranceBatchResult Run(int runCount, int maximumDays, int restFatigue,
            int regenerationFatigue, int resourceReserve, int startingResources,
            int rewardPercent, int payrollInterval)
        {
            var runs = new List<EnduranceRunResult>(runCount);
            for (int seed = 1; seed <= runCount; seed++)
                runs.Add(RunOnce(seed, maximumDays, restFatigue, regenerationFatigue,
                    resourceReserve, startingResources, rewardPercent, payrollInterval));

            int[] days = runs.Select(run => run.SurvivedDay).OrderBy(day => day).ToArray();
            return new EnduranceBatchResult
            {
                RunCount = runCount,
                MaximumDays = maximumDays,
                MedianDay = Percentile(days, .5f),
                P10Day = Percentile(days, .1f),
                P90Day = Percentile(days, .9f),
                CappedRuns = runs.Count(run => run.ReachedCap),
                MeanCompletedWorks = (float)runs.Average(run => run.CompletedWorks),
                MeanFailedWorks = (float)runs.Average(run => run.FailedWorks),
                Runs = runs.OrderBy(run => run.SurvivedDay).ToList()
            };
        }

        private static EnduranceRunResult RunOnce(int seed, int maximumDays, int restFatigue,
            int regenerationFatigue, int resourceReserve, int startingResources,
            int rewardPercent, int payrollInterval)
        {
            TaskSystemData data = TaskSystemDataLoader.Load();
            data.StartingResources = startingResources;
            data.Balance.PayrollIntervalDays = payrollInterval;
            data.Balance.RandomWorkMinReward = ScaleReward(data.Balance.RandomWorkMinReward, rewardPercent);
            data.Balance.RandomWorkMaxReward = ScaleReward(data.Balance.RandomWorkMaxReward, rewardPercent);
            foreach (WorkGroup group in data.Works)
                group.RewardCredits = ScaleReward(group.RewardCredits, rewardPercent);
            var game = new MilestoneSimulation(data, seed);
            while (!game.IsLost && game.Day <= maximumDays)
            {
                ResolveMail(game);
                ResolveCriticalEvent(game);
                ApplyCrewPolicy(game, restFatigue, regenerationFatigue, resourceReserve);
                AssignIdleCrew(game);
                game.AdvanceDay();
            }

            return new EnduranceRunResult
            {
                Seed = seed,
                SurvivedDay = game.Day,
                Resources = game.Resources,
                CompletedWorks = game.Groups.Count(group => group.State == WorkState.Complete),
                FailedWorks = game.Groups.Count(group => group.State == WorkState.Failed),
                ReachedCap = !game.IsLost
            };
        }

        private static void ResolveMail(MilestoneSimulation game)
        {
            foreach (MailEvent mail in game.Mail.Where(mail =>
                         mail.ArrivalDay <= game.Day && !mail.Resolved).ToList())
                game.ResolveMail(mail.Id);
        }

        private static void ResolveCriticalEvent(MilestoneSimulation game)
        {
            while (game.HasActiveCriticalEvent)
            {
                CriticalEventNode node = game.ActiveCriticalNode();
                if (node?.Choices == null || node.Choices.Length == 0) return;
                game.ChooseCriticalEvent(node.Choices[0].Id);
            }
        }

        private static void ApplyCrewPolicy(MilestoneSimulation game, int restFatigue,
            int regenerationFatigue, int resourceReserve)
        {
            for (int crewIndex = 0; crewIndex < game.Crew.Count; crewIndex++)
            {
                CrewMember member = game.Crew[crewIndex];
                if (member.InjuryDays > 0) continue;
                if (member.Fatigue >= regenerationFatigue &&
                    game.Resources - game.RegenerationResourceCost >= resourceReserve)
                {
                    game.Regenerate(crewIndex);
                    continue;
                }
                if (member.Fatigue >= restFatigue) game.Rest(crewIndex);
            }
        }

        private static void AssignIdleCrew(MilestoneSimulation game)
        {
            for (int crewIndex = 0; crewIndex < game.Crew.Count; crewIndex++)
            {
                CrewMember member = game.Crew[crewIndex];
                if (!member.Available || game.Tasks.Any(task =>
                        task.AssignedCharacter == crewIndex && !task.IsParallelAssignment &&
                        task.State != TaskState.Complete && task.State != TaskState.Failed))
                    continue;

                WorkTask best = game.Tasks
                    .Where(task => task.State == TaskState.Available && task.AssignedCharacter < 0)
                    .OrderBy(task => task.Deadline)
                    .ThenByDescending(task => CompetencyScore(member, task))
                    .ThenBy(task => task.RemainingWork)
                    .FirstOrDefault();
                if (best != null) game.Assign(best.Id, crewIndex);
            }
        }

        private static int CompetencyScore(CrewMember member, WorkTask task)
        {
            int score = member.Specialty == task.RequiredRole ? 100 : 0;
            if (task.RequiredCompetencies == null) return score;
            foreach (int competency in task.RequiredCompetencies)
                if (competency >= 0 && competency < member.Competencies.Length)
                    score += member.Competencies[competency];
            return score;
        }

        private static int ScaleReward(int reward, int rewardPercent) =>
            Mathf.Max(0, Mathf.RoundToInt(reward * rewardPercent / 100f));

        private static int Percentile(int[] sorted, float percentile)
        {
            if (sorted.Length == 0) return 0;
            int index = Mathf.Clamp(Mathf.RoundToInt((sorted.Length - 1) * percentile), 0, sorted.Length - 1);
            return sorted[index];
        }
    }
}
