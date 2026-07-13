using System;
using System.Collections.Generic;
using System.Linq;
using RCPSP.Scheduling.Model;

namespace RCPSP.Scheduling.Schemes
{
    public sealed class ParallelBackwardScheme : IScheduleScheme
    {
        public string Name => "PARALLEL_BACKWARD";
        public string Direction => "BACKWARD";

        public ScheduleComputationResult Compute(
            SchedulingProjectData projectData,
            List<SchedulingActivity> priorityOrderedActivities)
        {
            if (projectData == null)
                throw new ArgumentNullException(nameof(projectData));
            if (priorityOrderedActivities == null)
                throw new ArgumentNullException(nameof(priorityOrderedActivities));

            var nonSummary = projectData.GetNonSummaryActivities();
            var activityIds = new List<int>(nonSummary.Count);
            for (int i = 0; i < nonSummary.Count; i++)
                activityIds.Add(nonSummary[i].Id);

            var backwardPriority = new List<int>(priorityOrderedActivities.Count);
            for (int i = 0; i < priorityOrderedActivities.Count; i++)
            {
                var activity = priorityOrderedActivities[i];
                if (activity != null)
                    backwardPriority.Add(activity.Id);
            }

            var reversedPreds = BuildReversedPredecessors(projectData);
            ForwardResult reversedSchedule = RunParallelForwardOnCustomNetwork(projectData, backwardPriority, reversedPreds, activityIds);

            int arbitraryCompletionTime = reversedSchedule.Makespan;
            var startTimes = new Dictionary<int, int>(activityIds.Count);
            var finishTimes = new Dictionary<int, int>(activityIds.Count);

            for (int i = 0; i < activityIds.Count; i++)
            {
                int act = activityIds[i];
                int revStart = reversedSchedule.StartTimes[act];
                int revFinish = reversedSchedule.FinishTimes[act];

                startTimes[act] = arbitraryCompletionTime - revFinish;
                finishTimes[act] = arbitraryCompletionTime - revStart;
            }

            NormalizeSchedule(startTimes, finishTimes);
            ApplyLeftShift(projectData, activityIds, startTimes, finishTimes);

            activityIds.Sort((a, b) =>
            {
                int cmp = startTimes[a].CompareTo(startTimes[b]);
                if (cmp != 0) return cmp;
                cmp = finishTimes[a].CompareTo(finishTimes[b]);
                if (cmp != 0) return cmp;
                return a.CompareTo(b);
            });

            var startTimesFinal = new Dictionary<int, int>(activityIds.Count);
            var finishTimesFinal = new Dictionary<int, int>(activityIds.Count);
            for (int i = 0; i < activityIds.Count; i++)
            {
                int act = activityIds[i];
                startTimesFinal[act] = startTimes[act];
                finishTimesFinal[act] = finishTimes[act];
            }

            int makespan = 0;
            for (int i = 0; i < activityIds.Count; i++)
            {
                int act = activityIds[i];
                int finish = finishTimesFinal[act];
                if (finish > makespan)
                    makespan = finish;
            }

            return new ScheduleComputationResult
            {
                PriorityOrder = new List<int>(backwardPriority),
                ScheduledOrder = new List<int>(activityIds),
                StartTimesByActivity = startTimesFinal,
                FinishTimesByActivity = finishTimesFinal,
                Makespan = makespan
            };
        }

