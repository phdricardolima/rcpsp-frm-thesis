// Thesis traceability: Appendix A, Algorithms A.7-A.8 (crashing generation and complete reevaluation).
using System;
using System.Collections.Generic;
using System.Linq;
using RCPSP.Application;
using RCPSP.Contracts;

namespace RCPSP.Infrastructure.Cpu
{
    public sealed class CpuCrashingAnalyzer : ICrashingAnalyzer
    {
        private readonly IBaselineScheduler _baselineScheduler;
        private readonly CpuExactBaselineScheduler _exactBaselineScheduler;
        private readonly IFrmCalculator _frmCalculator;
        private readonly IRiskAnalyzer _riskAnalyzer;


        public CpuCrashingAnalyzer(
            IBaselineScheduler baselineScheduler,
            CpuExactBaselineScheduler exactBaselineScheduler,
            IFrmCalculator frmCalculator,
            IRiskAnalyzer riskAnalyzer)
        {
            _baselineScheduler = baselineScheduler ?? throw new ArgumentNullException(nameof(baselineScheduler));
            _exactBaselineScheduler = exactBaselineScheduler ?? throw new ArgumentNullException(nameof(exactBaselineScheduler));
            _frmCalculator = frmCalculator ?? throw new ArgumentNullException(nameof(frmCalculator));
            _riskAnalyzer = riskAnalyzer ?? throw new ArgumentNullException(nameof(riskAnalyzer));
        }

