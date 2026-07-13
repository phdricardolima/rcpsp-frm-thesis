// Thesis traceability: Appendix A, Algorithms A.5-A.6 (Monte Carlo risk and perturbation sensitivity).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RCPSP.Application;
using RCPSP.Contracts;

namespace RCPSP.Infrastructure.Cpu
{
    public sealed class CpuRiskAnalyzer : IRiskAnalyzer
    {
        private const string ModeFrmWorkContentBilateral = "FRM_WORKCONTENT_BILATERAL";
        private const string ModeDelayUnilateral = "DELAY_UNILATERAL";
        private const string ModeDelayStructural = "DELAY_STRUCTURAL";

        private static string NormalizeSamplingMode(string samplingMode)
        {
            if (string.Equals(samplingMode, ModeFrmWorkContentBilateral, StringComparison.OrdinalIgnoreCase))
                return ModeFrmWorkContentBilateral;
            if (string.Equals(samplingMode, ModeDelayStructural, StringComparison.OrdinalIgnoreCase))
                return ModeDelayStructural;
            if (string.Equals(samplingMode, "UNILATERAL_STRUCTURAL", StringComparison.OrdinalIgnoreCase))
                return ModeDelayUnilateral;
            return ModeDelayUnilateral;
        }

        private static bool IsFrmWorkContentBilateral(string samplingMode)
        {
            return string.Equals(samplingMode, ModeFrmWorkContentBilateral, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDelayStructural(string samplingMode)
        {
            return string.Equals(samplingMode, ModeDelayStructural, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDelayUnilateral(string samplingMode)
        {
            return string.Equals(samplingMode, ModeDelayUnilateral, StringComparison.OrdinalIgnoreCase)
                || string.Equals(samplingMode, "DELAY_UNILATERAL_STRUCTURAL", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDelayMode(string samplingMode)
        {
            return IsDelayUnilateral(samplingMode) || IsDelayStructural(samplingMode);
        }

        public RiskResultDto Run(ProjectDataDto project, BaselineResultDto baseline, FrmResultDto frm, RiskOptionsDto options)
        {
            var runtimeWatch = System.Diagnostics.Stopwatch.StartNew();
            var workOptions = options ?? new RiskOptionsDto();

            if (string.Equals(workOptions.SamplingMode, "UNILATERAL_STRUCTURAL", StringComparison.OrdinalIgnoreCase))
            {
                workOptions = CloneRiskOptions(workOptions);
                workOptions.SamplingMode = ModeDelayUnilateral;
                workOptions.RunPairedUnilateralStructural = true;
                workOptions.UseCommonRandomNumbers = true;
            }

            var primary = RunSingle(project, baseline, frm, workOptions, null);

            if (workOptions.RunPairedUnilateralStructural)
            {
                string oppositeMode = GetOppositeSamplingMode(primary.SamplingMode);
                var pairedOptions = CloneRiskOptions(workOptions);
                pairedOptions.SamplingMode = oppositeMode;
                pairedOptions.RunPairedUnilateralStructural = false;
                pairedOptions.UseCommonRandomNumbers = true;

                var pairedResult = RunSingle(project, baseline, frm, pairedOptions, null);

                if (string.Equals(primary.SamplingMode, ModeDelayUnilateral, StringComparison.OrdinalIgnoreCase))
                {
                    primary.PairedStructuralResult = pairedResult;
                    primary.PairedComparisonMode = "UNILATERAL_STRUCTURAL";
                }
                else
                {
                    primary.PairedUnilateralResult = pairedResult;
                    primary.PairedComparisonMode = "STRUCTURAL_UNILATERAL";
                }
            }

            runtimeWatch.Stop();
            primary.RuntimeMs = runtimeWatch.ElapsedMilliseconds;
            return primary;
        }

        private static string GetOppositeSamplingMode(string mode)
        {
            string normalized = NormalizeSamplingMode(mode);
            return normalized == ModeDelayUnilateral ? ModeDelayStructural : ModeDelayUnilateral;
        }

        private RiskResultDto RunSingle(ProjectDataDto project, BaselineResultDto baseline, FrmResultDto frm, RiskOptionsDto options, string samplingModeOverride)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));
            if (baseline == null)
                throw new ArgumentNullException(nameof(baseline));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            int scenarioCount = Math.Max(1, options.ScenarioCount);
            double gamma = NormalizeGamma(options.Gamma);
            int seed = options.Seed;
            int binCount = Math.Max(5, options.HistogramBinCount);
            string requestedSamplingMode = string.IsNullOrWhiteSpace(samplingModeOverride) ? options.SamplingMode : samplingModeOverride;
            string samplingMode = string.IsNullOrWhiteSpace(requestedSamplingMode)
                ? ModeFrmWorkContentBilateral
                : NormalizeSamplingMode(requestedSamplingMode);

            var activities = ExtractNonSummaryActivities(project);
            EnsureSuccessors(activities);

            var topo = TopologicalSort(activities);
            var baselineStart = baseline.StartTimesByActivity ?? new Dictionary<int, int>();
            var baselineFinish = baseline.FinishTimesByActivity ?? new Dictionary<int, int>();
            var activityOrder = BuildScenarioActivityOrder(baseline, topo);
            var frmMap = BuildFrmActivityMap(frm);

            var makespanSamples = new int[scenarioCount];
            var balanceRuptureFlags = new bool[scenarioCount];
            var balanceGeneratedSamples = new double[scenarioCount];
            var balanceConsumedSamples = new double[scenarioCount];
            var balanceUsageSamples = new double[scenarioCount];
            var balanceUsageRatioSamples = new double[scenarioCount];
            var minObservedBalanceSamples = new double[scenarioCount];
            var positiveWorkDemandSamples = new double[scenarioCount];
            var unabsorbedWorkSamples = new double[scenarioCount];
            var unabsorbedWorkRatioSamples = new double[scenarioCount];
            var resourceStatsByScenario = new Dictionary<int, ResourceScenarioStats>[scenarioCount];

            var scenarioScheduler = new RiskScenarioScheduler();
            var preparedProject = scenarioScheduler.Prepare(project, baselineFinish);
            bool useCommonRandomNumbers = options.UseCommonRandomNumbers;
            bool useFrmWorkContent = IsFrmWorkContentBilateral(samplingMode);

            Parallel.For(
                0,
                scenarioCount,
                new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount) },
                s =>
                {
                    int scenarioSeed = useCommonRandomNumbers
                        ? ComputeScenarioSeed(seed, s)
                        : ComputeScenarioSeed(seed + Environment.TickCount, s);

                    var rnd = new Random(scenarioSeed);
                    Dictionary<int, int> sampledDurations;
                    FrmWorkContentScenarioStats frmStats = null;

                    if (useFrmWorkContent)
                    {
                        frmStats = BuildFrmWorkContentScenario(topo, frmMap, gamma, rnd, frm);
                        sampledDurations = frmStats.EffectiveDurations;
                    }
                    else
                    {
                        sampledDurations = new Dictionary<int, int>(Math.Max(1, topo.Count));
                        for (int i = 0; i < topo.Count; i++)
                        {
                            var act = topo[i];
                            sampledDurations[act.Id] = SampleDuration(act, frmMap, gamma, rnd, samplingMode);
                        }
                    }

                    int sampledMakespan = scenarioScheduler.ScheduleMakespan(
                        preparedProject,
                        activityOrder,
                        sampledDurations,
                        baselineStart,
                        baselineFinish);

                    if (IsDelayMode(samplingMode) || useFrmWorkContent)
                        sampledMakespan = Math.Max(sampledMakespan, baseline.Makespan);

                    makespanSamples[s] = sampledMakespan;

                    if (frmStats != null)
                    {
                        balanceRuptureFlags[s] = frmStats.BalanceRuptured;
                        balanceGeneratedSamples[s] = frmStats.TotalBalanceGenerated;
                        balanceConsumedSamples[s] = frmStats.TotalBalanceConsumed;
                        balanceUsageSamples[s] = frmStats.TotalBalanceConsumed;
                        balanceUsageRatioSamples[s] = frmStats.BalanceUsageRatio;
                        minObservedBalanceSamples[s] = frmStats.MinObservedBalance;
                        positiveWorkDemandSamples[s] = frmStats.TotalPositiveWorkDemand;
                        unabsorbedWorkSamples[s] = frmStats.TotalUnabsorbedWork;
                        unabsorbedWorkRatioSamples[s] = frmStats.UnabsorbedWorkRatio;
                        resourceStatsByScenario[s] = frmStats.ByResource;
                    }
                });

            int[] unsortedMakespanSamples = (int[])makespanSamples.Clone();
            Array.Sort(makespanSamples);
            double meanMakespan = ComputeAverage(makespanSamples);
            var sampleList = new List<int>(makespanSamples);

            int referenceMakespan = baseline.Makespan;
            int[] delaySamples = BuildDelaySamples(makespanSamples, referenceMakespan);
            double cvar95Delay = CVar(delaySamples, 0.95);

            var result = new RiskResultDto
            {
                Iterations = scenarioCount,
                Gamma = gamma,
                Seed = seed,
                SamplingMode = samplingMode,
                ReferenceMakespan = referenceMakespan,
                MeanMakespan = meanMakespan,
                P50 = Percentile(makespanSamples, 0.50),
                P95 = Percentile(makespanSamples, 0.95),
                CVaR95 = cvar95Delay,
                MakespanCVaR95 = CVar(makespanSamples, 0.95),
                DelayProbability = ComputeDelayProbability(delaySamples),
                MeanDelay = ComputeAverage(delaySamples),
                P95Delay = Percentile(delaySamples, 0.95),
                CVaR95Delay = cvar95Delay,
                MaxDelay = delaySamples.Length == 0 ? 0.0 : delaySamples[delaySamples.Length - 1],
                MakespanSamples = sampleList
            };

            if (useFrmWorkContent)
            {
                PopulateFrmWorkContentMetrics(result, unsortedMakespanSamples, referenceMakespan, balanceRuptureFlags, balanceGeneratedSamples, balanceConsumedSamples, balanceUsageSamples, balanceUsageRatioSamples, minObservedBalanceSamples, positiveWorkDemandSamples, unabsorbedWorkSamples, unabsorbedWorkRatioSamples);
                result.ResourceAbsorption = AggregateResourceAbsorption(resourceStatsByScenario, scenarioCount);
            }

            BuildHistogram(result, makespanSamples, binCount);
            result.SummaryText = BuildSummaryText(result, frm, useFrmWorkContent);
            return result;
        }

        private static void PopulateFrmWorkContentMetrics(
            RiskResultDto result,
            int[] makespanSamplesByScenario,
            int referenceMakespan,
            bool[] ruptureFlags,
            double[] generated,
            double[] consumed,
            double[] usage,
            double[] usageRatio,
            double[] minBalance,
            double[] positiveWorkDemand,
            double[] unabsorbedWork,
            double[] unabsorbedWorkRatio)
        {
            if (result == null)
                return;

            int n = ruptureFlags != null ? ruptureFlags.Length : 0;
            if (n <= 0)
                return;

            int ruptureCount = 0;
            var ruptureDelays = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (!ruptureFlags[i])
                    continue;

                ruptureCount++;
                int makespan = (makespanSamplesByScenario != null && i < makespanSamplesByScenario.Length) ? makespanSamplesByScenario[i] : referenceMakespan;
                ruptureDelays.Add(Math.Max(0, makespan - referenceMakespan));
            }

            result.BalanceRuptureProbability = ruptureCount / (double)n;
            result.MeanBalanceGenerated = ComputeAverage(generated);
            result.MeanBalanceConsumed = ComputeAverage(consumed);
            result.MeanBalanceUsage = ComputeAverage(usage);
            result.MeanBalanceUsageRatio = ComputeAverage(usageRatio);
            result.MinObservedBalance = MinOrZero(minBalance);
            result.MeanPositiveWorkDemand = ComputeAverage(positiveWorkDemand);
            result.MeanUnabsorbedWork = ComputeAverage(unabsorbedWork);
            result.P95UnabsorbedWork = Percentile(SortedCopy(unabsorbedWork), 0.95);
            result.CVaR95UnabsorbedWork = CVar(SortedCopy(unabsorbedWork), 0.95);
            result.MeanUnabsorbedWorkRatio = ComputeAverage(unabsorbedWorkRatio);

            ruptureDelays.Sort();
            result.CVaR95GivenBalanceRupture = ruptureDelays.Count == 0 ? 0.0 : CVar(ruptureDelays.ToArray(), 0.95);
        }

