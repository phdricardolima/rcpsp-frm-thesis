using RCPSP.Contracts;

namespace RCPSP.Application
{
    public interface IRiskAnalyzer
    {
        RiskResultDto Run(ProjectDataDto project, BaselineResultDto baseline, FrmResultDto frm, RiskOptionsDto options);
    }
}
