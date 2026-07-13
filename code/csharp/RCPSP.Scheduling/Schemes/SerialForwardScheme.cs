using RCPSP.Scheduling.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RCPSP.Scheduling.Schemes
{
    public sealed class SerialForwardScheme : IScheduleScheme
    {
        public string Name => "SERIAL_FORWARD";
        public string Direction => "FORWARD";

        public ScheduleComputationResult Compute(
            SchedulingProjectData projectData,
            List<SchedulingActivity> priorityOrderedActivities)
        {
            if (projectData == null)
                throw new ArgumentNullException(nameof(projectData));
            if (priorityOrderedActivities == null)
                throw new ArgumentNullException(nameof(priorityOrderedActivities));

            var nonSummaryActivities = projectData.GetNonSummaryActivities();
            var regraLista = new List<int>(priorityOrderedActivities.Count);
            for (int i = 0; i < priorityOrderedActivities.Count; i++)
            {
                var act = priorityOrderedActivities[i];
                if (act != null)
                    regraLista.Add(act.Id);
            }

            int maxActivityId = 0;
            int horizon = 10;
            var capacities = new Dictionary<int, int>(projectData.Resources.Count);
            var usage = new Dictionary<int, int[]>();

            for (int i = 0; i < projectData.Resources.Count; i++)
            {
                var resource = projectData.Resources[i];
                if (resource == null)
                    continue;

                int capacity = Math.Max(0, resource.Capacity);
                capacities[resource.Id] = capacity;
            }

            for (int i = 0; i < nonSummaryActivities.Count; i++)
            {
                var activity = nonSummaryActivities[i];
                if (activity == null)
                    continue;

                if (activity.Id > maxActivityId)
                    maxActivityId = activity.Id;

                int d = activity.Duration;
                if (d < 1)
                    d = 1;
                horizon += d;
            }

            foreach (var resourceId in capacities.Keys)
                usage[resourceId] = new int[horizon + 1];

            var durIntByAct = new int[maxActivityId + 1];
            for (int i = 0; i < nonSummaryActivities.Count; i++)
            {
                var activity = nonSummaryActivities[i];
                if (activity == null)
                    continue;

                int d = activity.Duration;
                if (d < 1)
                    d = 1;
                durIntByAct[activity.Id] = d;
            }

            var sg = new List<int>(regraLista.Count);
            var decisionList = new List<int>(regraLista.Count);
            var scheduled = new HashSet<int>();
            var startTimes = new Dictionary<int, int>(regraLista.Count);
            var finishTimes = new Dictionary<int, int>(regraLista.Count);

            for (int x = 0; x < regraLista.Count; x++)
            {
                decisionList.Add(regraLista[x]);

                for (int y = 0; y < decisionList.Count; y++)
                {
                    int actId = decisionList[y];
                    var activity = projectData.GetActivity(actId);
                    if (activity == null || activity.IsSummary)
                    {
                        decisionList.RemoveAt(y);
                        y--;
                        continue;
                    }

                    if (!IsSchedulable(activity, scheduled))
                        continue;

                    int duration = actId < durIntByAct.Length ? durIntByAct[actId] : Math.Max(1, activity.Duration);
                    int earliestStart = ComputeEarliestStart(activity, finishTimes);
                    int chosenStart = FindEarliestFeasibleStart(activity, earliestStart, duration, usage, capacities, horizon);

                    sg.Add(actId);
                    scheduled.Add(actId);
                    startTimes[actId] = chosenStart;
                    finishTimes[actId] = chosenStart + duration;

                    ReserveResources(activity, chosenStart, duration, usage, horizon);

                    decisionList.RemoveAt(y);
                    y = -1;
                }
            }

            sg.Sort((a, b) =>
            {
                int sa = startTimes.ContainsKey(a) ? startTimes[a] : int.MaxValue;
                int sb = startTimes.ContainsKey(b) ? startTimes[b] : int.MaxValue;
                int cmp = sa.CompareTo(sb);
                if (cmp != 0) return cmp;

                int fa = finishTimes.ContainsKey(a) ? finishTimes[a] : int.MaxValue;
                int fb = finishTimes.ContainsKey(b) ? finishTimes[b] : int.MaxValue;
                cmp = fa.CompareTo(fb);
                if (cmp != 0) return cmp;

                return a.CompareTo(b);
            });

            int makespan = 0;
            foreach (var kv in finishTimes)
            {
                if (kv.Value > makespan)
                    makespan = kv.Value;
            }

            return new ScheduleComputationResult
            {
                PriorityOrder = new List<int>(regraLista),
                ScheduledOrder = new List<int>(sg),
                StartTimesByActivity = startTimes,
                FinishTimesByActivity = finishTimes,
                Makespan = makespan
            };
        }

        private static bool IsSchedulable(SchedulingActivity activity, HashSet<int> scheduled)
        {
            var predecessors = activity.PredecessorIds;
            if (predecessors == null || predecessors.Count == 0)
                return true;

            for (int i = 0; i < predecessors.Count; i++)
            {
                if (!scheduled.Contains(predecessors[i]))
                    return false;
            }

            return true;
        }

        private static int ComputeEarliestStart(SchedulingActivity activity, Dictionary<int, int> finishTimes)
        {
            int earliestStart = 0;
            var predecessors = activity.PredecessorIds;
            if (predecessors == null)
                return earliestStart;

            for (int i = 0; i < predecessors.Count; i++)
            {
                int predecessorFinish;
                if (finishTimes.TryGetValue(predecessors[i], out predecessorFinish) && predecessorFinish > earliestStart)
                    earliestStart = predecessorFinish;
            }

            return earliestStart;
        }

        private static int FindEarliestFeasibleStart(
            SchedulingActivity activity,
            int earliestStart,
            int duration,
            Dictionary<int, int[]> usage,
            Dictionary<int, int> capacities,
            int horizon)
        {
            if (duration <= 0)
                return earliestStart;

            int latestStartToTry = Math.Max(earliestStart, horizon - duration);

            for (int start = Math.Max(0, earliestStart); start <= latestStartToTry; start++)
            {
                if (HasResourceFeasibility(activity, start, duration, usage, capacities, horizon))
                    return start;
            }

            return latestStartToTry;
        }

        private static bool HasResourceFeasibility(
            SchedulingActivity activity,
            int start,
            int duration,
            Dictionary<int, int[]> usage,
            Dictionary<int, int> capacities,
            int horizon)
        {
            if (duration <= 0)
                return true;

            var demandMap = activity.ResourceDemandByResourceId;
            if (demandMap == null || demandMap.Count == 0)
                return true;

            foreach (var kv in demandMap)
            {
                int resourceId = kv.Key;
                int demand = kv.Value;
                if (demand <= 0)
                    continue;

                int capacity;
                if (!capacities.TryGetValue(resourceId, out capacity))
                    continue;

                int[] resourceUsage;
                if (!usage.TryGetValue(resourceId, out resourceUsage))
                    continue;

                for (int t = start; t < start + duration; t++)
                {
                    if (t < 0 || t > horizon)
                        return false;

                    if (resourceUsage[t] + demand > capacity)
                        return false;
                }
            }

            return true;
        }

        private static void ReserveResources(
            SchedulingActivity activity,
            int start,
            int duration,
            Dictionary<int, int[]> usage,
            int horizon)
        {
            if (duration <= 0)
                return;

            var demandMap = activity.ResourceDemandByResourceId;
            if (demandMap == null || demandMap.Count == 0)
                return;

            foreach (var kv in demandMap)
            {
                int resourceId = kv.Key;
                int demand = kv.Value;
                if (demand <= 0)
                    continue;

                int[] resourceUsage;
                if (!usage.TryGetValue(resourceId, out resourceUsage))
                    continue;

                for (int t = start; t < start + duration; t++)
                {
                    if (t < 0 || t > horizon)
                        throw new InvalidOperationException("SerialForwardScheme exceeded the usage horizon.");

                    resourceUsage[t] += demand;
                }
            }
        }
    }
}
