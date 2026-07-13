using RCPSP.Scheduling.Model;

namespace RCPSP.Scheduling.Cpm
{
    public interface ICpmCalculator
    {
        void Compute(SchedulingProjectData project);
    }
}
