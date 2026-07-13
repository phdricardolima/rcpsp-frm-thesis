using RCPSP.Contracts;

namespace RCPSP.Application
{
    public interface IRunAnalysisService
    {
        ExecutionSummary AnalyzeSelectedRun(
            ProjectDataDto project,
            BaselineRunSummaryDto selectedRun,
            FrmOptionsDto frmOptions,
            RiskOptionsDto riskOptions,
            CrashingOptionsDto crashingOptions);
    }
}
