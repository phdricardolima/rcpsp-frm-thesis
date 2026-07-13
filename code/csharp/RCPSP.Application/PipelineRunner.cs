// Thesis traceability: Appendix A, Algorithm A.1 (integrated experimental pipeline).
using RCPSP.Contracts;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RCPSP.Application
{
    public sealed class PipelineRunner : IPipelineRunner
    {
        private readonly IBaselineBatchScheduler _baselineBatchScheduler;
        private readonly IFrmCalculator _frmCalculator;
        private readonly IRiskAnalyzer _riskAnalyzer;
        private readonly ICrashingAnalyzer _crashingAnalyzer;

        public PipelineRunner(
            IBaselineBatchScheduler baselineBatchScheduler,
            IFrmCalculator frmCalculator,
            IRiskAnalyzer riskAnalyzer,
            ICrashingAnalyzer crashingAnalyzer)
        {
            _baselineBatchScheduler = baselineBatchScheduler;
            _frmCalculator = frmCalculator;
            _riskAnalyzer = riskAnalyzer;
            _crashingAnalyzer = crashingAnalyzer;
        }

        public ExecutionSummary Run(ExecutionRequest request)
        {
            var timings = new List<StageTimingDto>(4);

            var stopwatch = Stopwatch.StartNew();
            var runs = _baselineBatchScheduler.Run(request.Project, request.Scheduling);
            stopwatch.Stop();
            timings.Add(new StageTimingDto { StageName = "BaselineBatch", ElapsedMilliseconds = stopwatch.ElapsedMilliseconds });

            if (IsAllBaselineMode(request))
            {
                return new ExecutionSummary
                {
                    Baseline = new BaselineResultDto(),
                    BaselineRuns = runs,
                    SelectedBaselineRunIndex = -1,
                    Frm = new FrmResultDto(),
                    Risk = new RiskResultDto(),
                    Crashing = new CrashingResultDto(),
                    StageTimings = timings
                };
            }

            var selectedRun = runs.FirstOrDefault(r => r.Success)
                             ?? runs.FirstOrDefault()
                             ?? new BaselineRunSummaryDto
                             {
                                 Status = "Error",
                                 ErrorMessage = "No baseline run was generated."
                             };

            var baseline = selectedRun.BaselineResult ?? new BaselineResultDto();

            stopwatch.Restart();
            var frm = _frmCalculator.Run(request.Project, baseline, request.Frm);
            stopwatch.Stop();
            timings.Add(new StageTimingDto { StageName = "FRM", ElapsedMilliseconds = stopwatch.ElapsedMilliseconds });

            stopwatch.Restart();
            var risk = _riskAnalyzer.Run(request.Project, baseline, frm, request.Risk);
            stopwatch.Stop();
            timings.Add(new StageTimingDto { StageName = "Risk", ElapsedMilliseconds = stopwatch.ElapsedMilliseconds });

            stopwatch.Restart();
            var crashing = _crashingAnalyzer.Run(request.Project, baseline, frm, risk, request.Crashing);
            stopwatch.Stop();
            timings.Add(new StageTimingDto { StageName = "Crashing", ElapsedMilliseconds = stopwatch.ElapsedMilliseconds });

            var summary = new ExecutionSummary
            {
                Baseline = baseline,
                BaselineRuns = runs,
                SelectedBaselineRunIndex = runs.IndexOf(selectedRun),
                Frm = frm,
                Risk = risk,
                Crashing = crashing,
                StageTimings = timings
            };

            if (request != null && request.Risk != null && request.Risk.RunPairedUnilateralStructural
                && risk != null && !string.IsNullOrEmpty(risk.PairedComparisonMode))
            {
                var pairedRisk = risk.PairedStructuralResult ?? risk.PairedUnilateralResult;
                if (pairedRisk != null)
                {
                    stopwatch.Restart();
                    var pairedCrashing = _crashingAnalyzer.Run(request.Project, baseline, frm, pairedRisk, request.Crashing);
                    stopwatch.Stop();
                    timings.Add(new StageTimingDto { StageName = "PairedCrashing", ElapsedMilliseconds = stopwatch.ElapsedMilliseconds });

                    var pairedSummary = new ExecutionSummary
                    {
                        Baseline = baseline,
                        BaselineRuns = runs,
                        SelectedBaselineRunIndex = runs.IndexOf(selectedRun),
                        Frm = frm,
                        Risk = pairedRisk,
                        Crashing = pairedCrashing
                    };

                    if (risk.PairedComparisonMode.Equals("UNILATERAL_STRUCTURAL", System.StringComparison.OrdinalIgnoreCase))
                    {
                        summary.PairedStructuralSummary = pairedSummary;
                        crashing.PairedStructuralResult = pairedCrashing;
                    }
                    else
                    {
                        summary.PairedUnilateralSummary = pairedSummary;
                        crashing.PairedUnilateralResult = pairedCrashing;
                    }

                    summary.PairedComparisonMode = risk.PairedComparisonMode;
                    crashing.PairedComparisonMode = risk.PairedComparisonMode;
                }
            }

            return summary;
        }

        private static bool IsAllBaselineMode(ExecutionRequest request)
        {
            var heuristic = request != null && request.Scheduling != null
                ? request.Scheduling.Heuristic
                : string.Empty;

            return !string.IsNullOrWhiteSpace(heuristic)
                   && heuristic.Trim().Equals("ALL", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
