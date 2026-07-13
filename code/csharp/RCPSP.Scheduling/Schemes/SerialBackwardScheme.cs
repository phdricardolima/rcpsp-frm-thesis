using RCPSP.Scheduling.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace RCPSP.Scheduling.Schemes
{
    public sealed class SerialBackwardScheme : IScheduleScheme
    {
        public string Name => "SERIAL_BACKWARD";
        public string Direction => "BACKWARD";

        public ScheduleComputationResult Compute(
            SchedulingProjectData projectData,
            List<SchedulingActivity> priorityOrderedActivities)
        {
            if (projectData == null)
                throw new ArgumentNullException(nameof(projectData));

            int n = projectData.GetNonSummaryActivities().Count;
            int numRec = projectData.Resources.Count;
            int constructionHorizon = GetConstructionHorizon(projectData);

            int[] dur = BuildRoundedDurations(projectData, n);

            int[] S = new int[n + 1];
            int[] F = new int[n + 1];

            int[,] usage = new int[numRec + 1, constructionHorizon + 1];

            List<int> backwardPriority = priorityOrderedActivities.Select(a => a.Id).ToList();
            HashSet<int> scheduled = new HashSet<int>();

            while (scheduled.Count < n)
            {
                int act = TrySelectEligibleActivity(projectData, scheduled, backwardPriority);
                if (act == -1)
                {
                    throw new InvalidOperationException(
                        "SERIAL_BACKWARD: empty backward eligible set. Check precedences and the priority list.");
                }

                int duration = dur[act];
                var successors = GetSuccessors(projectData, act);
                int latestFinish = successors.Count == 0
                    ? constructionHorizon
                    : successors.Select(s => S[s]).Min();

                bool placed = false;

                for (int finish = latestFinish; finish >= duration; finish--)
                {
                    int start = finish - duration;

                    if (start < 0)
                        continue;

                    if (!CheckSuccessorConsistency(projectData, act, finish, S, scheduled))
                        continue;

                    if (!CheckResources(projectData, act, start, finish, usage))
                        continue;

                    S[act] = start;
                    F[act] = finish;

                    AllocateResources(projectData, act, start, finish, usage);
                    scheduled.Add(act);
                    placed = true;
                    break;
                }

                if (!placed)
                {
                    throw new InvalidOperationException(
                        $"SERIAL_BACKWARD: no feasible position found for activity {act}");
                }
            }

            NormalizeBackwardSchedule(S, F, n);
            ApplySinglePassLocalLeftShiftFast(projectData, S, F, dur);

            var ordered = projectData.GetNonSummaryActivities()
                .Select(a => a.Id)
                .OrderBy(a => S[a])
                .ThenBy(a => a)
                .ToList();

            var startTimes = new Dictionary<int, int>();
            var finishTimes = new Dictionary<int, int>();

            foreach (int act in ordered)
            {
                startTimes[act] = S[act];
                finishTimes[act] = F[act];
            }


            var orderedFinal = ordered
                                    .OrderBy(a => startTimes.ContainsKey(a) ? startTimes[a] : int.MaxValue)
                                    .ThenBy(a => finishTimes.ContainsKey(a) ? finishTimes[a] : int.MaxValue)
                                    .ThenBy(a => a)
                                    .ToList();

            var startTimesFinal = orderedFinal.ToDictionary(a => a, a => startTimes[a]);
            var finishTimesFinal = orderedFinal.ToDictionary(a => a, a => finishTimes[a]);

            ordered = orderedFinal;
            startTimes = startTimesFinal;
            finishTimes = finishTimesFinal;


            return new ScheduleComputationResult
            {
                PriorityOrder = new List<int>(backwardPriority),
                ScheduledOrder = new List<int>(ordered),
                StartTimesByActivity = startTimes,
                FinishTimesByActivity = finishTimes,
                Makespan = ordered.Count == 0 ? 0 : ordered.Max(a => F[a])
            };
        }

        private int[] BuildRoundedDurations(SchedulingProjectData projectData, int n)
        {
            int[] dur = new int[n + 1];

            for (int act = 1; act <= n; act++)
            {
                int d = RoundedDuration(projectData, act);
                dur[act] = d < 1 ? 1 : d;
            }

            return dur;
        }

        private void ApplySinglePassLocalLeftShiftFast(
            SchedulingProjectData projectData,
            int[] S,
            int[] F,
            int[] dur)
        {
            var order = projectData.GetNonSummaryActivities()
                .Select(a => a.Id)
                .OrderBy(a => S[a])
                .ThenBy(a => a)
                .ToList();

            int horizon = Math.Max(F.Max() + 5, dur.Skip(1).Sum() + 5);
            int numRec = projectData.Resources.Count;
            int[,] usage = new int[numRec + 1, horizon + 1];

            foreach (int act in projectData.GetNonSummaryActivities().Select(a => a.Id))
                AddActivityToUsage(projectData, act, S[act], F[act], usage, +1);

            foreach (int act in order)
            {
                int earliestPred = 0;
                foreach (int pred in GetPredecessors(projectData, act))
                    earliestPred = Math.Max(earliestPred, F[pred]);

                int latestAllowedStart = S[act];

                if (GetSuccessors(projectData, act).Count > 0)
                {
                    int latestAllowedFinish = GetSuccessors(projectData, act)
                        .Select(suc => S[suc])
                        .Min();

                    latestAllowedStart = Math.Min(latestAllowedStart, latestAllowedFinish - dur[act]);
                }

                AddActivityToUsage(projectData, act, S[act], F[act], usage, -1);

                int bestStart = S[act];

                for (int candidateStart = earliestPred; candidateStart <= latestAllowedStart; candidateStart++)
                {
                    int candidateFinish = candidateStart + dur[act];

                    if (FitsResources(projectData, act, candidateStart, candidateFinish, usage))
                    {
                        bestStart = candidateStart;
                        break;
                    }
                }

                S[act] = bestStart;
                F[act] = bestStart + dur[act];

                AddActivityToUsage(projectData, act, S[act], F[act], usage, +1);
            }
        }

        private void AddActivityToUsage(
            SchedulingProjectData projectData,
            int act,
            int start,
            int finish,
            int[,] usage,
            int signal)
        {
            for (int t = start; t < finish; t++)
            {
                foreach (var resource in projectData.Resources)
                {
                    int r = resource.Id;
                    int demand = GetDemand(projectData, act, r);
                    usage[r, t] += signal * demand;
                }
            }
        }

        private bool FitsResources(
            SchedulingProjectData projectData,
            int act,
            int start,
            int finish,
            int[,] usage)
        {
            for (int t = start; t < finish; t++)
            {
                foreach (var resource in projectData.Resources)
                {
                    int r = resource.Id;
                    int demand = GetDemand(projectData, act, r);

                    if (usage[r, t] + demand > resource.Capacity)
                        return false;
                }
            }

            return true;
        }

        private void NormalizeBackwardSchedule(int[] S, int[] F, int n)
        {
            int minStart = int.MaxValue;

            for (int i = 1; i <= n; i++)
            {
                if (S[i] < minStart)
                    minStart = S[i];
            }

            for (int i = 1; i <= n; i++)
            {
                S[i] -= minStart;
                F[i] -= minStart;
            }
        }

        private int GetConstructionHorizon(SchedulingProjectData projectData)
        {
            int sumDur = projectData.GetNonSummaryActivities().Sum(a => Math.Max(1, a.Duration));
            return sumDur + 10;
        }


        private int TrySelectEligibleActivity(
            SchedulingProjectData projectData,
            HashSet<int> scheduled,
            List<int> backwardPriority)
        {
            for (int i = 0; i < backwardPriority.Count; i++)
            {
                int act = backwardPriority[i];
                if (scheduled.Contains(act))
                    continue;

                var successors = GetSuccessors(projectData, act);
                bool allSuccessorsScheduled = true;
                for (int s = 0; s < successors.Count; s++)
                {
                    if (!scheduled.Contains(successors[s]))
                    {
                        allSuccessorsScheduled = false;
                        break;
                    }
                }

                if (allSuccessorsScheduled)
                    return act;
            }

            return -1;
        }

        private bool CheckSuccessorConsistency(
            SchedulingProjectData projectData,
            int act,
            int proposedFinish,
            int[] S,
            HashSet<int> scheduled)
        {
            foreach (int suc in GetSuccessors(projectData, act))
            {
                if (!scheduled.Contains(suc))
                    continue;

                if (proposedFinish > S[suc])
                    return false;
            }

            return true;
        }

        private bool CheckResources(
            SchedulingProjectData projectData,
            int act,
            int start,
            int finish,
            int[,] usage)
        {
            for (int t = start; t < finish; t++)
            {
                foreach (var resource in projectData.Resources)
                {
                    int r = resource.Id;
                    int demand = GetDemand(projectData, act, r);
                    int used = usage[r, t];

                    if (used + demand > resource.Capacity)
                        return false;
                }
            }

            return true;
        }

        private void AllocateResources(
            SchedulingProjectData projectData,
            int act,
            int start,
            int finish,
            int[,] usage)
        {
            for (int t = start; t < finish; t++)
            {
                foreach (var resource in projectData.Resources)
                {
                    int r = resource.Id;
                    int demand = GetDemand(projectData, act, r);
                    usage[r, t] += demand;
                }
            }
        }

        private int GetDemand(SchedulingProjectData projectData, int act, int rec)
        {
            var a = projectData.GetActivity(act);
            if (a == null)
                return 0;

            int value;
            return a.ResourceDemandByResourceId.TryGetValue(rec, out value) ? value : 0;
        }

        private int RoundedDuration(SchedulingProjectData projectData, int act)
        {
            var a = projectData.GetActivity(act);
            int d = a == null ? 0 : a.Duration;
            return d < 1 ? 1 : d;
        }

        private List<int> GetPredecessors(SchedulingProjectData projectData, int act)
        {
            return projectData.GetActivity(act)?.PredecessorIds ?? new List<int>();
        }

        private List<int> GetSuccessors(SchedulingProjectData projectData, int act)
        {
            return projectData.GetActivity(act)?.SuccessorIds ?? new List<int>();
        }
    }
}
