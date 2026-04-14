using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ProjectW.IngameCore.Meta;
using UnityEngine;

namespace ProjectW.IngameCore
{
    public interface IPlaytestAnalyticsWriter
    {
        bool Append(PlaytestAnalyticsRecord record, out string errorCode);
    }

    public sealed class JsonlPlaytestAnalyticsWriter : IPlaytestAnalyticsWriter
    {
        private readonly string _rootDirectory;
        public string RootDirectory => ResolveRootDirectory();

        public JsonlPlaytestAnalyticsWriter(string rootDirectory = null)
        {
            _rootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
                ? null
                : rootDirectory;
        }

        public bool Append(PlaytestAnalyticsRecord record, out string errorCode)
        {
            if (record == null)
            {
                errorCode = "E-ANL-302";
                return false;
            }

            try
            {
                var rootDirectory = ResolveRootDirectory();
                Directory.CreateDirectory(rootDirectory);
                var filePath = Path.Combine(rootDirectory, $"analytics-{DateTime.UtcNow:yyyyMMdd}.jsonl");
                var line = JsonUtility.ToJson(record, false);
                File.AppendAllText(filePath, line + Environment.NewLine, Encoding.UTF8);
                errorCode = null;
                return true;
            }
            catch
            {
                errorCode = "E-ANL-301";
                return false;
            }
        }

        private string ResolveRootDirectory()
        {
            return string.IsNullOrWhiteSpace(_rootDirectory)
                ? Path.Combine(Application.persistentDataPath, "IngameSnapshots")
                : _rootDirectory;
        }
    }

    public sealed class PlaytestAnalyticsPersistenceService
    {
        private readonly IPlaytestAnalyticsWriter _writer;

        public PlaytestAnalyticsPersistenceService(IPlaytestAnalyticsWriter writer = null)
        {
            _writer = writer ?? new JsonlPlaytestAnalyticsWriter();
        }

        public bool TryAppend(PlaytestAnalyticsRecord record, out string errorCode)
        {
            return _writer.Append(record, out errorCode);
        }

        public PlaytestAnalyticsAggregate BuildAggregateWindow(int windowSize)
        {
            var loaded = LoadAll();
            return PlaytestAnalyticsAggregator.AggregateRecent(loaded, windowSize);
        }

        public IReadOnlyList<PlaytestAnalyticsRecord> LoadAll()
        {
            var records = new List<PlaytestAnalyticsRecord>();
            var rootDirectory = ResolveRootDirectory();
            if (!Directory.Exists(rootDirectory))
            {
                return records;
            }

            var directoryInfo = new DirectoryInfo(rootDirectory);
            var files = directoryInfo.GetFiles("analytics-*.jsonl", SearchOption.TopDirectoryOnly);
            Array.Sort(files, (left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name));
            for (int i = 0; i < files.Length; i++)
            {
                foreach (var line in File.ReadLines(files[i].FullName, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    try
                    {
                        var record = JsonUtility.FromJson<PlaytestAnalyticsRecord>(line);
                        if (record != null)
                        {
                            records.Add(record);
                        }
                    }
                    catch
                    {
                        // JSONL 로그는 skip tolerant 정책으로 읽는다.
                    }
                }
            }

            return records;
        }

        public static string BuildAggregateSummaryText(PlaytestAnalyticsAggregate aggregate)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Window:{0} Sample:{1} | K01(MedianTick):{2:0.#} | K02(Complete):{3:0.0%} | K03(TotalWipe):{4:0.0%} | K04(Retry):{5:0.0%} | K05(Churn):{6:0.0%}",
                aggregate.WindowSize,
                aggregate.SampleSize,
                aggregate.MedianTickIndex,
                aggregate.CompletionRate,
                aggregate.AnnihilationRate,
                aggregate.RetryRate,
                aggregate.ImmediateChurnRate);
        }

        private string ResolveRootDirectory()
        {
            if (_writer is JsonlPlaytestAnalyticsWriter jsonlWriter)
            {
                return jsonlWriter.RootDirectory;
            }

            return Path.Combine(Application.persistentDataPath, "IngameSnapshots");
        }
    }
}
