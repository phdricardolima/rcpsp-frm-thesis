using System.Collections.Generic;
using RCPSP.Scheduling.Model;

namespace RCPSP.Scheduling.Heuristics
{
    public sealed class HeuristicSPT : IHeuristicPriorityRule
    {
        public string Name => "SPT";

        public List<SchedulingActivity> OrderActivities(SchedulingProjectData projectData)
        {
            var ordered = new List<SchedulingActivity>(projectData.GetNonSummaryActivities());
            ordered.Sort((a, b) =>
            {
                int c = a.Duration.CompareTo(b.Duration);
                if (c != 0) return c;
                return a.Id.CompareTo(b.Id);
            });
            return ordered;
        }
    }
}
