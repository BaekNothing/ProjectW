using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProjectW.IngameCore.Config;

namespace ProjectW.IngameCore.Meta
{
    public enum ChronicleEventCategory
    {
        RelationshipChange,
        Intervention,
        Termination,
        Progress,
        Risk
    }

    public sealed class ChronicleEvent
    {
        public ChronicleEventCategory Category { get; set; }
        public string Description { get; set; }
        public int Severity { get; set; }
        public bool? InterventionSucceeded { get; set; }
        public string TerminationReason { get; set; }
    }

    public sealed class ChronicleSummary
    {
        public IReadOnlyList<string> KeyEvents { get; set; }
        public string RawSummary { get; set; }
    }

    public static class ChronicleSummarizer
    {
        public static ChronicleSummary Summarize(IReadOnlyList<ChronicleEvent> events, int minEvents = 3, int maxEvents = 5)
        {
            var safeMin = Math.Clamp(minEvents, 1, 10);
            var safeMax = Math.Clamp(maxEvents, safeMin, 10);
            var source = events ?? Array.Empty<ChronicleEvent>();

            var ordered = source
                .Where(evt => evt != null && !string.IsNullOrWhiteSpace(evt.Description))
                .OrderByDescending(evt => evt.Severity)
                .ThenByDescending(evt => evt.Category == ChronicleEventCategory.Termination)
                .Take(safeMax)
                .ToList();

            EnsureCategory(ordered, source, ChronicleEventCategory.RelationshipChange);
            EnsureCategory(ordered, source, ChronicleEventCategory.Intervention);
            EnsureCategory(ordered, source, ChronicleEventCategory.Termination);

            while (ordered.Count < safeMin)
            {
                ordered.Add(new ChronicleEvent
                {
                    Category = ChronicleEventCategory.Progress,
                    Severity = 1,
                    Description = "중요 사건이 부족하여 운영 로그 기반으로 다음 사이클 점검이 필요합니다."
                });
            }

            if (ordered.Count > safeMax)
            {
                ordered = ordered.Take(safeMax).ToList();
            }

            var lines = ordered
                .Select((evt, idx) => $"{idx + 1}. [{evt.Category}] {evt.Description}")
                .ToList();

            return new ChronicleSummary
            {
                KeyEvents = lines,
                RawSummary = string.Join("\n", lines)
            };
        }

        private static void EnsureCategory(List<ChronicleEvent> selected, IReadOnlyList<ChronicleEvent> source, ChronicleEventCategory category)
        {
            if (selected.Any(evt => evt.Category == category))
            {
                return;
            }

            var candidate = source
                .Where(evt => evt != null && evt.Category == category && !string.IsNullOrWhiteSpace(evt.Description))
                .OrderByDescending(evt => evt.Severity)
                .FirstOrDefault();

            if (candidate != null)
            {
                selected.Add(candidate);
            }
        }
    }

    public sealed class MetaChoice
    {
        public string ChoiceId { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }
        public int InterventionSlotBonus { get; set; }
        public float StressBiasDelta { get; set; }
        public float StartTickScale { get; set; } = 1f;
    }

    public static class MetaChoiceCatalog
    {
        public static IReadOnlyList<MetaChoice> BuildDefaultChoices()
        {
            return new[]
            {
                new MetaChoice
                {
                    ChoiceId = "meta.slot_plus_1",
                    Label = "개입 슬롯 +1",
                    Description = "다음 세션의 동시 개입 여유를 +1 확보합니다.",
                    InterventionSlotBonus = 1
                },
                new MetaChoice
                {
                    ChoiceId = "meta.stress_guard",
                    Label = "안정 성향 보정",
                    Description = "모든 캐릭터의 시작 스트레스를 +10 보정합니다.",
                    StressBiasDelta = 10f
                },
                new MetaChoice
                {
                    ChoiceId = "meta.fast_tick",
                    Label = "시작 정책 프리셋(빠른 템포)",
                    Description = "다음 세션 Tick 시간을 10% 단축합니다.",
                    StartTickScale = 0.9f
                }
            };
        }

        public static MetaChoice ResolveOrDefault(string choiceId)
        {
            var choices = BuildDefaultChoices();
            if (!string.IsNullOrWhiteSpace(choiceId))
            {
                var resolved = choices.FirstOrDefault(choice => string.Equals(choice.ChoiceId, choiceId.Trim(), StringComparison.Ordinal));
                if (resolved != null)
                {
                    return resolved;
                }
            }

            return choices[0];
        }
    }

    public sealed class SessionMetaState
    {
        public int InterventionSlotBonus { get; private set; }
        public float StressBiasDelta { get; private set; }
        public float TickScale { get; private set; } = 1f;
        public string LastChoiceId { get; private set; }

        public void ApplyChoice(MetaChoice choice)
        {
            if (choice == null)
            {
                return;
            }

            InterventionSlotBonus += Math.Max(0, choice.InterventionSlotBonus);
            StressBiasDelta += choice.StressBiasDelta;
            TickScale *= Math.Clamp(choice.StartTickScale, 0.5f, 1.5f);
            LastChoiceId = choice.ChoiceId;
        }

        public void ApplyToSessionConfig(ProjectW.IngameCore.Config.SessionConfig config)
        {
            if (config == null)
            {
                return;
            }

            config.TickSeconds = (float)Math.Round(Math.Max(0.01f, config.TickSeconds * TickScale), 3);
        }
    }

    public sealed class CycleKpiSnapshot
    {
        public float InterventionUsageRate { get; set; }
        public float InterventionOutcomeChangeRate { get; set; }
        public int RememberedEventSelfScore { get; set; }
    }

    public sealed class PlaytestSurveyForm
    {
        public static readonly IReadOnlyList<string> FixedQuestions = new[]
        {
            "한 판(15~20분) 후 다시 한 판 하고 싶은가요?",
            "개입 결과가 체감될 만큼 명확했나요?",
            "사건 요약(Chronicle)이 다음 판 전략 수립에 도움이 되었나요?",
            "메타 선택지가 플레이 스타일 변화를 유도했나요?",
            "전체적으로 이번 사이클은 재미있었나요?"
        };

        private readonly int[] _responses = new int[5];

        public void SetResponse(int questionIndex, int score1To5)
        {
            if (questionIndex < 0 || questionIndex >= _responses.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(questionIndex));
            }

            _responses[questionIndex] = Math.Clamp(score1To5, 1, 5);
        }

        public IReadOnlyList<int> GetResponses()
        {
            return _responses;
        }

        public bool WantsReplay()
        {
            return _responses[0] >= 4;
        }

        public string ToCompactText()
        {
            var sb = new StringBuilder(120);
            for (int i = 0; i < _responses.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                sb.Append($"Q{i + 1}:{_responses[i]}");
            }

            return sb.ToString();
        }
    }
}
