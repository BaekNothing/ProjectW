using System;
using UnityEngine;

namespace ProjectW.IngameMvp
{
    public sealed class RoutineZoneAnchor : MonoBehaviour
    {
        [SerializeField] private string zoneId = "zone.default";
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private Collider boundary3D;
        [SerializeField] private Collider2D boundary2D;

        public string ZoneId => zoneId;
        public Vector3 Position => transform.position;

        public bool HasTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || tags == null)
            {
                return false;
            }

            for (int i = 0; i < tags.Length; i++)
            {
                if (string.Equals(tags[i], tag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public bool Contains(Vector3 worldPosition)
        {
            if (boundary2D != null)
            {
                return boundary2D.OverlapPoint(worldPosition);
            }

            if (boundary3D != null)
            {
                return boundary3D.bounds.Contains(worldPosition);
            }

            return false;
        }

        public void SetZoneId(string value)
        {
            zoneId = value;
        }

        public void SetTags(params string[] value)
        {
            tags = value ?? Array.Empty<string>();
        }

        private void Reset()
        {
            TryAutoBindBoundary();
        }

        private void OnValidate()
        {
            TryAutoBindBoundary();
        }

        private void TryAutoBindBoundary()
        {
            if (boundary2D == null)
            {
                boundary2D = GetComponent<Collider2D>();
            }

            if (boundary3D == null)
            {
                boundary3D = GetComponent<Collider>();
            }
        }
    }
}
