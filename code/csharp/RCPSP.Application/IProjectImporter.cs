namespace RCPSP.Application
{
    public interface IProjectImporter
    {
        void ImportPsplibRcp(string filePath, object applicationInstance);
    }
}
