using RCPSP.Scheduling.Model;
using System.Collections.Generic;

namespace RCPSP.Scheduling.Heuristics
{
    public sealed class HeuristicLPT : IHeuristicPriorityRule
    {
        public string Name => "LPT";

        public List<SchedulingActivity> OrderActivities(SchedulingProjectData project)
        {
            var ordered = new List<SchedulingActivity>(project.GetNonSummaryActivities());
            ordered.Sort((a, b) =>
            {
                int c = b.Duration.CompareTo(a.Duration);
                if (c != 0) return c;
                return a.Id.CompareTo(b.Id);
            });
            return ordered;
        }
    }
}
