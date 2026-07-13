namespace RCPSP.Contracts
{
    public sealed class ExecutionRequest
    {
        public ProjectDataDto Project { get; set; } = new ProjectDataDto();
        public SchedulingOptionsDto Scheduling { get; set; } = new SchedulingOptionsDto();
        public FrmOptionsDto Frm { get; set; } = new FrmOptionsDto();
        public RiskOptionsDto Risk { get; set; } = new RiskOptionsDto();
        public CrashingOptionsDto Crashing { get; set; } = new CrashingOptionsDto();
    }

    public sealed class SchedulingOptionsDto
    {
        public string Heuristic { get; set; } = "SPT";
        public string Scheme { get; set; } = "SERIAL";
        public string Direction { get; set; } = "FORWARD";

        public bool UseExactEngine { get; set; } = false;
        public string Engine { get; set; } = "HEURISTIC";
        public string RunLabel { get; set; } = string.Empty;


        public int? BranchAndBoundTimeLimitSeconds { get; set; }
    }

    public sealed class FrmOptionsDto
    {
        public double PositiveFlexibilityPercent { get; set; } = 25;
        public double NegativeFlexibilityPercent { get; set; } = 25;
        public string Mode { get; set; } = "NORMAL";
        public bool Enabled { get; set; } = true;
    }

    public sealed class RiskOptionsDto
    {
        public int ScenarioCount { get; set; } = 1000;
        public double Gamma { get; set; } = 0.2;
        public int Seed { get; set; } = 123;
        public bool Enabled { get; set; } = true;
        public int HistogramBinCount { get; set; } = 20;


        public string SamplingMode { get; set; } = "FRM_WORKCONTENT_BILATERAL";


        public bool UseCommonRandomNumbers { get; set; } = true;


        public bool RunPairedUnilateralStructural { get; set; } = false;
    }

    public sealed class CrashingOptionsDto
    {
        public bool Enabled { get; set; } = true;


        public string CrashingPolicyMode { get; set; } = "FRM_GUIDED";

        public int MaxActivitiesToCrash { get; set; } = 6;
        public int MaxCombinationSize { get; set; } = 3;
        public int MaxScenarioCount { get; set; } = 1000;


        public int? BranchAndBoundTimeLimitSeconds { get; set; }


        public bool CandidatesOnly { get; set; } = false;

        public bool RecalculateRiskAfterCrash { get; set; } = true;
        public bool UseFrmGuidance { get; set; } = true;
        public bool PrioritizeStructuralAcceptability { get; set; } = true;
        public bool KeepProblematicScenariosVisible { get; set; } = true;
        public double ScoreWeightMakespan { get; set; } = 0.30;
        public double ScoreWeightP95 { get; set; } = 0.25;
        public double ScoreWeightCVaR95 { get; set; } = 0.25;
        public double ScoreWeightFrmRobustness { get; set; } = 0.20;
        public System.Collections.Generic.List<CrashingCandidateActivityDto> CandidateActivities { get; set; } = new System.Collections.Generic.List<CrashingCandidateActivityDto>();
    }
}
