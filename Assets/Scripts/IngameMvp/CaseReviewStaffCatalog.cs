using System.Collections.Generic;
using UnityEngine;

namespace ProjectW.IngameMvp
{
    [CreateAssetMenu(menuName = "ProjectW/Case Review/Staff Catalog", fileName = "CaseReviewStaffCatalog")]
    public sealed class CaseReviewStaffCatalog : ScriptableObject
    {
        [SerializeField] private List<PersonnelDefinition> staff = new List<PersonnelDefinition>();

        public IReadOnlyList<PersonnelDefinition> Staff => staff;
    }
}
