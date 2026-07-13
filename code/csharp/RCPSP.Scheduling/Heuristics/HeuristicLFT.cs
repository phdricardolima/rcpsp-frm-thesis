using RCPSP.Scheduling.Model;
using System.Collections.Generic;

namespace RCPSP.Scheduling.Heuristics
{
    public sealed class HeuristicLFT : IHeuristicPriorityRule
    {
        public string Name => "LFT";

        public List<SchedulingActivity> OrderActivities(SchedulingProjectData project)
        {
            var ordered = new List<SchedulingActivity>(project.GetNonSummaryActivities());
            ordered.Sort((a, b) =>
            {
                int c = a.LF.CompareTo(b.LF);
                if (c != 0) return c;
                return a.Id.CompareTo(b.Id);
            });
            return ordered;
        }
    }
}
