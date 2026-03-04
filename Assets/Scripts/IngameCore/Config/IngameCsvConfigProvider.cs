using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ProjectW.IngameCore.Contracts;
using UnityEngine;

namespace ProjectW.IngameCore.Config
{
    public static class CsvErrorCodes
    {
        public const string HeaderMissing = "E-CSV-001";
        public const string RequiredColumnMissing = "E-CSV-002";
        public const string TypeMismatch = "E-CSV-003";
        public const string RequiredRowMissing = "E-CSV-004";
    }

    public sealed class CsvValidationException : Exception
    {
        public string ErrorCode { get; }

        public CsvValidationException(string errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }
    }

    public sealed class StreamingAssetsCsvConfigProvider : ICsvConfigProvider
    {
        private readonly string rootPath;

        public StreamingAssetsCsvConfigProvider(string configRootPath = null)
        {
            rootPath = string.IsNullOrWhiteSpace(configRootPath)
                ? Path.Combine(Application.streamingAssetsPath, "IngameConfig")
                : configRootPath;
        }

        public SessionConfig LoadSessionConfig()
        {
            var rows = ParseFile(Path.Combine(rootPath, "SessionConfig.csv"));
            var required = new[] { "session_id", "tick_seconds", "max_decision_retry", "max_persist_retry", "persist_retry_backoff_ms" };
            EnsureSchema(rows, required);
            if (rows.Count < 2)
            {
                throw new CsvValidationException(CsvErrorCodes.RequiredRowMissing, "SessionConfig has no data rows.");
            }

            var row = ToMap(rows[0], rows[1]);
            return new SessionConfig
            {
                SessionId = RequiredString(row, "session_id"),
                TickSeconds = RequiredFloat(row, "tick_seconds"),
                MaxDecisionRetry = RequiredInt(row, "max_decision_retry"),
                MaxPersistRetry = RequiredInt(row, "max_persist_retry"),
                PersistRetryBackoffMs = RequiredInt(row, "persist_retry_backoff_ms")
            };
        }

        public IReadOnlyList<StateTransitionRuleRow> LoadStateTransitionRules()
        {
            var rows = ParseFile(Path.Combine(rootPath, "StateTransitionRules.csv"));
            var required = new[] { "from_state", "to_state", "entry_condition", "exit_condition", "guard", "priority", "enabled" };
            EnsureSchema(rows, required);
            return ParseMany(rows, map => new StateTransitionRuleRow
            {
                FromState = RequiredString(map, "from_state"),
                ToState = RequiredString(map, "to_state"),
                EntryCondition = RequiredString(map, "entry_condition"),
                ExitCondition = RequiredString(map, "exit_condition"),
                Guard = RequiredString(map, "guard"),
                Priority = RequiredInt(map, "priority"),
                Enabled = RequiredBool(map, "enabled")
            });
        }

        public IReadOnlyList<CharacterProfileRow> LoadCharacterProfiles()
        {
            var rows = ParseFile(Path.Combine(rootPath, "CharacterProfiles.csv"));
            var required = new[] { "character_id", "trait_weights_json", "stress", "health", "trauma_level", "enabled" };
            EnsureSchema(rows, required);
            return ParseMany(rows, map => new CharacterProfileRow
            {
                CharacterId = RequiredString(map, "character_id"),
                TraitWeightsJson = RequiredString(map, "trait_weights_json"),
                Stress = RequiredFloat(map, "stress"),
                Health = RequiredFloat(map, "health"),
                TraumaLevel = RequiredInt(map, "trauma_level"),
                Enabled = RequiredBool(map, "enabled")
            });
        }

        public IReadOnlyList<InterventionCommandRow> LoadInterventionRules()
        {
            var rows = ParseFile(Path.Combine(rootPath, "InterventionCommands.csv"));
            var required = new[] { "command_id", "issued_tick", "apply_tick", "command_type", "target_scope", "payload_json", "priority", "supersedes_command_id" };
            EnsureSchema(rows, required);
            return ParseMany(rows, map => new InterventionCommandRow
            {
                CommandId = RequiredString(map, "command_id"),
                IssuedTick = RequiredInt(map, "issued_tick"),
                ApplyTick = RequiredInt(map, "apply_tick"),
                CommandType = RequiredString(map, "command_type"),
                TargetScope = RequiredString(map, "target_scope"),
                PayloadJson = RequiredString(map, "payload_json"),
                Priority = RequiredInt(map, "priority"),
                SupersedesCommandId = OptionalString(map, "supersedes_command_id")
            });
        }

        public IReadOnlyList<TerminationRuleRow> LoadTerminationRules()
        {
            var rows = ParseFile(Path.Combine(rootPath, "TerminationRules.csv"));
            var required = new[] { "rule_id", "condition_type", "threshold_expr", "result_code", "enabled", "priority" };
            EnsureSchema(rows, required);
            return ParseMany(rows, map => new TerminationRuleRow
            {
                RuleId = RequiredString(map, "rule_id"),
                ConditionType = RequiredString(map, "condition_type"),
                ThresholdExpr = RequiredString(map, "threshold_expr"),
                ResultCode = RequiredString(map, "result_code"),
                Enabled = RequiredBool(map, "enabled"),
                Priority = RequiredInt(map, "priority")
            });
        }

        private static IReadOnlyList<T> ParseMany<T>(IReadOnlyList<string[]> rows, Func<Dictionary<string, string>, T> parser)
        {
            if (rows.Count < 2)
            {
                throw new CsvValidationException(CsvErrorCodes.RequiredRowMissing, "CSV has no data rows.");
            }

            var list = new List<T>();
            for (int i = 1; i < rows.Count; i++)
            {
                list.Add(parser(ToMap(rows[0], rows[i])));
            }

            return list;
        }

