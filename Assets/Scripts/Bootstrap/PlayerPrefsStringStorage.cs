using ProjectW.Contracts;
using UnityEngine;

namespace ProjectW.Bootstrap
{
    public sealed class PlayerPrefsStringStorage : IStringStorage
    {
        public bool TryGetString(string key, out string value)
        {
            if (!PlayerPrefs.HasKey(key))
            {
                value = null;
                return false;
            }

            value = PlayerPrefs.GetString(key);
            return true;
        }

        public void SetString(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
        }

        public void DeleteKey(string key)
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
