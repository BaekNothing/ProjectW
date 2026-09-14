namespace ProjectW.MilestonePrototype
{
    public static class CrewPortraitCatalog
    {
        // All registered layers and complete fallbacks share a square canvas.
        public static UnityEngine.Rect FitPortraitRect(UnityEngine.Rect area)
        {
            float size = System.Math.Max(0f, System.Math.Min(area.width, area.height));
            return new UnityEngine.Rect(area.x + (area.width - size) * .5f,
                area.y + (area.height - size) * .5f, size, size);
        }

        public const int Count = 4;
        public const int ConditionCount = 4;
        public const int ConditionAssetCount = Count * ConditionCount;
        public const string HanTech = "portraits/crew/crew-han-tech";
        public const string YoonAnalysis = "portraits/crew/crew-yoon-analysis";
        public const string MiManagement = "portraits/crew/crew-mi-management";
        public const string KangAdaptation = "portraits/crew/crew-kang-adaptation";

        public static string ExpectedAddressForSlot(int index)
        {
            switch (index)
            {
                case 0: return HanTech;
                case 1: return YoonAnalysis;
                case 2: return MiManagement;
                case 3: return KangAdaptation;
                default: return string.Empty;
            }
        }

        public static string ExpectedConditionAddressForAsset(int index)
        {
            if (index < 0 || index >= ConditionAssetCount) return string.Empty;
            return ExpectedAddressForSlot(index % Count) + "/condition-" + (index / Count);
        }

        public static int ConditionAssetForCrew(int crewIndex, int fatigue, int injuryDays)
        {
            if (crewIndex < 0 || crewIndex >= Count) return -1;
            return DarkCircleVariant(fatigue, injuryDays) * Count + crewIndex;
        }

        public static int DarkCircleVariant(int fatigue, int injuryDays)
        {
            if (injuryDays > 0 || fatigue >= 80) return 3;
            if (fatigue >= 55) return 2;
            if (fatigue >= 30) return 1;
            return 0;
        }
    }
}