        private static string BuildSummaryText(RiskResultDto result, FrmResultDto frm, bool isFrmWorkContent)
        {
            if (result == null)
                return string.Empty;

            string core =
                "Monte Carlo executed with " + result.Iterations + " scenarios, " +
                "Gamma = " + result.Gamma.ToString("0.###") + ", " +
                "Mean = " + result.MeanMakespan.ToString("0.##") + ", " +
                "P50 = " + result.P50.ToString("0.##") + ", " +
                "P95(Cmax) = " + result.P95.ToString("0.##") + ", " +
                "CVaR95(L) = " + result.CVaR95.ToString("0.##") + ", " +
                "CVaR95(Cmax) = " + result.MakespanCVaR95.ToString("0.##") + ". ";

            if (frm == null)
                return core + "FRM was not provided; nominal fallback sampling was applied.";

            if (isFrmWorkContent)
            {
                return core +
                    "Sampling mode = " + result.SamplingMode + ". " +
                    "This is the main FRM framework mode: Monte Carlo samples bilateral work-content deviations, " +
                    "FRM Balance absorbs feasible deviations, and only the non-absorbed excess is converted into effective duration increase. " +
                    "Balance rupture probability = " + result.BalanceRuptureProbability.ToString("0.###") + ", " +
                    "Mean Balance consumed = " + result.MeanBalanceConsumed.ToString("0.##") + ", " +
                    "Mean usage ratio = " + result.MeanBalanceUsageRatio.ToString("0.###") + ", " +
                    "Mean unabsorbed work = " + result.MeanUnabsorbedWork.ToString("0.##") + ", " +
                    "Unabsorbed work ratio = " + result.MeanUnabsorbedWorkRatio.ToString("0.###") + ", " +
                    "Min observed Balance = " + result.MinObservedBalance.ToString("0.##") + ".";
            }

            return core +
                "Monte Carlo evaluates the FRM-conditioned schedule space. Sampling mode = " + result.SamplingMode + ". " +
                "In DELAY_UNILATERAL mode, simulated durations do not fall below the nominal duration and the upper perturbation bound uses DMax. " +
                "In DELAY_STRUCTURAL mode, the upper perturbation bound uses DSMax for ablation.";
        }

