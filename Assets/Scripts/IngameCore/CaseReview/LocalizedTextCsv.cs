using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectW.IngameCore.CaseReview
{
public static class LocalizedTextCsv
{
    private const string KeyHeader = "Key";

    public static string ToCsv(LocalizedTextTable table)
    {
        return table == null ? KeyHeader + Environment.NewLine : ToCsv(table.Entries);
    }

    public static string ToCsv(IReadOnlyList<LocalizedTextEntry> entries)
    {
        entries ??= Array.Empty<LocalizedTextEntry>();
        var columns = entries
            .SelectMany(entry => entry.Values ?? new List<LocalizedTextValue>())
            .Select(ToColumn)
            .Where(column => !string.IsNullOrWhiteSpace(column))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(column => column, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var builder = new StringBuilder();
        builder.Append(Escape(KeyHeader));
        foreach (var column in columns)
        {
            builder.Append(',').Append(Escape(column));
        }

        builder.AppendLine();
        foreach (var entry in entries.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append(Escape(entry.Key));
            foreach (var column in columns)
            {
                var text = (entry.Values ?? new List<LocalizedTextValue>())
                    .FirstOrDefault(value => string.Equals(ToColumn(value), column, StringComparison.OrdinalIgnoreCase))
                    ?.Text ?? "";
                builder.Append(',').Append(Escape(text));
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    public static List<LocalizedTextEntry> FromCsv(string csv)
    {
        var rows = ParseRows(csv ?? "");
        if (rows.Count == 0)
        {
            return new List<LocalizedTextEntry>();
        }

        var header = rows[0];
        if (header.Count == 0 || !string.Equals(header[0], KeyHeader, StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException("Localized text CSV must start with a Key column.");
        }

        var columns = header.Skip(1).Select(ParseColumn).ToList();
        var entries = new List<LocalizedTextEntry>();
        foreach (var row in rows.Skip(1))
        {
            if (row.Count == 0 || string.IsNullOrWhiteSpace(row[0]))
            {
                continue;
            }

            var entry = new LocalizedTextEntry { Key = row[0].Trim() };
            for (var i = 1; i < row.Count && i <= columns.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(row[i]))
                {
                    continue;
                }

                var column = columns[i - 1];
                entry.Values.Add(new LocalizedTextValue
                {
                    LanguageKey = column.LanguageKey,
                    CountryCode = column.CountryCode,
                    Text = row[i]
                });
            }

            entries.Add(entry);
        }

        return entries;
    }

    private static string ToColumn(LocalizedTextValue value)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.LanguageKey))
        {
            return "";
        }

        return string.IsNullOrWhiteSpace(value.CountryCode)
            ? value.LanguageKey.Trim()
            : $"{value.LanguageKey.Trim()}-{value.CountryCode.Trim()}";
    }

    private static LocalizedTextColumn ParseColumn(string value)
    {
        var normalized = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new FormatException("Localized text CSV contains an empty language column.");
        }

        var parts = normalized.Split(new[] { '-', '_' }, 2);
        return new LocalizedTextColumn(parts[0], parts.Length > 1 ? parts[1] : "");
    }

    private static string Escape(string value)
    {
        value ??= "";
        var requiresQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        var escaped = value.Replace("\"", "\"\"");
        return requiresQuotes ? $"\"{escaped}\"" : escaped;
    }

    private static List<List<string>> ParseRows(string csv)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < csv.Length; i++)
        {
            var ch = csv[i];
            if (inQuotes)
            {
                if (ch == '"' && i + 1 < csv.Length && csv[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                    continue;
                }

                if (ch == '"')
                {
                    inQuotes = false;
                    continue;
                }

                cell.Append(ch);
                continue;
            }

            if (ch == '"')
            {
                inQuotes = true;
                continue;
            }

            if (ch == ',')
            {
                row.Add(cell.ToString());
                cell.Clear();
                continue;
            }

            if (ch == '\n')
            {
                row.Add(TrimTrailingCarriageReturn(cell.ToString()));
                cell.Clear();
                rows.Add(row);
                row = new List<string>();
                continue;
            }

            cell.Append(ch);
        }

        row.Add(TrimTrailingCarriageReturn(cell.ToString()));
        if (row.Count > 1 || !string.IsNullOrWhiteSpace(row[0]))
        {
            rows.Add(row);
        }

        return rows;
    }

    private static string TrimTrailingCarriageReturn(string value)
    {
        return value.EndsWith("\r", StringComparison.Ordinal) ? value[..^1] : value;
    }

    private readonly struct LocalizedTextColumn
    {
        public LocalizedTextColumn(string languageKey, string countryCode)
        {
            LanguageKey = languageKey ?? "";
            CountryCode = countryCode ?? "";
        }

        public string LanguageKey { get; }
        public string CountryCode { get; }
    }
}
}
