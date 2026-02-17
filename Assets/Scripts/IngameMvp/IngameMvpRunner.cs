using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ProjectW.IngameMvp
{
    public enum LoopState
    {
        Plan,
        Drop,
        AutoNarrative,
        CaptainIntervention,
        NightDream,
        Resolve,
        NextCycle,
        SessionEnd,
        SafeHalt
    }

    [Serializable]
    public class SessionConfig
    {
        public string sessionId;
        public float tickSeconds;
        public int maxDecisionRetry;
        public int maxPersistRetry;
        public int persistRetryBackoffMs;
    }

    [Serializable]
    public class StateTransitionRuleRow
    {
        public LoopState fromState;
        public LoopState toState;
        public string entryCondition;
        public string exitCondition;
        public string guard;
        public int priority;
        public bool enabled;
    }

    [Serializable]
    public class InterventionCommandRow
    {
        public string commandId;
        public int issuedTick;
        public int applyTick;
        public string commandType;
        public string targetScope;
        public string payloadJson;
        public int priority;
        public string supersedesCommandId;
    }

    [Serializable]
    public class CharacterProfileRow
    {
        public string characterId;
        public string traitWeightsJson;
        public float stress;
        public float health;
        public int traumaLevel;
        public bool enabled;
    }

    [Serializable]
    public class TerminationRuleRow
    {
        public string ruleId;
        public string conditionType;
        public string thresholdExpr;
        public string resultCode;
        public bool enabled;
        public int priority;
    }

    [Serializable]
    public class CsvLoadResult
    {
        public bool success;
        public string errorCode;
        public string errorMessage;
        public SessionConfig sessionConfig;
        public List<StateTransitionRuleRow> transitions = new List<StateTransitionRuleRow>();
        public List<InterventionCommandRow> interventions = new List<InterventionCommandRow>();
        public List<CharacterProfileRow> characters = new List<CharacterProfileRow>();
        public List<TerminationRuleRow> terminations = new List<TerminationRuleRow>();
    }

    public interface ICsvConfigProvider
    {
        CsvLoadResult LoadAll(string folderPath);
    }

    public class IngameCsvConfigProvider : ICsvConfigProvider
    {
        public CsvLoadResult LoadAll(string folderPath)
        {
            var result = new CsvLoadResult();

            string[] orderedFiles =
            {
                "SessionConfig.csv",
                "StateTransitionRules.csv",
                "CharacterProfiles.csv",
                "TerminationRules.csv",
                "InterventionCommands.csv"
            };

            foreach (var file in orderedFiles)
            {
                if (!File.Exists(Path.Combine(folderPath, file)))
                {
                    return Fail("E-CSV-004", $"Required file missing: {file}");
                }
            }

            var sessionRaw = ReadRows(Path.Combine(folderPath, "SessionConfig.csv"), new[]
            {
                "session_id", "tick_seconds", "max_decision_retry", "max_persist_retry", "persist_retry_backoff_ms"
            });
            if (!sessionRaw.success)
            {
                return Fail(sessionRaw.errorCode, sessionRaw.errorMessage);
            }

            var transitionRaw = ReadRows(Path.Combine(folderPath, "StateTransitionRules.csv"), new[]
            {
                "from_state", "to_state", "entry_condition", "exit_condition", "guard", "priority", "enabled"
            });
            if (!transitionRaw.success)
            {
                return Fail(transitionRaw.errorCode, transitionRaw.errorMessage);
            }

            var characterRaw = ReadRows(Path.Combine(folderPath, "CharacterProfiles.csv"), new[]
            {
                "character_id", "trait_weights_json", "stress", "health", "trauma_level", "enabled"
            });
            if (!characterRaw.success)
            {
                return Fail(characterRaw.errorCode, characterRaw.errorMessage);
            }

            var terminationRaw = ReadRows(Path.Combine(folderPath, "TerminationRules.csv"), new[]
            {
                "rule_id", "condition_type", "threshold_expr", "result_code", "enabled", "priority"
            });
            if (!terminationRaw.success)
            {
                return Fail(terminationRaw.errorCode, terminationRaw.errorMessage);
            }

            var interventionRaw = ReadRows(Path.Combine(folderPath, "InterventionCommands.csv"), new[]
            {
                "command_id", "issued_tick", "apply_tick", "command_type", "target_scope", "payload_json", "priority", "supersedes_command_id"
            });
            if (!interventionRaw.success)
            {
                return Fail(interventionRaw.errorCode, interventionRaw.errorMessage);
            }

            try
            {
                var session = sessionRaw.rows[0];
                result.sessionConfig = new SessionConfig
                {
                    sessionId = session["session_id"],
                    tickSeconds = ParseFloat(session["tick_seconds"]),
                    maxDecisionRetry = ParseInt(session["max_decision_retry"]),
                    maxPersistRetry = ParseInt(session["max_persist_retry"]),
                    persistRetryBackoffMs = ParseInt(session["persist_retry_backoff_ms"])
                };

                foreach (var row in transitionRaw.rows)
                {
                    result.transitions.Add(new StateTransitionRuleRow
                    {
                        fromState = ParseState(row["from_state"]),
                        toState = ParseState(row["to_state"]),
                        entryCondition = row["entry_condition"],
                        exitCondition = row["exit_condition"],
                        guard = row["guard"],
                        priority = ParseInt(row["priority"]),
                        enabled = ParseBool(row["enabled"])
                    });
                }

                foreach (var row in characterRaw.rows)
                {
                    result.characters.Add(new CharacterProfileRow
                    {
                        characterId = row["character_id"],
                        traitWeightsJson = row["trait_weights_json"],
                        stress = ParseFloat(row["stress"]),
                        health = ParseFloat(row["health"]),
                        traumaLevel = ParseInt(row["trauma_level"]),
                        enabled = ParseBool(row["enabled"])
                    });
                }

                foreach (var row in terminationRaw.rows)
                {
                    result.terminations.Add(new TerminationRuleRow
                    {
                        ruleId = row["rule_id"],
                        conditionType = row["condition_type"],
                        thresholdExpr = row["threshold_expr"],
                        resultCode = row["result_code"],
                        enabled = ParseBool(row["enabled"]),
                        priority = ParseInt(row["priority"])
                    });
                }

                foreach (var row in interventionRaw.rows)
                {
                    result.interventions.Add(new InterventionCommandRow
                    {
                        commandId = row["command_id"],
                        issuedTick = ParseInt(row["issued_tick"]),
                        applyTick = ParseInt(row["apply_tick"]),
                        commandType = row["command_type"],
                        targetScope = row["target_scope"],
                        payloadJson = row["payload_json"],
                        priority = ParseInt(row["priority"]),
                        supersedesCommandId = row["supersedes_command_id"]
                    });
                }
            }
            catch (Exception ex)
            {
                return Fail("E-CSV-003", ex.Message);
            }

            if (result.sessionConfig == null || result.transitions.Count == 0)
            {
                return Fail("E-CSV-004", "Required rows missing.");
            }

            result.success = true;
            return result;
        }

        private static CsvLoadResult Fail(string code, string message)
        {
            return new CsvLoadResult
            {
                success = false,
                errorCode = code,
                errorMessage = message
            };
        }

        private static (bool success, string errorCode, string errorMessage, List<Dictionary<string, string>> rows) ReadRows(string path, string[] requiredHeaders)
        {
            string content;
            try
            {
                content = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                return (false, "E-CSV-003", ex.Message, null);
            }

            var lines = content.Replace("\r", string.Empty)
                .Split('\n')
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();

            if (lines.Length < 2)
            {
                return (false, "E-CSV-004", "No data rows.", null);
            }

            var headers = SplitCsv(lines[0]);
            if (headers.Length == 0)
            {
                return (false, "E-CSV-001", "Header missing.", null);
            }

            foreach (var required in requiredHeaders)
            {
                if (!headers.Contains(required))
                {
                    return (false, "E-CSV-002", $"Missing required column: {required}", null);
                }
            }

            var rows = new List<Dictionary<string, string>>();
            for (int i = 1; i < lines.Length; i++)
            {
                var cells = SplitCsv(lines[i]);
                var row = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int c = 0; c < headers.Length; c++)
                {
                    row[headers[c]] = c < cells.Length ? cells[c] : string.Empty;
                }

                rows.Add(row);
            }

            return (true, string.Empty, string.Empty, rows);
        }

        private static string[] SplitCsv(string line)
        {
            var cells = new List<string>();
            bool inQuotes = false;
            int start = 0;

            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        i++;
                        continue;
                    }

                    inQuotes = !inQuotes;
                    continue;
                }

                if (line[i] == ',' && !inQuotes)
                {
                    cells.Add(UnescapeCsv(line.Substring(start, i - start)));
                    start = i + 1;
                }
            }

            cells.Add(UnescapeCsv(line.Substring(start)));
            return cells.ToArray();
        }

        private static string UnescapeCsv(string raw)
        {
            var value = raw.Trim();
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
            {
                value = value.Substring(1, value.Length - 2);
            }

            return value.Replace("\"\"", "\"");
        }

        private static int ParseInt(string value)
        {
            return int.Parse(value, CultureInfo.InvariantCulture);
        }

        private static float ParseFloat(string value)
        {
            return float.Parse(value, CultureInfo.InvariantCulture);
        }

        private static bool ParseBool(string value)
        {
            return value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private static LoopState ParseState(string raw)
        {
            if (Enum.TryParse(raw, out LoopState parsed))
            {
                return parsed;
            }

            throw new ArgumentException($"Unknown state: {raw}");
        }
    }

    public class IngameMvpCore
    {
        private readonly SessionConfig _config;
        private readonly List<StateTransitionRuleRow> _rules;
        private readonly List<TerminationRuleRow> _terminationRules;

        public int TickIndex { get; private set; }
        public LoopState CurrentState { get; private set; } = LoopState.Plan;
        public bool IsPaused { get; private set; }

        public IngameMvpCore(SessionConfig config, List<StateTransitionRuleRow> rules, List<TerminationRuleRow> terminationRules)
        {
            _config = config;
            _rules = rules.OrderBy(r => r.priority).ToList();
            _terminationRules = terminationRules.OrderBy(r => r.priority).ToList();
        }

        public void Pause() => IsPaused = true;

        public void Resume() => IsPaused = false;

        public bool Step(out string errorCode)
        {
            errorCode = string.Empty;
            if (IsPaused)
            {
                return false;
            }

            if (_config.tickSeconds <= 0f)
            {
                errorCode = "E-TIME-001";
                return false;
            }

            var next = EvaluateTransition(CurrentState, TickIndex);
            if (next == CurrentState)
            {
                errorCode = "E-STATE-199";
                return false;
            }

            if (!IsValidTransition(CurrentState, next))
            {
                errorCode = "E-STATE-199";
                return false;
            }

            CurrentState = next;
            TickIndex += 1;

            if (CurrentState == LoopState.NextCycle)
            {
                CurrentState = LoopState.Plan;
            }

            return true;
        }

        public LoopState EvaluateTransition(LoopState current, int tick)
        {
            if (current == LoopState.Resolve)
            {
                return EvaluateResolveBranch(tick) ? LoopState.SessionEnd : LoopState.NextCycle;
            }

            foreach (var rule in _rules)
            {
                if (!rule.enabled || rule.fromState != current)
                {
                    continue;
                }

                if (IsGuardTrue(rule.guard))
                {
                    return rule.toState;
                }
            }

            return current;
        }

        private bool EvaluateResolveBranch(int tick)
        {
            foreach (var rule in _terminationRules)
            {
                if (!rule.enabled)
                {
                    continue;
                }

                if (rule.conditionType.Equals("ObjectiveComplete", StringComparison.Ordinal) &&
                    EvaluateThreshold(rule.thresholdExpr, tick))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool EvaluateThreshold(string expr, int tick)
        {
            if (string.IsNullOrWhiteSpace(expr))
            {
                return false;
            }

            const string pattern = "tick_index>=";
            if (expr.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
            {
                var value = expr.Substring(pattern.Length);
                if (int.TryParse(value, out var requiredTick))
                {
                    return tick >= requiredTick;
                }
            }

            return false;
        }

        private static bool IsGuardTrue(string guard)
        {
            return !string.Equals(guard, "false", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsValidTransition(LoopState from, LoopState to)
        {
            return from switch
            {
                LoopState.Plan => to == LoopState.Drop,
                LoopState.Drop => to == LoopState.AutoNarrative,
                LoopState.AutoNarrative => to == LoopState.CaptainIntervention,
                LoopState.CaptainIntervention => to == LoopState.NightDream,
                LoopState.NightDream => to == LoopState.Resolve,
                LoopState.Resolve => to == LoopState.NextCycle || to == LoopState.SessionEnd,
                _ => false
            };
        }
    }

    public class IngameMvpRunner : MonoBehaviour
    {
        [SerializeField] private string csvFolderName = "IngameMvp";
        [SerializeField] private bool autoStartOnPlay = true;

        private readonly Dictionary<string, GameObject> _characters = new Dictionary<string, GameObject>();
        private IngameMvpCore _core;
        private List<InterventionCommandRow> _interventions;

        public string LastErrorCode { get; private set; } = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrap()
        {
            var existing = FindObjectOfType<IngameMvpRunner>();
            if (existing != null)
            {
                return;
            }

            var bootstrap = new GameObject("IngameMvpBootstrap");
            bootstrap.AddComponent<IngameMvpRunner>();
        }

        private void Start()
        {
            if (autoStartOnPlay)
            {
                InitializeAndRun();
            }
        }

        public bool InitializeAndRun()
        {
            string folderPath = Path.Combine(Application.streamingAssetsPath, csvFolderName);
            ICsvConfigProvider provider = new IngameCsvConfigProvider();
            var loadResult = provider.LoadAll(folderPath);

            if (!loadResult.success)
            {
                LastErrorCode = loadResult.errorCode;
                Debug.LogError($"[{LastErrorCode}] {loadResult.errorMessage}");
                return false;
            }

            if (loadResult.sessionConfig.tickSeconds <= 0f)
            {
                LastErrorCode = "E-TIME-001";
                Debug.LogError("[E-TIME-001] tick_seconds must be > 0.");
                return false;
            }

            _core = new IngameMvpCore(loadResult.sessionConfig, loadResult.transitions, loadResult.terminations);
            _interventions = loadResult.interventions;

            SpawnCharacters(loadResult.characters);
            StartCoroutine(RunLoop(loadResult.sessionConfig.tickSeconds));
            return true;
        }

        private void SpawnCharacters(List<CharacterProfileRow> characters)
        {
            int index = 0;
            foreach (var character in characters.Where(c => c.enabled))
            {
                if (_characters.ContainsKey(character.characterId))
                {
                    continue;
                }

                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = $"Character_{character.characterId}";
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(index * 2f, 0f, 0f);
                _characters.Add(character.characterId, go);
                index++;
            }
        }

        private IEnumerator RunLoop(float tickSeconds)
        {
            while (_core != null && _core.CurrentState != LoopState.SessionEnd && _core.CurrentState != LoopState.SafeHalt)
            {
                yield return new WaitForSeconds(tickSeconds);

                ApplyInterventionsForTick(_core.TickIndex);

                if (!_core.Step(out var errorCode) && !string.IsNullOrEmpty(errorCode))
                {
                    LastErrorCode = errorCode;
                    Debug.LogWarning($"[{errorCode}] Transition blocked at tick {_core.TickIndex}.");
                }

                UpdateCharacterVisualization(_core.TickIndex);
            }
        }

        private void ApplyInterventionsForTick(int tick)
        {
            if (_interventions == null)
            {
                return;
            }

            foreach (var command in _interventions.Where(c => c.applyTick == tick).OrderBy(c => c.priority))
            {
                if (command.commandType.Equals("ObjectPriority", StringComparison.OrdinalIgnoreCase))
                {
                    var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    marker.name = $"Intervention_{command.commandId}";
                    marker.transform.position = new Vector3(0f, 1.5f + tick * 0.2f, 0f);
                    marker.transform.SetParent(transform, false);
                }
            }
        }

        private void UpdateCharacterVisualization(int tick)
        {
            foreach (var pair in _characters)
            {
                var go = pair.Value;
                float scale = 1f + Mathf.PingPong(tick * 0.1f, 0.3f);
                go.transform.localScale = Vector3.one * scale;
            }
        }
    }
}