        private sealed class ResourceScenarioStats
        {
            public double Generated;
            public double Consumed;
            public double Demand;
            public double Unabsorbed;
            public bool Ruptured;
            public double MinBalance;
        }

        private sealed class FrmWorkContentScenarioStats
        {
            public readonly Dictionary<int, int> EffectiveDurations = new Dictionary<int, int>();
            public readonly Dictionary<int, ResourceScenarioStats> ByResource = new Dictionary<int, ResourceScenarioStats>();
            public bool BalanceRuptured;
            public double TotalBalanceGenerated;
            public double TotalBalanceConsumed;
            public double BalanceUsageRatio;
            public double MinObservedBalance;
            public double TotalPositiveWorkDemand;
            public double TotalUnabsorbedWork;
            public double UnabsorbedWorkRatio;
        }

        private static FrmWorkContentScenarioStats BuildFrmWorkContentScenario(
            List<ActivityDto> topo,
            Dictionary<int, FrmActivityResultDto> frmMap,
            double gamma,
            Random rnd,
            FrmResultDto frm)
        {
            var stats = new FrmWorkContentScenarioStats();
            var balances = new Dictionary<int, double>();
            var actualEquivalentDurationByActivity = new Dictionary<int, double>();
            var projectDemandByActivity = BuildProjectDemandByActivity(topo);

            double alphaPlus = frm != null ? Math.Max(0.0, frm.FlexPositivePercent / 100.0) : 0.25;
            double alphaMinus = frm != null ? Math.Max(0.0, frm.FlexNegativePercent / 100.0) : 0.25;
            if (alphaPlus > 10.0) alphaPlus = 10.0;
            if (alphaMinus >= 1.0) alphaMinus = 0.999;


            for (int i = 0; i < topo.Count; i++)
            {
                var act = topo[i];
                if (act == null)
                    continue;

                if (string.Equals(act.ExecutionState, "Completed", StringComparison.OrdinalIgnoreCase))
                {
                    stats.EffectiveDurations[act.Id] = 0;
                    actualEquivalentDurationByActivity[act.Id] = 0.0;
                    continue;
                }

                FrmActivityResultDto frmActivity;
                frmMap.TryGetValue(act.Id, out frmActivity);

                int nominal = GetNominalDuration(act, frmActivity);
                if (nominal <= 0)
                {
                    stats.EffectiveDurations[act.Id] = 0;
                    actualEquivalentDurationByActivity[act.Id] = 0.0;
                    continue;
                }

                double delta = SampleTriangularDouble(-gamma, 0.0, gamma, rnd);
                double actualEquivalentDuration = Math.Max(0.0, nominal * (1.0 + delta));
                actualEquivalentDurationByActivity[act.Id] = actualEquivalentDuration;

                int slack = frmActivity != null ? Math.Max(0, frmActivity.SlackI) : 0;
                int dsMax = frmActivity != null && frmActivity.DSMax > 0 ? frmActivity.DSMax : nominal;
                if (dsMax < nominal)
                    dsMax = nominal;

                bool nonCritical = slack > 0 && dsMax > nominal;
                if (!nonCritical)
                {
                    stats.EffectiveDurations[act.Id] = nominal;
                    continue;
                }

                int plannedWorkingDuration = dsMax;
                var demandMap = GetDemandMap(projectDemandByActivity, act.Id);
                foreach (var kv in demandMap)
                {
                    int resourceId = kv.Key;
                    int demand = kv.Value;
                    if (demand <= 0)
                        continue;

                    double generated = demand * Math.Max(0.0, plannedWorkingDuration - actualEquivalentDuration);
                    if (generated > 0.0)
                    {
                        AddToDictionary(balances, resourceId, generated);
                        stats.TotalBalanceGenerated += generated;
                        GetResourceStats(stats, resourceId).Generated += generated;
                    }
                }

                double residualBeyondWorkingDuration = Math.Max(0.0, actualEquivalentDuration - plannedWorkingDuration);
                if (residualBeyondWorkingDuration > 1e-9)
                {
                    foreach (var kv in demandMap)
                    {
                        int demand = kv.Value;
                        if (demand <= 0)
                            continue;

                        double residualWork = demand * residualBeyondWorkingDuration;
                        stats.TotalPositiveWorkDemand += residualWork;
                        stats.TotalUnabsorbedWork += residualWork;
                        var resourceStats = GetResourceStats(stats, kv.Key);
                        resourceStats.Demand += residualWork;
                        resourceStats.Unabsorbed += residualWork;
                        resourceStats.Ruptured = true;
                    }
                }

                int residualPeriods = residualBeyondWorkingDuration <= 1e-9 ? 0 : (int)Math.Ceiling(residualBeyondWorkingDuration);
                stats.EffectiveDurations[act.Id] = plannedWorkingDuration + residualPeriods;
                if (residualPeriods > 0)
                    stats.BalanceRuptured = true;
            }

            stats.MinObservedBalance = balances.Count == 0 ? 0.0 : balances.Values.Min();


            for (int i = 0; i < topo.Count; i++)
            {
                var act = topo[i];
                if (act == null)
                    continue;

                FrmActivityResultDto frmActivity;
                frmMap.TryGetValue(act.Id, out frmActivity);

                int nominal = GetNominalDuration(act, frmActivity);
                if (nominal <= 0)
                    continue;

                int slack = frmActivity != null ? Math.Max(0, frmActivity.SlackI) : 0;
                int dsMax = frmActivity != null && frmActivity.DSMax > 0 ? frmActivity.DSMax : nominal;
                if (dsMax < nominal)
                    dsMax = nominal;

                bool nonCritical = slack > 0 && dsMax > nominal;
                if (nonCritical)
                    continue;

                double actualEquivalentDuration;
                if (!actualEquivalentDurationByActivity.TryGetValue(act.Id, out actualEquivalentDuration))
                    actualEquivalentDuration = nominal;

                double extraEquivalentDuration = Math.Max(0.0, actualEquivalentDuration - nominal);
                if (extraEquivalentDuration <= 1e-9)
                {
                    stats.EffectiveDurations[act.Id] = nominal;
                    continue;
                }

                var demandMap = GetDemandMap(projectDemandByActivity, act.Id);
                double maxResidualPeriods = 0.0;
                bool hasDemand = false;

                foreach (var kv in demandMap)
                {
                    int resourceId = kv.Key;
                    int demand = kv.Value;
                    if (demand <= 0)
                        continue;

                    hasDemand = true;
                    double requiredExtraWork = demand * extraEquivalentDuration;
                    stats.TotalPositiveWorkDemand += requiredExtraWork;
                    var resourceStats = GetResourceStats(stats, resourceId);
                    resourceStats.Demand += requiredExtraWork;
                    double physicalAbsorptionLimit = demand * nominal * alphaPlus;
                    double availableBalance = GetOrDefault(balances, resourceId, 0.0);
                    double absorbed = Math.Min(requiredExtraWork, Math.Min(availableBalance, physicalAbsorptionLimit));
                    if (absorbed > 0.0)
                    {
                        balances[resourceId] = availableBalance - absorbed;
                        stats.TotalBalanceConsumed += absorbed;
                        resourceStats.Consumed += absorbed;
                    }

                    double residualWork = requiredExtraWork - absorbed;
                    if (residualWork > 1e-9)
                    {
                        stats.BalanceRuptured = true;
                        stats.TotalUnabsorbedWork += residualWork;
                        resourceStats.Unabsorbed += residualWork;
                        resourceStats.Ruptured = true;
                        double residualPeriods = residualWork / demand;
                        if (residualPeriods > maxResidualPeriods)
                            maxResidualPeriods = residualPeriods;
                    }

                    double currentBalance = GetOrDefault(balances, resourceId, 0.0);
                    if (currentBalance < stats.MinObservedBalance)
                        stats.MinObservedBalance = currentBalance;
                    if (resourceStats.MinBalance == 0.0 || currentBalance < resourceStats.MinBalance)
                        resourceStats.MinBalance = currentBalance;
                }

                if (!hasDemand)
                {
                    stats.EffectiveDurations[act.Id] = nominal;
                    continue;
                }

                int residualDuration = maxResidualPeriods <= 1e-9 ? 0 : (int)Math.Ceiling(maxResidualPeriods);
                stats.EffectiveDurations[act.Id] = nominal + residualDuration;
            }

            if (stats.TotalBalanceGenerated > 0.0)
                stats.BalanceUsageRatio = stats.TotalBalanceConsumed / stats.TotalBalanceGenerated;
            else
                stats.BalanceUsageRatio = stats.TotalBalanceConsumed > 0.0 ? 1.0 : 0.0;

            if (stats.TotalPositiveWorkDemand > 0.0)
                stats.UnabsorbedWorkRatio = stats.TotalUnabsorbedWork / stats.TotalPositiveWorkDemand;
            else
                stats.UnabsorbedWorkRatio = 0.0;

            return stats;
        }

