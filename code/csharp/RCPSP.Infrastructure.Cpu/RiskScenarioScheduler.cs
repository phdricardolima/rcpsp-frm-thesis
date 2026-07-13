// Thesis traceability: resource-feasible makespan propagation for simulated scenarios.
using System;
using System.Collections.Generic;
using System.Linq;
using RCPSP.Contracts;

namespace RCPSP.Infrastructure.Cpu
{
    public sealed class RiskScenarioScheduler
    {
        public sealed class PreparedProject
        {
            public readonly List<PreparedActivity> Activities;
            public readonly Dictionary<int, PreparedActivity> ActivityMap;
            public readonly Dictionary<int, int> Capacities;
            public readonly int BaselineFinishMax;
            public readonly int NominalDurationSum;

            public PreparedProject(
                List<PreparedActivity> activities,
                Dictionary<int, PreparedActivity> activityMap,
                Dictionary<int, int> capacities,
                int baselineFinishMax,
                int nominalDurationSum)
            {
                Activities = activities;
                ActivityMap = activityMap;
                Capacities = capacities;
                BaselineFinishMax = baselineFinishMax;
                NominalDurationSum = nominalDurationSum;
            }
        }

        public sealed class PreparedActivity
        {
            public int Id;
            public int DurationDays;
            public bool IsCompleted;
            public bool IsInProgress;
            public int[] PredecessorIds;
            public ResourceDemand[] Demands;
        }

        public struct ResourceDemand
        {
            public int ResourceId;
            public int Units;
        }

        public sealed class ScenarioScheduleResult
        {
            public Dictionary<int, int> StartTimes = new Dictionary<int, int>();
            public Dictionary<int, int> FinishTimes = new Dictionary<int, int>();
            public int Makespan;
        }

        public PreparedProject Prepare(ProjectDataDto project, Dictionary<int, int> baselineFinish)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            var sourceActivities = project.Activities ?? new List<ActivityDto>();
            var activities = new List<PreparedActivity>(sourceActivities.Count);
            var activityMap = new Dictionary<int, PreparedActivity>(sourceActivities.Count);
            int nominalDurationSum = 0;

            for (int i = 0; i < sourceActivities.Count; i++)
            {
                var activity = sourceActivities[i];
                if (activity == null || activity.IsSummary)
                    continue;

                var prepared = new PreparedActivity
                {
                    Id = activity.Id,
                    DurationDays = Math.Max(0, activity.DurationDays),
                    IsCompleted = string.Equals(activity.ExecutionState, "Completed", StringComparison.OrdinalIgnoreCase),
                    IsInProgress = string.Equals(activity.ExecutionState, "InProgress", StringComparison.OrdinalIgnoreCase),
                    PredecessorIds = activity.PredecessorIds != null ? activity.PredecessorIds.ToArray() : new int[0],
                    Demands = BuildDemands(activity.Assignments)
                };

                activities.Add(prepared);
                if (!activityMap.ContainsKey(prepared.Id))
                    activityMap.Add(prepared.Id, prepared);

                nominalDurationSum += prepared.DurationDays;
            }

            var capacities = BuildCapacityMap(project);
            int baselineFinishMax = 0;
            if (baselineFinish != null && baselineFinish.Count > 0)
            {
                foreach (var value in baselineFinish.Values)
                {
                    if (value > baselineFinishMax)
                        baselineFinishMax = value;
                }
            }

            return new PreparedProject(activities, activityMap, capacities, baselineFinishMax, nominalDurationSum);
        }

        public ScenarioScheduleResult Schedule(
            ProjectDataDto project,
            IReadOnlyList<int> activityOrder,
            Dictionary<int, int> sampledDurations,
            Dictionary<int, int> baselineStart,
            Dictionary<int, int> baselineFinish)
        {
            var prepared = Prepare(project, baselineFinish);
            return Schedule(prepared, activityOrder, sampledDurations, baselineStart, baselineFinish);
        }

