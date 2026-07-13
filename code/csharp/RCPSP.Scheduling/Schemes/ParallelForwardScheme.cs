using System;
using System.Collections.Generic;
using System.Linq;
using RCPSP.Scheduling.Model;

namespace RCPSP.Scheduling.Schemes
{
    public sealed class ParallelForwardScheme : IScheduleScheme
    {
        public string Name => "PARALLEL_FORWARD";
        public string Direction => "FORWARD";

        public ScheduleComputationResult Compute(
            SchedulingProjectData projectData,
            List<SchedulingActivity> priorityOrderedActivities)
        {
            if (projectData == null)
                throw new ArgumentNullException(nameof(projectData));
            if (priorityOrderedActivities == null)
                throw new ArgumentNullException(nameof(priorityOrderedActivities));

            var regraLista = new List<int>(priorityOrderedActivities.Count);
            for (int i = 0; i < priorityOrderedActivities.Count; i++)
            {
                var activity = priorityOrderedActivities[i];
                if (activity != null)
                    regraLista.Add(activity.Id);
            }

            var result = RunParallelForward(projectData, regraLista);
            var ordered = new List<int>(result.ScheduleOrder);
            var startTimes = new Dictionary<int, int>(ordered.Count);
            var finishTimes = new Dictionary<int, int>(ordered.Count);

            for (int i = 0; i < ordered.Count; i++)
            {
                int actId = ordered[i];
                startTimes[actId] = result.StartTimes[actId];
                finishTimes[actId] = result.FinishTimesByActivity[actId];
            }

            int makespan = 0;
            foreach (var kv in finishTimes)
            {
                if (kv.Value > makespan)
                    makespan = kv.Value;
            }

            return new ScheduleComputationResult
            {
                PriorityOrder = new List<int>(regraLista),
                ScheduledOrder = ordered,
                StartTimesByActivity = startTimes,
                FinishTimesByActivity = finishTimes,
                Makespan = makespan
            };
        }