        private static ResourceScenarioStats GetResourceStats(FrmWorkContentScenarioStats stats, int resourceId)
        {
            ResourceScenarioStats value;
            if (!stats.ByResource.TryGetValue(resourceId, out value))
            {
                value = new ResourceScenarioStats();
                stats.ByResource[resourceId] = value;
            }
            return value;
        }

        private static List<ResourceAbsorptionMetricDto> AggregateResourceAbsorption(
            Dictionary<int, ResourceScenarioStats>[] scenarios, int scenarioCount)
        {
            var ids = new HashSet<int>();
            foreach (var scenario in scenarios ?? new Dictionary<int, ResourceScenarioStats>[0])
                if (scenario != null) foreach (int id in scenario.Keys) ids.Add(id);

            var result = new List<ResourceAbsorptionMetricDto>();
            foreach (int id in ids.OrderBy(x => x))
            {
                double generated = 0.0, consumed = 0.0, demand = 0.0, unabsorbed = 0.0;
                double minBalance = 0.0;
                int rupture = 0;
                bool hasMin = false;
                for (int i = 0; i < scenarioCount; i++)
                {
                    ResourceScenarioStats value;
                    if (scenarios[i] == null || !scenarios[i].TryGetValue(id, out value))
                        continue;
                    generated += value.Generated;
                    consumed += value.Consumed;
                    demand += value.Demand;
                    unabsorbed += value.Unabsorbed;
                    if (value.Ruptured) rupture++;
                    if (!hasMin || value.MinBalance < minBalance) { minBalance = value.MinBalance; hasMin = true; }
                }
                double n = Math.Max(1, scenarioCount);
                double meanDemand = demand / n;
                result.Add(new ResourceAbsorptionMetricDto
                {
                    ResourceId = id,
                    MeanBalanceGenerated = generated / n,
                    MeanBalanceConsumed = consumed / n,
                    MeanPositiveWorkDemand = meanDemand,
                    MeanUnabsorbedWork = unabsorbed / n,
                    MeanUnabsorbedWorkRatio = meanDemand > 1e-12 ? (unabsorbed / n) / meanDemand : 0.0,
                    RuptureProbability = rupture / n,
                    MinObservedBalance = hasMin ? minBalance : 0.0
                });
            }
            return result;
        }

