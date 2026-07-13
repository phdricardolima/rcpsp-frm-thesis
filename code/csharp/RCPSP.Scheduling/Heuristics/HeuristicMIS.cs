using RCPSP.Scheduling.Model;
using System.Collections.Generic;

namespace RCPSP.Scheduling.Heuristics
{
    public sealed class HeuristicMIS : IHeuristicPriorityRule
    {
        public string Name => "MIS";

        public List<SchedulingActivity> OrderActivities(SchedulingProjectData project)
        {
            var ordered = new List<SchedulingActivity>(project.GetNonSummaryActivities());
            ordered.Sort((a, b) =>
            {
                int succA = a.SuccessorIds == null ? 0 : a.SuccessorIds.Count;
                int succB = b.SuccessorIds == null ? 0 : b.SuccessorIds.Count;
                int c = succB.CompareTo(succA);
                if (c != 0) return c;
                return a.Id.CompareTo(b.Id);
            });
            return ordered;
        }
    }
}
