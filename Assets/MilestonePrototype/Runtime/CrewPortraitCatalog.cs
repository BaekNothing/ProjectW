namespace ProjectW.MilestonePrototype
{
    public static class CrewPortraitCatalog
    {
        public const int Count = 4;
        public const int ModularAssetCount = 25;
        public const int BodyOffset = 0;
        public const int FaceBaseIndex = 4;
        public const int EyesOffset = 5;
        public const int BrowsOffset = 9;
        public const int MouthOffset = 13;
        public const int HairOffset = 17;
        public const int DarkCirclesOffset = 21;
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

        public static string ExpectedModularAddressForAsset(int index)
        {
            switch (index)
            {
                case 0: return "portraits/crew/modular/body-01-tech";
                case 1: return "portraits/crew/modular/body-02-analysis";
                case 2: return "portraits/crew/modular/body-03-management";
                case 3: return "portraits/crew/modular/body-04-adaptation";
                case 4: return "portraits/crew/modular/face-base";
                case 5: return "portraits/crew/modular/eyes-01-focused";
                case 6: return "portraits/crew/modular/eyes-02-friendly";
                case 7: return "portraits/crew/modular/eyes-03-decisive";
                case 8: return "portraits/crew/modular/eyes-04-calm";
                case 9: return "portraits/crew/modular/brow-01-straight";
                case 10: return "portraits/crew/modular/brow-02-soft-arch";
                case 11: return "portraits/crew/modular/brow-03-bold";
                case 12: return "portraits/crew/modular/brow-04-calm";
                case 13: return "portraits/crew/modular/mouth-01-neutral";
                case 14: return "portraits/crew/modular/mouth-02-smile";
                case 15: return "portraits/crew/modular/mouth-03-determined";
                case 16: return "portraits/crew/modular/mouth-04-concerned";
                case 17: return "portraits/crew/modular/hair-01-asym-bob";
                case 18: return "portraits/crew/modular/hair-02-low-bun";
                case 19: return "portraits/crew/modular/hair-03-tousled";
                case 20: return "portraits/crew/modular/hair-04-side-part";
                case 21: return "portraits/crew/modular/dark-00-none";
                case 22: return "portraits/crew/modular/dark-01-fatigue";
                case 23: return "portraits/crew/modular/dark-02-overwork";
                case 24: return "portraits/crew/modular/dark-03-illness";
                default: return string.Empty;
            }
        }

        public static int EyesVariantForCrew(int crewIndex)
        {
            switch (crewIndex)
            {
                case 0: return 2;
                case 1: return 0;
                case 2: return 1;
                case 3: return 3;
                default: return 0;
            }
        }

        public static int BrowsVariantForCrew(int crewIndex)
        {
            switch (crewIndex)
            {
                case 0: return 2;
                case 1: return 0;
                case 2: return 1;
                case 3: return 3;
                default: return 0;
            }
        }

        public static int MouthVariantForCrew(int crewIndex)
        {
            switch (crewIndex)
            {
                case 0: return 2;
                case 1: return 0;
                case 2: return 1;
                case 3: return 3;
                default: return 0;
            }
        }

        public static int HairVariantForCrew(int crewIndex)
        {
            switch (crewIndex)
            {
                case 0: return 2;
                case 1: return 3;
                case 2: return 1;
                case 3: return 0;
                default: return 0;
            }
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