        private static IReadOnlyList<string[]> ParseFile(string path)
        {
            if (!File.Exists(path))
            {
                throw new CsvValidationException(CsvErrorCodes.RequiredRowMissing, "CSV file missing: " + path);
            }

            var text = File.ReadAllText(path, new UTF8Encoding(false, true));
            var lines = text.Replace("\r\n", "\n").Split('\n');
            var rows = new List<string[]>();
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                rows.Add(ParseCsvLine(lines[i]));
            }

            return rows;
        }

        private static string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            var builder = new StringBuilder();
            bool inQuote = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuote && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        builder.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuote = !inQuote;
                    }
                }
                else if (c == ',' && !inQuote)
                {
                    result.Add(builder.ToString().Trim());
                    builder.Length = 0;
                }
                else
                {
                    builder.Append(c);
                }
            }

            result.Add(builder.ToString().Trim());
            return result.ToArray();
        }

        private static void EnsureSchema(IReadOnlyList<string[]> rows, IReadOnlyList<string> requiredColumns)
        {
            if (rows.Count == 0)
            {
                throw new CsvValidationException(CsvErrorCodes.HeaderMissing, "CSV has no header row.");
            }

            var headers = rows[0];
            if (headers.Length == 0)
            {
                throw new CsvValidationException(CsvErrorCodes.HeaderMissing, "CSV header is empty.");
            }

            var names = new HashSet<string>(headers, StringComparer.OrdinalIgnoreCase);
            foreach (var required in requiredColumns)
            {
                if (!names.Contains(required))
                {
                    throw new CsvValidationException(CsvErrorCodes.RequiredColumnMissing, "Missing column: " + required);
                }
            }
        }

        private static Dictionary<string, string> ToMap(string[] header, string[] row)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < header.Length; i++)
            {
                var value = i < row.Length ? row[i] : string.Empty;
                map[header[i]] = value;
            }

            return map;
        }

        private static string RequiredString(IReadOnlyDictionary<string, string> map, string key)
        {
            if (!map.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                throw new CsvValidationException(CsvErrorCodes.RequiredRowMissing, "Missing required value: " + key);
            }

            return value.Trim();
        }

        private static string OptionalString(IReadOnlyDictionary<string, string> map, string key)
        {
            return map.TryGetValue(key, out var value) ? (value ?? string.Empty).Trim() : string.Empty;
        }

        private static int RequiredInt(IReadOnlyDictionary<string, string> map, string key)
        {
            var value = RequiredString(map, key);
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                throw new CsvValidationException(CsvErrorCodes.TypeMismatch, "Expected int for " + key);
            }

            return parsed;
        }

        private static float RequiredFloat(IReadOnlyDictionary<string, string> map, string key)
        {
            var value = RequiredString(map, key);
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                throw new CsvValidationException(CsvErrorCodes.TypeMismatch, "Expected float for " + key);
            }

            return parsed;
        }

        private static bool RequiredBool(IReadOnlyDictionary<string, string> map, string key)
        {
            var value = RequiredString(map, key);
            if (!bool.TryParse(value, out var parsed))
            {
                throw new CsvValidationException(CsvErrorCodes.TypeMismatch, "Expected bool for " + key);
            }

            return parsed;
        }
    }

    public sealed class IngameCsvBootstrapResult
    {
        public bool Success { get; }
        public bool IsReadOnlyRecoveryMode { get; }
        public string ErrorCode { get; }
        public IngameCsvConfigSet ConfigSet { get; }

        public IngameCsvBootstrapResult(bool success, bool isReadOnlyRecoveryMode, string errorCode, IngameCsvConfigSet configSet)
        {
            Success = success;
            IsReadOnlyRecoveryMode = isReadOnlyRecoveryMode;
            ErrorCode = errorCode;
            ConfigSet = configSet;
        }
    }

    public interface ISnapshotRecoveryProbe
    {
        bool HasSnapshot(string sessionId);
    }

    public sealed class PersistentSnapshotRecoveryProbe : ISnapshotRecoveryProbe
    {
        public bool HasSnapshot(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return false;
            }

            var snapshotPath = Path.Combine(Application.persistentDataPath, "IngameSnapshots", sessionId + ".json");
            return File.Exists(snapshotPath);
        }
    }

    public sealed class IngameCsvBootstrapService
    {
        private readonly ICsvConfigProvider configProvider;
        private readonly ISnapshotRecoveryProbe snapshotRecoveryProbe;

        public IngameCsvBootstrapService(ICsvConfigProvider configProvider, ISnapshotRecoveryProbe snapshotRecoveryProbe)
        {
            this.configProvider = configProvider;
            this.snapshotRecoveryProbe = snapshotRecoveryProbe;
        }

        public IngameCsvBootstrapResult Load()
        {
            try
            {
                var configSet = new IngameCsvConfigSet
                {
                    SessionConfig = configProvider.LoadSessionConfig(),
                    StateTransitionRules = configProvider.LoadStateTransitionRules(),
                    CharacterProfiles = configProvider.LoadCharacterProfiles(),
                    TerminationRules = configProvider.LoadTerminationRules(),
                    InterventionCommands = configProvider.LoadInterventionRules()
                };

                return new IngameCsvBootstrapResult(true, false, string.Empty, configSet);
            }
            catch (CsvValidationException ex)
            {
                var sessionId = "default";
                bool hasSnapshot = snapshotRecoveryProbe != null && snapshotRecoveryProbe.HasSnapshot(sessionId);
                return new IngameCsvBootstrapResult(false, hasSnapshot, ex.ErrorCode, null);
            }
        }
    }
}
