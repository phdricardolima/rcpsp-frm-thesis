using RCPSP.Contracts;

namespace RCPSP.Application
{
    public interface IProjectDataReader
    {
        ProjectDataDto ReadActiveProject(object activeProject);
    }
}
