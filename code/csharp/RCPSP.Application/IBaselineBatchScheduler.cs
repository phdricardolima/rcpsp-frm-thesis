using RCPSP.Contracts;
using System.Collections.Generic;

namespace RCPSP.Application
{
    public interface IBaselineBatchScheduler
    {
        List<BaselineRunSummaryDto> Run(ProjectDataDto project, SchedulingOptionsDto options);
    }
}
