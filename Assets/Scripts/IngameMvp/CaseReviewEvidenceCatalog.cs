using System.Collections.Generic;
using UnityEngine;

namespace ProjectW.IngameMvp
{
    [CreateAssetMenu(menuName = "ProjectW/Case Review/Evidence Catalog", fileName = "CaseReviewEvidenceCatalog")]
    public sealed class CaseReviewEvidenceCatalog : ScriptableObject
    {
        [SerializeField] private List<TruthFrameDefinition> truthFrames = new List<TruthFrameDefinition>();
        [SerializeField] private List<VisibleLogDefinition> visibleLogs = new List<VisibleLogDefinition>();

        public IReadOnlyList<TruthFrameDefinition> TruthFrames => truthFrames;
        public IReadOnlyList<VisibleLogDefinition> VisibleLogs => visibleLogs;
    }
}
