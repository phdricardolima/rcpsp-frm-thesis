using System;
using System.Collections.Generic;

namespace RCPSP.Contracts
{
    public sealed class ExecutionSummary
    {
        public BaselineResultDto Baseline { get; set; } = new BaselineResultDto();
        public List<BaselineRunSummaryDto> BaselineRuns { get; set; } = new List<BaselineRunSummaryDto>();
        public int SelectedBaselineRunIndex { get; set; } = 0;

        public FrmResultDto Frm { get; set; } = new FrmResultDto();
        public RiskResultDto Risk { get; set; } = new RiskResultDto();
        public CrashingResultDto Crashing { get; set; } = new CrashingResultDto();

        public List<StageTimingDto> StageTimings { get; set; } = new List<StageTimingDto>();


        public ExecutionSummary PairedStructuralSummary { get; set; }
        public ExecutionSummary PairedUnilateralSummary { get; set; }
        public string PairedComparisonMode { get; set; }
    }

    public sealed class StageTimingDto
    {
        public string StageName { get; set; } = string.Empty;
        public long ElapsedMilliseconds { get; set; }
    }

    public sealed class BaselineRunSummaryDto
    {
        public string RunType { get; set; } = string.Empty;
        public string Heuristic { get; set; } = string.Empty;
        public string Scheme { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;

        public bool IsExact { get; set; }
        public string ExactMode { get; set; } = string.Empty;
        public string EngineKey { get; set; } = string.Empty;
        public string MethodClassification { get; set; } = string.Empty;

        public bool Success { get; set; }
        public string Status { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;


        public int? BranchAndBoundTimeLimitSeconds { get; set; }
        public bool? BranchAndBoundTimeLimitReached { get; set; }
        public bool? BranchAndBoundOptimalityProven { get; set; }
        public long? BranchAndBoundNodesVisited { get; set; }
        public double? BranchAndBoundSlackSum { get; set; }
        public string BranchAndBoundTrace { get; set; } = string.Empty;

        public int Makespan { get; set; }
        public int ScheduledActivities { get; set; }

        public string PriorityListText { get; set; } = string.Empty;
        public string ScheduledOrderText { get; set; } = string.Empty;

        public BaselineResultDto BaselineResult { get; set; } = new BaselineResultDto();
    }

    public static class BaselineMethodClassifier
    {
        public const string Heuristic = "HEURISTIC";
        public const string ExactReference = "EXACT_REFERENCE";
        public const string ExactTimeLimit = "EXACT_TIME_LIMIT";
        public const string ExactUnproven = "EXACT_UNPROVEN";
        public const string ExactFailed = "EXACT_FAILED";
        public const string ExactPending = "EXACT_PENDING";

        public static string Classify(BaselineRunSummaryDto run, bool baselineValid)
        {
            if (run == null)
                return ExactFailed;

            if (!run.IsExact)
                return Heuristic;

            if (string.Equals(run.Status, "Running", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(run.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                return ExactPending;


            if (run.BranchAndBoundTimeLimitReached == true)
                return ExactTimeLimit;

            if (!run.Success || !baselineValid || run.BaselineResult == null ||
                run.BaselineResult.Activities == null || run.BaselineResult.Activities.Count == 0)
                return ExactFailed;

            if (run.BranchAndBoundTimeLimitReached == false &&
                run.BranchAndBoundOptimalityProven == true)
                return ExactReference;

            return ExactUnproven;
        }
    }

    public sealed class BaselineResultDto
    {
        public string RunLabel { get; set; } = string.Empty;
        public int Makespan { get; set; }
        public List<int> PriorityList { get; set; } = new List<int>();
        public List<int> ScheduledOrder { get; set; } = new List<int>();
        public List<int> Sequence { get; set; } = new List<int>();
        public Dictionary<int, int> StartTimesByActivity { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> FinishTimesByActivity { get; set; } = new Dictionary<int, int>();
        public List<ScheduledActivityDto> Activities { get; set; } = new List<ScheduledActivityDto>();
    }

    public sealed class ScheduledActivityDto
    {
        public int ActivityId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Start { get; set; }
        public int Finish { get; set; }
        public int Duration { get; set; }
    }

    public sealed class FrmResultDto
    {
        public string Heuristic { get; set; } = string.Empty;
        public string Scheme { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;

        public int Makespan { get; set; }
        public double FlexPositivePercent { get; set; }
        public double FlexNegativePercent { get; set; }

        public List<int> Sequence { get; set; } = new List<int>();
        public List<FrmActivityResultDto> Activities { get; set; } = new List<FrmActivityResultDto>();
        public Dictionary<int, int> Balance0ByResourceId { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, double> SifByResourceId { get; set; } = new Dictionary<int, double>();
        public double SifGlobal { get; set; }
        public List<FrmResourceDiagnosticDto> ResourceDiagnostics { get; set; } = new List<FrmResourceDiagnosticDto>();

        public bool IsStructurallyRobust { get; set; }
        public string SummaryText { get; set; } = string.Empty;
    }

    public sealed class FrmActivityResultDto
    {
        public int ActivityId { get; set; }
        public string ActivityName { get; set; } = string.Empty;
        public int Start { get; set; }
        public int Finish { get; set; }

        public int DurationNominal { get; set; }
        public int SlackI { get; set; }

        public double DSup { get; set; }
        public int DMax { get; set; }
        public double DInf { get; set; }
        public int DMin { get; set; }
        public int DSMax { get; set; }
        public int DSMin { get; set; }
        public int DNew { get; set; }


        public int StructuralDurationBeforeBalance { get; set; }
        public int StructuralDurationAfterBalance { get; set; }
        public bool IsCritical { get; set; }
        public bool WasBalanceLimited { get; set; }
        public int? LimitingResourceId { get; set; }
        public string DurationDecision { get; set; } = string.Empty;

        public Dictionary<int, int> SlackIkByResourceId { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> ScoreBrutoByResourceId { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> ScoreIkByResourceId { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> BalanceBeforeByResourceId { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> BalanceGeneratedByResourceId { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> BalanceRequestedByResourceId { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> BalanceConsumedByResourceId { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> BalanceByResourceId { get; set; } = new Dictionary<int, int>();
    }

    public sealed class FrmResourceDiagnosticDto
    {
        public int ResourceId { get; set; }
        public string ResourceName { get; set; } = string.Empty;
        public int Balance0 { get; set; }
        public int BalanceFinal { get; set; }
        public int FinalBalance
        {
            get { return BalanceFinal; }
            set { BalanceFinal = value; }
        }
        public bool IsRobust { get; set; }
        public double RobustnessIndex { get; set; }
        public double Sif { get; set; }
        public string Classification { get; set; } = string.Empty;
    }

    public sealed class RiskResultDto
    {
        public int Iterations { get; set; }
        public double Gamma { get; set; }
        public int Seed { get; set; }


        public long RuntimeMs { get; set; }
        public string SamplingMode { get; set; } = "FRM_WORKCONTENT_BILATERAL";
        public double ReferenceMakespan { get; set; }
        public double MeanMakespan { get; set; }
        public double P50 { get; set; }
        public double P95 { get; set; }


        public double CVaR95 { get; set; }


        public double MakespanCVaR95 { get; set; }

        public double DelayProbability { get; set; }
        public double MeanDelay { get; set; }
        public double P95Delay { get; set; }
        public double CVaR95Delay { get; set; }
        public double MaxDelay { get; set; }


        public double BalanceRuptureProbability { get; set; }
        public double MeanBalanceGenerated { get; set; }
        public double MeanBalanceConsumed { get; set; }
        public double MeanBalanceUsage { get; set; }
        public double MeanBalanceUsageRatio { get; set; }
        public double MinObservedBalance { get; set; }
        public double CVaR95GivenBalanceRupture { get; set; }


        public double MeanPositiveWorkDemand { get; set; }
        public double MeanUnabsorbedWork { get; set; }
        public double P95UnabsorbedWork { get; set; }
        public double CVaR95UnabsorbedWork { get; set; }
        public double MeanUnabsorbedWorkRatio { get; set; }

        public List<int> MakespanSamples { get; set; } = new List<int>();
        public List<int> SimulatedMakespans
        {
            get { return MakespanSamples; }
            set { MakespanSamples = value ?? new List<int>(); }
        }
        public List<double> HistogramEdges { get; set; } = new List<double>();
        public List<int> HistogramCounts { get; set; } = new List<int>();
        public string SummaryText { get; set; } = string.Empty;


        public int ReplicationCount { get; set; } = 1;
        public List<int> ReplicationSeeds { get; set; } = new List<int>();
        public List<double> ReplicationP95 { get; set; } = new List<double>();
        public List<double> ReplicationCVaR95Delay { get; set; } = new List<double>();
        public List<double> ReplicationDelayProbability { get; set; } = new List<double>();
        public List<double> ReplicationMeanDelay { get; set; } = new List<double>();
        public List<ResourceAbsorptionMetricDto> ResourceAbsorption { get; set; } = new List<ResourceAbsorptionMetricDto>();


        public RiskResultDto PairedStructuralResult { get; set; }
        public RiskResultDto PairedUnilateralResult { get; set; }
        public string PairedComparisonMode { get; set; }
    }

    public sealed class ResourceAbsorptionMetricDto
    {
        public int ResourceId { get; set; }
        public double MeanBalanceGenerated { get; set; }
        public double MeanBalanceConsumed { get; set; }
        public double MeanPositiveWorkDemand { get; set; }
        public double MeanUnabsorbedWork { get; set; }
        public double MeanUnabsorbedWorkRatio { get; set; }
        public double RuptureProbability { get; set; }
        public double MinObservedBalance { get; set; }
    }

    public sealed class CrashingResultDto
    {
        public List<CrashingCandidateActivityDto> Candidates { get; set; } = new List<CrashingCandidateActivityDto>();
        public List<CrashingScenarioResultDto> Scenarios { get; set; } = new List<CrashingScenarioResultDto>();
        public int GeneratedScenarioCount { get; set; }
        public int ExecutedScenarioCount { get; set; }
        public string SummaryText { get; set; } = string.Empty;


        public CrashingResultDto PairedStructuralResult { get; set; }
        public CrashingResultDto PairedUnilateralResult { get; set; }
        public string PairedComparisonMode { get; set; }
    }

    public sealed class CrashingCandidateActivityDto
    {
        public bool Use { get; set; }
        public int ActivityId { get; set; }
        public string ActivityName { get; set; } = string.Empty;
        public int NominalDuration { get; set; }
        public int MinimumDuration { get; set; }
        public int NewDuration { get; set; }
        public int RecommendedNewDuration { get; set; }
        public bool IsEligible { get; set; } = true;
        public bool IsDummy { get; set; }
        public int FrmSlackI { get; set; }
        public double FrmCriticality { get; set; }
        public double FrmSensitivity { get; set; }
        public double FrmBalanceRisk { get; set; }
        public double FrmPriority { get; set; }

        public string FrmBalanceRiskBand
        {
            get
            {
                return FrmBalanceRisk >= 0.66 ? "HIGH"
                    : FrmBalanceRisk >= 0.33 ? "MED"
                    : "LOW";
            }
        }

        public string FrmBalanceRiskDisplay
        {
            get { return string.Format("{0} ({1:0.###})", FrmBalanceRiskBand, FrmBalanceRisk); }
        }

        public int Reduction
        {
            get { return NominalDuration - NewDuration; }
        }
    }

    public sealed class CrashingScenarioResultDto
    {
        public string ScenarioName { get; set; } = string.Empty;
        public List<int> ActivityIds { get; set; } = new List<int>();
        public Dictionary<int, int> CrashedDurations { get; set; } = new Dictionary<int, int>();
        public int ActivityCount { get; set; }
        public int BaselineMakespan { get; set; }
        public int ScenarioMakespan { get; set; }
        public int DeltaMakespan { get; set; }
        public double BaselineP50 { get; set; }
        public double ScenarioP50 { get; set; }
        public double BaselineP95 { get; set; }
        public double ScenarioP95 { get; set; }
        public double DeltaP95 { get; set; }
        public double BaselineCVaR95 { get; set; }
        public double ScenarioCVaR95 { get; set; }
        public double DeltaCVaR95 { get; set; }
        public double BaselineDelayProbability { get; set; }
        public double ScenarioDelayProbability { get; set; }
        public double BaselineMeanDelay { get; set; }
        public double ScenarioMeanDelay { get; set; }
        public double BaselineP95Delay { get; set; }
        public double ScenarioP95Delay { get; set; }
        public double BaselineCVaR95Delay { get; set; }
        public double ScenarioCVaR95Delay { get; set; }
        public double DeltaCVaR95Delay { get; set; }
        public double BaselineMaxDelay { get; set; }
        public double ScenarioMaxDelay { get; set; }

        public double BaselineBalanceRuptureProbability { get; set; }
        public double ScenarioBalanceRuptureProbability { get; set; }
        public double DeltaBalanceRuptureProbability { get; set; }
        public double BaselineMeanBalanceUsage { get; set; }
        public double ScenarioMeanBalanceUsage { get; set; }
        public double DeltaMeanBalanceUsage { get; set; }
        public double BaselineMinObservedBalance { get; set; }
        public double ScenarioMinObservedBalance { get; set; }
        public double DeltaMinObservedBalance { get; set; }
        public double BaselineMeanUnabsorbedWork { get; set; }
        public double ScenarioMeanUnabsorbedWork { get; set; }
        public double DeltaMeanUnabsorbedWork { get; set; }
        public double BaselineMeanUnabsorbedWorkRatio { get; set; }
        public double ScenarioMeanUnabsorbedWorkRatio { get; set; }
        public double DeltaMeanUnabsorbedWorkRatio { get; set; }

        public double RelativeP95Reduction { get; set; }
        public double Frri { get; set; }
        public int ReplicationCount { get; set; }
        public double ScenarioCVaR95StdDev { get; set; }
        public double ScenarioCVaR95CiLower { get; set; }
        public double ScenarioCVaR95CiUpper { get; set; }
        public double FrriStdDev { get; set; }
        public double FrriCiLower { get; set; }
        public double FrriCiUpper { get; set; }
        public double Sif { get; set; }
        public double RobustnessIndex { get; set; }
        public bool IsStructurallyRobust { get; set; }
        public string StructuralStatus { get; set; } = string.Empty;
        public bool IsStructurallyAcceptable { get; set; }
        public int StructuralRankBucket { get; set; }
        public double StructuralPenalty { get; set; }
        public double Score { get; set; }
        public int Rank { get; set; }
        public string ActivitiesLabel { get; set; } = string.Empty;
    }
}
