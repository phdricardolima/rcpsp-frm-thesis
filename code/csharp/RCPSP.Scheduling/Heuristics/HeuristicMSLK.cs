using RCPSP.Scheduling.Model;
using System.Collections.Generic;

namespace RCPSP.Scheduling.Heuristics
{
    public sealed class HeuristicMSLK : IHeuristicPriorityRule
    {
        public string Name => "MSLK";

        public List<SchedulingActivity> OrderActivities(SchedulingProjectData project)
        {
            var ordered = new List<SchedulingActivity>(project.GetNonSummaryActivities());
            ordered.Sort((a, b) =>
            {
                int c = a.Slack.CompareTo(b.Slack);
                if (c != 0) return c;
                return a.Id.CompareTo(b.Id);
            });
            return ordered;
        }
    }
}
