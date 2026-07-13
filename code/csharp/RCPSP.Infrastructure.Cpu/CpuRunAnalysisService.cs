using System;
using System.Collections.Generic;
using System.Diagnostics;
using RCPSP.Application;
using RCPSP.Contracts;

namespace RCPSP.Infrastructure.Cpu
{
    public sealed class CpuRunAnalysisService : IRunAnalysisService
    {
        private readonly IFrmCalculator _frmCalculator;
        private readonly IRiskAnalyzer _riskAnalyzer;
        private readonly ICrashingAnalyzer _crashingAnalyzer;

        public CpuRunAnalysisService(
            IFrmCalculator frmCalculator,
            IRiskAnalyzer riskAnalyzer,
            ICrashingAnalyzer crashingAnalyzer)
        {
            _frmCalculator = frmCalculator ?? throw new ArgumentNullException(nameof(frmCalculator));
            _riskAnalyzer = riskAnalyzer ?? throw new ArgumentNullException(nameof(riskAnalyzer));
            _crashingAnalyzer = crashingAnalyzer ?? throw new ArgumentNullException(nameof(crashingAnalyzer));
        }

        public ExecutionSummary AnalyzeSelectedRun(
            ProjectDataDto project,
            BaselineRunSummaryDto selectedRun,
            FrmOptionsDto frmOptions,
            RiskOptionsDto riskOptions,
            CrashingOptionsDto crashingOptions)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));
            if (selectedRun == null)
                throw new ArgumentNullException(nameof(selectedRun));
            if (selectedRun.BaselineResult == null ||
                selectedRun.BaselineResult.Activities == null ||
                selectedRun.BaselineResult.Activities.Count == 0)
                throw new InvalidOperationException("The selected run does not have a valid baseline.");


            var baseline = selectedRun.BaselineResult;
            var timings = new List<StageTimingDto>(3);
            var stopwatch = Stopwatch.StartNew();

            var frm = _frmCalculator.Run(project, baseline, frmOptions ?? new FrmOptionsDto());
            stopwatch.Stop();
            timings.Add(new StageTimingDto { StageName = "FRM", ElapsedMilliseconds = stopwatch.ElapsedMilliseconds });

            stopwatch.Restart();
            var risk = _riskAnalyzer.Run(project, baseline, frm, riskOptions ?? new RiskOptionsDto());
            stopwatch.Stop();
            timings.Add(new StageTimingDto { StageName = "Risk", ElapsedMilliseconds = stopwatch.ElapsedMilliseconds });

            stopwatch.Restart();
            var crashing = _crashingAnalyzer.Run(project, baseline, frm, risk, crashingOptions ?? new CrashingOptionsDto());
            stopwatch.Stop();
            timings.Add(new StageTimingDto { StageName = "Crashing", ElapsedMilliseconds = stopwatch.ElapsedMilliseconds });

            var summary = new ExecutionSummary
            {
                Baseline = baseline,
                Frm = frm,
                Risk = risk,
                Crashing = crashing,
                StageTimings = timings
            };


            if (riskOptions != null && riskOptions.RunPairedUnilateralStructural
                && !string.IsNullOrEmpty(risk.PairedComparisonMode))
            {
                var pairedRisk = risk.PairedStructuralResult ?? risk.PairedUnilateralResult;
                if (pairedRisk != null)
                {
                    stopwatch.Restart();
                    var pairedCrashing = _crashingAnalyzer.Run(
                        project, baseline, frm, pairedRisk,
                        crashingOptions ?? new CrashingOptionsDto());
                    stopwatch.Stop();
                    timings.Add(new StageTimingDto { StageName = "PairedCrashing", ElapsedMilliseconds = stopwatch.ElapsedMilliseconds });

                    var pairedSummary = new ExecutionSummary
                    {
                        Baseline = baseline,
                        Frm = frm,
                        Risk = pairedRisk,
                        Crashing = pairedCrashing
                    };

                    if (string.Equals(risk.PairedComparisonMode, "UNILATERAL_STRUCTURAL", StringComparison.OrdinalIgnoreCase))
                        summary.PairedStructuralSummary = pairedSummary;
                    else
                        summary.PairedUnilateralSummary = pairedSummary;

                    summary.PairedComparisonMode = risk.PairedComparisonMode;
                    crashing.PairedComparisonMode = risk.PairedComparisonMode;

                    if (string.Equals(risk.PairedComparisonMode, "UNILATERAL_STRUCTURAL", StringComparison.OrdinalIgnoreCase))
                        crashing.PairedStructuralResult = pairedCrashing;
                    else
                        crashing.PairedUnilateralResult = pairedCrashing;
                }
            }

            return summary;
        }

        private static string NormalizeSamplingMode(string samplingMode)
        {
            if (string.Equals(samplingMode, "DELAY_STRUCTURAL", StringComparison.OrdinalIgnoreCase))
                return "DELAY_STRUCTURAL";
            return "DELAY_UNILATERAL";
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
    }
}
