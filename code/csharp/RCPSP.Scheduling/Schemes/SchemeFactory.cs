using System;

namespace RCPSP.Scheduling.Schemes
{
    public static class SchemeFactory
    {
        public static IScheduleScheme Create(string schemeName, string directionName)
        {
            string scheme = Normalize(schemeName, "SERIAL");
            string direction = Normalize(directionName, "FORWARD");

            switch (scheme)
            {
                case "SERIAL":
                    switch (direction)
                    {
                        case "FORWARD":
                            return new SerialForwardScheme();
                        case "BACKWARD":
                            return new SerialBackwardScheme();
                    }
                    break;

                case "PARALLEL":
                    switch (direction)
                    {
                        case "FORWARD":
                            return new ParallelForwardScheme();
                        case "BACKWARD":
                            return new ParallelBackwardScheme();
                    }
                    break;
            }

            throw new NotSupportedException(
                "Unsupported scheme/direction combination: " + schemeName + " / " + directionName);
        }

        private static string Normalize(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Trim().ToUpperInvariant();
        }
    }
}