        private static int GetNominalDuration(ActivityDto act, FrmActivityResultDto frmActivity)
        {
            if (act == null)
                return 0;

            if (string.Equals(act.ExecutionState, "InProgress", StringComparison.OrdinalIgnoreCase) && act.RemainingDurationDays.HasValue)
                return Math.Max(0, act.RemainingDurationDays.Value);

            if (frmActivity != null && frmActivity.DurationNominal > 0)
                return frmActivity.DurationNominal;

            return Math.Max(0, act.DurationDays);
        }

        private static Dictionary<int, Dictionary<int, int>> BuildProjectDemandByActivity(List<ActivityDto> activities)
        {
            var result = new Dictionary<int, Dictionary<int, int>>();
            if (activities == null)
                return result;

            for (int i = 0; i < activities.Count; i++)
            {
                var activity = activities[i];
                if (activity == null)
                    continue;

                var map = new Dictionary<int, int>();
                foreach (var assignment in activity.Assignments ?? new List<ResourceAssignmentDto>())
                {
                    if (assignment == null)
                        continue;

                    int demand = (int)Math.Round(assignment.Units, MidpointRounding.AwayFromZero);
                    if (demand <= 0)
                        continue;

                    int current;
                    if (map.TryGetValue(assignment.ResourceId, out current))
                        map[assignment.ResourceId] = current + demand;
                    else
                        map[assignment.ResourceId] = demand;
                }

                result[activity.Id] = map;
            }

            return result;
        }

        private static Dictionary<int, int> GetDemandMap(Dictionary<int, Dictionary<int, int>> demandByActivity, int activityId)
        {
            Dictionary<int, int> map;
            return demandByActivity != null && demandByActivity.TryGetValue(activityId, out map)
                ? map
                : EmptyDemandMap.Instance;
        }

        private sealed class EmptyDemandMap
        {
            public static readonly Dictionary<int, int> Instance = new Dictionary<int, int>();
        }

        private static RiskOptionsDto CloneRiskOptions(RiskOptionsDto source)
        {
            if (source == null)
                return new RiskOptionsDto();

            return new RiskOptionsDto
            {
                ScenarioCount = source.ScenarioCount,
                Gamma = source.Gamma,
                Seed = source.Seed,
                Enabled = source.Enabled,
                HistogramBinCount = source.HistogramBinCount,
                SamplingMode = source.SamplingMode,
                UseCommonRandomNumbers = source.UseCommonRandomNumbers,
                RunPairedUnilateralStructural = source.RunPairedUnilateralStructural
            };
        }

        private static int ComputeScenarioSeed(int baseSeed, int scenarioIndex)
        {
            unchecked
            {
                int h = baseSeed == 0 ? 104729 : baseSeed;
                h = (h * 397) ^ (scenarioIndex + 1);
                h = (h * 397) ^ 0x5f3759df;
                return h == int.MinValue ? int.MaxValue : Math.Abs(h);
            }
        }

