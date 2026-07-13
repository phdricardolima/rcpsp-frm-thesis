using RCPSP.Scheduling.Model;
using System.Collections.Generic;

namespace RCPSP.Scheduling.Heuristics
{
    public interface IHeuristicPriorityRule
    {
        string Name { get; }

        List<SchedulingActivity> OrderActivities(SchedulingProjectData project);
    }
}