        public ScenarioScheduleResult Schedule(
            PreparedProject prepared,
            IReadOnlyList<int> activityOrder,
            Dictionary<int, int> sampledDurations,
            Dictionary<int, int> baselineStart,
            Dictionary<int, int> baselineFinish)
        {
            if (prepared == null)
                throw new ArgumentNullException(nameof(prepared));
            if (activityOrder == null)
                throw new ArgumentNullException(nameof(activityOrder));
            if (sampledDurations == null)
                throw new ArgumentNullException(nameof(sampledDurations));

            int horizon = ComputeHorizon(prepared, sampledDurations);
            var usage = InitializeUsage(prepared.Capacities, horizon);
            var result = new ScenarioScheduleResult();
            int makespan = 0;

            for (int i = 0; i < activityOrder.Count; i++)
            {
                int activityId = activityOrder[i];
                PreparedActivity activity;
                if (!prepared.ActivityMap.TryGetValue(activityId, out activity))
                    continue;

                int duration;
                if (!sampledDurations.TryGetValue(activityId, out duration))
                    duration = activity.DurationDays;
                else
                    duration = Math.Max(0, duration);

                if (activity.IsCompleted)
                {
                    int finishCompleted = GetOrDefault(baselineFinish, activityId, 0);
                    int startCompleted = GetOrDefault(baselineStart, activityId, Math.Max(0, finishCompleted - duration));

                    result.StartTimes[activityId] = startCompleted;
                    result.FinishTimes[activityId] = finishCompleted;
                    if (finishCompleted > makespan)
                        makespan = finishCompleted;
                    continue;
                }

                int earliestStart = ComputeEarliestStartByPrecedence(activity.PredecessorIds, result.FinishTimes);
                int actualStart;

                if (activity.IsInProgress)
                {
                    int forcedStart = GetOrDefault(baselineStart, activityId, earliestStart);
                    actualStart = Math.Max(earliestStart, forcedStart);
                }
                else
                {
                    actualStart = earliestStart;
                }

                if (duration > 0)
                {
                    actualStart = FindEarliestFeasibleStart(
                        activity.Demands,
                        activityId,
                        actualStart,
                        duration,
                        usage,
                        prepared.Capacities,
                        horizon);

                    ReserveResources(activity.Demands, actualStart, duration, usage, horizon);
                }

                int finish = actualStart + duration;
                result.StartTimes[activityId] = actualStart;
                result.FinishTimes[activityId] = finish;
                if (finish > makespan)
                    makespan = finish;
            }

            result.Makespan = makespan;
            return result;
        }

        public int ScheduleMakespan(
            PreparedProject prepared,
            IReadOnlyList<int> activityOrder,
            Dictionary<int, int> sampledDurations,
            Dictionary<int, int> baselineStart,
            Dictionary<int, int> baselineFinish)
        {
            if (prepared == null)
                throw new ArgumentNullException(nameof(prepared));
            if (activityOrder == null)
                throw new ArgumentNullException(nameof(activityOrder));
            if (sampledDurations == null)
                throw new ArgumentNullException(nameof(sampledDurations));

            int horizon = ComputeHorizon(prepared, sampledDurations);
            var usage = InitializeUsage(prepared.Capacities, horizon);
            var finishTimes = new Dictionary<int, int>(activityOrder.Count);
            int makespan = 0;

            for (int i = 0; i < activityOrder.Count; i++)
            {
                int activityId = activityOrder[i];
                PreparedActivity activity;
                if (!prepared.ActivityMap.TryGetValue(activityId, out activity))
                    continue;

                int duration;
                if (!sampledDurations.TryGetValue(activityId, out duration))
                    duration = activity.DurationDays;
                else
                    duration = Math.Max(0, duration);

                if (activity.IsCompleted)
                {
                    int finishCompleted = GetOrDefault(baselineFinish, activityId, 0);
                    finishTimes[activityId] = finishCompleted;
                    if (finishCompleted > makespan)
                        makespan = finishCompleted;
                    continue;
                }

                int earliestStart = ComputeEarliestStartByPrecedence(activity.PredecessorIds, finishTimes);
                int actualStart;

                if (activity.IsInProgress)
                {
                    int forcedStart = GetOrDefault(baselineStart, activityId, earliestStart);
                    actualStart = Math.Max(earliestStart, forcedStart);
                }
                else
                {
                    actualStart = earliestStart;
                }

                if (duration > 0)
                {
                    actualStart = FindEarliestFeasibleStart(
                        activity.Demands,
                        activityId,
                        actualStart,
                        duration,
                        usage,
                        prepared.Capacities,
                        horizon);

                    ReserveResources(activity.Demands, actualStart, duration, usage, horizon);
                }

                int finish = actualStart + duration;
                finishTimes[activityId] = finish;
                if (finish > makespan)
                    makespan = finish;
            }

            return makespan;
        }

        private static ResourceDemand[] BuildDemands(List<ResourceAssignmentDto> assignments)
        {
            if (assignments == null || assignments.Count == 0)
                return new ResourceDemand[0];

            var totals = new Dictionary<int, int>(assignments.Count);
            for (int i = 0; i < assignments.Count; i++)
            {
                var assignment = assignments[i];
                if (assignment == null)
                    continue;

                int units = (int)Math.Round(assignment.Units, MidpointRounding.AwayFromZero);
                if (units <= 0)
                    continue;

                int current;
                if (totals.TryGetValue(assignment.ResourceId, out current))
                    totals[assignment.ResourceId] = current + units;
                else
                    totals.Add(assignment.ResourceId, units);
            }

            if (totals.Count == 0)
                return new ResourceDemand[0];

            var result = new ResourceDemand[totals.Count];
            int index = 0;
            foreach (var kv in totals)
            {
                result[index++] = new ResourceDemand
                {
                    ResourceId = kv.Key,
                    Units = kv.Value
                };
            }

            return result;
        }

