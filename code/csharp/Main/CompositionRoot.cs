using RCPSP.Application;
using RCPSP.Infrastructure.Cpu;
using RCPSP.Infrastructure.MsProject;

namespace Main
{
    internal static class CompositionRoot
    {
        public static IPipelineRunner BuildPipelineRunner()
        {
            var baselineScheduler = new CpuBaselineScheduler();
            var exactBaselineScheduler = new CpuExactBaselineScheduler();
            var baselineBatchScheduler = new CpuBaselineBatchScheduler(baselineScheduler);

            var frmCalculator = new CpuFrmCalculator();
            var riskAnalyzer = new CpuRiskAnalyzer();
            var crashingAnalyzer = new CpuCrashingAnalyzer(
                baselineScheduler,
                exactBaselineScheduler,
                frmCalculator,
                riskAnalyzer);

            return new PipelineRunner(
                baselineBatchScheduler,
                frmCalculator,
                riskAnalyzer,
                crashingAnalyzer);
        }

        public static IRunAnalysisService BuildRunAnalysisService()
        {
            var baselineScheduler = new CpuBaselineScheduler();
            var exactBaselineScheduler = new CpuExactBaselineScheduler();
            var frmCalculator = new CpuFrmCalculator();
            var riskAnalyzer = new CpuRiskAnalyzer();
            var crashingAnalyzer = new CpuCrashingAnalyzer(
                baselineScheduler,
                exactBaselineScheduler,
                frmCalculator,
                riskAnalyzer);

            return new CpuRunAnalysisService(
                frmCalculator,
                riskAnalyzer,
                crashingAnalyzer);
        }

        public static IProjectDataReader BuildReader()
        {
            return new MsProjectReader();
        }

        public static IProjectScheduleWriter BuildWriter()
        {
            return new MsProjectWriter();
        }

        public static IProjectImporter BuildImporter()
        {
            return new MsProjectPsplibImporter();
        }
    }
}
