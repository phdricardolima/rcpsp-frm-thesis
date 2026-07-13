// Thesis traceability: exact nominal reference used in the computational study and Appendix H.
using RCPSP.Contracts;
using RCPSP.Scheduling.Exact;
using RCPSP.Scheduling.Model;
using System;
using System.Collections.Generic;

namespace RCPSP.Infrastructure.Cpu
{
    public sealed class ExactBaselineRunDto
    {
        public BaselineResultDto Baseline { get; set; } = new BaselineResultDto();
        public bool Success { get; set; }
        public bool TimeLimitReached { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int TimeLimitSeconds { get; set; }
        public long NodesVisited { get; set; }
        public double SlackSum { get; set; }
        public bool OptimalityProven { get; set; }
        public string Trace { get; set; } = string.Empty;
    }

    public sealed class CpuExactBaselineScheduler
    {
        private const int DefaultTimeLimitSeconds = 10;

        public ExactBaselineRunDto RunModifiedDhBranchAndBoundDetailed(ProjectDataDto project, SchedulingOptionsDto options)
        {
            return RunBranchAndBoundDetailed(project, options, BranchAndBoundMode.ModifiedDh);
        }

        public BaselineResultDto RunModifiedDhBranchAndBound(ProjectDataDto project, SchedulingOptionsDto options)
        {
            return RunModifiedDhBranchAndBoundDetailed(project, options).Baseline;
        }

        public BaselineResultDto RunBranchAndBound(ProjectDataDto project, SchedulingOptionsDto options, BranchAndBoundMode mode)
        {
            return RunBranchAndBoundDetailed(project, options, mode).Baseline;
        }

        public ExactBaselineRunDto RunBranchAndBoundDetailed(ProjectDataDto project, SchedulingOptionsDto options, BranchAndBoundMode mode)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            SchedulingProjectData schedulingProject = ProjectDataMapper.Map(project);

            int timeLimitSeconds = (options != null && options.BranchAndBoundTimeLimitSeconds.HasValue && options.BranchAndBoundTimeLimitSeconds.Value > 0)
                ? options.BranchAndBoundTimeLimitSeconds.Value
                : DefaultTimeLimitSeconds;


            var solver = new DhBranchAndBoundSolver(schedulingProject, mode);
            var run = solver.Run(timeLimitSeconds);
            var schedule = run.schedule;

            var orderedIds = new List<int>(schedule.Count);
            var keys = new List<int>(schedule.Keys);
            keys.Sort((a, b) =>
            {
                var sa = schedule[a];
                var sb = schedule[b];
                int cmp = sa.start.CompareTo(sb.start);
                if (cmp != 0) return cmp;
                cmp = sa.end.CompareTo(sb.end);
                return cmp != 0 ? cmp : a.CompareTo(b);
            });
            orderedIds.AddRange(keys);

            var startTimes = new Dictionary<int, int>(schedule.Count);
            var finishTimes = new Dictionary<int, int>(schedule.Count);
            for (int i = 0; i < keys.Count; i++)
            {
                int id = keys[i];
                var item = schedule[id];
                startTimes[id] = (int)Math.Round(item.start, MidpointRounding.AwayFromZero);
                finishTimes[id] = (int)Math.Round(item.end, MidpointRounding.AwayFromZero);
            }

            string defaultLabel = mode == BranchAndBoundMode.ModifiedDh ? "Modified DH B&B" : "DH B&B";

            var result = new BaselineResultDto
            {
                RunLabel = !string.IsNullOrWhiteSpace(options != null ? options.RunLabel : null)
                    ? options.RunLabel
                    : defaultLabel,
                Makespan = (int)Math.Round(run.makespan, MidpointRounding.AwayFromZero),
                PriorityList = new List<int>(),
                ScheduledOrder = orderedIds,
                Sequence = new List<int>(orderedIds),
                StartTimesByActivity = startTimes,
                FinishTimesByActivity = finishTimes
            };

            var originalActivities = project.Activities ?? new List<ActivityDto>();
            var originalById = new Dictionary<int, ActivityDto>(originalActivities.Count);
            for (int i = 0; i < originalActivities.Count; i++)
            {
                var activity = originalActivities[i];
                if (activity == null || activity.IsSummary)
                    continue;

                originalById[activity.Id] = activity;
            }

            var scheduledActivities = new List<ScheduledActivityDto>(keys.Count);
            for (int i = 0; i < keys.Count; i++)
            {
                int id = keys[i];
                ActivityDto original;
                originalById.TryGetValue(id, out original);

                int start = startTimes[id];
                int finish = finishTimes[id];

                scheduledActivities.Add(new ScheduledActivityDto
                {
                    ActivityId = id,
                    Name = original != null && !string.IsNullOrWhiteSpace(original.Name)
                        ? original.Name
                        : "Act " + id,
                    Start = start,
                    Finish = finish,
                    Duration = finish - start
                });
            }

            result.Activities = scheduledActivities;

            bool timeLimit = solver.WasTimeLimitReached || ContainsLog(run.log, "TimeLimit");
            long nodes = solver.NodesVisited;
            double slackSum = solver.BestSlackSum;

            bool usedGreedyFallback = ContainsLog(run.log, "FallbackGreedy");
            bool noCompleteBbSolution = ContainsLog(run.log, "NoCompleteBBSolution");
            bool solverReportedOptimal = ContainsLog(run.log, "Optimal");
            bool completeSchedule = result.Activities != null &&
                result.Activities.Count == originalById.Count &&
                result.StartTimesByActivity != null && result.StartTimesByActivity.Count == originalById.Count &&
                result.FinishTimesByActivity != null && result.FinishTimesByActivity.Count == originalById.Count;
            bool usableSchedule = completeSchedule && !noCompleteBbSolution;
            bool optimalityProven = solverReportedOptimal && !timeLimit && !usedGreedyFallback && usableSchedule;
            string trace = run.log != null && run.log.Count > 0
                ? string.Join(" | ", run.log.ToArray())
                : string.Empty;

            string status = timeLimit ? "TimeLimit" : (optimalityProven ? "Optimal" : "FeasibleUnproven");
            string message = timeLimit
                ? string.Format("Modified DH B&B was truncated by the {0}s limit; showing the best feasible incumbent found.", timeLimitSeconds)
                : string.Empty;

            if (usedGreedyFallback && string.IsNullOrWhiteSpace(message))
                message = "Modified DH B&B did not find a complete tree solution before fallback; showing the greedy fallback baseline.";
            if (noCompleteBbSolution)
                message = "Modified DH B&B did not produce a complete feasible schedule; the partial solver state cannot be used as a baseline.";

            return new ExactBaselineRunDto
            {
                Baseline = result,
                Success = usableSchedule,
                TimeLimitReached = timeLimit,
                Status = status,
                Message = message,
                TimeLimitSeconds = timeLimitSeconds,
                NodesVisited = nodes,
                SlackSum = slackSum,
                OptimalityProven = optimalityProven,
                Trace = trace
            };
        }

        private static bool ContainsLog(List<string> log, string token)
        {
            if (log == null || string.IsNullOrWhiteSpace(token))
                return false;

            for (int i = 0; i < log.Count; i++)
            {
                if (string.Equals(log[i], token, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
