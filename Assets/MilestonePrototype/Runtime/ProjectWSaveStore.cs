using System;
using ProjectW.Contracts;
using UnityEngine;

namespace ProjectW.MilestonePrototype
{
    public static class ProjectWSaveStore
    {
        public const int CampaignSchema = 2;
        private static IStringStorage storage;

        public static void Configure(IStringStorage value) => storage = value;

        public static bool SaveCampaign(string key, CampaignSnapshot snapshot) =>
            Save(key, JsonUtility.ToJson(snapshot, true));

        public static bool TryLoadCampaign(string key, out CampaignSnapshot snapshot)
        {
            snapshot = Load<CampaignSnapshot>(key);
            return snapshot != null && snapshot.SchemaVersion == CampaignSchema &&
                   snapshot.Tasks != null && snapshot.Groups != null &&
                   snapshot.Crew != null && snapshot.Mail != null;
        }

        public static void Delete(string key)
        {
            try
            {
                storage?.DeleteKey(key);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"ProjectW save delete failed: {exception.Message}");
            }
        }

        private static bool Save(string key, string json)
        {
            try
            {
                if (storage == null) return false;
                storage.SetString(key, json);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"ProjectW save failed: {exception.Message}");
                return false;
            }
        }

        private static T Load<T>(string key) where T : class
        {
            try
            {
                return storage != null && storage.TryGetString(key, out string json)
                    ? JsonUtility.FromJson<T>(json)
                    : null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"ProjectW save ignored: {exception.Message}");
                return null;
            }
        }
    }
}
