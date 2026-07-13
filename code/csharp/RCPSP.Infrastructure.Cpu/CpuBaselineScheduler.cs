// Thesis traceability: Appendix A, Algorithm A.2 (heuristic baseline construction).
using RCPSP.Application;
using RCPSP.Contracts;
using RCPSP.Scheduling.Cpm;
using RCPSP.Scheduling.Heuristics;
using RCPSP.Scheduling.Model;
using RCPSP.Scheduling.Schemes;
using System;
using System.Collections.Generic;

namespace RCPSP.Infrastructure.Cpu
{
    public sealed class CpuBaselineScheduler : IBaselineScheduler
    {
        public BaselineResultDto Run(ProjectDataDto project, SchedulingOptionsDto options)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            if (options.UseExactEngine || IsModifiedDhBranchAndBound(options.Heuristic) || IsModifiedDhBranchAndBound(options.Engine))
                throw new NotSupportedException("Modified DH B&B has not yet been integrated into CpuBaselineScheduler.");

            SchedulingProjectData schedulingProject = ProjectDataMapper.Map(project);

            var cpm = new PertCpmCalculator();
            cpm.Compute(schedulingProject);

            var priorityResolver = new PriorityRuleResolver(cpm);
            List<SchedulingActivity> priorityOrderedActivities =
                priorityResolver.Resolve(schedulingProject, options.Heuristic);

            IScheduleScheme scheme = SchemeFactory.Create(options.Scheme, options.Direction);
            ScheduleComputationResult schedule = scheme.Compute(schedulingProject, priorityOrderedActivities);

            return BuildBaselineResult(project, schedulingProject, schedule, options);
        }


        public BaselineResultDto RunWithInheritedOrder(ProjectDataDto project, IReadOnlyList<int> inheritedOrder, SchedulingOptionsDto options)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));
            if (options == null)
                throw new ArgumentNullException(nameof(options));


            if (inheritedOrder == null || inheritedOrder.Count == 0)
                return Run(project, options);

            SchedulingProjectData schedulingProject = ProjectDataMapper.Map(project);
            List<SchedulingActivity> nonSummary = schedulingProject.GetNonSummaryActivities();

            var rank = new Dictionary<int, int>(inheritedOrder.Count);
            for (int i = 0; i < inheritedOrder.Count; i++)
            {
                if (!rank.ContainsKey(inheritedOrder[i]))
                    rank[inheritedOrder[i]] = i;
            }


            int overflowBase = nonSummary.Count + inheritedOrder.Count + 1;
            var ordered = new List<SchedulingActivity>(nonSummary);
            ordered.Sort((a, b) =>
            {
                int ra; if (!rank.TryGetValue(a.Id, out ra)) ra = overflowBase + a.Id;
                int rb; if (!rank.TryGetValue(b.Id, out rb)) rb = overflowBase + b.Id;
                int cmp = ra.CompareTo(rb);
                return cmp != 0 ? cmp : a.Id.CompareTo(b.Id);
            });


            IScheduleScheme scheme = SchemeFactory.Create("SERIAL", "FORWARD");
            ScheduleComputationResult schedule = scheme.Compute(schedulingProject, ordered);

            return BuildBaselineResult(project, schedulingProject, schedule, options);
        }

        private static BaselineResultDto BuildBaselineResult(
            ProjectDataDto originalProject,
            SchedulingProjectData schedulingProject,
            ScheduleComputationResult schedule,
            SchedulingOptionsDto options)
        {
            var priorityList = schedule != null && schedule.PriorityOrder != null
                ? new List<int>(schedule.PriorityOrder)
                : new List<int>();

            var scheduledOrder = schedule != null && schedule.ScheduledOrder != null
                ? new List<int>(schedule.ScheduledOrder)
                : new List<int>();

            var startTimes = schedule != null && schedule.StartTimesByActivity != null
                ? new Dictionary<int, int>(schedule.StartTimesByActivity)
                : new Dictionary<int, int>();

            var finishTimes = schedule != null && schedule.FinishTimesByActivity != null
                ? new Dictionary<int, int>(schedule.FinishTimesByActivity)
                : new Dictionary<int, int>();

            var result = new BaselineResultDto
            {
                RunLabel = BuildRunLabel(options),
                Makespan = schedule != null ? schedule.Makespan : 0,
                PriorityList = priorityList,
                ScheduledOrder = scheduledOrder,
                Sequence = new List<int>(scheduledOrder),
                StartTimesByActivity = startTimes,
                FinishTimesByActivity = finishTimes
            };

            var originalActivities = originalProject.Activities ?? new List<ActivityDto>();
            var originalById = new Dictionary<int, ActivityDto>(originalActivities.Count);
            for (int i = 0; i < originalActivities.Count; i++)
            {
                var original = originalActivities[i];
                if (original == null)
                    continue;

                originalById[original.Id] = original;
            }

            var nonSummaryActivities = schedulingProject.GetNonSummaryActivities();
            var scheduledActivities = new List<ScheduledActivityDto>(nonSummaryActivities.Count);

            for (int i = 0; i < nonSummaryActivities.Count; i++)
            {
                var activity = nonSummaryActivities[i];
                if (activity == null)
                    continue;

                int start;
                if (!startTimes.TryGetValue(activity.Id, out start))
                    start = activity.ScheduledStart;

                int finish;
                if (!finishTimes.TryGetValue(activity.Id, out finish))
                    finish = activity.ScheduledFinish;

                string name = activity.Name;
                ActivityDto original;
                if (originalById.TryGetValue(activity.Id, out original) && !string.IsNullOrWhiteSpace(original.Name))
                    name = original.Name;

                scheduledActivities.Add(new ScheduledActivityDto
                {
                    ActivityId = activity.Id,
                    Name = name ?? ("Act " + activity.Id),
                    Start = start,
                    Finish = finish,
                    Duration = activity.Duration
                });
            }

            if (scheduledActivities.Count > 1)
            {
                scheduledActivities.Sort((x, y) =>
                {
                    int cmp = x.Start.CompareTo(y.Start);
                    if (cmp != 0) return cmp;
                    cmp = x.Finish.CompareTo(y.Finish);
                    return cmp != 0 ? cmp : x.ActivityId.CompareTo(y.ActivityId);
                });
            }

            result.Activities = scheduledActivities;
            return result;
        }

        private static string BuildRunLabel(SchedulingOptionsDto options)
        {
            if (options == null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(options.RunLabel))
                return options.RunLabel;

            string heuristic = Normalize(options.Heuristic);
            string scheme = Normalize(options.Scheme);
            string direction = Normalize(options.Direction);

            return string.Format("{0} | {1} | {2}", heuristic, scheme, direction);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToUpperInvariant();
        }

        private static bool IsModifiedDhBranchAndBound(string value)
        {
            string normalized = Normalize(value);
            return normalized == "MODIFIED DH B&B"
                   || normalized == "B&B"
                   || normalized == "DHBB"
                   || normalized == "B&B EF"
                   || normalized == "DH_BB";
        }
    }
}
