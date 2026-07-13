using RCPSP.Scheduling.Model;
using System.Collections.Generic;

namespace RCPSP.Scheduling.Heuristics
{
    public sealed class HeuristicEST : IHeuristicPriorityRule
    {
        public string Name => "EST";

        public List<SchedulingActivity> OrderActivities(SchedulingProjectData project)
        {
            var ordered = new List<SchedulingActivity>(project.GetNonSummaryActivities());
            ordered.Sort((a, b) =>
            {
                int c = a.ES.CompareTo(b.ES);
                if (c != 0) return c;
                return a.Id.CompareTo(b.Id);
            });
            return ordered;
        }
    }
}