        public CrashingResultDto Run(
            ProjectDataDto project,
            BaselineResultDto baseline,
            FrmResultDto frm,
            RiskResultDto risk,
            CrashingOptionsDto crashingOptions)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));
            if (baseline == null)
                throw new ArgumentNullException(nameof(baseline));
            if (frm == null)
                throw new ArgumentNullException(nameof(frm));
            if (risk == null)
                throw new ArgumentNullException(nameof(risk));
            if (crashingOptions == null)
                throw new ArgumentNullException(nameof(crashingOptions));

            var result = new CrashingResultDto();

            if (!crashingOptions.Enabled)
            {
                result.SummaryText = "Crashing is disabled in the options.";
                return result;
            }

            var scheduling = InferSchedulingOptions(baseline, frm);
            if (crashingOptions.BranchAndBoundTimeLimitSeconds.HasValue)
                scheduling.BranchAndBoundTimeLimitSeconds = crashingOptions.BranchAndBoundTimeLimitSeconds;
            bool useExactEngine = IsExactRun(baseline, frm);
            var frmOptions = InferFrmOptions(frm);
            var riskOptions = InferRiskOptions(risk);

            List<CrashingCandidateActivityDto> candidates = BuildCandidates(project, baseline, frm, crashingOptions);
            result.Candidates = CloneCandidates(candidates);

            if (crashingOptions.CandidatesOnly)
            {
                result.GeneratedScenarioCount = 0;
                result.ExecutedScenarioCount = 0;
                result.Scenarios = new List<CrashingScenarioResultDto>();
                result.SummaryText = "Crashing candidates loaded. Click Run Scenarios to generate and evaluate Sj scenarios.";
                return result;
            }

            var activeCandidates = BuildActiveCandidates(candidates, crashingOptions.MaxActivitiesToCrash);
            if (activeCandidates.Count == 0)
            {
                result.SummaryText = "No valid candidate activity was selected for crashing.";
                return result;
            }

            int maxCombinationSize = Math.Max(1, Math.Min(crashingOptions.MaxCombinationSize, activeCandidates.Count));
            int maxScenarioCount = Math.Max(1, crashingOptions.MaxScenarioCount);

            List<ScenarioDefinition> scenarioDefinitions = BuildScenarioDefinitions(
                activeCandidates,
                maxCombinationSize,
                maxScenarioCount);

            result.GeneratedScenarioCount = scenarioDefinitions.Count;
            var cloneTemplate = ProjectCloneTemplate.Create(project);
            var scenarios = new CrashingScenarioResultDto[scenarioDefinitions.Count];

            bool canParallelize = !crashingOptions.RecalculateRiskAfterCrash && !useExactEngine && scenarioDefinitions.Count > 1;
            if (canParallelize)
            {
                System.Threading.Tasks.Parallel.For(
                    0,
                    scenarioDefinitions.Count,
                    new System.Threading.Tasks.ParallelOptions
                    {
                        MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount)
                    },
                    i =>
                    {
                        scenarios[i] = EvaluateScenario(
                            cloneTemplate,
                            scenarioDefinitions[i],
                            baseline,
                            frm,
                            risk,
                            scheduling,
                            frmOptions,
                            riskOptions,
                            crashingOptions,
                            useExactEngine);
                    });
            }
            else
            {
                for (int i = 0; i < scenarioDefinitions.Count; i++)
                {
                    scenarios[i] = EvaluateScenario(
                        cloneTemplate,
                        scenarioDefinitions[i],
                        baseline,
                        frm,
                        risk,
                        scheduling,
                        frmOptions,
                        riskOptions,
                        crashingOptions,
                        useExactEngine);
                }
            }

            result.Scenarios = new List<CrashingScenarioResultDto>(scenarios.Length);
            for (int i = 0; i < scenarios.Length; i++)
            {
                if (scenarios[i] != null)
                    result.Scenarios.Add(scenarios[i]);
            }

            result.ExecutedScenarioCount = result.Scenarios.Count;
            result.Scenarios = result.Scenarios
                .OrderBy(x => x.StructuralRankBucket)
                .ThenByDescending(x => x.Frri)
                .ThenByDescending(x => x.RobustnessIndex)
                .ThenBy(x => x.DeltaMakespan)
                .ThenBy(x => x.DeltaCVaR95)
                .ThenBy(x => x.DeltaP95)
                .ThenBy(x => x.ScenarioName)
                .ToList();

            for (int i = 0; i < result.Scenarios.Count; i++)
                result.Scenarios[i].Rank = i + 1;

            result.SummaryText = BuildSummaryText(result);
            return result;
        }

        private CrashingScenarioResultDto EvaluateScenario(
            ProjectCloneTemplate cloneTemplate,
            ScenarioDefinition def,
            BaselineResultDto baseline,
            FrmResultDto frm,
            RiskResultDto risk,
            SchedulingOptionsDto scheduling,
            FrmOptionsDto frmOptions,
            RiskOptionsDto riskOptions,
            CrashingOptionsDto crashingOptions,
            bool useExactEngine)
        {
            ProjectDataDto crashedProject = cloneTemplate.CloneWithCrashing(def);


            var inheritedOrder = (baseline.ScheduledOrder != null && baseline.ScheduledOrder.Count > 0)
                ? baseline.ScheduledOrder
                : baseline.Sequence;

            BaselineResultDto crashedBaseline = useExactEngine
                ? _exactBaselineScheduler.RunModifiedDhBranchAndBound(crashedProject, scheduling)
                : _baselineScheduler.RunWithInheritedOrder(crashedProject, inheritedOrder, scheduling);

            FrmResultDto crashedFrm = _frmCalculator.Run(crashedProject, crashedBaseline, frmOptions);


            List<RiskResultDto> crashedReplications = new List<RiskResultDto>();
            if (crashingOptions.RecalculateRiskAfterCrash)
            {
                var seeds = risk.ReplicationSeeds != null && risk.ReplicationSeeds.Count > 0
                    ? risk.ReplicationSeeds
                    : new List<int> { risk.Seed };
                foreach (int replicationSeed in seeds)
                {
                    var replicationOptions = CloneRiskOptions(riskOptions);
                    replicationOptions.Seed = replicationSeed;
                    crashedReplications.Add(_riskAnalyzer.Run(crashedProject, crashedBaseline, crashedFrm, replicationOptions));
                }
            }

            RiskResultDto crashedRisk = crashingOptions.RecalculateRiskAfterCrash
                ? AggregateReplications(crashedReplications, riskOptions.Gamma, risk.Seed)
                : risk;


            if (IsDelayUnilateral(riskOptions.SamplingMode))
                crashedRisk = NormalizeRiskForReference(crashedRisk, crashedBaseline.Makespan, riskOptions.SamplingMode);

            double sif = ComputeSif(crashedFrm);
            double robustnessIndex = ComputeRobustnessIndex(crashedFrm);
            double structuralPenalty = ComputeStructuralPenalty(crashedFrm, sif, robustnessIndex);

            double baselineCVar95Delay = risk.CVaR95Delay > 0.0 ? risk.CVaR95Delay : ComputeDelayCVar(risk, baseline.Makespan, 0.95);
            double scenarioCVar95Delay = crashedRisk.CVaR95Delay > 0.0 ? crashedRisk.CVaR95Delay : ComputeDelayCVar(crashedRisk, crashedBaseline.Makespan, 0.95);

            var baselineReplicationCvars = risk.ReplicationCVaR95Delay != null && risk.ReplicationCVaR95Delay.Count > 0
                ? risk.ReplicationCVaR95Delay
                : new List<double> { baselineCVar95Delay };
            var scenarioReplicationCvars = crashedReplications.Count > 0
                ? crashedReplications.Select(x => x.CVaR95Delay).ToList()
                : new List<double> { scenarioCVar95Delay };
            int pairedCount = Math.Min(baselineReplicationCvars.Count, scenarioReplicationCvars.Count);
            var frriReplications = new List<double>(pairedCount);
            for (int replicationIndex = 0; replicationIndex < pairedCount; replicationIndex++)
                frriReplications.Add(ComputeFrri(baselineReplicationCvars[replicationIndex], scenarioReplicationCvars[replicationIndex]));

            double scenarioCvarSd = StandardDeviation(scenarioReplicationCvars);
            double frriMean = frriReplications.Count > 0 ? frriReplications.Average() : ComputeFrri(baselineCVar95Delay, scenarioCVar95Delay);
            double frriSd = StandardDeviation(frriReplications);
            double scenarioCvarMargin = ConfidenceMargin95(scenarioCvarSd, scenarioReplicationCvars.Count);
            double frriMargin = ConfidenceMargin95(frriSd, frriReplications.Count);

            var scenario = new CrashingScenarioResultDto
            {
                ScenarioName = def.Name,
                ActivityIds = new List<int>(def.ActivityIds),
                ActivityCount = def.ActivityIds.Count,
                CrashedDurations = new Dictionary<int, int>(def.CrashedDurations),

                BaselineMakespan = baseline.Makespan,
                ScenarioMakespan = crashedBaseline.Makespan,
                DeltaMakespan = crashedBaseline.Makespan - baseline.Makespan,

                BaselineP50 = risk.P50,
                ScenarioP50 = crashedRisk.P50,

                BaselineP95 = risk.P95,
                ScenarioP95 = crashedRisk.P95,
                DeltaP95 = crashedRisk.P95 - risk.P95,


                BaselineCVaR95 = baselineCVar95Delay,
                ScenarioCVaR95 = scenarioCVar95Delay,
                DeltaCVaR95 = scenarioCVar95Delay - baselineCVar95Delay,

                BaselineDelayProbability = ComputeDelayProbability(risk, baseline.Makespan),
                ScenarioDelayProbability = ComputeDelayProbability(crashedRisk, crashedBaseline.Makespan),
                BaselineMeanDelay = ComputeMeanDelay(risk, baseline.Makespan),
                ScenarioMeanDelay = ComputeMeanDelay(crashedRisk, crashedBaseline.Makespan),
                BaselineP95Delay = ComputeDelayPercentile(risk, baseline.Makespan, 0.95),
                ScenarioP95Delay = ComputeDelayPercentile(crashedRisk, crashedBaseline.Makespan, 0.95),
                BaselineCVaR95Delay = baselineCVar95Delay,
                ScenarioCVaR95Delay = scenarioCVar95Delay,
                DeltaCVaR95Delay = scenarioCVar95Delay - baselineCVar95Delay,
                BaselineMaxDelay = ComputeMaxDelay(risk, baseline.Makespan),
                ScenarioMaxDelay = ComputeMaxDelay(crashedRisk, crashedBaseline.Makespan),

                BaselineBalanceRuptureProbability = risk.BalanceRuptureProbability,
                ScenarioBalanceRuptureProbability = crashedRisk.BalanceRuptureProbability,
                DeltaBalanceRuptureProbability = crashedRisk.BalanceRuptureProbability - risk.BalanceRuptureProbability,
                BaselineMeanBalanceUsage = risk.MeanBalanceUsage,
                ScenarioMeanBalanceUsage = crashedRisk.MeanBalanceUsage,
                DeltaMeanBalanceUsage = crashedRisk.MeanBalanceUsage - risk.MeanBalanceUsage,
                BaselineMinObservedBalance = risk.MinObservedBalance,
                ScenarioMinObservedBalance = crashedRisk.MinObservedBalance,
                DeltaMinObservedBalance = crashedRisk.MinObservedBalance - risk.MinObservedBalance,
                BaselineMeanUnabsorbedWork = risk.MeanUnabsorbedWork,
                ScenarioMeanUnabsorbedWork = crashedRisk.MeanUnabsorbedWork,
                DeltaMeanUnabsorbedWork = crashedRisk.MeanUnabsorbedWork - risk.MeanUnabsorbedWork,
                BaselineMeanUnabsorbedWorkRatio = risk.MeanUnabsorbedWorkRatio,
                ScenarioMeanUnabsorbedWorkRatio = crashedRisk.MeanUnabsorbedWorkRatio,
                DeltaMeanUnabsorbedWorkRatio = crashedRisk.MeanUnabsorbedWorkRatio - risk.MeanUnabsorbedWorkRatio,

                RelativeP95Reduction = ComputeRelativeRiskReduction(risk.P95, crashedRisk.P95),
                Frri = frriMean,
                ReplicationCount = pairedCount,
                ScenarioCVaR95StdDev = scenarioCvarSd,
                ScenarioCVaR95CiLower = scenarioCVar95Delay - scenarioCvarMargin,
                ScenarioCVaR95CiUpper = scenarioCVar95Delay + scenarioCvarMargin,
                FrriStdDev = frriSd,
                FrriCiLower = frriMean - frriMargin,
                FrriCiUpper = frriMean + frriMargin,
                Sif = sif,
                RobustnessIndex = robustnessIndex,
                IsStructurallyRobust = crashedFrm.IsStructurallyRobust,
                StructuralPenalty = structuralPenalty
            };

            scenario.StructuralStatus = ClassifyStructuralStatus(scenario.IsStructurallyRobust, scenario.Sif, scenario.RobustnessIndex);
            scenario.StructuralRankBucket = GetStructuralRankBucket(scenario.StructuralStatus);
            scenario.IsStructurallyAcceptable = scenario.StructuralRankBucket <= 1;
            scenario.Score = ComputeScore(scenario, crashingOptions);
            scenario.ActivitiesLabel = BuildActivitiesLabel(scenario.CrashedDurations);
            return scenario;
        }

        private static List<CrashingCandidateActivityDto> CloneCandidates(List<CrashingCandidateActivityDto> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return new List<CrashingCandidateActivityDto>();

            var result = new List<CrashingCandidateActivityDto>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                var clone = CloneCandidate(candidates[i]);
                if (clone != null)
                    result.Add(clone);
            }
            return result;
        }

        private static List<CrashingCandidateActivityDto> BuildActiveCandidates(List<CrashingCandidateActivityDto> candidates, int maxActivitiesToCrash)
        {
            if (candidates == null || candidates.Count == 0)
                return new List<CrashingCandidateActivityDto>();

            var active = new List<CrashingCandidateActivityDto>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (IsCandidateActive(candidate))
                    active.Add(candidate);
            }

            active.Sort((a, b) =>
            {
                int cmp = b.FrmPriority.CompareTo(a.FrmPriority);
                if (cmp != 0) return cmp;
                return a.ActivityId.CompareTo(b.ActivityId);
            });

            int maxCount = Math.Max(1, maxActivitiesToCrash);
            if (active.Count > maxCount)
                active.RemoveRange(maxCount, active.Count - maxCount);

            return active;
        }

        private static List<CrashingCandidateActivityDto> BuildCandidates(
            ProjectDataDto project,
            BaselineResultDto baseline,
            FrmResultDto frm,
            CrashingOptionsDto options)
        {
            var scheduledIds = new HashSet<int>(baseline.Sequence ?? new List<int>());

            var scheduledActivities = new List<ScheduledActivityDto>();
            foreach (var activity in baseline.Activities ?? new List<ScheduledActivityDto>())
            {
                if (activity != null)
                    scheduledActivities.Add(activity);
            }
            scheduledActivities.Sort((a, b) =>
            {
                int cmp = a.Start.CompareTo(b.Start);
                if (cmp != 0) return cmp;
                cmp = a.Finish.CompareTo(b.Finish);
                if (cmp != 0) return cmp;
                return a.ActivityId.CompareTo(b.ActivityId);
            });

            var scheduledOrder = new Dictionary<int, int>(scheduledActivities.Count);
            for (int i = 0; i < scheduledActivities.Count; i++)
            {
                int activityId = scheduledActivities[i].ActivityId;
                if (!scheduledOrder.ContainsKey(activityId))
                    scheduledOrder[activityId] = i;
            }

            var frmById = new Dictionary<int, FrmActivityResultDto>();
            foreach (var activity in frm.Activities ?? new List<FrmActivityResultDto>())
            {
                if (activity != null && !frmById.ContainsKey(activity.ActivityId))
                    frmById[activity.ActivityId] = activity;
            }

            var resourceDiagnostics = new Dictionary<int, FrmResourceDiagnosticDto>();
            foreach (var diagnostic in frm.ResourceDiagnostics ?? new List<FrmResourceDiagnosticDto>())
            {
                if (diagnostic != null && !resourceDiagnostics.ContainsKey(diagnostic.ResourceId))
                    resourceDiagnostics[diagnostic.ResourceId] = diagnostic;
            }

            var defaults = new List<CandidateWorkingData>();
            foreach (var a in project.Activities ?? new List<ActivityDto>())
            {
                if (a == null)
                    continue;
                if (scheduledIds.Count != 0 && !scheduledIds.Contains(a.Id))
                    continue;
                if (a.IsSummary)
                    continue;
                if (string.Equals(a.ExecutionState, "Completed", StringComparison.OrdinalIgnoreCase))
                    continue;

                FrmActivityResultDto frmActivity;
                frmById.TryGetValue(a.Id, out frmActivity);

                int nominal = Math.Max(0, a.DurationDays);
                int frmFixedDuration = nominal;


                if (frmActivity != null && frmActivity.DSMin > 0 && frmActivity.DSMin < nominal)
                    frmFixedDuration = frmActivity.DSMin;
                else if (frmActivity != null && frmActivity.DMin > 0 && frmActivity.DMin < nominal)
                    frmFixedDuration = frmActivity.DMin;
                else if (frmActivity != null && frmActivity.DNew > 0 && frmActivity.DNew < nominal)
                    frmFixedDuration = frmActivity.DNew;

                bool hasStructuralReduction = frmActivity != null
                    && nominal > 0
                    && frmFixedDuration > 0
                    && frmFixedDuration < nominal;

                int fixedDuration = hasStructuralReduction ? frmFixedDuration : nominal;
                bool hasSufficientResourceBalance = HasSufficientResourceBalanceForCompression(a, frmActivity, resourceDiagnostics, nominal, fixedDuration);
                bool eligible = !a.IsDummy && nominal > 0 && hasStructuralReduction && hasSufficientResourceBalance;
                int slack = frmActivity != null ? frmActivity.SlackI : nominal;
                double rawSensitivity = ComputeFrmSensitivity(frmActivity);
                double balanceRisk = ComputeBalanceRisk(a, resourceDiagnostics);

                int sequenceOrder;
                if (!scheduledOrder.TryGetValue(a.Id, out sequenceOrder))
                    sequenceOrder = int.MaxValue;

                var working = new CandidateWorkingData
                {
                    Candidate = new CrashingCandidateActivityDto
                    {
                        Use = false,
                        ActivityId = a.Id,
                        ActivityName = a.Name ?? string.Empty,
                        NominalDuration = nominal,
                        MinimumDuration = fixedDuration,
                        NewDuration = fixedDuration,
                        RecommendedNewDuration = fixedDuration,
                        IsEligible = eligible,
                        IsDummy = a.IsDummy,
                        FrmSlackI = slack,
                        FrmSensitivity = rawSensitivity,
                        FrmBalanceRisk = balanceRisk
                    },
                    RawReductionPotential = Math.Max(0, nominal - fixedDuration),
                    RawSensitivity = rawSensitivity,
                    RawBalanceRisk = balanceRisk,
                    FrmActivity = frmActivity,
                    Activity = a,
                    ScheduleOrder = sequenceOrder
                };

                if (working.Candidate.NominalDuration > 0)
                    defaults.Add(working);
            }

            double maxReduction = defaults.Count > 0 ? defaults.Max(x => (double)x.RawReductionPotential) : 1.0;
            double maxSensitivity = defaults.Count > 0 ? defaults.Max(x => x.RawSensitivity) : 1.0;

            if (maxReduction <= 0.0)
                maxReduction = 1.0;
            if (maxSensitivity <= 0.0)
                maxSensitivity = 1.0;

            string policyMode = string.IsNullOrWhiteSpace(options.CrashingPolicyMode)
                ? "FRM_GUIDED"
                : options.CrashingPolicyMode.Trim().ToUpperInvariant();


            double maxSlack = 1.0;
            if (string.Equals(policyMode, "CRITICAL_PATH", StringComparison.OrdinalIgnoreCase))
            {
                maxSlack = defaults.Count > 0
                    ? Math.Max(1.0, defaults.Max(x => (double)(x.FrmActivity != null ? x.FrmActivity.SlackI : 0)))
                    : 1.0;
            }


            double maxRiskExposure = 1.0;
            if (string.Equals(policyMode, "RISK_DRIVEN", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var item in defaults)
                {
                    double exposure = ComputeRiskExposure(item.FrmActivity);
                    if (exposure > maxRiskExposure)
                        maxRiskExposure = exposure;
                }
            }

            foreach (var item in defaults)
            {
                double reductionNorm = item.RawReductionPotential / maxReduction;
                double priority;

                if (string.Equals(policyMode, "CRITICAL_PATH", StringComparison.OrdinalIgnoreCase))
                {

                    int slackI = item.FrmActivity != null ? item.FrmActivity.SlackI : 0;
                    double criticalityFactor = 1.0 - (slackI / maxSlack);
                    priority = Clamp01(criticalityFactor) * reductionNorm;
                }
                else if (string.Equals(policyMode, "RISK_DRIVEN", StringComparison.OrdinalIgnoreCase))
                {

                    double riskExposureNorm = ComputeRiskExposure(item.FrmActivity) / maxRiskExposure;
                    priority = Clamp01(riskExposureNorm) * reductionNorm;
                }
                else
                {

                    double sensitivityNorm = item.RawSensitivity / maxSensitivity;
                    priority = (0.50 * reductionNorm)
                               + (0.30 * sensitivityNorm)
                               + (0.20 * item.RawBalanceRisk);
                }

                item.Candidate.FrmPriority = Clamp01(priority);
            }

            var requested = options.CandidateActivities ?? new List<CrashingCandidateActivityDto>();
            var requestedById = requested
                .Where(x => x != null)
                .GroupBy(x => x.ActivityId)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var item in defaults)
            {
                var candidate = item.Candidate;
                CrashingCandidateActivityDto requestedCandidate;

                if (requestedById.TryGetValue(candidate.ActivityId, out requestedCandidate))
                    candidate.Use = requestedCandidate.Use && candidate.IsEligible;
                else


                    candidate.Use = candidate.IsEligible;

                candidate.MinimumDuration = candidate.RecommendedNewDuration;
                candidate.NewDuration = candidate.RecommendedNewDuration;

                if (!candidate.IsEligible)
                    candidate.Use = false;
            }

            return defaults
                .OrderBy(x => x.ScheduleOrder)
                .ThenBy(x => x.Candidate.ActivityId)
                .Select(x => x.Candidate)
                .ToList();
        }


        private static double ComputeRiskExposure(FrmActivityResultDto frmActivity)
        {
            if (frmActivity == null)
                return 0.0;

            int dMax = frmActivity.DMax;
            int dSMax = frmActivity.DSMax;
            int dSMin = frmActivity.DSMin;

            int overflowRange = Math.Max(0, dMax - dSMax);
            int totalRange = Math.Max(1, dMax - dSMin);

            return (double)overflowRange / totalRange;
        }

        private static double ComputeFrmSensitivity(FrmActivityResultDto frmActivity)
        {
            if (frmActivity == null)
                return 0.0;

            double bruto = frmActivity.ScoreBrutoByResourceId != null
                ? frmActivity.ScoreBrutoByResourceId.Values.Sum(v => Math.Abs(v))
                : 0.0;

            double score = frmActivity.ScoreIkByResourceId != null
                ? frmActivity.ScoreIkByResourceId.Values.Sum(v => Math.Abs(v))
                : 0.0;

            double balance = frmActivity.BalanceByResourceId != null
                ? frmActivity.BalanceByResourceId.Values.Where(v => v < 0).Sum(v => Math.Abs(v))
                : 0.0;

            return bruto + score + balance;
        }

        private static double ComputeBalanceRisk(ActivityDto activity, Dictionary<int, FrmResourceDiagnosticDto> resourceDiagnostics)
        {
            if (activity == null || activity.Assignments == null || activity.Assignments.Count == 0)
                return 0.0;

            double sum = 0.0;
            int count = 0;

            foreach (var assignment in activity.Assignments)
            {
                if (assignment == null)
                    continue;

                FrmResourceDiagnosticDto diagnostic;
                if (!resourceDiagnostics.TryGetValue(assignment.ResourceId, out diagnostic) || diagnostic == null)
                    continue;

                double resourceRisk = 1.0 - Clamp01(diagnostic.RobustnessIndex);
                if (!diagnostic.IsRobust)
                    resourceRisk = Math.Min(1.0, resourceRisk + 0.35);

                sum += resourceRisk;
                count++;
            }

            if (count == 0)
                return 0.0;

            return Clamp01(sum / count);
        }

        private static bool HasSufficientResourceBalanceForCompression(
            ActivityDto activity,
            FrmActivityResultDto frmActivity,
            Dictionary<int, FrmResourceDiagnosticDto> resourceDiagnostics,
            int nominalDuration,
            int compressedDuration)
        {
            if (activity == null || activity.Assignments == null || activity.Assignments.Count == 0)
                return false;

            if (frmActivity == null || compressedDuration <= 0 || nominalDuration <= 0 || compressedDuration >= nominalDuration)
                return false;

            int reduction = nominalDuration - compressedDuration;

            foreach (var assignment in activity.Assignments)
            {
                if (assignment == null || assignment.Units <= 0)
                    continue;

                FrmResourceDiagnosticDto diagnostic;
                if (resourceDiagnostics == null || !resourceDiagnostics.TryGetValue(assignment.ResourceId, out diagnostic) || diagnostic == null)
                    return false;

                if (!diagnostic.IsRobust)
                    return false;

                int availableBalance = diagnostic.BalanceFinal;
                int additionalConsumption = Math.Max(1, (int)Math.Round(reduction * Math.Max(1.0, assignment.Units), MidpointRounding.AwayFromZero));

                if (availableBalance < additionalConsumption)
                    return false;
            }

            return true;
        }

        private static bool IsCandidateActive(CrashingCandidateActivityDto candidate)
        {
            return candidate != null
                   && candidate.Use
                   && candidate.IsEligible
                   && !candidate.IsDummy
                   && candidate.MinimumDuration > 0
                   && candidate.MinimumDuration <= candidate.NominalDuration
                   && candidate.MinimumDuration < candidate.NominalDuration;
        }

        private static List<ScenarioDefinition> BuildScenarioDefinitions(
            List<CrashingCandidateActivityDto> candidates,
            int maxCombinationSize,
            int maxScenarioCount)
        {
            var scenarios = new List<ScenarioDefinition>();

            if (candidates == null || candidates.Count == 0 || maxScenarioCount <= 0)
                return scenarios;

            var orderedCandidates = candidates
                .Where(x => x != null)
                .OrderBy(x => x.ActivityId)
                .ToList();

            if (orderedCandidates.Count == 0)
                return scenarios;

            maxCombinationSize = Math.Max(1, maxCombinationSize);
            maxScenarioCount = Math.Max(1, maxScenarioCount);

            var selectedDurations = new int[orderedCandidates.Count];
            for (int i = 0; i < orderedCandidates.Count; i++)
                selectedDurations[i] = Math.Max(0, orderedCandidates[i].NominalDuration);

            int sequence = 1;
            GenerateScenarioDefinitionsRecursive(
                orderedCandidates,
                0,
                0,
                selectedDurations,
                maxCombinationSize,
                maxScenarioCount,
                scenarios,
                ref sequence);

            return scenarios;
        }

        private static void GenerateScenarioDefinitionsRecursive(
            List<CrashingCandidateActivityDto> candidates,
            int index,
            int crashedCount,
            int[] selectedDurations,
            int maxCombinationSize,
            int maxScenarioCount,
            List<ScenarioDefinition> scenarios,
            ref int sequence)
        {
            if (scenarios.Count >= maxScenarioCount)
                return;

            if (crashedCount > maxCombinationSize)
                return;

            if (index >= candidates.Count)
            {
                if (crashedCount == 0)
                    return;

                var activityIds = new List<int>(crashedCount);
                var crashedDurations = new Dictionary<int, int>(crashedCount);

                for (int i = 0; i < candidates.Count; i++)
                {
                    var candidate = candidates[i];
                    int duration = selectedDurations[i];
                    if (duration >= candidate.NominalDuration)
                        continue;

                    activityIds.Add(candidate.ActivityId);
                    crashedDurations[candidate.ActivityId] = duration;
                }

                if (activityIds.Count == 0)
                    return;

                scenarios.Add(new ScenarioDefinition
                {
                    Name = "SCN_" + sequence.ToString("000"),
                    ActivityIds = activityIds,
                    CrashedDurations = crashedDurations
                });

                sequence++;
                return;
            }

            var currentCandidate = candidates[index];
            int nominal = Math.Max(0, currentCandidate.NominalDuration);
            int lowerBound = currentCandidate.MinimumDuration;
            if (lowerBound <= 0)
                lowerBound = 1;
            if (lowerBound > nominal)
                lowerBound = nominal;

            for (int duration = nominal; duration >= lowerBound; duration--)
            {
                selectedDurations[index] = duration;
                int nextCrashedCount = crashedCount + (duration < nominal ? 1 : 0);

                GenerateScenarioDefinitionsRecursive(
                    candidates,
                    index + 1,
                    nextCrashedCount,
                    selectedDurations,
                    maxCombinationSize,
                    maxScenarioCount,
                    scenarios,
                    ref sequence);

                if (scenarios.Count >= maxScenarioCount)
                    return;
            }

            selectedDurations[index] = nominal;
        }


        private sealed class ProjectCloneTemplate
        {
            private readonly string _projectName;
            private readonly DateTime? _statusDate;
            private readonly DateTime? _decisionDate;
            private readonly double _hoursPerDay;
            private readonly List<ResourceDto> _resources;
            private readonly ActivityTemplate[] _activities;

            private ProjectCloneTemplate(
                string projectName,
                DateTime? statusDate,
                DateTime? decisionDate,
                double hoursPerDay,
                List<ResourceDto> resources,
                ActivityTemplate[] activities)
            {
                _projectName = projectName;
                _statusDate = statusDate;
                _decisionDate = decisionDate;
                _hoursPerDay = hoursPerDay;
                _resources = resources;
                _activities = activities;
            }

            public static ProjectCloneTemplate Create(ProjectDataDto source)
            {
                var resources = new List<ResourceDto>();
                var sourceResources = source.Resources ?? new List<ResourceDto>();
                for (int i = 0; i < sourceResources.Count; i++)
                {
                    var r = sourceResources[i];
                    if (r == null)
                        continue;

                    resources.Add(new ResourceDto
                    {
                        Id = r.Id,
                        Name = r.Name,
                        Capacity = r.Capacity
                    });
                }

                var sourceActivities = source.Activities ?? new List<ActivityDto>();
                var activities = new ActivityTemplate[sourceActivities.Count];
                for (int i = 0; i < sourceActivities.Count; i++)
                {
                    var a = sourceActivities[i];
                    if (a == null)
                        continue;

                    activities[i] = new ActivityTemplate
                    {
                        Id = a.Id,
                        OriginalId = a.OriginalId,
                        Name = a.Name,
                        DurationDays = a.DurationDays,
                        RemainingDurationDays = a.RemainingDurationDays,
                        IsSummary = a.IsSummary,
                        IsMilestone = a.IsMilestone,
                        IsDummy = a.IsDummy,
                        ExecutionState = a.ExecutionState,
                        PlannedStart = a.PlannedStart,
                        PlannedFinish = a.PlannedFinish,
                        ActualStart = a.ActualStart,
                        ActualFinish = a.ActualFinish,
                        PredecessorIds = CloneIntList(a.PredecessorIds),
                        SuccessorIds = CloneIntList(a.SuccessorIds),
                        Assignments = CloneAssignments(a.Assignments)
                    };
                }

                return new ProjectCloneTemplate(
                    source.ProjectName,
                    source.StatusDate,
                    source.DecisionDate,
                    source.HoursPerDay,
                    resources,
                    activities);
            }

            public ProjectDataDto CloneWithCrashing(ScenarioDefinition definition)
            {
                var clone = new ProjectDataDto
                {
                    ProjectName = _projectName,
                    StatusDate = _statusDate,
                    DecisionDate = _decisionDate,
                    HoursPerDay = _hoursPerDay,
                    Resources = new List<ResourceDto>(_resources.Count),
                    Activities = new List<ActivityDto>(_activities.Length)
                };

                for (int i = 0; i < _resources.Count; i++)
                {
                    var r = _resources[i];
                    clone.Resources.Add(new ResourceDto
                    {
                        Id = r.Id,
                        Name = r.Name,
                        Capacity = r.Capacity
                    });
                }

                for (int i = 0; i < _activities.Length; i++)
                {
                    var template = _activities[i];
                    if (template == null)
                        continue;

                    int duration = template.DurationDays;
                    int? remaining = template.RemainingDurationDays;
                    int crashed;
                    if (definition.CrashedDurations.TryGetValue(template.Id, out crashed))
                    {
                        duration = crashed;
                        if (remaining.HasValue)
                            remaining = Math.Min(remaining.Value, crashed);
                    }

                    clone.Activities.Add(new ActivityDto
                    {
                        Id = template.Id,
                        OriginalId = template.OriginalId,
                        Name = template.Name,
                        DurationDays = duration,
                        RemainingDurationDays = remaining,
                        IsSummary = template.IsSummary,
                        IsMilestone = template.IsMilestone,
                        IsDummy = template.IsDummy,
                        ExecutionState = template.ExecutionState,
                        PlannedStart = template.PlannedStart,
                        PlannedFinish = template.PlannedFinish,
                        ActualStart = template.ActualStart,
                        ActualFinish = template.ActualFinish,
                        PredecessorIds = CloneIntList(template.PredecessorIds),
                        SuccessorIds = CloneIntList(template.SuccessorIds),
                        Assignments = CloneAssignments(template.Assignments)
                    });
                }

                return clone;
            }

            private static List<int> CloneIntList(List<int> source)
            {
                if (source == null || source.Count == 0)
                    return new List<int>();

                var clone = new List<int>(source.Count);
                for (int i = 0; i < source.Count; i++)
                    clone.Add(source[i]);
                return clone;
            }

            private static List<ResourceAssignmentDto> CloneAssignments(List<ResourceAssignmentDto> source)
            {
                if (source == null || source.Count == 0)
                    return new List<ResourceAssignmentDto>();

                var clone = new List<ResourceAssignmentDto>(source.Count);
                for (int i = 0; i < source.Count; i++)
                {
                    var x = source[i];
                    if (x == null)
                        continue;

                    clone.Add(new ResourceAssignmentDto
                    {
                        ResourceId = x.ResourceId,
                        ResourceName = x.ResourceName,
                        Units = x.Units
                    });
                }

                return clone;
            }
        }

        private sealed class ActivityTemplate
        {
            public int Id;
            public int OriginalId;
            public string Name;
            public int DurationDays;
            public int? RemainingDurationDays;
            public bool IsSummary;
            public bool IsMilestone;
            public bool IsDummy;
            public string ExecutionState;
            public DateTime? PlannedStart;
            public DateTime? PlannedFinish;
            public DateTime? ActualStart;
            public DateTime? ActualFinish;
            public List<int> PredecessorIds;
            public List<int> SuccessorIds;
            public List<ResourceAssignmentDto> Assignments;
        }

        private static SchedulingOptionsDto InferSchedulingOptions(BaselineResultDto baseline, FrmResultDto frm)
        {
            bool isExact = IsExactRun(baseline, frm);

            if (isExact)
            {
                return new SchedulingOptionsDto
                {
                    Heuristic = "Modified DH B&B",
                    Scheme = "EXACT",
                    Direction = "EXACT",
                    UseExactEngine = true,
                    Engine = "DH_BB",
                    RunLabel = "Modified DH B&B"
                };
            }

            return new SchedulingOptionsDto
            {
                Heuristic = !string.IsNullOrWhiteSpace(frm.Heuristic) ? frm.Heuristic : ExtractRunLabelPart(baseline.RunLabel, 0, "SPT"),
                Scheme = !string.IsNullOrWhiteSpace(frm.Scheme) ? frm.Scheme : ExtractRunLabelPart(baseline.RunLabel, 1, "SERIAL"),
                Direction = !string.IsNullOrWhiteSpace(frm.Direction) ? frm.Direction : ExtractRunLabelPart(baseline.RunLabel, 2, "FORWARD"),
                UseExactEngine = false,
                Engine = "HEURISTIC",
                RunLabel = baseline.RunLabel ?? string.Empty
            };
        }

        private static bool IsExactRun(BaselineResultDto baseline, FrmResultDto frm)
        {
            string runLabel = baseline != null ? baseline.RunLabel : string.Empty;
            string heuristic = frm != null ? frm.Heuristic : string.Empty;

            return IsModifiedDhBranchAndBound(runLabel)
                   || IsModifiedDhBranchAndBound(heuristic)
                   || string.Equals(Safe(frm != null ? frm.Scheme : null), "EXACT", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(Safe(frm != null ? frm.Direction : null), "EXACT", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsModifiedDhBranchAndBound(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string normalized = text.Trim().ToUpperInvariant();
            return normalized == "MODIFIED DH B&B"
                   || normalized == "B&B"
                   || normalized == "B&B EF"
                   || normalized == "DHBB"
                   || normalized == "DH_BB";
        }

        private static string Safe(string text)
        {
            return text ?? string.Empty;
        }

        private static FrmOptionsDto InferFrmOptions(FrmResultDto frm)
        {
            return new FrmOptionsDto
            {
                PositiveFlexibilityPercent = frm.FlexPositivePercent,
                NegativeFlexibilityPercent = frm.FlexNegativePercent,
                Enabled = true,
                Mode = "NORMAL"
            };
        }

        private static string NormalizeSamplingMode(string samplingMode)
        {
            if (string.Equals(samplingMode, "FRM_WORKCONTENT_BILATERAL", StringComparison.OrdinalIgnoreCase))
                return "FRM_WORKCONTENT_BILATERAL";
            if (string.Equals(samplingMode, "DELAY_STRUCTURAL", StringComparison.OrdinalIgnoreCase))
                return "DELAY_STRUCTURAL";

            return "DELAY_UNILATERAL";
        }

        private static bool IsDelayUnilateral(string samplingMode)
        {
            return string.Equals(samplingMode, "DELAY_UNILATERAL", StringComparison.OrdinalIgnoreCase)
                || string.Equals(samplingMode, "DELAY_STRUCTURAL", StringComparison.OrdinalIgnoreCase)
                || string.Equals(samplingMode, "DELAY_UNILATERAL_STRUCTURAL", StringComparison.OrdinalIgnoreCase);
        }

        private static RiskOptionsDto CloneRiskOptions(RiskOptionsDto source)
        {
            return new RiskOptionsDto
            {
                Enabled = source.Enabled,
                ScenarioCount = source.ScenarioCount,
                Gamma = source.Gamma,
                Seed = source.Seed,
                HistogramBinCount = source.HistogramBinCount,
                SamplingMode = source.SamplingMode,
                UseCommonRandomNumbers = true,
                RunPairedUnilateralStructural = false
            };
        }

        private static RiskResultDto AggregateReplications(IList<RiskResultDto> values, double gamma, int seed)
        {
            if (values == null || values.Count == 0)
                return new RiskResultDto { Gamma = gamma, Seed = seed };

            var result = new RiskResultDto
            {
                Iterations = values[0].Iterations, Gamma = gamma, Seed = seed,
                SamplingMode = values[0].SamplingMode,
                ReferenceMakespan = values.Average(x => x.ReferenceMakespan),
                MeanMakespan = values.Average(x => x.MeanMakespan),
                P50 = values.Average(x => x.P50), P95 = values.Average(x => x.P95),
                CVaR95 = values.Average(x => x.CVaR95Delay),
                CVaR95Delay = values.Average(x => x.CVaR95Delay),
                MakespanCVaR95 = values.Average(x => x.MakespanCVaR95),
                DelayProbability = values.Average(x => x.DelayProbability),
                MeanDelay = values.Average(x => x.MeanDelay),
                P95Delay = values.Average(x => x.P95Delay),
                MaxDelay = values.Average(x => x.MaxDelay),
                BalanceRuptureProbability = values.Average(x => x.BalanceRuptureProbability),
                MeanBalanceGenerated = values.Average(x => x.MeanBalanceGenerated),
                MeanBalanceConsumed = values.Average(x => x.MeanBalanceConsumed),
                MeanBalanceUsage = values.Average(x => x.MeanBalanceUsage),
                MeanBalanceUsageRatio = values.Average(x => x.MeanBalanceUsageRatio),
                MinObservedBalance = values.Average(x => x.MinObservedBalance),
                MeanPositiveWorkDemand = values.Average(x => x.MeanPositiveWorkDemand),
                MeanUnabsorbedWork = values.Average(x => x.MeanUnabsorbedWork),
                P95UnabsorbedWork = values.Average(x => x.P95UnabsorbedWork),
                CVaR95UnabsorbedWork = values.Average(x => x.CVaR95UnabsorbedWork),
                MeanUnabsorbedWorkRatio = values.Average(x => x.MeanUnabsorbedWorkRatio),
                ReplicationCount = values.Count,
                ReplicationSeeds = values.Select(x => x.Seed).ToList(),
                ReplicationP95 = values.Select(x => x.P95).ToList(),
                ReplicationCVaR95Delay = values.Select(x => x.CVaR95Delay).ToList(),
                ReplicationDelayProbability = values.Select(x => x.DelayProbability).ToList(),
                ReplicationMeanDelay = values.Select(x => x.MeanDelay).ToList(),
                MakespanSamples = values.Where(x => x.MakespanSamples != null).SelectMany(x => x.MakespanSamples).ToList()
            };
            result.ResourceAbsorption = values
                .Where(x => x.ResourceAbsorption != null).SelectMany(x => x.ResourceAbsorption)
                .GroupBy(x => x.ResourceId)
                .Select(g => new ResourceAbsorptionMetricDto
                {
                    ResourceId = g.Key,
                    MeanBalanceGenerated = g.Average(x => x.MeanBalanceGenerated),
                    MeanBalanceConsumed = g.Average(x => x.MeanBalanceConsumed),
                    MeanPositiveWorkDemand = g.Average(x => x.MeanPositiveWorkDemand),
                    MeanUnabsorbedWork = g.Average(x => x.MeanUnabsorbedWork),
                    MeanUnabsorbedWorkRatio = g.Average(x => x.MeanUnabsorbedWorkRatio),
                    RuptureProbability = g.Average(x => x.RuptureProbability),
                    MinObservedBalance = g.Average(x => x.MinObservedBalance)
                }).OrderBy(x => x.ResourceId).ToList();
            return result;
        }

        private static double StandardDeviation(IList<double> values)
        {
            if (values == null || values.Count < 2) return 0.0;
            double mean = values.Average();
            double sum = values.Sum(x => (x - mean) * (x - mean));
            return Math.Sqrt(sum / (values.Count - 1));
        }

        private static double ConfidenceMargin95(double standardDeviation, int n)
        {
            if (n < 2) return 0.0;
            return StudentTCritical975(n - 1) * standardDeviation / Math.Sqrt(n);
        }

        private static double StudentTCritical975(int degreesOfFreedom)
        {
            double[] table = { 0, 12.706, 4.303, 3.182, 2.776, 2.571, 2.447, 2.365, 2.306, 2.262, 2.228, 2.201, 2.179, 2.160, 2.145, 2.131, 2.120, 2.110, 2.101, 2.093, 2.086, 2.080, 2.074, 2.069, 2.064, 2.060, 2.056, 2.052, 2.048, 2.045, 2.042 };
            if (degreesOfFreedom <= 0) return double.NaN;
            if (degreesOfFreedom < table.Length) return table[degreesOfFreedom];
            if (degreesOfFreedom <= 40) return 2.021;
            if (degreesOfFreedom <= 60) return 2.000;
            if (degreesOfFreedom <= 120) return 1.980;
            return 1.960;
        }

        private static RiskOptionsDto InferRiskOptions(RiskResultDto risk)
        {
            string samplingMode = string.IsNullOrWhiteSpace(risk.SamplingMode)
                ? "DELAY_UNILATERAL"
                : NormalizeSamplingMode(risk.SamplingMode);

            return new RiskOptionsDto
            {
                ScenarioCount = Math.Max(1, risk.Iterations),
                Gamma = risk.Gamma,
                Seed = risk.Seed,
                Enabled = true,
                HistogramBinCount = Math.Max(5, risk.HistogramCounts != null && risk.HistogramCounts.Count > 0 ? risk.HistogramCounts.Count : 20),
                SamplingMode = samplingMode,
                UseCommonRandomNumbers = true
            };
        }

        private static string ExtractRunLabelPart(string runLabel, int index, string fallback)
        {
            if (string.IsNullOrWhiteSpace(runLabel))
                return fallback;

            string[] parts = runLabel
                .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .ToArray();

            if (index < 0 || index >= parts.Length || string.IsNullOrWhiteSpace(parts[index]))
                return fallback;

            return parts[index];
        }

        private static CrashingCandidateActivityDto CloneCandidate(CrashingCandidateActivityDto source)
        {
            if (source == null)
                return null;

            return new CrashingCandidateActivityDto
            {
                Use = source.Use,
                ActivityId = source.ActivityId,
                ActivityName = source.ActivityName,
                NominalDuration = source.NominalDuration,
                MinimumDuration = source.MinimumDuration,
                NewDuration = source.NewDuration,
                RecommendedNewDuration = source.RecommendedNewDuration,
                IsEligible = source.IsEligible,
                IsDummy = source.IsDummy,
                FrmSlackI = source.FrmSlackI,
                FrmSensitivity = source.FrmSensitivity,
                FrmBalanceRisk = source.FrmBalanceRisk,
                FrmPriority = source.FrmPriority
            };
        }


        private static string BuildActivitiesLabel(Dictionary<int, int> crashedDurations)
        {
            if (crashedDurations == null || crashedDurations.Count == 0)
                return string.Empty;

            var keys = new List<int>(crashedDurations.Keys);
            keys.Sort();
            var parts = new List<string>(keys.Count);
            for (int i = 0; i < keys.Count; i++)
            {
                int activityId = keys[i];
                int duration;
                crashedDurations.TryGetValue(activityId, out duration);
                parts.Add(activityId + "→" + duration);
            }

            return string.Join(", ", parts);
        }

        private static string BuildSummaryText(CrashingResultDto result)
        {
            if (result == null)
                return string.Empty;

            int count = result.Scenarios != null ? result.Scenarios.Count : 0;
            if (count == 0)
                return "No crashing scenario was executed.";

            CrashingScenarioResultDto best = result.Scenarios[0];
            string bestName = best != null ? Safe(best.ScenarioName) : "-";
            double bestFrri = best != null ? best.Frri : 0.0;
            double bestRob = best != null ? best.RobustnessIndex : 0.0;
            string bestStatus = best != null ? Safe(best.StructuralStatus) : "-";

            return "Generated scenarios: " + result.GeneratedScenarioCount
                   + "; executed scenarios: " + result.ExecutedScenarioCount
                   + "; best scenario: " + bestName
                   + " (FRRI=" + bestFrri.ToString("0.###")
                   + ", Robustness=" + bestRob.ToString("0.###")
                   + ", Status=" + bestStatus + ").";
        }

        private static double ComputeSif(FrmResultDto frm)
        {
            return frm != null ? Math.Max(0.0, frm.SifGlobal) : 0.0;
        }

        private static double ComputeRobustnessIndex(FrmResultDto frm)
        {
            if (frm == null || frm.ResourceDiagnostics == null || frm.ResourceDiagnostics.Count == 0)
                return 0.0;

            double sum = 0.0;
            int count = 0;
            for (int i = 0; i < frm.ResourceDiagnostics.Count; i++)
            {
                var d = frm.ResourceDiagnostics[i];
                if (d == null)
                    continue;
                sum += Clamp01(d.RobustnessIndex);
                count++;
            }

            return count == 0 ? 0.0 : Clamp01(sum / count);
        }

        private static double ComputeStructuralPenalty(FrmResultDto frm, double sif, double robustnessIndex)
        {
            double penalty = 1.0 - Clamp01(0.50 * Clamp01(sif) + 0.50 * Clamp01(robustnessIndex));
            if (frm != null && !frm.IsStructurallyRobust)
                penalty = Math.Min(1.0, penalty + 0.25);
            return Clamp01(penalty);
        }

        private static RiskResultDto NormalizeRiskForReference(RiskResultDto risk, int referenceMakespan, string samplingMode)
        {
            if (risk == null)
                return new RiskResultDto { SamplingMode = samplingMode };

            if (!IsDelayUnilateral(samplingMode))
                return risk;

            var samples = risk.MakespanSamples == null
                ? new List<int>()
                : new List<int>(risk.MakespanSamples.Count);

            if (risk.MakespanSamples != null)
            {
                for (int i = 0; i < risk.MakespanSamples.Count; i++)
                    samples.Add(Math.Max(referenceMakespan, risk.MakespanSamples[i]));
            }

            samples.Sort();

            if (samples.Count == 0)
            {
                samples.Add(referenceMakespan);
            }

            int[] sortedMakespans = samples.ToArray();
            int[] sortedDelays = BuildDelaySamples(sortedMakespans, referenceMakespan);
            double cvar95Delay = CVar(sortedDelays, 0.95);

            return new RiskResultDto
            {
                Iterations = risk.Iterations > 0 ? risk.Iterations : samples.Count,
                Gamma = risk.Gamma,
                Seed = risk.Seed,
                SamplingMode = samplingMode,
                ReferenceMakespan = referenceMakespan,
                MeanMakespan = samples.Average(),
                P50 = Percentile(sortedMakespans, 0.50),
                P95 = Percentile(sortedMakespans, 0.95),
                CVaR95 = cvar95Delay,
                MakespanCVaR95 = CVar(sortedMakespans, 0.95),
                DelayProbability = ComputeDelayProbabilityFromSamples(sortedDelays),
                MeanDelay = sortedDelays.Length == 0 ? 0.0 : sortedDelays.Average(),
                P95Delay = Percentile(sortedDelays, 0.95),
                CVaR95Delay = cvar95Delay,
                MaxDelay = sortedDelays.Length == 0 ? 0.0 : sortedDelays[sortedDelays.Length - 1],
                MakespanSamples = samples,
                HistogramEdges = risk.HistogramEdges == null ? new List<double>() : new List<double>(risk.HistogramEdges),
                HistogramCounts = risk.HistogramCounts == null ? new List<int>() : new List<int>(risk.HistogramCounts),
                BalanceRuptureProbability = risk.BalanceRuptureProbability,
                MeanBalanceGenerated = risk.MeanBalanceGenerated,
                MeanBalanceConsumed = risk.MeanBalanceConsumed,
                MeanBalanceUsage = risk.MeanBalanceUsage,
                MeanBalanceUsageRatio = risk.MeanBalanceUsageRatio,
                MinObservedBalance = risk.MinObservedBalance,
                CVaR95GivenBalanceRupture = risk.CVaR95GivenBalanceRupture,
                MeanPositiveWorkDemand = risk.MeanPositiveWorkDemand,
                MeanUnabsorbedWork = risk.MeanUnabsorbedWork,
                P95UnabsorbedWork = risk.P95UnabsorbedWork,
                CVaR95UnabsorbedWork = risk.CVaR95UnabsorbedWork,
                MeanUnabsorbedWorkRatio = risk.MeanUnabsorbedWorkRatio,
                SummaryText = risk.SummaryText
            };
        }

        private static int[] BuildDelaySamples(int[] sortedMakespanSamples, int referenceMakespan)
        {
            if (sortedMakespanSamples == null || sortedMakespanSamples.Length == 0)
                return new int[0];

            var delays = new int[sortedMakespanSamples.Length];
            for (int i = 0; i < sortedMakespanSamples.Length; i++)
                delays[i] = Math.Max(0, sortedMakespanSamples[i] - referenceMakespan);

            Array.Sort(delays);
            return delays;
        }

        private static double ComputeDelayProbabilityFromSamples(int[] sortedDelaySamples)
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

        private static int[] GetSortedDelays(RiskResultDto risk, int nominalMakespan)
        {
            if (risk == null || risk.MakespanSamples == null || risk.MakespanSamples.Count == 0)
                return new int[0];

            var delays = new int[risk.MakespanSamples.Count];
            for (int i = 0; i < risk.MakespanSamples.Count; i++)
                delays[i] = Math.Max(0, risk.MakespanSamples[i] - nominalMakespan);

            Array.Sort(delays);
            return delays;
        }

        private static double ComputeDelayProbability(RiskResultDto risk, int nominalMakespan)
        {
            if (risk == null || risk.MakespanSamples == null || risk.MakespanSamples.Count == 0)
                return 0.0;

            int positive = 0;
            for (int i = 0; i < risk.MakespanSamples.Count; i++)
            {
                if (risk.MakespanSamples[i] > nominalMakespan)
                    positive++;
            }

            return positive / (double)risk.MakespanSamples.Count;
        }

        private static double ComputeMeanDelay(RiskResultDto risk, int nominalMakespan)
        {
            if (risk == null || risk.MakespanSamples == null || risk.MakespanSamples.Count == 0)
                return 0.0;

            double sum = 0.0;
            for (int i = 0; i < risk.MakespanSamples.Count; i++)
                sum += Math.Max(0, risk.MakespanSamples[i] - nominalMakespan);

            return sum / risk.MakespanSamples.Count;
        }

        private static double ComputeDelayPercentile(RiskResultDto risk, int nominalMakespan, double p)
        {
            return Percentile(GetSortedDelays(risk, nominalMakespan), p);
        }

        private static double ComputeDelayCVar(RiskResultDto risk, int nominalMakespan, double p)
        {
            return CVar(GetSortedDelays(risk, nominalMakespan), p);
        }

        private static double ComputeMaxDelay(RiskResultDto risk, int nominalMakespan)
        {
            var delays = GetSortedDelays(risk, nominalMakespan);
            return delays.Length == 0 ? 0.0 : delays[delays.Length - 1];
        }

        private static double Percentile(int[] sortedValues, double p)
        {
            if (sortedValues == null || sortedValues.Length == 0)
                return 0.0;
            if (p <= 0.0)
                return sortedValues[0];
            if (p >= 1.0)
                return sortedValues[sortedValues.Length - 1];

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
            double sum = 0.0;
            int count = 0;
            for (int i = 0; i < sortedValues.Length; i++)
            {
                if (sortedValues[i] >= threshold)
                {
                    sum += sortedValues[i];
                    count++;
                }
            }

            return count == 0 ? threshold : sum / count;
        }

        private static double ComputeRelativeRiskReduction(double baselineRisk, double scenarioRisk)
        {
            if (Math.Abs(baselineRisk) < 1e-9)
                return 0.0;
            return (baselineRisk - scenarioRisk) / baselineRisk;
        }

        private static double ComputeFrri(double baselineRisk, double scenarioRisk)
        {
            return ComputeRelativeRiskReduction(baselineRisk, scenarioRisk);
        }

        private static string ClassifyStructuralStatus(bool isStructurallyRobust, double sif, double robustness)
        {
            if (isStructurallyRobust && sif > 0.0 && robustness >= 0.20)
                return "ROBUST";
            if (isStructurallyRobust)
                return "FEASIBLE";
            if (sif > 0.0 || robustness > 0.05)
                return "FRAGILE";
            return "INVIABLE";
        }

        private static int GetStructuralRankBucket(string status)
        {
            switch (Safe(status).Trim().ToUpperInvariant())
            {
                case "ROBUST": return 0;
                case "FEASIBLE": return 1;
                case "FRAGILE": return 2;
                default: return 3;
            }
        }

        private static double ComputeScore(CrashingScenarioResultDto scenario, CrashingOptionsDto options)
        {
            if (scenario == null)
                return 0.0;

            double wMakespan = options != null ? options.ScoreWeightMakespan : 0.30;
            double wP95 = options != null ? options.ScoreWeightP95 : 0.25;
            double wCVaR = options != null ? options.ScoreWeightCVaR95 : 0.25;
            double wFrm = options != null ? options.ScoreWeightFrmRobustness : 0.20;

            double makespanGain = -scenario.DeltaMakespan;
            double p95Gain = scenario.RelativeP95Reduction;
            double cvarGain = scenario.Frri;
            double frmGain = Clamp01(scenario.RobustnessIndex) - Clamp01(scenario.StructuralPenalty);

            return (wMakespan * makespanGain) + (wP95 * p95Gain) + (wCVaR * cvarGain) + (wFrm * frmGain);
        }

        private static double Clamp01(double value)
        {
            if (value < 0.0)
                return 0.0;
            if (value > 1.0)
                return 1.0;
            return value;
        }

        private sealed class CandidateWorkingData
        {
            public CrashingCandidateActivityDto Candidate { get; set; }
            public int RawReductionPotential { get; set; }
            public double RawSensitivity { get; set; }
            public double RawBalanceRisk { get; set; }
            public FrmActivityResultDto FrmActivity { get; set; }
            public ActivityDto Activity { get; set; }
            public int ScheduleOrder { get; set; }
        }

        private sealed class ScenarioDefinition
        {
            public string Name { get; set; }
            public List<int> ActivityIds { get; set; } = new List<int>();
            public Dictionary<int, int> CrashedDurations { get; set; } = new Dictionary<int, int>();
        }
    }
}
