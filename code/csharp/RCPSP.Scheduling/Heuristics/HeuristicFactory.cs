using System;

namespace RCPSP.Scheduling.Heuristics
{
    public static class HeuristicFactory
    {
        public static IHeuristicPriorityRule Create(string heuristicName)
        {
            string key = Normalize(heuristicName);

            switch (key)
            {
                case "SPT":
                    return new HeuristicSPT();
                case "LPT":
                    return new HeuristicLPT();
                case "EST":
                    return new HeuristicEST();
                case "EFT":
                    return new HeuristicEFT();
                case "LST":
                    return new HeuristicLST();
                case "LFT":
                    return new HeuristicLFT();
                case "MSLK":
                    return new HeuristicMSLK();
                case "MIS":
                    return new HeuristicMIS();
                case "MTS":
                    return new HeuristicMTS();
                case "GRWC":
                    return new HeuristicGRWC();
                default:
                    throw new NotSupportedException("Unsupported heuristic: " + heuristicName);
            }
        }

        public static string Normalize(string heuristicName)
        {
            return string.IsNullOrWhiteSpace(heuristicName)
                ? string.Empty
                : heuristicName.Trim().ToUpperInvariant();
        }
    }
}