        private static List<ActivityDto> ExtractNonSummaryActivities(ProjectDataDto project)
        {
            var source = project.Activities ?? new List<ActivityDto>();
            var activities = new List<ActivityDto>(source.Count);

            for (int i = 0; i < source.Count; i++)
            {
                var activity = source[i];
                if (activity == null || activity.IsSummary)
                    continue;

                activities.Add(activity);
            }

            activities.Sort((a, b) => a.Id.CompareTo(b.Id));
            return activities;
        }

        private static int[] BuildDelaySamples(int[] makespanSamplesByScenario, int referenceMakespan)
        {
            if (makespanSamplesByScenario == null || makespanSamplesByScenario.Length == 0)
                return new int[0];

            var delays = new int[makespanSamplesByScenario.Length];
            for (int i = 0; i < makespanSamplesByScenario.Length; i++)
                delays[i] = Math.Max(0, makespanSamplesByScenario[i] - referenceMakespan);

            Array.Sort(delays);
            return delays;
        }

        private static double ComputeDelayProbability(int[] sortedDelaySamples)
        {
            if (sortedDelaySamples == null || sortedDelaySamples.Length == 0)
                return 0.0;

            int positive = 0;
            for (int i = 0; i < sortedDelaySamples.Length; i++)
            {
                if (sortedDelaySamples[i] > 0)
                    positive++;
            }

            return positive / (double)sortedDelaySamples.Length;
        }

        private static double NormalizeGamma(double gamma)
        {
            if (double.IsNaN(gamma) || double.IsInfinity(gamma))
                throw new InvalidOperationException("Gamma is invalid.");
            if (gamma < 0.0)
                return 0.0;
            if (gamma > 1.0)
                return 1.0;
            return gamma;
        }

        private static void EnsureSuccessors(List<ActivityDto> activities)
        {
            var byId = new Dictionary<int, ActivityDto>(activities.Count);
            for (int i = 0; i < activities.Count; i++)
            {
                var activity = activities[i];
                byId[activity.Id] = activity;
                if (activity.SuccessorIds == null)
                    activity.SuccessorIds = new List<int>();
            }

            for (int i = 0; i < activities.Count; i++)
            {
                var activity = activities[i];
                if (activity.PredecessorIds == null || activity.PredecessorIds.Count == 0)
                    continue;

                var seenPreds = new HashSet<int>();
                for (int p = 0; p < activity.PredecessorIds.Count; p++)
                {
                    int predecessorId = activity.PredecessorIds[p];
                    if (!seenPreds.Add(predecessorId))
                        continue;

                    ActivityDto predecessor;
                    if (!byId.TryGetValue(predecessorId, out predecessor))
                        continue;

                    if (predecessor.SuccessorIds == null)
                        predecessor.SuccessorIds = new List<int>();

                    if (!predecessor.SuccessorIds.Contains(activity.Id))
                        predecessor.SuccessorIds.Add(activity.Id);
                }
            }
        }

        private static List<ActivityDto> TopologicalSort(List<ActivityDto> activities)
        {
            var byId = new Dictionary<int, ActivityDto>(activities.Count);
            var indegree = new Dictionary<int, int>(activities.Count);

            for (int i = 0; i < activities.Count; i++)
            {
                var activity = activities[i];
                byId[activity.Id] = activity;
                indegree[activity.Id] = 0;
            }

            for (int i = 0; i < activities.Count; i++)
            {
                var activity = activities[i];
                var preds = activity.PredecessorIds;
                if (preds == null)
                    continue;

                for (int p = 0; p < preds.Count; p++)
                {
                    if (byId.ContainsKey(preds[p]))
                        indegree[activity.Id]++;
                }
            }

            var ready = new SortedSet<int>();
            foreach (var kv in indegree)
            {
                if (kv.Value == 0)
                    ready.Add(kv.Key);
            }

            var result = new List<ActivityDto>(activities.Count);
            while (ready.Count > 0)
            {
                int id = ready.Min;
                ready.Remove(id);
                var current = byId[id];
                result.Add(current);

                var successors = current.SuccessorIds;
                if (successors == null)
                    continue;

                for (int i = 0; i < successors.Count; i++)
                {
                    int sucId = successors[i];
                    if (!indegree.ContainsKey(sucId))
                        continue;

                    indegree[sucId]--;
                    if (indegree[sucId] == 0)
                        ready.Add(sucId);
                }
            }

            if (result.Count != activities.Count)
                throw new InvalidOperationException("The project network contains a cycle or inconsistent precedence data.");

            return result;
        }

        private static List<int> BuildScenarioActivityOrder(BaselineResultDto baseline, List<ActivityDto> topo)
        {
            if (baseline != null)
            {
                if (baseline.ScheduledOrder != null && baseline.ScheduledOrder.Count > 0)
                    return DistinctPreserveOrder(baseline.ScheduledOrder);

                if (baseline.Sequence != null && baseline.Sequence.Count > 0)
                    return DistinctPreserveOrder(baseline.Sequence);

                if (baseline.Activities != null && baseline.Activities.Count > 0)
                {
                    var orderedActivities = new List<ScheduledActivityDto>(baseline.Activities);
                    orderedActivities.Sort((a, b) =>
                    {
                        int cmp = a.Start.CompareTo(b.Start);
                        if (cmp != 0) return cmp;
                        return a.ActivityId.CompareTo(b.ActivityId);
                    });

                    var result = new List<int>(orderedActivities.Count);
                    var seen = new HashSet<int>();
                    for (int i = 0; i < orderedActivities.Count; i++)
                    {
                        int id = orderedActivities[i].ActivityId;
                        if (seen.Add(id))
                            result.Add(id);
                    }
                    return result;
                }
            }

            var topoIds = new List<int>(topo.Count);
            for (int i = 0; i < topo.Count; i++)
                topoIds.Add(topo[i].Id);
            return topoIds;
        }

