using System;
using System.IO;
using UnityEngine;

namespace ProjectW.MilestonePrototype
{
    public static class ProjectWSaveStore
    {
        public const int CampaignSchema = 1;
        public const int DesktopSchema = 1;

        public static bool SaveCampaign(string path, CampaignSnapshot snapshot) =>
            Save(path, JsonUtility.ToJson(snapshot, true));

        public static bool SaveDesktop(string path, DesktopSnapshot snapshot) =>
            Save(path, JsonUtility.ToJson(snapshot, true));

        public static bool TryLoadCampaign(string path, out CampaignSnapshot snapshot)
        {
            snapshot = Load<CampaignSnapshot>(path);
            return snapshot != null && snapshot.SchemaVersion == CampaignSchema &&
                   snapshot.Tasks != null && snapshot.Crew != null && snapshot.Mail != null;
        }

        public static bool TryLoadDesktop(string path, out DesktopSnapshot snapshot)
        {
            snapshot = Load<DesktopSnapshot>(path);
            return snapshot != null && snapshot.SchemaVersion == DesktopSchema && snapshot.Windows != null;
        }

        public static void Delete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"ProjectW save delete failed: {exception.Message}");
            }
        }

        private static bool Save(string path, string json)
        {
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                string temporary = path + ".tmp";
                File.WriteAllText(temporary, json);
                if (File.Exists(path)) File.Delete(path);
                File.Move(temporary, path);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"ProjectW save failed: {exception.Message}");
                return false;
            }
        }

        private static T Load<T>(string path) where T : class
        {
            try
            {
                return File.Exists(path) ? JsonUtility.FromJson<T>(File.ReadAllText(path)) : null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"ProjectW save ignored: {exception.Message}");
                return null;
            }
        }
    }
}
