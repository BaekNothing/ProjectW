using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ProjectW.IngameCore
{
    public enum PersistenceResolutionState
    {
        Persisted,
        PersistenceRetry,
        SafeHalt
    }

    public sealed class SessionConfig
    {
        public int MaxRetry { get; }
        public int BackoffMs { get; }

        public SessionConfig(int maxRetry, int backoffMs)
        {
            MaxRetry = Math.Max(0, maxRetry);
            BackoffMs = Math.Max(0, backoffMs);
        }
    }

    public sealed class SessionSnapshotDto
    {
        public string SessionId { get; set; }
        public int TickIndex { get; set; }
        public string LoopState { get; set; }
        public List<CharacterSnapshotDto> CharactersSnapshot { get; set; } = new List<CharacterSnapshotDto>();
        public List<EventLogEntryDto> EventLog { get; set; } = new List<EventLogEntryDto>();
        public string TerminationResultCode { get; set; }
        public int LastAppliedTick { get; set; }
    }

    public sealed class CharacterSnapshotDto
    {
        public string CharacterId { get; set; }
        public string SerializedState { get; set; }
    }

    public sealed class EventLogEntryDto
    {
        public string EventCode { get; set; }
        public string Message { get; set; }
        public string TimestampUtc { get; set; }
    }

    public readonly struct SnapshotWriteResult
    {
        public bool Success { get; }
        public string ErrorCode { get; }

        public SnapshotWriteResult(bool success, string errorCode = null)
        {
            Success = success;
            ErrorCode = errorCode;
        }
    }

    public interface ISnapshotWriter
    {
        SnapshotWriteResult PersistSnapshot(SessionSnapshotDto snapshot);
    }

    public sealed class SnapshotPersistenceResult
    {
        public bool Success { get; }
        public int AttemptCount { get; }
        public PersistenceResolutionState State { get; }
        public string ErrorCode { get; }
        public IReadOnlyList<string> LoggedErrorCodes { get; }

        public SnapshotPersistenceResult(
            bool success,
            int attemptCount,
            PersistenceResolutionState state,
            string errorCode,
            IReadOnlyList<string> loggedErrorCodes)
        {
            Success = success;
            AttemptCount = attemptCount;
            State = state;
            ErrorCode = errorCode;
            LoggedErrorCodes = loggedErrorCodes;
        }
    }


    public sealed class JsonSnapshotWriter : ISnapshotWriter
    {
        private readonly string rootDirectory;
        private readonly int maxSnapshotsPerSession;

        public JsonSnapshotWriter(string rootDirectory = null, int maxSnapshotsPerSession = 3)
        {
            this.rootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
                ? Path.Combine(Application.persistentDataPath, "IngameSnapshots")
                : rootDirectory;
            this.maxSnapshotsPerSession = Math.Max(1, maxSnapshotsPerSession);
        }

        public SnapshotWriteResult PersistSnapshot(SessionSnapshotDto snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.SessionId))
            {
                return new SnapshotWriteResult(false, "E-PST-302");
            }

            try
            {
                var sessionDirectory = Path.Combine(rootDirectory, snapshot.SessionId.Trim());
                Directory.CreateDirectory(sessionDirectory);
                var fileName = $"tick_{snapshot.TickIndex:D8}_{DateTime.UtcNow:yyyyMMddTHHmmssfff}.json";
                var filePath = Path.Combine(sessionDirectory, fileName);
                var json = JsonUtility.ToJson(snapshot, true);
                File.WriteAllText(filePath, json);

                var files = new DirectoryInfo(sessionDirectory)
                    .GetFiles("tick_*.json", SearchOption.TopDirectoryOnly);
                Array.Sort(files, (left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name));

                var deleteCount = Math.Max(0, files.Length - maxSnapshotsPerSession);
                for (int i = 0; i < deleteCount; i++)
                {
                    files[i].Delete();
                }

                return new SnapshotWriteResult(true);
            }
            catch
            {
                return new SnapshotWriteResult(false, "E-PST-301");
            }
        }
    }
    public sealed class SnapshotPersistenceService
    {
        private readonly ISnapshotWriter snapshotWriter;
        private readonly Action<int> backoffDelayAction;

        public SnapshotPersistenceService(ISnapshotWriter snapshotWriter, Action<int> backoffDelayAction = null)
        {
            this.snapshotWriter = snapshotWriter ?? throw new ArgumentNullException(nameof(snapshotWriter));
            this.backoffDelayAction = backoffDelayAction ?? (_ => { });
        }

        public SnapshotPersistenceResult PersistWithRetry(SessionSnapshotDto snapshot, SessionConfig config)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            var loggedErrors = new List<string>();
            int maxAttempts = 1 + config.MaxRetry;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var writeResult = snapshotWriter.PersistSnapshot(snapshot);
                if (writeResult.Success)
                {
                    var state = attempt == 1 ? PersistenceResolutionState.Persisted : PersistenceResolutionState.PersistenceRetry;
                    return new SnapshotPersistenceResult(true, attempt, state, null, loggedErrors);
                }

                loggedErrors.Add(string.IsNullOrEmpty(writeResult.ErrorCode) ? "E-PST-301" : writeResult.ErrorCode);
                if (attempt >= maxAttempts)
                {
                    loggedErrors.Add("E-PST-399");
                    return new SnapshotPersistenceResult(false, attempt, PersistenceResolutionState.SafeHalt, "E-PST-399", loggedErrors);
                }

                backoffDelayAction(config.BackoffMs);
            }

            loggedErrors.Add("E-PST-399");
            return new SnapshotPersistenceResult(false, maxAttempts, PersistenceResolutionState.SafeHalt, "E-PST-399", loggedErrors);
        }
    }
}
