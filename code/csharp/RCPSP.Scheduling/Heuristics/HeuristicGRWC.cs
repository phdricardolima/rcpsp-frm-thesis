using RCPSP.Scheduling.Model;
using System.Collections.Generic;

namespace RCPSP.Scheduling.Heuristics
{
    public sealed class HeuristicGRWC : IHeuristicPriorityRule
    {
        public string Name => "GRWC";

        public List<SchedulingActivity> OrderActivities(SchedulingProjectData project)
        {
            var ordered = new List<SchedulingActivity>(project.GetNonSummaryActivities());
            ordered.Sort((a, b) =>
            {
                long scoreA = ComputeScore(a);
                long scoreB = ComputeScore(b);
                int c = scoreB.CompareTo(scoreA);
                if (c != 0) return c;
                return a.Id.CompareTo(b.Id);
            });
            return ordered;
        }

        private static long ComputeScore(SchedulingActivity activity)
        {
            if (activity == null || activity.ResourceDemandByResourceId == null)
                return 0L;

            long totalDemand = 0L;
            foreach (var kv in activity.ResourceDemandByResourceId)
                totalDemand += kv.Value;

            return totalDemand * activity.Duration;
        }
    }
}