        private static ForwardResult RunParallelForwardOnCustomNetwork(
            SchedulingProjectData projectData,
            List<int> priorityList,
            List<List<int>> predecessors,
            List<int> activityIds)
        {
            var priorityRank = new Dictionary<int, int>(priorityList.Count);
            for (int i = 0; i < priorityList.Count; i++)
            {
                int actId = priorityList[i];
                if (!priorityRank.ContainsKey(actId))
                    priorityRank.Add(actId, i);
            }

            int horizon = GetConstructionHorizon(projectData);
            var capacities = new Dictionary<int, int>(projectData.Resources.Count);
            var usage = new Dictionary<int, int[]>(projectData.Resources.Count);
            for (int i = 0; i < projectData.Resources.Count; i++)
            {
                var resource = projectData.Resources[i];
                if (resource == null)
                    continue;

                int capacity = Math.Max(0, resource.Capacity);
                capacities[resource.Id] = capacity;
                usage[resource.Id] = new int[horizon + 1];
            }

            var durationByActivity = new Dictionary<int, int>(activityIds.Count);
            for (int i = 0; i < activityIds.Count; i++)
            {
                int actId = activityIds[i];
                durationByActivity[actId] = RoundedDuration(projectData, actId);
            }

            var unscheduled = new HashSet<int>(activityIds);
            var active = new HashSet<int>();
            var completed = new HashSet<int>();
            var startTimes = new Dictionary<int, int>(activityIds.Count);
            var finishTimes = new Dictionary<int, int>(activityIds.Count);
            int currentTime = 0;

            while (unscheduled.Count > 0)
            {
                MoveFinishedToCompleted(active, completed, finishTimes, currentTime);

                bool inserted;
                do
                {
                    inserted = false;
                    int chosen = SelectFeasibleActivity(
                        projectData,
                        unscheduled,
                        completed,
                        predecessors,
                        priorityRank,
                        durationByActivity,
                        currentTime,
                        usage,
                        capacities,
                        horizon);

                    if (chosen <= 0)
                        break;

                    int duration = durationByActivity[chosen];
                    startTimes[chosen] = currentTime;
                    finishTimes[chosen] = currentTime + duration;
                    unscheduled.Remove(chosen);
                    active.Add(chosen);
                    ParallelForwardScheme.ReserveResources(projectData, chosen, currentTime, duration, usage, horizon);
                    inserted = true;
                }
                while (inserted);

                if (unscheduled.Count == 0)
                    break;

                int? nextTime = GetNextDecisionTime(active, finishTimes, currentTime);
                if (nextTime == null)
                    throw new InvalidOperationException("PARALLEL_BACKWARD failure.");

                currentTime = nextTime.Value;
            }

            int makespan = 0;
            foreach (var kv in finishTimes)
            {
                if (kv.Value > makespan)
                    makespan = kv.Value;
            }

            return new ForwardResult
            {
                StartTimes = startTimes,
                FinishTimes = finishTimes,
                Makespan = makespan
            };
        }

        private static int SelectFeasibleActivity(
            SchedulingProjectData projectData,
            HashSet<int> unscheduled,
            HashSet<int> completed,
            List<List<int>> predecessors,
            Dictionary<int, int> priorityRank,
            Dictionary<int, int> durationByActivity,
            int currentTime,
            Dictionary<int, int[]> usage,
            Dictionary<int, int> capacities,
            int horizon)
        {
            int bestAct = -1;
            int bestRank = int.MaxValue;
            int bestDuration = int.MaxValue;

            foreach (int act in unscheduled)
            {
                var preds = predecessors[act - 1];
                bool predsCompleted = true;
                for (int i = 0; i < preds.Count; i++)
                {
                    if (!completed.Contains(preds[i]))
                    {
                        predsCompleted = false;
                        break;
                    }
                }

                if (!predsCompleted)
                    continue;

                int duration = durationByActivity[act];
                if (!ParallelForwardScheme.CanStartAtTime(projectData, act, currentTime, duration, usage, capacities, horizon))
                    continue;

                int rank;
                if (!priorityRank.TryGetValue(act, out rank))
                    rank = int.MaxValue;

                if (rank < bestRank ||
                    (rank == bestRank && duration < bestDuration) ||
                    (rank == bestRank && duration == bestDuration && act < bestAct))
                {
                    bestAct = act;
                    bestRank = rank;
                    bestDuration = duration;
                }
            }

            return bestAct;
        }

        private static List<List<int>> BuildReversedPredecessors(SchedulingProjectData projectData)
        {
            int n = projectData.GetNonSummaryActivities().Count;
            var reversedPreds = new List<List<int>>(n);

            for (int i = 1; i <= n; i++)
                reversedPreds.Add(new List<int>(projectData.GetActivity(i).SuccessorIds));

            return reversedPreds;
        }

