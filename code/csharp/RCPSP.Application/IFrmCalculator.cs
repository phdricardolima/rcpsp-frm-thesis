using RCPSP.Contracts;

namespace RCPSP.Application
{
    public interface IFrmCalculator
    {
        FrmResultDto Run(ProjectDataDto project, BaselineResultDto baseline, FrmOptionsDto options);
    }
}
