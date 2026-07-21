using System.Linq;
using UnityEngine;

namespace ProjectW.MilestonePrototype
{
    public sealed class MilestonePrototypeController : MonoBehaviour
    {
        private MilestoneSimulation game;
        private Vector2 taskScroll;
        private Vector2 crewScroll;
        private GUIStyle title;
        private GUIStyle section;
        private GUIStyle warning;
        private GUIStyle versionBadge;
        private GUIStyle statusBox;
        private GUIStyle statusWarningBox;
        private string patchVersion = "embedded";

        private void Awake() => game = new MilestoneSimulation();

        public void Initialize(string version)
        {
            patchVersion = string.IsNullOrWhiteSpace(version) ? "unknown" : version.Trim();
        }

        private void OnGUI()
        {
            EnsureStyles();
            float scale = Mathf.Clamp(Screen.width / 1500f, 0.75f, 1.35f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            UiCoordinateGrid.Draw(width, height);

            GUILayout.BeginArea(new Rect(18, 14, width - 36, height - 28));
            GUILayout.BeginHorizontal();
            GUILayout.Label("PROJECT W — MILESTONE CONTROL", title);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"PATCH {patchVersion}", versionBadge, GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            GUILayout.Label($"DAY {game.Day:00}/{game.CampaignEndDay}     자원 {game.Resources}     가용 {game.Crew.Count(c => c.Available)}/{game.Crew.Count}     평균 피로 {(int)game.Crew.Average(c => c.Fatigue)}%     미처리 사이드 {game.Tasks.Count(t => t.Kind == TaskKind.SideMission && t.State != TaskState.Complete)}");
            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            DrawTasks(width * 0.62f, height - 160);
            GUILayout.Space(10);
            DrawCrew(width * 0.35f, height - 160);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.Height(54));
            GUI.enabled = !game.IsWon && !game.IsLost;
            if (GUILayout.Button("하루 실행", GUILayout.Height(54), GUILayout.Width(180))) game.AdvanceDay();
            GUI.enabled = true;
            GUILayout.Space(10);
            GUILayout.Label(FormatStatus(game.LastReport, game.IsWon, game.IsLost), game.IsLost ? statusWarningBox : statusBox, GUILayout.Height(54), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        public static string FormatStatus(DayReport report, bool isWon, bool isLost)
        {
            if (isWon) return "마일스톤 완료 — 캠페인 승리";
            if (isLost) return "운영 붕괴 — 캠페인 실패";
            if (report == null || report.Lines.Count == 0) return string.Empty;

            return string.Join("\n", report.Lines.Skip(System.Math.Max(0, report.Lines.Count - 2)));
        }

        private void DrawTasks(float width, float height)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(width), GUILayout.Height(height));
            GUILayout.Label("작업 간트 / 배치", section);
            GUILayout.Label("작업                           역할       진행              기한       담당 (클릭하여 변경)");
            taskScroll = GUILayout.BeginScrollView(taskScroll);
            foreach (WorkTask task in game.Tasks)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label(task.Kind == TaskKind.SideMission ? $"! {task.Name}" : task.Required ? task.Name : $"◇ {task.Name}", GUILayout.Width(215));
                GUILayout.Label(RoleName(task.RequiredRole), GUILayout.Width(58));
                GUILayout.HorizontalSlider(task.Completion, 0, 1, GUILayout.Width(105));
                GUILayout.Label($"{task.Progress}/{task.RequiredWork}", GUILayout.Width(55));
                GUILayout.Label($"D{task.Deadline}" + (task.DelayDays > 0 ? $" +{task.DelayDays}" : ""), task.DelayDays > 0 ? warning : GUI.skin.label, GUILayout.Width(58));
                GUI.enabled = task.State == TaskState.Available || task.State == TaskState.Active;
                string assignee = task.AssignedCharacter < 0 ? StateName(task.State) : game.Crew[task.AssignedCharacter].Name;
                if (GUILayout.Button(assignee, GUILayout.Width(130))) AssignNext(task);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawCrew(float width, float height)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(width), GUILayout.Height(height));
            GUILayout.Label("인력 운영", section);
            GUILayout.Label("휴식은 하루 점유·피로 -18 / 재생성은 자원 3, 경험 손실");
            crewScroll = GUILayout.BeginScrollView(crewScroll);
            for (int i = 0; i < game.Crew.Count; i++)
            {
                CrewMember member = game.Crew[i];
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"{member.Name}   {RoleName(member.Specialty)} {member.Skill}   경험 {member.Experience}   {(member.RestScheduled ? "휴식 예정" : member.Condition)}");
                GUILayout.Label($"피로 {member.Fatigue}%  {new string('■', member.Fatigue / 10)}");
                GUILayout.BeginHorizontal();
                GUI.enabled = member.InjuryDays <= 0 && !member.RestScheduled;
                if (GUILayout.Button(member.RestScheduled ? "휴식 예정" : "휴식 예약")) game.Rest(i);
                GUI.enabled = game.Resources >= 3;
                if (GUILayout.Button($"재생성 ({member.RegenerationCount})")) game.Regenerate(i);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void AssignNext(WorkTask task)
        {
            int start = task.AssignedCharacter;
            for (int offset = 1; offset <= game.Crew.Count + 1; offset++)
            {
                int candidate = start + offset;
                if (candidate >= game.Crew.Count) candidate = -1;
                if (candidate < 0 || game.Crew[candidate].Available)
                {
                    game.Assign(task.Id, candidate);
                    return;
                }
            }
        }

        private void EnsureStyles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            section = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            warning = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(1f, .38f, .25f) }, fontStyle = FontStyle.Bold };
            versionBadge = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(10, 10, 4, 4)
            };
            statusBox = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                wordWrap = true,
                padding = new RectOffset(10, 10, 5, 5)
            };
            statusWarningBox = new GUIStyle(statusBox) { fontStyle = FontStyle.Bold };
            statusWarningBox.normal.textColor = new Color(1f, .38f, .25f);
        }

        private static string RoleName(WorkRole role) => role switch { WorkRole.Tech => "기술", WorkRole.Analysis => "분석", WorkRole.Management => "관리", _ => "적응" };
        private static string StateName(TaskState state) => state switch { TaskState.Locked => "잠김", TaskState.Complete => "완료", _ => "미배치" };
    }
}
