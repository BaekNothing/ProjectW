using System;
using UnityEngine;

namespace ProjectW.MilestonePrototype
{
    public static class ProjectWSaveStore
    {
        public const int CampaignSchema = 1;
        public const int DesktopSchema = 1;

        public static bool SaveCampaign(string key, CampaignSnapshot snapshot) =>
            Save(key, JsonUtility.ToJson(snapshot, true));

        public static bool SaveDesktop(string key, DesktopSnapshot snapshot) =>
            Save(key, JsonUtility.ToJson(snapshot, true));

        public static bool TryLoadCampaign(string key, out CampaignSnapshot snapshot)
        {
            snapshot = Load<CampaignSnapshot>(key);
            return snapshot != null && snapshot.SchemaVersion == CampaignSchema &&
                   snapshot.Tasks != null && snapshot.Crew != null && snapshot.Mail != null;
        }

        public static bool TryLoadDesktop(string key, out DesktopSnapshot snapshot)
        {
            snapshot = Load<DesktopSnapshot>(key);
            return snapshot != null && snapshot.SchemaVersion == DesktopSchema && snapshot.Windows != null;
        }

        public static void Delete(string key)
        {
            try
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
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
                PlayerPrefs.SetString(key, json);
                PlayerPrefs.Save();
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
                return PlayerPrefs.HasKey(key) ? JsonUtility.FromJson<T>(PlayerPrefs.GetString(key)) : null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"ProjectW save ignored: {exception.Message}");
                return null;
            }
        }
    }
}
