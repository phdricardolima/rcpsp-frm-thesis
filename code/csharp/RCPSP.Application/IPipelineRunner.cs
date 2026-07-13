using RCPSP.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RCPSP.Application
{
    public interface IPipelineRunner
    {
        ExecutionSummary Run(ExecutionRequest request);
    }
}