        private static Dictionary<int, int> BuildCapacityMap(ProjectDataDto project)
        {
            var capacities = new Dictionary<int, int>();
            var resources = project.Resources ?? new List<ResourceDto>();

            for (int i = 0; i < resources.Count; i++)
            {
                var resource = resources[i];
                if (resource == null)
                    continue;

                int capacity = Math.Max(0, (int)Math.Round((double)resource.Capacity, MidpointRounding.AwayFromZero));
                if (capacity < 0)
                    capacity = 0;

                if (!capacities.ContainsKey(resource.Id))
                    capacities.Add(resource.Id, capacity);
            }

            return capacities;
        }

        private static int ComputeHorizon(PreparedProject prepared, Dictionary<int, int> sampledDurations)
        {
            int sampledTotal = 0;

            if (sampledDurations != null && sampledDurations.Count > 0)
            {
                foreach (var kv in sampledDurations)
                    sampledTotal += Math.Max(0, kv.Value);

                if (sampledDurations.Count < prepared.Activities.Count)
                {
                    for (int i = 0; i < prepared.Activities.Count; i++)
                    {
                        var activity = prepared.Activities[i];
                        if (!sampledDurations.ContainsKey(activity.Id))
                            sampledTotal += activity.DurationDays;
                    }
                }
            }
            else
            {
                sampledTotal = prepared.NominalDurationSum;
            }

            int horizon = Math.Max(sampledTotal + 10, prepared.BaselineFinishMax + sampledTotal + 10);
            if (horizon < 100)
                horizon = 100;

            return horizon;
        }

        private static Dictionary<int, int[]> InitializeUsage(Dictionary<int, int> capacities, int horizon)
        {
            var usage = new Dictionary<int, int[]>(capacities.Count);
            foreach (var kv in capacities)
                usage[kv.Key] = new int[horizon + 1];
            return usage;
        }

        private static int ComputeEarliestStartByPrecedence(int[] predecessors, Dictionary<int, int> finishTimes)
        {
            int earliest = 0;
            if (predecessors == null)
                return earliest;

            for (int i = 0; i < predecessors.Length; i++)
            {
                int predecessorFinish;
                if (finishTimes.TryGetValue(predecessors[i], out predecessorFinish) && predecessorFinish > earliest)
                    earliest = predecessorFinish;
            }

            return earliest;
        }

        private static int FindEarliestFeasibleStart(
            ResourceDemand[] demands,
            int activityId,
            int earliestStart,
            int duration,
            Dictionary<int, int[]> usage,
            Dictionary<int, int> capacities,
            int horizon)
        {
            if (duration <= 0)
                return earliestStart;

            int latestStartToTry = Math.Max(earliestStart, horizon - duration);
            int normalizedStart = Math.Max(0, earliestStart);

            for (int start = normalizedStart; start <= latestStartToTry; start++)
            {
                if (HasResourceFeasibility(demands, start, duration, usage, capacities, horizon))
                    return start;
            }

            throw new InvalidOperationException(
                "RiskScenarioScheduler could not find a feasible start within the current horizon for activity " +
                activityId + ".");
        }

        private static bool HasResourceFeasibility(
            ResourceDemand[] demands,
            int start,
            int duration,
            Dictionary<int, int[]> usage,
            Dictionary<int, int> capacities,
            int horizon)
        {
            if (duration <= 0 || demands == null || demands.Length == 0)
                return true;

            int end = start + duration;
            if (start < 0 || end > horizon + 1)
                return false;

            for (int i = 0; i < demands.Length; i++)
            {
                var demand = demands[i];

                int capacity;
                if (!capacities.TryGetValue(demand.ResourceId, out capacity))
                    continue;

                int[] resourceUsage;
                if (!usage.TryGetValue(demand.ResourceId, out resourceUsage))
                    continue;

                for (int t = start; t < end; t++)
                {
                    if (resourceUsage[t] + demand.Units > capacity)
                        return false;
                }
            }

            return true;
        }

        private static void ReserveResources(
            ResourceDemand[] demands,
            int start,
            int duration,
            Dictionary<int, int[]> usage,
            int horizon)
        {
            if (duration <= 0 || demands == null || demands.Length == 0)
                return;

            int boundedStart = Math.Max(0, start);
            int end = Math.Min(horizon + 1, start + duration);
            for (int i = 0; i < demands.Length; i++)
            {
                int[] resourceUsage;
                if (!usage.TryGetValue(demands[i].ResourceId, out resourceUsage))
                    continue;

                for (int t = boundedStart; t < end; t++)
                    resourceUsage[t] += demands[i].Units;
            }
        }

        private static int GetOrDefault(Dictionary<int, int> map, int key, int defaultValue)
        {
            if (map == null)
                return defaultValue;

            int value;
            return map.TryGetValue(key, out value) ? value : defaultValue;
        }
    }
}
