using System;
using System.Collections.Generic;

namespace RCPSP.WinForms.ExperimentalTese
{
    internal sealed class ExperimentalStudyConfig
    {
        public List<string> InputFiles { get; set; } = new List<string>();
        public string OutputDirectory { get; set; } = string.Empty;
        public int Iterations { get; set; } = 10000;
        public int Replications { get; set; } = 5;
        public int Seed { get; set; } = 12345;
        public double Gamma { get; set; } = 0.50;
        public string SamplingMode { get; set; } = "FRM_WORKCONTENT_BILATERAL";
        public int HistogramBins { get; set; } = 20;
        public double PositiveFlexibilityPercent { get; set; } = 25.0;
        public double NegativeFlexibilityPercent { get; set; } = 25.0;
        public int MaxCombinationSize { get; set; } = 3;
        public int MaxCandidateActivities { get; set; } = 20;
        public int MaxCrashingScenarios { get; set; } = 500;


        public int BranchAndBoundTimeLimitSeconds { get; set; } = 10;
        public bool RunSensitivity { get; set; } = true;
        public bool RunDryOnly { get; set; }
        public List<double> SensitivityGammas { get; set; } = new List<double> { 0.25, 0.50, 0.75, 1.00 };
        public List<string> BaselineHeuristics { get; set; } = new List<string> { "ALL" };
        public string ExperimentId { get; set; } = "EXP_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");


        public bool RunThreePolicies { get; set; } = false;


        public string SamplingModeUi { get; set; } = "FRM_WORKCONTENT_BILATERAL";
        public bool RunPairedUnilateralStructural { get; set; } = false;
        public bool UseCommonRandomNumbers { get; set; } = true;


        public bool UseRfRsStratifiedSelection { get; set; } = true;
        public int RfRsTargetInstanceCount { get; set; } = 0;
    }

    internal sealed class ExperimentalRunResult
    {
        public bool Success { get; set; }
        public string OutputDirectory { get; set; } = string.Empty;
        public int FilesSelected { get; set; }
        public int FilesValid { get; set; }
        public int FilesInvalid { get; set; }
        public int BaselinesGenerated { get; set; }
        public int FrmGenerated { get; set; }
        public int MonteCarloGenerated { get; set; }
        public int CrashingGenerated { get; set; }
        public int Errors { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    internal sealed class ScenarioRecord
    {
        public string InstanceId { get; set; } = string.Empty;
        public string Family { get; set; } = string.Empty;
        public string ScenarioId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string BaselineId { get; set; } = string.Empty;
        public string Heuristic { get; set; } = string.Empty;
        public string Scheme { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public string MethodClassification { get; set; } = string.Empty;
        public bool? BranchAndBoundTimeLimitReached { get; set; }
        public bool? BranchAndBoundOptimalityProven { get; set; }
        public int Makespan { get; set; }
        public double SifGlobal { get; set; }
        public double P50 { get; set; }
        public double P95 { get; set; }
        public double Cvar95 { get; set; }
        public double P95Relative { get; set; }
        public double Cvar95Relative { get; set; }
        public double ExcessP95 { get; set; }
        public double ExcessCvar95 { get; set; }
        public double MeanDelay { get; set; }
        public double P95Delay { get; set; }
        public double Cvar95Delay { get; set; }
        public double DelayProbability { get; set; }
        public double MaxDelay { get; set; }


        public double BalanceRuptureProbability { get; set; }
        public double MeanBalanceUsage { get; set; }
        public double MeanUnabsorbedWork { get; set; }
        public double MeanUnabsorbedWorkRatio { get; set; }
        public double DeltaBalanceRupture { get; set; }
        public double DeltaMeanBalanceUsage { get; set; }
        public double DeltaMeanUnabsorbedWork { get; set; }
        public double DeltaMeanUnabsorbedWorkRatio { get; set; }

        public double Frri { get; set; }
        public bool HasPaired { get; set; }
        public string PairedMode { get; set; } = string.Empty;
        public int PairedMakespan { get; set; }
        public double PairedSifGlobal { get; set; }
        public double PairedP95 { get; set; }
        public double PairedCvar95 { get; set; }
        public double PairedP95Relative { get; set; }
        public double PairedCvar95Relative { get; set; }
        public double PairedExcessP95 { get; set; }
        public double PairedExcessCvar95 { get; set; }
        public double PairedDelayProbability { get; set; }
        public double PairedMeanDelay { get; set; }
        public double PairedP95Delay { get; set; }
        public double PairedCvar95Delay { get; set; }
        public double PairedMaxDelay { get; set; }
        public double PairedFrri { get; set; }
        public double DeltaModesCvar95 { get; set; }
        public double DeltaModesP95 { get; set; }
        public double DeltaModesFrri { get; set; }
        public double DeltaModesSif { get; set; }
        public bool Dominated { get; set; }
        public string DominatedBy { get; set; } = string.Empty;
        public bool PairedDominated { get; set; }
        public string PairedDominatedBy { get; set; } = string.Empty;
        public string Classification { get; set; } = string.Empty;
        public string Observation { get; set; } = string.Empty;
        public string PairedObservation { get; set; } = string.Empty;


        public double ResourceFactor { get; set; }
        public double ResourceStrength { get; set; }
        public string RsRfBand { get; set; } = string.Empty;
    }


    internal sealed class WilcoxonResult
    {
        public int N { get; set; }
        public int NZeros { get; set; }
        public double WPlus { get; set; }
        public double WMinus { get; set; }
        public double Z { get; set; }
        public double PValueOneTailed { get; set; }
        public double EffectSizeR { get; set; }
        public bool Significant { get; set; }
        public string Note { get; set; } = string.Empty;
    }


    internal sealed class PolicyPairRecord
    {
        public string InstanceId { get; set; } = string.Empty;
        public string BaselineId { get; set; } = string.Empty;
        public double FrriFrmGuided { get; set; }
        public double FrriCriticalPath { get; set; }
        public double FrriRiskDriven { get; set; }

        public double DiffCrfVsCrt { get; set; }
        public double DiffCrfVsRrd { get; set; }
    }
}
