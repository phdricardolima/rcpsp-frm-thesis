using RCPSP.Contracts;
using System.Collections.Generic;

namespace RCPSP.Application
{
    public interface IBaselineScheduler
    {
        BaselineResultDto Run(ProjectDataDto project, SchedulingOptionsDto options);


        BaselineResultDto RunWithInheritedOrder(ProjectDataDto project, IReadOnlyList<int> inheritedOrder, SchedulingOptionsDto options);
    }
}
