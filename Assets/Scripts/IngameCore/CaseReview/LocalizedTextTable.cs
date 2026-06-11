using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectW.IngameCore.CaseReview
{
[CreateAssetMenu(menuName = "ProjectW/Case Review/Localized Text Table", fileName = "LocalizedTextTable")]
public sealed class LocalizedTextTable : ScriptableObject, ILocalizedTextSource
{
    [SerializeField] private string tableId = "";
    [SerializeField] private string defaultLanguageKey = "ko";
    [SerializeField] private string defaultCountryCode = "KR";
    [SerializeField] private List<LocalizedTextEntry> entries = new();

    public string TableId => tableId;
    public string DefaultLanguageKey => defaultLanguageKey;
    public string DefaultCountryCode => defaultCountryCode;
    public IReadOnlyList<LocalizedTextEntry> Entries => entries;

    public void ReplaceEntries(IEnumerable<LocalizedTextEntry> replacement)
    {
        entries = (replacement ?? Enumerable.Empty<LocalizedTextEntry>()).ToList();
    }

    public string GetText(string key, string languageKey, string countryCode = "")
    {
        return TryGetText(key, languageKey, countryCode, out var text) ? text : key ?? "";
    }

    public bool TryGetText(string key, string languageKey, string countryCode, out string text)
    {
        text = "";
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (LocalizedTextRuntimeOverrides.TryGetText(
                key,
                languageKey,
                countryCode,
                defaultLanguageKey,
                defaultCountryCode,
                out text))
        {
            return true;
        }

        var entry = entries.FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
        {
            return false;
        }

        var requestedLanguage = Normalize(languageKey, defaultLanguageKey);
        var requestedCountry = Normalize(countryCode, defaultCountryCode);
        var defaultLanguage = Normalize(defaultLanguageKey, "ko");
        var defaultCountry = Normalize(defaultCountryCode, "KR");

        var localized = FindValue(entry.Values, requestedLanguage, requestedCountry)
            ?? FindLanguageOnly(entry.Values, requestedLanguage)
            ?? FindValue(entry.Values, defaultLanguage, defaultCountry)
            ?? FindLanguageOnly(entry.Values, defaultLanguage)
            ?? entry.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v.Text));

        if (localized == null || string.IsNullOrWhiteSpace(localized.Text))
        {
            return false;
        }

        text = localized.Text;
        return true;
    }

    private static LocalizedTextValue FindValue(IEnumerable<LocalizedTextValue> values, string languageKey, string countryCode)
    {
        return values.FirstOrDefault(v =>
            string.Equals(v.LanguageKey, languageKey, StringComparison.OrdinalIgnoreCase)
            && string.Equals(v.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase));
    }

    private static LocalizedTextValue FindLanguageOnly(IEnumerable<LocalizedTextValue> values, string languageKey)
    {
        return values.FirstOrDefault(v =>
            string.Equals(v.LanguageKey, languageKey, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(v.Text));
    }

    private static string Normalize(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}

[Serializable]
public sealed class LocalizedTextEntry
{
    public string Key = "";
    public List<LocalizedTextValue> Values = new();
}

[Serializable]
public sealed class LocalizedTextValue
{
    public string LanguageKey = "ko";
    public string CountryCode = "KR";
    [TextArea(2, 6)] public string Text = "";
}
}
