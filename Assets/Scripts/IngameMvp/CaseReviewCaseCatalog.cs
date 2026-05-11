using System.Collections.Generic;
using UnityEngine;

namespace ProjectW.IngameMvp
{
    [CreateAssetMenu(menuName = "ProjectW/Case Review/Case Catalog", fileName = "CaseReviewCaseCatalog")]
    public sealed class CaseReviewCaseCatalog : ScriptableObject
    {
        [SerializeField] private List<CaseDefinition> cases = new List<CaseDefinition>();

        public IReadOnlyList<CaseDefinition> Cases => cases;
    }
}