        private static List<int> DistinctPreserveOrder(List<int> source)
        {
            var result = new List<int>(source.Count);
            var seen = new HashSet<int>();
            for (int i = 0; i < source.Count; i++)
            {
                int value = source[i];
                if (seen.Add(value))
                    result.Add(value);
            }
            return result;
        }

        private static int SampleDuration(ActivityDto activity, Dictionary<int, FrmActivityResultDto> frmMap, double gamma, Random rnd, string samplingMode)
        {
            if (activity == null)
                return 0;

            if (string.Equals(activity.ExecutionState, "Completed", StringComparison.OrdinalIgnoreCase))
                return 0;

            if (string.Equals(activity.ExecutionState, "InProgress", StringComparison.OrdinalIgnoreCase) && activity.RemainingDurationDays.HasValue)
            {
                return IsDelayMode(samplingMode)
                    ? SampleDelayUnilateral(Math.Max(0, activity.RemainingDurationDays.Value), Math.Max(0, activity.RemainingDurationDays.Value), gamma, rnd)
                    : SampleAroundNominal(Math.Max(0, activity.RemainingDurationDays.Value), gamma, rnd);
            }

            FrmActivityResultDto frmActivity;
            if (frmMap != null && frmMap.TryGetValue(activity.Id, out frmActivity))
                return SampleFromFrmBounds(frmActivity, activity, gamma, rnd, samplingMode);

            return IsDelayMode(samplingMode)
                ? SampleDelayUnilateral(Math.Max(0, activity.DurationDays), Math.Max(0, activity.DurationDays), gamma, rnd)
                : SampleAroundNominal(Math.Max(0, activity.DurationDays), gamma, rnd);
        }

        private static Dictionary<int, FrmActivityResultDto> BuildFrmActivityMap(FrmResultDto frm)
        {
            var map = new Dictionary<int, FrmActivityResultDto>();
            if (frm == null || frm.Activities == null)
                return map;

            for (int i = 0; i < frm.Activities.Count; i++)
            {
                var activity = frm.Activities[i];
                if (activity == null)
                    continue;

                if (!map.ContainsKey(activity.ActivityId))
                    map.Add(activity.ActivityId, activity);
            }
            return map;
        }

        private static int SampleFromFrmBounds(FrmActivityResultDto frmActivity, ActivityDto projectActivity, double gamma, Random rnd, string samplingMode)
        {
            if (frmActivity == null)
            {
                int fallbackNominal = Math.Max(0, projectActivity.DurationDays);
                return IsDelayMode(samplingMode)
                    ? SampleDelayUnilateral(fallbackNominal, fallbackNominal, gamma, rnd)
                    : SampleAroundNominal(fallbackNominal, gamma, rnd);
            }

            int nominal = frmActivity.DurationNominal > 0 ? frmActivity.DurationNominal : Math.Max(0, projectActivity.DurationDays);
            if (nominal <= 0)
                return 0;

            int dsMin = frmActivity.DSMin;
            int dsMax = frmActivity.DSMax;
            int dNew = frmActivity.DNew;

            if (dsMin <= 0 && dsMax <= 0)
                return IsDelayMode(samplingMode)
                    ? SampleDelayUnilateral(nominal, nominal, gamma, rnd)
                    : SampleAroundNominal(nominal, gamma, rnd);

            if (dsMin <= 0) dsMin = nominal;
            if (dsMax <= 0) dsMax = nominal;
            if (dsMin > nominal) dsMin = nominal;
            if (dsMax < nominal) dsMax = nominal;

            if (gamma <= 0.0)
                return nominal;

            if (IsDelayMode(samplingMode))
            {
                int perturbationUpper = IsDelayStructural(samplingMode)
                    ? Math.Max(nominal, dsMax)
                    : Math.Max(nominal, frmActivity.DMax);
                return SampleDelayUnilateral(nominal, perturbationUpper, gamma, rnd);
            }

            int operationalMin = ComputeOperationalLowerBound(nominal, dsMin, gamma);
            int operationalMax = ComputeOperationalUpperBound(nominal, dsMax, gamma);

            if (operationalMin < dsMin) operationalMin = dsMin;
            if (operationalMin > nominal) operationalMin = nominal;
            if (operationalMax < nominal) operationalMax = nominal;
            if (operationalMax > dsMax) operationalMax = dsMax;
            if (operationalMax < operationalMin) operationalMax = operationalMin;
            if (operationalMin == operationalMax) return operationalMin;

            if (dNew <= 0) dNew = nominal;
            if (dNew < operationalMin) dNew = operationalMin;
            if (dNew > operationalMax) dNew = operationalMax;

            return SampleTriangular(operationalMin, dNew, operationalMax, rnd);
        }

