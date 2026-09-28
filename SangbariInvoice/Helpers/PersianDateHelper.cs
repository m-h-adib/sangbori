using System.Globalization;

namespace SangbariInvoice.Helpers
{
    /// <summary>
    /// Small helper around System.Globalization.PersianCalendar so the rest of the app
    /// never has to deal with Gregorian dates directly.
    /// Dates are stored/displayed as text in the format yyyy/MM/dd (e.g. 1403/05/28).
    /// </summary>
    public static class PersianDateHelper
    {
        private static readonly PersianCalendar Pc = new();

        public static readonly string[] MonthNames =
        {
            "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
            "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
        };

        /// <summary>Today's date as a Persian yyyy/MM/dd string.</summary>
        public static string Today() => ToPersianString(DateTime.Now);

        public static string ToPersianString(DateTime dt)
        {
            int y = Pc.GetYear(dt);
            int m = Pc.GetMonth(dt);
            int d = Pc.GetDayOfMonth(dt);
            return $"{y:0000}/{m:00}/{d:00}";
        }

        public static (int Year, int Month, int Day) TodayParts()
        {
            var now = DateTime.Now;
            return (Pc.GetYear(now), Pc.GetMonth(now), Pc.GetDayOfMonth(now));
        }

        /// <summary>Number of days in the given Persian year/month (handles leap years).</summary>
        public static int DaysInMonth(int year, int month) => Pc.GetDaysInMonth(year, month);

        /// <summary>
        /// Gregorian weekday (0=Sunday..6=Saturday) of the 1st day of the given Persian
        /// year/month - used to lay out the calendar grid with the correct offset.
        /// The Iranian week starts on Saturday, so we remap to 0=Saturday..6=Friday.
        /// </summary>
        public static int FirstDayOfMonthWeekIndex(int year, int month)
        {
            var dt = Pc.ToDateTime(year, month, 1, 0, 0, 0, 0);
            int dow = (int)dt.DayOfWeek; // 0=Sunday..6=Saturday
            return (dow + 1) % 7; // 0=Saturday..6=Friday
        }

        /// <summary>Parses "yyyy/MM/dd" into its numeric parts. Returns false if invalid.</summary>
        public static bool TryParse(string text, out int year, out int month, out int day)
        {
            year = month = day = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var parts = text.Split('/');
            if (parts.Length != 3) return false;

            if (!int.TryParse(parts[0], out year)) return false;
            if (!int.TryParse(parts[1], out month)) return false;
            if (!int.TryParse(parts[2], out day)) return false;

            if (year is < 1300 or > 1500) return false;
            if (month is < 1 or > 12) return false;
            if (day is < 1 or > 31) return false;

            return true;
        }

        public static bool IsValid(string text) => TryParse(text, out _, out _, out _);
    }
}
