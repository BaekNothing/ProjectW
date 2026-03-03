using System;
using UnityEngine;

namespace ProjectW.IngameMvp
{
    public sealed class RoutineInspectableWorldObject : MonoBehaviour
    {
        [SerializeField] private string displayName;
        [SerializeField] private string zoneId;
        [SerializeField] private string primaryTag;
        [SerializeField] private string detail;

        public string DisplayName => displayName;
        public string ZoneId => zoneId;
        public string PrimaryTag => primaryTag;
        public string Detail => detail;

        public void Configure(string objectDisplayName, string objectZoneId, string objectPrimaryTag, string objectDetail)
        {
            displayName = string.IsNullOrWhiteSpace(objectDisplayName) ? gameObject.name : objectDisplayName.Trim();
            zoneId = string.IsNullOrWhiteSpace(objectZoneId) ? "unknown" : objectZoneId.Trim();
            primaryTag = string.IsNullOrWhiteSpace(objectPrimaryTag) ? "object" : objectPrimaryTag.Trim();
            detail = string.IsNullOrWhiteSpace(objectDetail) ? string.Empty : objectDetail.Trim();
        }

        public string BuildSummary()
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "name={0} | tag={1} | zone={2}{3}",
                DisplayName,
                PrimaryTag,
                ZoneId,
                string.IsNullOrWhiteSpace(Detail) ? string.Empty : " | " + Detail);
        }
    }
}
