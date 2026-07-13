using RCPSP.Contracts;

namespace RCPSP.Application
{
    public interface IProjectScheduleWriter
    {
        void WriteSchedule(object activeProject, ExecutionSummary summary);
    }
}
