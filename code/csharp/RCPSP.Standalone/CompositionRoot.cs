using RCPSP.Application;
using RCPSP.Infrastructure.Cpu;

namespace RCPSP.Standalone
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
    }
}
