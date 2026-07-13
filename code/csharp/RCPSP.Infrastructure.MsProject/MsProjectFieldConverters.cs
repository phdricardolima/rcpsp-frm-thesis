using System;

namespace RCPSP.Infrastructure.MsProject
{
    internal static class MsProjectFieldConverters
    {
        public static bool SafeBool(object value)
        {
            try
            {
                if (value == null)
                    return false;

                if (value is bool b)
                    return b;

                string text = value.ToString().Trim();

                if (string.Equals(text, "True", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (string.Equals(text, "False", StringComparison.OrdinalIgnoreCase))
                    return false;

                if (int.TryParse(text, out int number))
                    return number != 0;

                return Convert.ToBoolean(value);
            }
            catch
            {
                return false;
            }
        }

        public static DateTime? SafeDate(object value)
        {
            try
            {
                if (value == null)
                    return null;

                string text = value.ToString().Trim();

                if (string.IsNullOrWhiteSpace(text) ||
                    string.Equals(text, "NA", StringComparison.OrdinalIgnoreCase))
                    return null;

                if (DateTime.TryParse(text, out DateTime parsed))
                    return parsed;
            }
            catch
            {
            }

            return null;
        }

        public static int SafeDurationDays(object durationValue, double minutesPerDay)
        {
            try
            {
                if (durationValue == null)
                    return 0;

                if (minutesPerDay <= 0)
                    minutesPerDay = 480.0;

                double minutes = Convert.ToDouble(durationValue);
                int days = (int)Math.Round(minutes / minutesPerDay, MidpointRounding.AwayFromZero);
                return Math.Max(0, days);
            }
            catch
            {
                return 0;
            }
        }

        public static double SafeDouble(object value, double fallback = 0.0)
        {
            try
            {
                return value == null ? fallback : Convert.ToDouble(value);
            }
            catch
            {
                return fallback;
            }
        }
    }
}