        private static void ApplyLeftShift(
            SchedulingProjectData projectData,
            List<int> activityIds,
            Dictionary<int, int> startTimes,
            Dictionary<int, int> finishTimes)
        {
            int horizon = GetConstructionHorizon(projectData);
            var capacities = new Dictionary<int, int>(projectData.Resources.Count);
            var usage = new Dictionary<int, int[]>(projectData.Resources.Count);
            for (int i = 0; i < projectData.Resources.Count; i++)
            {
                var resource = projectData.Resources[i];
                if (resource == null)
                    continue;

                int capacity = Math.Max(0, resource.Capacity);
                capacities[resource.Id] = capacity;
                usage[resource.Id] = new int[horizon + 1];
            }

            for (int i = 0; i < activityIds.Count; i++)
            {
                int act = activityIds[i];
                int duration = finishTimes[act] - startTimes[act];
                ParallelForwardScheme.ReserveResources(projectData, act, startTimes[act], duration, usage, horizon);
            }

            var ordered = new List<int>(activityIds);
            ordered.Sort((a, b) =>
            {
                int cmp = startTimes[a].CompareTo(startTimes[b]);
                if (cmp != 0) return cmp;
                cmp = finishTimes[a].CompareTo(finishTimes[b]);
                if (cmp != 0) return cmp;
                return a.CompareTo(b);
            });

            bool changed;
            do
            {
                changed = false;
                for (int i = 0; i < ordered.Count; i++)
                {
                    int act = ordered[i];
                    int duration = finishTimes[act] - startTimes[act];
                    int earliestPredFinish = 0;
                    var predecessors = projectData.GetActivity(act).PredecessorIds;
                    for (int p = 0; p < predecessors.Count; p++)
                    {
                        int pred = predecessors[p];
                        int predFinish;
                        if (finishTimes.TryGetValue(pred, out predFinish) && predFinish > earliestPredFinish)
                            earliestPredFinish = predFinish;
                    }

                    RemoveResources(projectData, act, startTimes[act], duration, usage, horizon);

                    int bestStart = startTimes[act];
                    for (int candidateStart = earliestPredFinish; candidateStart < startTimes[act]; candidateStart++)
                    {
                        if (ParallelForwardScheme.CanStartAtTime(projectData, act, candidateStart, duration, usage, capacities, horizon))
                        {
                            bestStart = candidateStart;
                            break;
                        }
                    }

                    if (bestStart != startTimes[act])
                    {
                        startTimes[act] = bestStart;
                        finishTimes[act] = bestStart + duration;
                        changed = true;
                    }

                    ParallelForwardScheme.ReserveResources(projectData, act, startTimes[act], duration, usage, horizon);
                }
            }
            while (changed);
        }

        private static void RemoveResources(
            SchedulingProjectData projectData,
            int activityId,
            int proposedStart,
            int duration,
            Dictionary<int, int[]> usage,
            int horizon)
        {
            if (duration <= 0)
                return;

            var demandMap = ParallelForwardScheme.GetDemandMap(projectData, activityId);
            foreach (var demandKv in demandMap)
            {
                int resourceId = demandKv.Key;
                int demand = demandKv.Value;
                if (demand <= 0)
                    continue;

                int[] resourceUsage;
                if (!usage.TryGetValue(resourceId, out resourceUsage))
                    continue;

                for (int tau = proposedStart; tau < proposedStart + duration; tau++)
                {
                    if (tau < 0 || tau > horizon)
                        throw new InvalidOperationException("ParallelBackward exceeded the resource usage horizon.");

                    resourceUsage[tau] -= demand;
                }
            }
        }

        private static int GetConstructionHorizon(SchedulingProjectData projectData)
        {
            var nonSummary = projectData.GetNonSummaryActivities();
            int sumDur = 10;
            for (int i = 0; i < nonSummary.Count; i++)
                sumDur += Math.Max(1, nonSummary[i].Duration);
            return Math.Max(100, sumDur + 10);
        }

        private static int RoundedDuration(SchedulingProjectData projectData, int act)
        {
            var a = projectData.GetActivity(act);
            int d = a == null ? 0 : a.Duration;
            return d < 1 ? 1 : d;
        }

        private static void MoveFinishedToCompleted(
            HashSet<int> active,
            HashSet<int> completed,
            Dictionary<int, int> finishTimes,
            int currentTime)
        {
            var toMove = new List<int>();
            foreach (int act in active)
            {
                int finish;
                if (finishTimes.TryGetValue(act, out finish) && finish <= currentTime)
                    toMove.Add(act);
            }

            for (int i = 0; i < toMove.Count; i++)
            {
                int act = toMove[i];
                active.Remove(act);
                completed.Add(act);
            }
        }

        private static int? GetNextDecisionTime(
            HashSet<int> active,
            Dictionary<int, int> finishTimes,
            int currentTime)
        {
            int next = int.MaxValue;
            foreach (int act in active)
            {
                int finish;
                if (!finishTimes.TryGetValue(act, out finish))
                    continue;
                if (finish > currentTime && finish < next)
                    next = finish;
            }

            return next == int.MaxValue ? (int?)null : next;
        }

        private static void NormalizeSchedule(
            Dictionary<int, int> startTimes,
            Dictionary<int, int> finishTimes)
        {
            int minStart = int.MaxValue;
            foreach (var kv in startTimes)
            {
                if (kv.Value < minStart)
                    minStart = kv.Value;
            }

            var keys = startTimes.Keys.ToList();
            for (int i = 0; i < keys.Count; i++)
            {
                int act = keys[i];
                startTimes[act] -= minStart;
                finishTimes[act] -= minStart;
            }
        }

        private sealed class ForwardResult
        {
            public Dictionary<int, int> StartTimes { get; set; } = new Dictionary<int, int>();
            public Dictionary<int, int> FinishTimes { get; set; } = new Dictionary<int, int>();
            public int Makespan { get; set; }
        }
    }
}