        internal static ParallelScheduleResult RunParallelForward(
            SchedulingProjectData projectData,
            List<int> priorityList)
        {
            var nonSummary = projectData.GetNonSummaryActivities();
            var activities = new List<int>(nonSummary.Count);
            var durByAct = new Dictionary<int, int>(nonSummary.Count);
            int horizon = 10;

            for (int i = 0; i < nonSummary.Count; i++)
            {
                var activity = nonSummary[i];
                if (activity == null)
                    continue;

                int actId = activity.Id;
                activities.Add(actId);

                int d = Math.Max(1, activity.Duration);
                durByAct[actId] = d;
                horizon += d;
            }

            if (horizon < 10)
                horizon = 10;

            var capacities = new Dictionary<int, int>(projectData.Resources.Count);
            var usage = new Dictionary<int, int[]>();
            for (int i = 0; i < projectData.Resources.Count; i++)
            {
                var resource = projectData.Resources[i];
                if (resource == null)
                    continue;

                int capacity = Math.Max(0, resource.Capacity);
                capacities[resource.Id] = capacity;
                usage[resource.Id] = new int[horizon + 1];
            }

            var priorityRank = new Dictionary<int, int>(priorityList.Count);
            for (int i = 0; i < priorityList.Count; i++)
            {
                int actId = priorityList[i];
                if (!priorityRank.ContainsKey(actId))
                    priorityRank.Add(actId, i);
            }

            int n = activities.Count;
            var scheduled = new HashSet<int>();
            var active = new HashSet<int>();
            var completed = new HashSet<int>();
            var start = new Dictionary<int, int>(n);
            var finish = new Dictionary<int, int>(n);
            int currentTime = 0;

            while (scheduled.Count < n)
            {
                var justFinished = new List<int>();
                foreach (var act in active)
                {
                    int actFinish;
                    if (finish.TryGetValue(act, out actFinish) && actFinish <= currentTime)
                        justFinished.Add(act);
                }

                for (int i = 0; i < justFinished.Count; i++)
                {
                    int act = justFinished[i];
                    active.Remove(act);
                    completed.Add(act);
                }

                var eligible = new List<int>();
                for (int i = 0; i < activities.Count; i++)
                {
                    int act = activities[i];
                    if (scheduled.Contains(act))
                        continue;
                    if (!AllPredecessorsCompleted(projectData, act, completed))
                        continue;

                    eligible.Add(act);
                }

                eligible.Sort((a, b) =>
                {
                    int ra;
                    if (!priorityRank.TryGetValue(a, out ra)) ra = int.MaxValue;
                    int rb;
                    if (!priorityRank.TryGetValue(b, out rb)) rb = int.MaxValue;

                    int cmp = ra.CompareTo(rb);
                    if (cmp != 0) return cmp;
                    return a.CompareTo(b);
                });

                bool inserted;
                do
                {
                    inserted = false;

                    for (int i = 0; i < eligible.Count; i++)
                    {
                        int act = eligible[i];
                        if (scheduled.Contains(act))
                            continue;

                        int duration = durByAct[act];
                        if (CanStartAtTime(projectData, act, currentTime, duration, usage, capacities, horizon))
                        {
                            start[act] = currentTime;
                            finish[act] = currentTime + duration;
                            scheduled.Add(act);
                            active.Add(act);
                            ReserveResources(projectData, act, currentTime, duration, usage, horizon);
                            eligible.RemoveAt(i);
                            i--;
                            inserted = true;
                        }
                    }
                }
                while (inserted);

                if (scheduled.Count == n && active.Count == 0)
                    break;

                if (active.Count > 0)
                {
                    int nextTime = int.MaxValue;
                    foreach (var act in active)
                    {
                        int actFinish = finish[act];
                        if (actFinish < nextTime)
                            nextTime = actFinish;
                    }

                    if (nextTime <= currentTime)
                        throw new InvalidOperationException("ParallelForward entered an invalid time advance.");

                    currentTime = nextTime;
                }
                else
                {
                    currentTime++;
                    if (currentTime > horizon * 5)
                    {
                        throw new InvalidOperationException(
                            "ParallelForward exceeded the time limit. Check precedences and resources.");
                    }
                }
            }

            if (scheduled.Count < n)
            {
                var missing = new List<int>();
                for (int i = 0; i < activities.Count; i++)
                {
                    int act = activities[i];
                    if (!scheduled.Contains(act))
                        missing.Add(act);
                }

                throw new InvalidOperationException(
                    "ParallelForward could not schedule all activities. " +
                    $"Escalonadas={scheduled.Count}/{n}. Faltando: {string.Join(",", missing)}");
            }

            var ordered = start.ToList();
            ordered.Sort((a, b) =>
            {
                int cmp = a.Value.CompareTo(b.Value);
                if (cmp != 0) return cmp;
                cmp = finish[a.Key].CompareTo(finish[b.Key]);
                if (cmp != 0) return cmp;
                return a.Key.CompareTo(b.Key);
            });

            var sg = new List<int>(ordered.Count);
            var fg = new List<double>(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
            {
                int actId = ordered[i].Key;
                sg.Add(actId);
                fg.Add(finish[actId]);
            }

            return new ParallelScheduleResult
            {
                StartTimes = start,
                FinishTimesByActivity = finish,
                ScheduleOrder = sg,
                FinishTimes = fg
            };
        }

        internal static bool AllPredecessorsCompleted(
            SchedulingProjectData projectData,
            int activityId,
            HashSet<int> completed)
        {
            var predecessors = GetPredecessors(projectData, activityId);
            for (int i = 0; i < predecessors.Count; i++)
            {
                if (!completed.Contains(predecessors[i]))
                    return false;
            }
            return true;
        }

        internal static bool CanStartAtTime(
            SchedulingProjectData projectData,
            int activityId,
            int proposedStart,
            int duration,
            Dictionary<int, int[]> usage,
            Dictionary<int, int> capacities,
            int horizon)
        {
            if (duration <= 0)
                return true;

            var demandMap = GetDemandMap(projectData, activityId);
            foreach (var demandKv in demandMap)
            {
                int resourceId = demandKv.Key;
                int demand = demandKv.Value;
                if (demand <= 0)
                    continue;

                int capacity;
                if (!capacities.TryGetValue(resourceId, out capacity))
                    continue;

                int[] resourceUsage;
                if (!usage.TryGetValue(resourceId, out resourceUsage))
                    continue;

                for (int tau = proposedStart; tau < proposedStart + duration; tau++)
                {
                    if (tau < 0 || tau > horizon)
                        return false;

                    if (resourceUsage[tau] + demand > capacity)
                        return false;
                }
            }

            return true;
        }

        internal static void ReserveResources(
            SchedulingProjectData projectData,
            int activityId,
            int proposedStart,
            int duration,
            Dictionary<int, int[]> usage,
            int horizon)
        {
            if (duration <= 0)
                return;

            var demandMap = GetDemandMap(projectData, activityId);
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
                        throw new InvalidOperationException("ParallelForward exceeded the resource usage horizon.");

                    resourceUsage[tau] += demand;
                }
            }
        }

        internal static Dictionary<int, int> GetDemandMap(SchedulingProjectData projectData, int act)
        {
            return projectData.GetActivity(act)?.ResourceDemandByResourceId ?? new Dictionary<int, int>();
        }

        internal static List<int> GetPredecessors(SchedulingProjectData projectData, int act)
        {
            return projectData.GetActivity(act)?.PredecessorIds ?? new List<int>();
        }

        internal static int RoundedDuration(SchedulingProjectData projectData, int act)
        {
            var a = projectData.GetActivity(act);
            return a == null ? 0 : Math.Max(1, a.Duration);
        }

        internal static double GetCapacity(SchedulingProjectData projectData, int resourceId)
        {
            var r = projectData.GetResource(resourceId);
            return r == null ? 0.0 : r.Capacity;
        }
    }

    internal sealed class ParallelScheduleResult
    {
        public Dictionary<int, int> StartTimes { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> FinishTimesByActivity { get; set; } = new Dictionary<int, int>();
        public List<int> ScheduleOrder { get; set; } = new List<int>();
        public List<double> FinishTimes { get; set; } = new List<double>();
    }
}
