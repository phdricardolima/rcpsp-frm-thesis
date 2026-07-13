using RCPSP.Scheduling.Model;
using System.Collections.Generic;

namespace RCPSP.Scheduling.Schemes
{
    public interface IScheduleScheme
    {
        string Name { get; }
        string Direction { get; }

        ScheduleComputationResult Compute(
            SchedulingProjectData project,
            List<SchedulingActivity> priorityOrderedActivities);
    }

    public sealed class ScheduleComputationResult
    {
        public int Makespan { get; set; }

        public Dictionary<int, int> StartTimesByActivity { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> FinishTimesByActivity { get; set; } = new Dictionary<int, int>();

        public List<int> ScheduledOrder { get; set; } = new List<int>();
        public List<int> PriorityOrder { get; set; } = new List<int>();
    }
}
