namespace ProjectW.MilestonePrototype
{
    public static class GameCalendar
    {
        public const int DaysPerYear = 365;
        public const int StartMonth = 10;
        public const int StartDayOfMonth = 1;

        private static readonly int[] DaysInMonth =
        {
            31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31
        };

        public static string FormatDay(int gameDay)
        {
            if (gameDay <= 0) return "날짜 없음";

            GetDate(gameDay, out int year, out int month, out int dayOfMonth);
            string yearText = year > 1 ? $"{year}년차 " : string.Empty;
            return $"DAY {gameDay:00} · {yearText}{month}월 {dayOfMonth}일 · {Quarter(month)}분기";
        }

        public static void GetDate(int gameDay, out int year, out int month, out int dayOfMonth)
        {
            int elapsedDays = gameDay > 0 ? gameDay - 1 : 0;
            int startOffset = DaysBeforeMonth(StartMonth) + StartDayOfMonth - 1;
            year = elapsedDays / DaysPerYear + 1;
            int dayOfYear = (startOffset + elapsedDays % DaysPerYear) % DaysPerYear;

            month = 1;
            while (dayOfYear >= DaysInMonth[month - 1])
            {
                dayOfYear -= DaysInMonth[month - 1];
                month++;
            }

            dayOfMonth = dayOfYear + 1;
        }

        public static int Quarter(int month)
        {
            if (month < 1) month = 1;
            if (month > 12) month = 12;
            return (month - 1) / 3 + 1;
        }

        private static int DaysBeforeMonth(int month)
        {
            int total = 0;
            for (int index = 0; index < month - 1; index++) total += DaysInMonth[index];
            return total;
        }
    }
}
