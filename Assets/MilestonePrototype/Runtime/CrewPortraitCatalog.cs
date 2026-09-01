namespace ProjectW.MilestonePrototype
{
    public static class CrewPortraitCatalog
    {
        public const int Count = 4;
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
    }
}