        private static int ComputeOperationalLowerBound(int nominal, int dsMin, double gamma)
        {
            double value = nominal - gamma * (nominal - dsMin);
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        private static int ComputeOperationalUpperBound(int nominal, int dsMax, double gamma)
        {
            double value = nominal + gamma * (dsMax - nominal);
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        private static int SampleTriangular(int min, int mode, int max, Random rnd)
        {
            if (max <= min)
                return min;
            if (mode < min) mode = min;
            if (mode > max) mode = max;

            double value = SampleTriangularDouble(min, mode, max, rnd);
            int sampled = (int)Math.Round(value);
            if (sampled < min) sampled = min;
            if (sampled > max) sampled = max;
            return sampled;
        }

        private static double SampleTriangularDouble(double min, double mode, double max, Random rnd)
        {
            if (max <= min)
                return min;
            if (mode < min) mode = min;
            if (mode > max) mode = max;

            double u = rnd.NextDouble();
            double c = (mode - min) / (max - min);
            if (u < c)
                return min + Math.Sqrt(u * (max - min) * (mode - min));
            return max - Math.Sqrt((1.0 - u) * (max - min) * (max - mode));
        }

        private static int SampleDelayUnilateral(int nominal, int perturbationUpper, double gamma, Random rnd)
        {
            if (nominal <= 0)
                return 0;

            int min = nominal;
            int upper = Math.Max(nominal, perturbationUpper);
            int max = nominal + (int)Math.Ceiling(NormalizeGamma(gamma) * (upper - nominal));
            if (max <= min)
                return min;
            return SampleTriangular(min, min, max, rnd);
        }

        private static int SampleAroundNominal(int nominal, double gamma, Random rnd)
        {
            if (nominal <= 0)
                return 0;

            int min = Math.Max(1, (int)Math.Floor(nominal * (1.0 - gamma)));
            int max = Math.Max(min, (int)Math.Ceiling(nominal * (1.0 + gamma)));
            if (min == max)
                return min;
            return rnd.Next(min, max + 1);
        }

        private static void BuildHistogram(RiskResultDto result, int[] sortedSamples, int binCount)
        {
            result.HistogramCounts.Clear();
            result.HistogramEdges.Clear();

            if (sortedSamples == null || sortedSamples.Length == 0)
                return;

            int min = sortedSamples[0];
            int max = sortedSamples[sortedSamples.Length - 1];

            if (min == max)
            {
                result.HistogramEdges.Add(min);
                result.HistogramEdges.Add(max + 1.0);
                result.HistogramCounts.Add(sortedSamples.Length);
                return;
            }

            double width = (double)(max - min) / binCount;
            if (width <= 0.0)
                width = 1.0;

            for (int i = 0; i <= binCount; i++)
                result.HistogramEdges.Add(min + i * width);

            for (int i = 0; i < binCount; i++)
                result.HistogramCounts.Add(0);

            for (int i = 0; i < sortedSamples.Length; i++)
            {
                int sample = sortedSamples[i];
                int idx = (int)Math.Floor((sample - min) / width);
                if (idx >= binCount) idx = binCount - 1;
                if (idx < 0) idx = 0;
                result.HistogramCounts[idx]++;
            }
        }

        private static double[] SortedCopy(double[] values)
        {
            if (values == null || values.Length == 0)
                return new double[0];
            var copy = (double[])values.Clone();
            Array.Sort(copy);
            return copy;
        }

        private static double Percentile(double[] sortedValues, double p)
        {
            if (sortedValues == null || sortedValues.Length == 0)
                return 0.0;
            if (p <= 0.0) return sortedValues[0];
            if (p >= 1.0) return sortedValues[sortedValues.Length - 1];

            double pos = (sortedValues.Length - 1) * p;
            int lower = (int)Math.Floor(pos);
            int upper = (int)Math.Ceiling(pos);
            if (lower == upper)
                return sortedValues[lower];
            double weight = pos - lower;
            return sortedValues[lower] + weight * (sortedValues[upper] - sortedValues[lower]);
        }

        private static double CVar(double[] sortedValues, double p)
        {
            if (sortedValues == null || sortedValues.Length == 0)
                return 0.0;

            double threshold = Percentile(sortedValues, p);
            double sum = 0.0;
            int count = 0;
            for (int i = 0; i < sortedValues.Length; i++)
            {
                double value = sortedValues[i];
                if (value >= threshold)
                {
                    sum += value;
                    count++;
                }
            }
            return count == 0 ? threshold : sum / count;
        }

        private static double Percentile(int[] sortedValues, double p)
        {
            if (sortedValues == null || sortedValues.Length == 0)
                return 0.0;
            if (p <= 0.0) return sortedValues[0];
            if (p >= 1.0) return sortedValues[sortedValues.Length - 1];

            double pos = (sortedValues.Length - 1) * p;
            int lower = (int)Math.Floor(pos);
            int upper = (int)Math.Ceiling(pos);
            if (lower == upper)
                return sortedValues[lower];
            double weight = pos - lower;
            return sortedValues[lower] + weight * (sortedValues[upper] - sortedValues[lower]);
        }

        private static double CVar(int[] sortedValues, double p)
        {
            if (sortedValues == null || sortedValues.Length == 0)
                return 0.0;

            double threshold = Percentile(sortedValues, p);
            long sum = 0;
            int count = 0;
            for (int i = 0; i < sortedValues.Length; i++)
            {
                int value = sortedValues[i];
                if (value >= threshold)
                {
                    sum += value;
                    count++;
                }
            }
            return count == 0 ? threshold : (double)sum / count;
        }

        private static double ComputeAverage(int[] values)
        {
            if (values == null || values.Length == 0)
                return 0.0;

            long sum = 0;
            for (int i = 0; i < values.Length; i++)
                sum += values[i];
            return (double)sum / values.Length;
        }

        private static double ComputeAverage(double[] values)
        {
            if (values == null || values.Length == 0)
                return 0.0;

            double sum = 0.0;
            for (int i = 0; i < values.Length; i++)
                sum += values[i];
            return sum / values.Length;
        }

        private static double MinOrZero(double[] values)
        {
            if (values == null || values.Length == 0)
                return 0.0;

            double min = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] < min)
                    min = values[i];
            }
            return min;
        }

        private static void AddToDictionary(Dictionary<int, double> map, int key, double value)
        {
            double current;
            if (map.TryGetValue(key, out current))
                map[key] = current + value;
            else
                map[key] = value;
        }

        private static double GetOrDefault(Dictionary<int, double> map, int key, double fallback)
        {
            if (map == null)
                return fallback;

            double value;
            return map.TryGetValue(key, out value) ? value : fallback;
        }
    }
}
