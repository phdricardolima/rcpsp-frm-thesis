// Thesis traceability: Appendix A, Algorithm A.4 (FRM structural diagnosis: slack, score, balance, and SIF).
using System;
using System.Collections.Generic;
using System.Linq;
using RCPSP.Application;
using RCPSP.Contracts;

namespace RCPSP.Infrastructure.Cpu
{
    public sealed class CpuFrmCalculator : IFrmCalculator
    {
        public FrmResultDto Run(ProjectDataDto project, BaselineResultDto baseline, FrmOptionsDto options)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));
            if (baseline == null)
                throw new ArgumentNullException(nameof(baseline));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            double alphaPlus = NormalizePositivePercent(options.PositiveFlexibilityPercent);
            double alphaMinus = NormalizeNegativePercent(options.NegativeFlexibilityPercent);

            var activities = new List<ActivityDto>();
            foreach (var activity in project.Activities ?? new List<ActivityDto>())
            {
                if (activity != null && !activity.IsSummary)
                    activities.Add(activity);
            }
            activities.Sort((a, b) => a.Id.CompareTo(b.Id));

            var resources = new List<ResourceDto>();
            foreach (var resource in project.Resources ?? new List<ResourceDto>())
            {
                if (resource != null)
                    resources.Add(resource);
            }
            resources.Sort((a, b) => a.Id.CompareTo(b.Id));

            var activityMap = new Dictionary<int, ActivityDto>(activities.Count);
            foreach (var activity in activities)
                activityMap[activity.Id] = activity;

            var resourceIds = new List<int>(resources.Count);
            var resourceCapacity = new Dictionary<int, int>(resources.Count);
            foreach (var resource in resources)
            {
                resourceIds.Add(resource.Id);
                resourceCapacity[resource.Id] = Math.Max(0, (int)Math.Round(resource.Capacity, MidpointRounding.AwayFromZero));
            }

            var demandByActivity = BuildDemandByActivity(activities);
            EnsureSuccessors(activities, activityMap);

            int horizon = Math.Max(0, baseline.Makespan);
            var usageByResourceTime = BuildBaselineUsageProfile(baseline, activityMap, resourceIds, horizon);
            var successorMinStartByActivity = BuildSuccessorMinStartMap(activities, baseline, activityMap, horizon);

            var result = new FrmResultDto
            {
                Heuristic = ExtractHeuristic(baseline.RunLabel),
                Scheme = ExtractScheme(baseline.RunLabel),
                Direction = ExtractDirection(baseline.RunLabel),
                Makespan = baseline.Makespan,
                FlexPositivePercent = alphaPlus,
                FlexNegativePercent = alphaMinus
            };

            var balancePlusByResourceId = new Dictionary<int, int>(resourceIds.Count);
            foreach (int resId in resourceIds)
            {
                balancePlusByResourceId[resId] = 0;
                result.Balance0ByResourceId[resId] = 0;
            }

            var frmSequence = new List<RemainingItem>();
            foreach (int id in baseline.Sequence ?? new List<int>())
            {
                if (!activityMap.ContainsKey(id))
                    continue;

                frmSequence.Add(new RemainingItem
                {
                    ActivityId = id,
                    Start = GetOrDefault(baseline.StartTimesByActivity, id, 0)
                });
            }
            frmSequence.Sort((a, b) =>
            {
                int cmp = a.Start.CompareTo(b.Start);
                return cmp != 0 ? cmp : a.ActivityId.CompareTo(b.ActivityId);
            });


            foreach (var item in frmSequence)
            {
                int actId = item.ActivityId;
                ActivityDto act = activityMap[actId];

                int start = GetOrDefault(baseline.StartTimesByActivity, actId, 0);
                int finish = GetOrDefault(baseline.FinishTimesByActivity, actId, start);
                int dNom = Math.Max(0, finish - start);

                var row = CreateEmptyRow(act, start, finish, dNom, resourceIds);
                var demandMap = GetActivityDemandMap(demandByActivity, actId);

                int successorMinStart = GetOrDefault(successorMinStartByActivity, actId, horizon);
                int precedenceBound = Math.Max(0, successorMinStart - finish);

                int minSlack = int.MaxValue;
                bool hasPositiveDemand = false;

                foreach (int resId in resourceIds)
                {
                    int rik = GetDemand(demandMap, resId);
                    if (rik <= 0)
                    {
                        row.SlackIkByResourceId[resId] = 0;
                        continue;
                    }

                    hasPositiveDemand = true;
                    int ak = GetOrDefault(resourceCapacity, resId, 0);
                    int[] usage = usageByResourceTime[resId];

                    int tau = 0;
                    while (tau < precedenceBound)
                    {
                        int t = finish + tau;
                        if (t >= usage.Length)
                            break;

                        if (usage[t] + rik <= ak)
                            tau++;
                        else
                            break;
                    }

                    row.SlackIkByResourceId[resId] = tau;
                    if (tau < minSlack)
                        minSlack = tau;
                }

                row.SlackI = hasPositiveDemand && minSlack != int.MaxValue ? minSlack : 0;

                double dSup = dNom == 0 ? 0.0 : dNom / (1.0 - alphaMinus);
                int dMax = dNom == 0 ? 0 : (int)Math.Floor(dSup);

                double dInf = dNom == 0 ? 0.0 : dNom / (1.0 + alphaPlus);
                int dMin = dNom == 0 ? 0 : (int)Math.Ceiling(dInf);

                if (dMax < dMin)
                    dMax = dMin;

                int dSmax = Math.Min(dMax, dNom + row.SlackI);
                int dSmin = dMin;
                bool isCritical = hasPositiveDemand && row.SlackI <= 0 && dNom > 0;
                int durationBeforeBalance = !hasPositiveDemand
                    ? dNom
                    : (row.SlackI > 0 ? dSmax : dNom);

                row.DSup = dSup;
                row.DMax = dMax;
                row.DInf = dInf;
                row.DMin = dMin;
                row.DSMax = dSmax;
                row.DSMin = dSmin;
                row.IsCritical = isCritical;
                row.StructuralDurationBeforeBalance = durationBeforeBalance;
                row.StructuralDurationAfterBalance = durationBeforeBalance;
                row.DNew = durationBeforeBalance;
                row.DurationDecision = !hasPositiveDemand
                    ? "NO_RENEWABLE_RESOURCE_DEMAND"
                    : (row.SlackI > 0 ? "POSITIVE_RESERVE_GENERATOR" : "CRITICAL_PENDING_BALANCE_CHECK");

                foreach (int resId in resourceIds)
                {
                    int rik = GetDemand(demandMap, resId);
                    if (rik <= 0)
                        continue;

                    int rawScore;
                    if (row.SlackI > 0)
                    {
                        rawScore = rik * (durationBeforeBalance - dNom);
                        int generated = Math.Max(rawScore, 0);
                        row.BalanceGeneratedByResourceId[resId] = generated;
                        balancePlusByResourceId[resId] += generated;
                    }
                    else if (isCritical)
                    {


                        rawScore = rik * (dSmin - dNom);
                        row.BalanceRequestedByResourceId[resId] = Math.Max(-rawScore, 0);
                    }
                    else
                    {
                        rawScore = 0;
                    }

                    row.ScoreBrutoByResourceId[resId] = rawScore;
                }

                result.Sequence.Add(actId);
                result.Activities.Add(row);
            }

            foreach (int resId in resourceIds)
                result.Balance0ByResourceId[resId] = balancePlusByResourceId[resId];


            var runningBalance = new Dictionary<int, int>(resourceIds.Count);
            foreach (int resId in resourceIds)
                runningBalance[resId] = result.Balance0ByResourceId[resId];

            var rowByActivityId = new Dictionary<int, FrmActivityResultDto>(result.Activities.Count);
            foreach (var row in result.Activities)
                rowByActivityId[row.ActivityId] = row;

            foreach (int actId in result.Sequence)
            {
                FrmActivityResultDto row;
                if (!rowByActivityId.TryGetValue(actId, out row))
                    continue;

                var demandMap = GetActivityDemandMap(demandByActivity, actId);
                foreach (int resId in resourceIds)
                    row.BalanceBeforeByResourceId[resId] = runningBalance[resId];

                if (row.IsCritical)
                {
                    int requestedReduction = Math.Max(0, row.DurationNominal - row.DSMin);
                    int allowedReduction = requestedReduction;
                    int? limitingResourceId = null;

                    foreach (int resId in resourceIds)
                    {
                        int rik = GetDemand(demandMap, resId);
                        if (rik <= 0)
                            continue;

                        int resourceAllowedReduction = runningBalance[resId] / rik;
                        if (resourceAllowedReduction < allowedReduction)
                        {
                            allowedReduction = resourceAllowedReduction;
                            limitingResourceId = resId;
                        }
                        else if (resourceAllowedReduction == allowedReduction &&
                                 allowedReduction < requestedReduction &&
                                 (!limitingResourceId.HasValue || resId < limitingResourceId.Value))
                        {
                            limitingResourceId = resId;
                        }
                    }

                    allowedReduction = Math.Max(0, Math.Min(requestedReduction, allowedReduction));
                    int durationAfterBalance = row.DurationNominal - allowedReduction;
                    if (durationAfterBalance < row.DSMin)
                        durationAfterBalance = row.DSMin;

                    row.StructuralDurationAfterBalance = durationAfterBalance;
                    row.DNew = durationAfterBalance;
                    row.WasBalanceLimited = allowedReduction < requestedReduction;
                    row.LimitingResourceId = row.WasBalanceLimited ? limitingResourceId : null;
                    row.DurationDecision = requestedReduction == 0
                        ? "CRITICAL_NO_COMPRESSION_AVAILABLE"
                        : (row.WasBalanceLimited
                            ? "CRITICAL_COMPRESSION_LIMITED_BY_BALANCE"
                            : "CRITICAL_COMPRESSION_FULLY_FUNDED_BY_BALANCE");

                    foreach (int resId in resourceIds)
                    {
                        int rik = GetDemand(demandMap, resId);
                        int consumed = rik > 0 ? rik * allowedReduction : 0;
                        int availableBefore = runningBalance[resId];

                        if (consumed > availableBefore)
                        {
                            throw new InvalidOperationException(
                                "FRM Balance invariant violated for activity " + row.ActivityId +
                                " and resource " + resId + ": requested consumption " + consumed +
                                " exceeds available Balance " + availableBefore + ".");
                        }

                        row.BalanceConsumedByResourceId[resId] = consumed;
                        row.ScoreIkByResourceId[resId] = -consumed;
                        runningBalance[resId] = availableBefore - consumed;
                        row.BalanceByResourceId[resId] = runningBalance[resId];
                    }
                }
                else
                {
                    foreach (int resId in resourceIds)
                    {


                        int generated = row.BalanceGeneratedByResourceId != null &&
                                        row.BalanceGeneratedByResourceId.ContainsKey(resId)
                            ? row.BalanceGeneratedByResourceId[resId]
                            : 0;
                        row.ScoreIkByResourceId[resId] = generated;
                        row.BalanceConsumedByResourceId[resId] = 0;
                        row.BalanceByResourceId[resId] = runningBalance[resId];
                    }
                }
            }

            bool isStructurallyRobust = true;
            foreach (var row in result.Activities)
            {
                foreach (var value in row.BalanceByResourceId.Values)
                {
                    if (value < 0)
                    {
                        isStructurallyRobust = false;
                        break;
                    }
                }
                if (!isStructurallyRobust)
                    break;
            }
            result.IsStructurallyRobust = isStructurallyRobust;


            result.SifByResourceId = ComputeSifByResourceId(result, demandByActivity, resourceIds);
            double sifGlobal = 0.0;
            bool hasSif = false;
            foreach (var kv in result.SifByResourceId)
            {
                if (!hasSif || kv.Value < sifGlobal)
                {
                    sifGlobal = kv.Value;
                    hasSif = true;
                }
            }
            result.SifGlobal = hasSif ? sifGlobal : 0.0;

            result.ResourceDiagnostics = BuildDiagnostics(result, resources);
            result.SummaryText = BuildSummaryText(result, options);

            return result;
        }

        private static Dictionary<int, Dictionary<int, int>> BuildDemandByActivity(List<ActivityDto> activities)
        {
            var demandByActivity = new Dictionary<int, Dictionary<int, int>>(activities.Count);

            foreach (var activity in activities)
            {
                var demandMap = new Dictionary<int, int>();
                foreach (var assignment in activity.Assignments ?? new List<ResourceAssignmentDto>())
                {
                    if (assignment == null)
                        continue;

                    int demand = (int)Math.Round(assignment.Units, MidpointRounding.AwayFromZero);
                    if (demand <= 0)
                        continue;

                    int current;
                    if (demandMap.TryGetValue(assignment.ResourceId, out current))
                        demandMap[assignment.ResourceId] = current + demand;
                    else
                        demandMap[assignment.ResourceId] = demand;
                }

                demandByActivity[activity.Id] = demandMap;
            }

            return demandByActivity;
        }

        private static Dictionary<int, int> GetActivityDemandMap(Dictionary<int, Dictionary<int, int>> demandByActivity, int activityId)
        {
            Dictionary<int, int> demandMap;
            return demandByActivity.TryGetValue(activityId, out demandMap)
                ? demandMap
                : EmptyDemandMap.Instance;
        }

        private static void EnsureSuccessors(List<ActivityDto> activities, Dictionary<int, ActivityDto> byId)
        {
            var successorSets = new Dictionary<int, HashSet<int>>(activities.Count);
            foreach (var activity in activities)
            {
                var set = new HashSet<int>();
                foreach (int successorId in activity.SuccessorIds ?? new List<int>())
                    set.Add(successorId);
                successorSets[activity.Id] = set;
            }

            foreach (var activity in activities)
            {
                foreach (int predecessorId in activity.PredecessorIds ?? new List<int>())
                {
                    ActivityDto predecessor;
                    if (!byId.TryGetValue(predecessorId, out predecessor))
                        continue;

                    successorSets[predecessorId].Add(activity.Id);
                }
            }

            foreach (var activity in activities)
                activity.SuccessorIds = successorSets[activity.Id].OrderBy(x => x).ToList();
        }

        private static Dictionary<int, int[]> BuildBaselineUsageProfile(
            BaselineResultDto baseline,
            Dictionary<int, ActivityDto> activityMap,
            List<int> resourceIds,
            int horizon)
        {
            int safeHorizon = Math.Max(horizon, 1);

            var usageByResourceTime = new Dictionary<int, int[]>(resourceIds.Count);
            foreach (int resId in resourceIds)
                usageByResourceTime[resId] = new int[safeHorizon];

            foreach (int actId in baseline.Sequence ?? new List<int>())
            {
                ActivityDto act;
                if (!activityMap.TryGetValue(actId, out act))
                    continue;

                int start = GetOrDefault(baseline.StartTimesByActivity, actId, 0);
                int finish = GetOrDefault(baseline.FinishTimesByActivity, actId, start);

                if (finish <= start)
                    continue;

                foreach (var assignment in act.Assignments ?? new List<ResourceAssignmentDto>())
                {
                    if (assignment == null)
                        continue;

                    int[] usage;
                    if (!usageByResourceTime.TryGetValue(assignment.ResourceId, out usage))
                        continue;

                    int t0 = Math.Max(0, start);
                    int t1 = Math.Min(usage.Length, finish);
                    int demand = (int)Math.Round(assignment.Units, MidpointRounding.AwayFromZero);

                    for (int t = t0; t < t1; t++)
                        usage[t] += demand;
                }
            }

            return usageByResourceTime;
        }

        private static Dictionary<int, int> BuildSuccessorMinStartMap(
            List<ActivityDto> activities,
            BaselineResultDto baseline,
            Dictionary<int, ActivityDto> activityMap,
            int horizon)
        {
            var map = new Dictionary<int, int>(activities.Count);
            foreach (var act in activities)
                map[act.Id] = FindSuccessorMinStart(act, baseline, activityMap, horizon);
            return map;
        }

        private static FrmActivityResultDto CreateEmptyRow(
            ActivityDto act,
            int start,
            int finish,
            int dNom,
            List<int> resourceIds)
        {
            var row = new FrmActivityResultDto
            {
                ActivityId = act.Id,
                ActivityName = act.Name ?? string.Empty,
                Start = start,
                Finish = finish,
                DurationNominal = dNom
            };

            foreach (int resId in resourceIds)
            {
                row.SlackIkByResourceId[resId] = 0;
                row.ScoreBrutoByResourceId[resId] = 0;
                row.ScoreIkByResourceId[resId] = 0;
                row.BalanceBeforeByResourceId[resId] = 0;
                row.BalanceGeneratedByResourceId[resId] = 0;
                row.BalanceRequestedByResourceId[resId] = 0;
                row.BalanceConsumedByResourceId[resId] = 0;
                row.BalanceByResourceId[resId] = 0;
            }

            return row;
        }

        private static int FindSuccessorMinStart(
            ActivityDto act,
            BaselineResultDto baseline,
            Dictionary<int, ActivityDto> activityMap,
            int horizon)
        {
            if (act.SuccessorIds == null || act.SuccessorIds.Count == 0)
                return horizon;

            int minStart = int.MaxValue;

            foreach (int sucId in act.SuccessorIds)
            {
                if (!activityMap.ContainsKey(sucId))
                    continue;

                int start;
                if (baseline.StartTimesByActivity.TryGetValue(sucId, out start) && start < minStart)
                    minStart = start;
            }

            return minStart == int.MaxValue ? horizon : minStart;
        }

        private static int GetDemand(Dictionary<int, int> demandMap, int resId)
        {
            int value;
            return demandMap != null && demandMap.TryGetValue(resId, out value) ? value : 0;
        }

        private static Dictionary<int, double> ComputeSifByResourceId(
            FrmResultDto result,
            Dictionary<int, Dictionary<int, int>> demandByActivity,
            List<int> resourceIds)
        {
            var sif = new Dictionary<int, double>(resourceIds.Count);
            foreach (int resId in resourceIds)
                sif[resId] = 0.0;

            foreach (var row in result.Activities ?? new List<FrmActivityResultDto>())
            {
                var demandMap = GetActivityDemandMap(demandByActivity, row.ActivityId);
                foreach (int resId in resourceIds)
                {
                    int rik = GetDemand(demandMap, resId);
                    if (rik <= 0)
                        continue;

                    sif[resId] += rik * row.SlackI;
                }
            }

            return sif;
        }

        private static List<FrmResourceDiagnosticDto> BuildDiagnostics(
            FrmResultDto result,
            List<ResourceDto> resources)
        {
            var diagnostics = new List<FrmResourceDiagnosticDto>(resources.Count);

            foreach (var resource in resources)
            {
                int balance0 = result.Balance0ByResourceId.ContainsKey(resource.Id)
                    ? result.Balance0ByResourceId[resource.Id]
                    : 0;

                int finalBalance = balance0;
                bool isRobust = true;

                foreach (var row in result.Activities)
                {
                    int val;
                    if (row.BalanceByResourceId.TryGetValue(resource.Id, out val))
                    {
                        if (val < 0)
                            isRobust = false;

                        finalBalance = val;
                    }
                }

                double robustnessIndex =
                    balance0 > 0
                        ? (double)finalBalance / balance0
                        : (finalBalance >= 0 ? 1.0 : 0.0);

                double sif = result.SifByResourceId != null && result.SifByResourceId.ContainsKey(resource.Id)
                    ? result.SifByResourceId[resource.Id]
                    : 0.0;

                diagnostics.Add(new FrmResourceDiagnosticDto
                {
                    ResourceId = resource.Id,
                    ResourceName = resource.Name ?? string.Empty,
                    Balance0 = balance0,
                    BalanceFinal = finalBalance,
                    RobustnessIndex = robustnessIndex,
                    Sif = sif,
                    IsRobust = isRobust,
                    Classification = !isRobust
                        ? "Non-robust"
                        : (balance0 == 0 ? "No positive reserve" : "Robust")
                });
            }

            return diagnostics;
        }

        private static string BuildSummaryText(FrmResultDto result, FrmOptionsDto options)
        {
            int activityCount = result.Activities != null ? result.Activities.Count : 0;
            int criticalActivities = result.Activities != null ? result.Activities.Count(a => a != null && a.IsCritical) : 0;
            int balanceLimitedActivities = result.Activities != null ? result.Activities.Count(a => a != null && a.WasBalanceLimited) : 0;
            int totalBalanceGenerated = result.Activities == null
                ? 0
                : result.Activities.Sum(a => a == null || a.BalanceGeneratedByResourceId == null ? 0 : a.BalanceGeneratedByResourceId.Values.Sum());
            int totalBalanceConsumed = result.Activities == null
                ? 0
                : result.Activities.Sum(a => a == null || a.BalanceConsumedByResourceId == null ? 0 : a.BalanceConsumedByResourceId.Values.Sum());
            int robustResources = 0;
            int totalResources = result.ResourceDiagnostics != null ? result.ResourceDiagnostics.Count : 0;

            if (result.ResourceDiagnostics != null)
            {
                foreach (var diagnostic in result.ResourceDiagnostics)
                {
                    if (diagnostic != null && diagnostic.IsRobust)
                        robustResources++;
                }
            }

            return
                "FRM calculado com baseline estrutural.\r\n" +
                "Analyzed activities = " + activityCount + ", " +
                "Critical activities = " + criticalActivities + ", " +
                "Balance-limited critical activities = " + balanceLimitedActivities + ", " +
                "Balance generated = " + totalBalanceGenerated + ", " +
                "Balance consumed = " + totalBalanceConsumed + ", " +
                "Mode = " + (options.Mode ?? "NORMAL") + ", " +
                "Structural robustness = " + (result.IsStructurallyRobust ? "YES" : "NO") + ", " +
                "Recursos robustos = " + robustResources + "/" + totalResources + ".";
        }

        private static int GetOrDefault(Dictionary<int, int> map, int key, int fallback)
        {
            int value;
            return map != null && map.TryGetValue(key, out value) ? value : fallback;
        }

        private static double NormalizePositivePercent(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new InvalidOperationException("PositiveFlexibilityPercent is invalid.");
            if (value < 0.0)
                throw new InvalidOperationException("PositiveFlexibilityPercent cannot be negative.");

            return ConvertPercentIfNeeded(value);
        }

        private static double NormalizeNegativePercent(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new InvalidOperationException("NegativeFlexibilityPercent is invalid.");
            if (value < 0.0)
                throw new InvalidOperationException("NegativeFlexibilityPercent cannot be negative.");

            double normalized = ConvertPercentIfNeeded(value);

            if (normalized >= 1.0)
                throw new InvalidOperationException("NegativeFlexibilityPercent must be less than 100%.");

            return normalized;
        }

        private static double ConvertPercentIfNeeded(double value)
        {
            return value > 1.0 ? value / 100.0 : value;
        }

        private static string ExtractHeuristic(string runLabel)
        {
            return ExtractRunLabelPart(runLabel, 0);
        }

        private static string ExtractScheme(string runLabel)
        {
            return ExtractRunLabelPart(runLabel, 1);
        }

        private static string ExtractDirection(string runLabel)
        {
            return ExtractRunLabelPart(runLabel, 2);
        }

        private static string ExtractRunLabelPart(string runLabel, int index)
        {
            if (string.IsNullOrWhiteSpace(runLabel))
                return string.Empty;

            string[] parts = runLabel
                .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .ToArray();

            if (index < 0 || index >= parts.Length)
                return string.Empty;

            return parts[index];
        }

        private sealed class RemainingItem
        {
            public int ActivityId { get; set; }
            public int Start { get; set; }
        }

        private sealed class EmptyDemandMap
        {
            public static readonly Dictionary<int, int> Instance = new Dictionary<int, int>();
        }
    }
}
