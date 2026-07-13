using RCPSP.Contracts;

namespace RCPSP.Application
{
    public interface ICrashingAnalyzer
    {
        CrashingResultDto Run(
            ProjectDataDto project,
            BaselineResultDto baseline,
            FrmResultDto frm,
            RiskResultDto risk,
            CrashingOptionsDto options);
    }
}
