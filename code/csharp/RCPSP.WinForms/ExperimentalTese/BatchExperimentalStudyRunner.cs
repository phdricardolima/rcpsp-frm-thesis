// Thesis traceability: computational-study orchestration, deduplication, sensitivity, consolidated outputs, and audits.
using RCPSP.Application;
using RCPSP.Contracts;
using RCPSP.Infrastructure.Cpu;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace RCPSP.WinForms.ExperimentalTese
{
    internal sealed class BatchExperimentalStudyRunner
    {
        private readonly RcpProjectDataImporter _importer = new RcpProjectDataImporter();
        private readonly IBaselineBatchScheduler _baselineBatchScheduler;
        private readonly IRunAnalysisService _runAnalysisService;
        private readonly IFrmCalculator _frmCalculator;
        private readonly IRiskAnalyzer _riskAnalyzer;
        private readonly ICrashingAnalyzer _crashingAnalyzer;

        private readonly List<ScenarioRecord> _integrated = new List<ScenarioRecord>();
        private readonly List<string[]> _allBaselines = new List<string[]>();
        private readonly List<string[]> _allFrm = new List<string[]>();
        private readonly List<string[]> _allFrmDetails = new List<string[]>();
        private readonly List<string[]> _allRisk = new List<string[]>();
        private readonly List<string[]> _allCrashing = new List<string[]>();
        private readonly List<string[]> _allCrashingCandidates = new List<string[]>();
        private readonly List<string[]> _errorRows = new List<string[]>();
        private readonly List<string[]> _warningRows = new List<string[]>();
        private readonly List<string[]> _pipelineRows = new List<string[]>();
        private readonly List<string[]> _excludedRows = new List<string[]>();
        private readonly List<string[]> _sensitivityRows = new List<string[]>();
        private readonly List<string[]> _confidenceRows = new List<string[]>();
        private readonly List<string[]> _replicationRows = new List<string[]>();
        private readonly List<string[]> _stabilityRows = new List<string[]>();
        private readonly List<string[]> _baselineDeduplicationRows = new List<string[]>();
        private readonly List<string[]> _absorptionByGammaRows = new List<string[]>();
        private readonly List<string[]> _resourceAbsorptionRows = new List<string[]>();
        private readonly List<string[]> _crashingPolicyRows = new List<string[]>();
        private readonly List<string[]> _instanceSelectionRows = new List<string[]>();
        private double _rfSelectionThreshold;
        private double _rsSelectionThreshold;


        private readonly List<string[]> _policyComparisonRows = new List<string[]>();
        private readonly List<PolicyPairRecord> _policyPairs = new List<PolicyPairRecord>();


        private readonly Dictionary<string, double> _rfByInstance = new Dictionary<string, double>();
        private readonly Dictionary<string, double> _rsByInstance = new Dictionary<string, double>();

        public BatchExperimentalStudyRunner()
        {
            var baselineScheduler = new CpuBaselineScheduler();
            var exactBaselineScheduler = new CpuExactBaselineScheduler();
            _baselineBatchScheduler = new CpuBaselineBatchScheduler(baselineScheduler);
            _frmCalculator = new CpuFrmCalculator();
            _riskAnalyzer = new CpuRiskAnalyzer();
            _crashingAnalyzer = new CpuCrashingAnalyzer(baselineScheduler, exactBaselineScheduler, _frmCalculator, _riskAnalyzer);
            _runAnalysisService = new CpuRunAnalysisService(_frmCalculator, _riskAnalyzer, _crashingAnalyzer);
        }

        public ExperimentalRunResult Run(ExperimentalStudyConfig config, Action<string, int> progress)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (config.InputFiles == null || config.InputFiles.Count == 0)
                throw new InvalidOperationException("No .rcp file was selected.");

            if (string.IsNullOrWhiteSpace(config.OutputDirectory))
                throw new InvalidOperationException("The output folder was not provided.");

            ResetState();
            PrepareDirectories(config.OutputDirectory);
            if (!config.RunDryOnly)
            {
                ClearOutputFilesForFreshRun(config.OutputDirectory);
                WriteExperimentConfig(config);
            }

            var selectedInputFiles = SelectInputFilesByRfRs(config);
            if (selectedInputFiles.Count == 0)
                throw new InvalidOperationException("No valid .rcp instance remained after RF/RS pre-validation.");

            var result = new ExperimentalRunResult
            {
                FilesSelected = selectedInputFiles.Count,
                OutputDirectory = config.OutputDirectory
            };

            var stopwatch = Stopwatch.StartNew();
            int index = 0;

            foreach (string file in selectedInputFiles)
            {
                index++;
                int percent = (int)Math.Round((index - 1) * 100.0 / selectedInputFiles.Count);
                Report(progress, "Validando/Processando " + Path.GetFileName(file), percent);

                try
                {
                    ProcessFile(config, file, result, progress, index);
                }
                catch (Exception ex)
                {
                    result.Errors++;
                    result.FilesInvalid++;
                    AddError(GetInstanceId(file), string.Empty, string.Empty, "ProcessFile", ex.GetType().Name, ex.Message, "File ignored; execution continues.");
                }
            }

            Report(progress, "Consolidando resultados", 95);
            ApplyDominance();
            WriteConsolidated(config, stopwatch.Elapsed);
            WriteGraphData(config);
            WriteReportTables(config);
            WriteLogs(config);
            WriteReadinessChecklist(config);

            stopwatch.Stop();
            result.Success = result.Errors == 0;
            result.BaselinesGenerated = _allBaselines.Count;
            result.FrmGenerated = _allFrm.Count;
            result.MonteCarloGenerated = _allRisk.Count;
            result.CrashingGenerated = _allCrashing.Count;
            result.Message = BuildFinalMessage(result, stopwatch.Elapsed);

            Report(progress, "Completed", 100);
            return result;
        }

        private void ResetState()
        {
            _integrated.Clear();
            _allBaselines.Clear();
            _allFrm.Clear();
            _allFrmDetails.Clear();
            _allRisk.Clear();
            _allCrashing.Clear();
            _allCrashingCandidates.Clear();
            _errorRows.Clear();
            _warningRows.Clear();
            _pipelineRows.Clear();
            _excludedRows.Clear();
            _sensitivityRows.Clear();
            _confidenceRows.Clear();
            _replicationRows.Clear();
            _stabilityRows.Clear();
            _baselineDeduplicationRows.Clear();
            _absorptionByGammaRows.Clear();
            _resourceAbsorptionRows.Clear();
            _crashingPolicyRows.Clear();
            _instanceSelectionRows.Clear();
            _rfSelectionThreshold = 0.0;
            _rsSelectionThreshold = 0.0;
            _policyComparisonRows.Clear();
            _policyPairs.Clear();
            _rfByInstance.Clear();
            _rsByInstance.Clear();
        }

        private static void AppendManifest(ExperimentalStudyConfig config, string instanceId, string filePath, string hash, long fileSize, string family, string validationStatus)
        {
            ExperimentalCsv.AppendRow(
                Path.Combine(config.OutputDirectory, "00_config", "manifesto_files.csv"),
                new[] { "instance_id", "file", "path", "sha256", "size_bytes", "instance_family", "validation_status" },
                new[] { instanceId, Path.GetFileName(filePath), filePath, hash, ExperimentalCsv.S(fileSize), family, validationStatus });
        }

        private static void ClearOutputFilesForFreshRun(string root)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return;

            foreach (var file in Directory.GetFiles(root, "*.csv", SearchOption.AllDirectories))
                File.Delete(file);

            foreach (var file in Directory.GetFiles(root, "*.txt", SearchOption.AllDirectories))
                File.Delete(file);

            foreach (var file in Directory.GetFiles(root, "*.json", SearchOption.AllDirectories))
                File.Delete(file);

            foreach (var file in Directory.GetFiles(root, "*.png", SearchOption.AllDirectories))
                File.Delete(file);

            foreach (var file in Directory.GetFiles(root, "*.xlsx", SearchOption.AllDirectories))
                File.Delete(file);
        }

        private sealed class InstanceCandidate
        {
            public string FilePath { get; set; }
            public string InstanceId { get; set; }
            public double Rf { get; set; }
            public double Rs { get; set; }
            public string Stratum { get; set; }
            public bool Selected { get; set; }
            public string Reason { get; set; }
        }

        private List<string> SelectInputFilesByRfRs(ExperimentalStudyConfig config)
        {
            var candidates = new List<InstanceCandidate>();
            foreach (string file in config.InputFiles.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var project = _importer.Import(file);
                    var validation = ValidateProject(project);
                    if (!validation.IsValid)
                    {
                        _instanceSelectionRows.Add(new[] { GetInstanceId(file), file, "", "", "", "", "", "False", "INVALID_INSTANCE", "" });
                        continue;
                    }
                    double rf, rs;
                    ComputeRfRs(project, out rf, out rs);
                    candidates.Add(new InstanceCandidate { FilePath = file, InstanceId = GetInstanceId(file), Rf = rf, Rs = rs });
                }
                catch (Exception ex)
                {
                    _instanceSelectionRows.Add(new[] { GetInstanceId(file), file, "", "", "", "", "", "False", "IMPORT_ERROR", ex.GetType().Name + ": " + ex.Message });
                }
            }

            if (candidates.Count == 0)
                return new List<string>();

            _rfSelectionThreshold = Median(candidates.Select(c => c.Rf).ToList());
            _rsSelectionThreshold = Median(candidates.Select(c => c.Rs).ToList());
            foreach (var c in candidates)
                c.Stratum = GetRfRsStratum(c.Rf, c.Rs, _rfSelectionThreshold, _rsSelectionThreshold);

            bool useStratifiedSelection = config.UseRfRsStratifiedSelection;
            if (!useStratifiedSelection)
            {
                _warningRows.Add(new[]
                {
                    DateTime.Now.ToString("s"), string.Empty, "RF_RS_SELECTION",
                    "RF/RS-stratified selection is disabled. All valid candidate instances will be processed.",
                    "The run can be used for exploratory analysis, but it must not be reported as a confirmatory RF/RS-balanced sample.",
                    "Enable RF/RS stratification and provide a candidate pool covering the four strata for the confirmatory protocol."
                });
            }

            var requiredStrata = new[] { "RF_LOW_RS_LOW", "RF_LOW_RS_HIGH", "RF_HIGH_RS_LOW", "RF_HIGH_RS_HIGH" };
            var availableStrata = candidates.Select(c => c.Stratum).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var missingStrata = requiredStrata.Where(stratum => !availableStrata.Contains(stratum, StringComparer.OrdinalIgnoreCase)).ToList();
            if (missingStrata.Count > 0)
            {
                _warningRows.Add(new[]
                {
                    DateTime.Now.ToString("s"), string.Empty, "RF_RS_CANDIDATE_COVERAGE",
                    "The candidate instance pool does not cover all required RF/RS strata. Missing: " + string.Join(", ", missingStrata.ToArray()) + ".",
                    "Execution will continue with the available strata, but the run is exploratory and does not satisfy complete confirmatory RF/RS coverage.",
                    "Add instances from the missing strata before using the results as the confirmatory Chapter 4 sample."
                });
            }

            int target = config.RfRsTargetInstanceCount;
            int minimumTargetForAvailableStrata = useStratifiedSelection ? availableStrata.Count : 0;
            if (target > 0 && target < minimumTargetForAvailableStrata)
            {
                _warningRows.Add(new[]
                {
                    DateTime.Now.ToString("s"), string.Empty, "RF_RS_TARGET",
                    "The requested RF/RS target (" + target.ToString(CultureInfo.InvariantCulture) + ") is smaller than the number of available strata (" + minimumTargetForAvailableStrata.ToString(CultureInfo.InvariantCulture) + ").",
                    "The target was automatically increased so every available stratum can be represented.",
                    "Use target 0 to process all valid instances or choose a target at least equal to the number of available strata."
                });
                target = minimumTargetForAvailableStrata;
            }

            if (!useStratifiedSelection)
            {
                foreach (var c in candidates)
                {
                    c.Selected = true;
                    c.Reason = "ALL_VALID_INSTANCES_STRATIFICATION_DISABLED";
                }
            }
            else if (target <= 0 || target >= candidates.Count)
            {
                foreach (var c in candidates)
                {
                    c.Selected = true;
                    c.Reason = target <= 0 ? "ALL_VALID_INSTANCES_TARGET_ZERO" : "ALL_VALID_INSTANCES_TARGET_GE_POOL";
                }
            }
            else
            {
                var groups = candidates.GroupBy(c => c.Stratum)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(c => Math.Abs(c.Rf - _rfSelectionThreshold) + Math.Abs(c.Rs - _rsSelectionThreshold)).ThenBy(c => c.InstanceId).ToList());
                var strata = requiredStrata.Where(stratum => availableStrata.Contains(stratum, StringComparer.OrdinalIgnoreCase)).ToArray();
                int selected = 0;
                int round = 0;
                while (selected < target)
                {
                    bool added = false;
                    foreach (string stratum in strata)
                    {
                        List<InstanceCandidate> list;
                        if (!groups.TryGetValue(stratum, out list) || round >= list.Count || selected >= target)
                            continue;
                        list[round].Selected = true;
                        list[round].Reason = "STRATIFIED_ROUND_ROBIN_AVAILABLE_STRATA";
                        selected++;
                        added = true;
                    }
                    if (!added) break;
                    round++;
                }
            }

            int order = 0;
            foreach (var c in candidates.OrderBy(c => c.Stratum).ThenBy(c => c.InstanceId))
            {
                if (c.Selected) order++;
                _instanceSelectionRows.Add(new[] { c.InstanceId, c.FilePath, F(c.Rf), F(c.Rs), F(_rfSelectionThreshold), F(_rsSelectionThreshold), c.Stratum, ExperimentalCsv.S(c.Selected), c.Reason ?? "NOT_SELECTED_TARGET_REACHED", ExperimentalCsv.S(c.Selected ? order : 0) });
            }

            string selectionPath = Path.Combine(config.OutputDirectory, "00_config", "instance_selection_rfrs.csv");
            ExperimentalCsv.WriteRows(selectionPath, InstanceSelectionHeaders(), _instanceSelectionRows);

            var selectedStrata = candidates.Where(c => c.Selected).Select(c => c.Stratum).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var selectedMissingStrata = requiredStrata.Where(stratum => !selectedStrata.Contains(stratum, StringComparer.OrdinalIgnoreCase)).ToList();
            if (selectedMissingStrata.Count > 0)
            {
                _warningRows.Add(new[]
                {
                    DateTime.Now.ToString("s"), string.Empty, "RF_RS_SELECTED_COVERAGE",
                    "The selected sample does not contain all four RF/RS strata. Missing after selection: " + string.Join(", ", selectedMissingStrata.ToArray()) + ".",
                    "The experiment will continue and export all results, but RF/RS confirmatory conclusions must be marked as incomplete.",
                    "Expand the candidate pool with instances from the missing strata before the definitive Chapter 4 run."
                });
            }

            return candidates.Where(c => c.Selected).OrderBy(c => c.Stratum).ThenBy(c => c.InstanceId).Select(c => c.FilePath).ToList();
        }

        private static string GetRfRsStratum(double rf, double rs, double rfThreshold, double rsThreshold)
        {
            return (rf <= rfThreshold ? "RF_LOW" : "RF_HIGH") + "_" + (rs <= rsThreshold ? "RS_LOW" : "RS_HIGH");
        }

        private static string[] InstanceSelectionHeaders()
        {
            return new[] { "instance_id", "file_path", "resource_factor", "resource_strength", "rf_threshold", "rs_threshold", "rf_rs_stratum", "selected", "selection_reason", "selection_order" };
        }

        private static List<double> BuildEqualInstanceBaselineWeights(List<ScenarioRecord> records)
        {
            var counts = records.GroupBy(r => r.InstanceId).ToDictionary(g => g.Key, g => g.Count());
            int instanceCount = counts.Count;
            return records.Select(r => instanceCount == 0 || counts[r.InstanceId] == 0 ? 0.0 : 1.0 / (instanceCount * counts[r.InstanceId])).ToList();
        }

        private void WriteWeightedChapter4Results(string path)
        {
            var rows = new List<string[]>();
            AppendWeightedScopeRows(rows, "BASELINE_ONLY", _integrated.Where(s => string.Equals(s.Type, "baseline", StringComparison.OrdinalIgnoreCase)).ToList());
            AppendWeightedScopeRows(rows, "ALL_SCENARIOS_WITHIN_BASELINE", _integrated.ToList());
            ExperimentalCsv.WriteRows(path, new[] { "analysis_scope", "aggregation_level", "instance_id", "baseline_id", "n_scenarios", "n_unique_baselines", "n_instances", "mean_makespan", "mean_sif", "mean_relative_p95", "mean_relative_cvar95", "mean_delay_cvar95", "mean_delay_probability", "weighting_method" }, rows);
        }

        private static void AppendWeightedScopeRows(List<string[]> rows, string scope, List<ScenarioRecord> records)
        {
            if (records == null || records.Count == 0)
                return;

            var baselineAggregates = records
                .GroupBy(r => new { r.InstanceId, Baseline = string.IsNullOrWhiteSpace(r.BaselineId) ? r.ScenarioId : r.BaselineId })
                .Select(g => new
                {
                    g.Key.InstanceId,
                    BaselineId = g.Key.Baseline,
                    ScenarioCount = g.Count(),
                    Makespan = g.Average(x => x.Makespan),
                    Sif = g.Average(x => x.SifGlobal),
                    P95 = g.Average(x => x.P95Relative),
                    Cvar = g.Average(x => x.Cvar95Relative),
                    DelayCvar = g.Average(x => x.Cvar95Delay),
                    DelayProb = g.Average(x => x.DelayProbability)
                }).ToList();

            foreach (var b in baselineAggregates.OrderBy(x => x.InstanceId).ThenBy(x => x.BaselineId))
                rows.Add(new[] { scope, "BASELINE", b.InstanceId, b.BaselineId, ExperimentalCsv.S(b.ScenarioCount), "1", "1", F(b.Makespan), F(b.Sif), F(b.P95), F(b.Cvar), F(b.DelayCvar), F(b.DelayProb), "equal_weight_per_scenario_within_baseline" });

            var instanceAggregates = baselineAggregates.GroupBy(b => b.InstanceId).Select(g => new
            {
                InstanceId = g.Key,
                BaselineCount = g.Count(),
                ScenarioCount = g.Sum(x => x.ScenarioCount),
                Makespan = g.Average(x => x.Makespan),
                Sif = g.Average(x => x.Sif),
                P95 = g.Average(x => x.P95),
                Cvar = g.Average(x => x.Cvar),
                DelayCvar = g.Average(x => x.DelayCvar),
                DelayProb = g.Average(x => x.DelayProb)
            }).ToList();

            foreach (var i in instanceAggregates.OrderBy(x => x.InstanceId))
                rows.Add(new[] { scope, "INSTANCE", i.InstanceId, "ALL", ExperimentalCsv.S(i.ScenarioCount), ExperimentalCsv.S(i.BaselineCount), "1", F(i.Makespan), F(i.Sif), F(i.P95), F(i.Cvar), F(i.DelayCvar), F(i.DelayProb), "equal_weight_per_scenario_then_equal_weight_per_baseline" });

            rows.Add(new[] { scope, "GLOBAL", "ALL", "ALL", ExperimentalCsv.S(records.Count), ExperimentalCsv.S(baselineAggregates.Count), ExperimentalCsv.S(instanceAggregates.Count), F(instanceAggregates.Average(x => x.Makespan)), F(instanceAggregates.Average(x => x.Sif)), F(instanceAggregates.Average(x => x.P95)), F(instanceAggregates.Average(x => x.Cvar)), F(instanceAggregates.Average(x => x.DelayCvar)), F(instanceAggregates.Average(x => x.DelayProb)), "equal_weight_per_scenario_then_baseline_then_instance" });
        }

        private void ProcessFile(ExperimentalStudyConfig config, string filePath, ExperimentalRunResult result, Action<string, int> progress, int fileIndex)
        {
            string instanceId = GetInstanceId(filePath);
            string family = DetectFamily(instanceId);
            string instanceDir = Path.Combine(config.OutputDirectory, "02_processed", instanceId);
            Directory.CreateDirectory(instanceDir);

            string hash = ComputeSha256(filePath);
            long fileSize = new FileInfo(filePath).Length;

            ProjectDataDto project = null;
            var validationRows = new List<string[]>();
            try
            {
                project = _importer.Import(filePath);
                var validation = ValidateProject(project);
                validationRows.Add(new[]
                {
                    instanceId,
                    Path.GetFileName(filePath),
                    validation.IsValid ? "OK" : "ERROR",
                    ExperimentalCsv.S(validation.HasCycle),
                    ExperimentalCsv.S(project.Activities.Count),
                    ExperimentalCsv.S(project.Resources.Count),
                    ExperimentalCsv.S(project.Activities.Sum(a => a.SuccessorIds == null ? 0 : a.SuccessorIds.Count)),
                    ExperimentalCsv.S(validation.InvalidDurations),
                    ExperimentalCsv.S(validation.InvalidDemands),
                    ExperimentalCsv.S(validation.DemandsAboveCapacity),
                    ExperimentalCsv.S(validation.DisconnectedActivities),
                    validation.Message
                });

                ExperimentalCsv.WriteRows(Path.Combine(instanceDir, "00_instance_validation.csv"), ValidationHeaders(), validationRows);
                AppendManifest(config, instanceId, filePath, hash, fileSize, family, validation.IsValid ? "OK" : "ERROR");

                if (!validation.IsValid)
                {
                    result.FilesInvalid++;
                    AddExcluded(instanceId, string.Empty, string.Empty, "invalid instance", "instance_validation", "file not processed", false, false);
                    return;
                }

                result.FilesValid++;
            }
            catch (Exception ex)
            {
                validationRows.Add(new[] { instanceId, Path.GetFileName(filePath), "ERROR", "false", "0", "0", "0", "0", "0", "0", "0", ex.Message });
                ExperimentalCsv.WriteRows(Path.Combine(instanceDir, "00_instance_validation.csv"), ValidationHeaders(), validationRows);
                AppendManifest(config, instanceId, filePath, hash, fileSize, family, "ERROR");
                result.FilesInvalid++;
                AddError(instanceId, string.Empty, string.Empty, "RcpImport", ex.GetType().Name, ex.Message, "Instance skipped.");
                AddExcluded(instanceId, string.Empty, string.Empty, "import error", "import", "file not processed", false, false);
                return;
            }

            WriteInstanceCsv(instanceDir, instanceId, family, filePath, project);


            double rf, rs;
            ComputeRfRs(project, out rf, out rs);
            _rfByInstance[instanceId] = rf;
            _rsByInstance[instanceId] = rs;

            if (config.RunDryOnly)
            {
                _pipelineRows.Add(new[] { instanceId, string.Empty, string.Empty, "OK", "OK", "SKIP_DRY_RUN", "SKIP", "SKIP", "SKIP", "SKIP", "SKIP", "DRY_RUN" });
                return;
            }

            var requestOptions = new SchedulingOptionsDto { Heuristic = "ALL", Scheme = "SERIAL", Direction = "FORWARD", Engine = "HEURISTIC", RunLabel = "ALL" };
            var baselineRuns = (_baselineBatchScheduler.Run(project, requestOptions) ?? new List<BaselineRunSummaryDto>())
                .Where(r => r != null && !r.IsExact)
                .ToList();


            try
            {
                var bbOptions = new SchedulingOptionsDto { Heuristic = "DH_BB", Scheme = "EXACT", Direction = "EXACT", Engine = "DH_BB", RunLabel = "Modified DH B&B", UseExactEngine = true, BranchAndBoundTimeLimitSeconds = config.BranchAndBoundTimeLimitSeconds };
                var bbRuns = _baselineBatchScheduler.Run(project, bbOptions);
                if (bbRuns != null)
                    baselineRuns.AddRange(bbRuns.Where(r => r != null));
            }
            catch (Exception ex)
            {
                AddError(instanceId, string.Empty, string.Empty, "BranchAndBound_Experimental", ex.GetType().Name, ex.Message, "Modified DH B&B skipped for this instance; heuristic baselines continue.");
            }


            var uniqueBaselineRuns = DeduplicateBaselineRuns(instanceId, baselineRuns);

            var baselineRows = new List<string[]>();
            var baselineValidationRows = new List<string[]>();
            var frmRows = new List<string[]>();
            var frmDetailRows = new List<string[]>();
            var riskRows = new List<string[]>();
            var crashingRows = new List<string[]>();
            var crashingCandidateRows = new List<string[]>();
            var crashingDetailRows = new List<string[]>();
            var integratedRows = new List<string[]>();

            foreach (var uniqueRun in uniqueBaselineRuns)
            {
                var run = uniqueRun.CanonicalRun;
                string baselineId = uniqueRun.UniqueBaselineId;
                var baselineValidation = ValidateBaseline(project, run != null ? run.BaselineResult : null);
                if (run != null)
                    run.MethodClassification = BaselineMethodClassifier.Classify(run, baselineValidation.IsValid);

                baselineRows.Add(BaselineRow(instanceId, baselineId, run));
                _allBaselines.Add(BaselineRow(instanceId, baselineId, run));

                baselineValidationRows.Add(new[]
                {
                    baselineId,
                    ExperimentalCsv.S(baselineValidation.PrecedenceFeasible),
                    ExperimentalCsv.S(baselineValidation.ResourceFeasible),
                    ExperimentalCsv.S(baselineValidation.PrecedenceViolations),
                    ExperimentalCsv.S(baselineValidation.ResourceViolations),
                    ExperimentalCsv.S(baselineValidation.MaxResourceViolation),
                    ExperimentalCsv.S(baselineValidation.MissingActivities),
                    ExperimentalCsv.S(baselineValidation.MakespanConsistent),
                    baselineValidation.IsValid ? "OK" : "ERROR"
                });

                if (run != null && run.IsExact &&
                    !string.Equals(run.MethodClassification, BaselineMethodClassifier.ExactReference, StringComparison.OrdinalIgnoreCase))
                {
                    _warningRows.Add(new[]
                    {
                        DateTime.Now.ToString("s"), instanceId, "ExactClassification",
                        "Modified DH B&B was classified as " + run.MethodClassification + " and will not be used as EXACT_REFERENCE.",
                        "The feasible incumbent may remain in the unified FRM/risk pipeline, but it is excluded from exact-reference comparisons.",
                        "Increase the B&B time limit or verify the optimality trace before treating the result as an exact reference."
                    });
                }

                if (run == null || !run.Success || run.BaselineResult == null || !baselineValidation.IsValid)
                {
                    AddExcluded(instanceId, baselineId, string.Empty, "invalid baseline", "baseline", "FRM/Monte Carlo/Crashing not executed", false, false);
                    _pipelineRows.Add(new[] { instanceId, baselineId, string.Empty, "OK", "OK", "ERROR", "SKIP", "SKIP", "SKIP", "SKIP", "SKIP", "INCOMPLETO" });
                    continue;
                }

                try
                {
                    var frmOptions = BuildFrmOptions(config);
                    var riskOptions = BuildRiskOptions(config, DeriveSeed(config.Seed, baselineId, 0));
                    var crashOptions = BuildCrashingOptions(config);
                    var summary = _runAnalysisService.AnalyzeSelectedRun(project, run, frmOptions, riskOptions, crashOptions);

                    frmRows.Add(FrmRow(instanceId, baselineId, summary.Frm));
                    _allFrm.Add(FrmRow(instanceId, baselineId, summary.Frm));
                    foreach (var row in FrmDetailRows(instanceId, baselineId, summary.Frm))
                    {
                        frmDetailRows.Add(row);
                        _allFrmDetails.Add(row);
                    }


                    summary.Risk = RunReplicationsAndConfidence(config, project, run.BaselineResult, summary.Frm, run.Makespan, instanceId, family, baselineId);


                    summary.Crashing = _crashingAnalyzer.Run(
                        project, run.BaselineResult, summary.Frm, summary.Risk, crashOptions);

                    var officialPairedRisk = summary.Risk.PairedStructuralResult ?? summary.Risk.PairedUnilateralResult;
                    if (officialPairedRisk != null)
                    {
                        var officialPairedCrashing = _crashingAnalyzer.Run(
                            project, run.BaselineResult, summary.Frm, officialPairedRisk, crashOptions);
                        summary.Crashing.PairedComparisonMode = summary.Risk.PairedComparisonMode;
                        if (summary.Risk.PairedStructuralResult != null)
                            summary.Crashing.PairedStructuralResult = officialPairedCrashing;
                        else
                            summary.Crashing.PairedUnilateralResult = officialPairedCrashing;
                    }

                    riskRows.Add(RiskRow(instanceId, baselineId, config.Seed, config.Replications, summary.Risk, run.Makespan));
                    _allRisk.Add(RiskRow(instanceId, baselineId, config.Seed, config.Replications, summary.Risk, run.Makespan));
                    AppendResourceAbsorptionRows(instanceId, family, baselineId, config.Gamma, summary.Risk, "BASELINE_OFFICIAL");

                    var baseScenario = BuildScenarioRecord(instanceId, family, baselineId, "baseline", baselineId, run, summary.Frm, summary.Risk, 0.0, "BASELINE");
                    ApplyRfRs(baseScenario, instanceId);
                    _integrated.Add(baseScenario);
                    integratedRows.Add(IntegratedRow(baseScenario));

                    RunSensitivity(config, project, run.BaselineResult, summary.Frm, run.Makespan, instanceId, family, baselineId);


                    if (config.RunThreePolicies)
                        RunThreePolicyCrashing(config, project, run, summary, instanceId, baselineId);

                    var crash = summary.Crashing;
                    int totalPossible = crash != null ? crash.GeneratedScenarioCount : 0;
                    int totalEvaluated = crash != null ? crash.ExecutedScenarioCount : 0;
                    _crashingPolicyRows.Add(new[] { baselineId, ExperimentalCsv.S(crash != null && crash.Candidates != null ? crash.Candidates.Count : 0), ExperimentalCsv.S(totalPossible), ExperimentalCsv.S(totalEvaluated), "FRM_GUIDED_LIMITED", ExperimentalCsv.S(config.MaxCombinationSize), ExperimentalCsv.S(config.MaxCandidateActivities), ExperimentalCsv.S(config.MaxCrashingScenarios), Percent(totalEvaluated, Math.Max(totalPossible, 1)), ExperimentalCsv.S(config.Seed), "Scenarios generated by the existing crashing analyzer with the configured candidate, combo and scenario limits." });

                    if (crash != null && crash.Candidates != null)
                    {
                        int candidateRank = 0;
                        foreach (var candidate in crash.Candidates)
                        {
                            candidateRank++;
                            var candidateRow = CrashingCandidateRow(instanceId, baselineId, candidateRank, candidate);
                            crashingCandidateRows.Add(candidateRow);
                            _allCrashingCandidates.Add(candidateRow);
                        }
                    }

                    if (crash != null && crash.Scenarios != null)
                    {
                        var pairedCrash = crash.PairedStructuralResult ?? crash.PairedUnilateralResult;
                        var pairedRisk = summary.Risk != null ? (summary.Risk.PairedStructuralResult ?? summary.Risk.PairedUnilateralResult) : null;
                        var pairedBySignature = BuildCrashingScenarioLookupBySignature(pairedCrash);
                        foreach (var sc in crash.Scenarios)
                        {
                            string scenarioId = baselineId + "_" + Sanitize(sc.ScenarioName);
                            CrashingScenarioResultDto pairedScenario;
                            pairedBySignature.TryGetValue(BuildCrashingScenarioSignature(sc), out pairedScenario);

                            var crow = CrashingRow(instanceId, baselineId, scenarioId, sc, pairedScenario, run.Makespan, summary.Frm.SifGlobal, summary.Risk, pairedRisk);
                            crashingRows.Add(crow);
                            _allCrashing.Add(crow);

                            foreach (var drow in CrashingDetailRows(scenarioId, sc, crash.Candidates))
                                crashingDetailRows.Add(drow);

                            var scenario = BuildCrashingScenarioRecord(instanceId, family, scenarioId, baselineId, run, summary.Frm, summary.Risk, sc, pairedScenario, pairedRisk);
                            ApplyRfRs(scenario, instanceId);
                            _integrated.Add(scenario);
                            integratedRows.Add(IntegratedRow(scenario));
                        }
                    }

                    _pipelineRows.Add(new[] { instanceId, baselineId, string.Empty, "OK", "OK", "OK", "OK", "OK", "OK", "OK", "OK", "COMPLETO" });
                }
                catch (Exception ex)
                {
                    AddError(instanceId, baselineId, string.Empty, "BaselineAnalysis", ex.GetType().Name, ex.Message, "Baseline partially skipped; execution continues.");
                    AddExcluded(instanceId, baselineId, string.Empty, "baseline analysis failure", "frm_monte_carlo_crashing", "incomplete scenario", true, false);
                    _pipelineRows.Add(new[] { instanceId, baselineId, string.Empty, "OK", "OK", "OK", "ERROR", "SKIP", "SKIP", "SKIP", "SKIP", "INCOMPLETO" });
                }
            }

            ExperimentalCsv.WriteRows(Path.Combine(instanceDir, "02_baselines.csv"), BaselineHeaders(), baselineRows);
            ExperimentalCsv.WriteRows(Path.Combine(instanceDir, "02_baseline_validation.csv"), BaselineValidationHeaders(), baselineValidationRows);
            ExperimentalCsv.WriteRows(Path.Combine(instanceDir, "03_frm.csv"), FrmHeaders(), frmRows);
            ExperimentalCsv.WriteRows(Path.Combine(instanceDir, "03_frm_detalhado.csv"), FrmDetailHeaders(), frmDetailRows);
            ExperimentalCsv.WriteRows(Path.Combine(instanceDir, "04_monte_carlo.csv"), RiskHeaders(), riskRows);
            ExperimentalCsv.WriteRows(Path.Combine(instanceDir, "05_crashing.csv"), CrashingHeaders(), crashingRows);
            ExperimentalCsv.WriteRows(Path.Combine(instanceDir, "05_crashing_candidates.csv"), CrashingCandidateHeaders(), crashingCandidateRows);
            ExperimentalCsv.WriteRows(Path.Combine(instanceDir, "05_crashing_detalhes_activities.csv"), CrashingDetailHeaders(), crashingDetailRows);
            ExperimentalCsv.WriteRows(Path.Combine(instanceDir, "06_integrated.csv"), IntegratedHeaders(), integratedRows);
            ExperimentalCsv.WriteRows(Path.Combine(instanceDir, "02_baseline_deduplication.csv"), BaselineDeduplicationHeaders(), _baselineDeduplicationRows.Where(r => r.Length > 0 && string.Equals(r[0], instanceId, StringComparison.OrdinalIgnoreCase)));
            ExperimentalCsv.WriteRows(Path.Combine(instanceDir, "07_absorption_by_gamma.csv"), AbsorptionByGammaHeaders(), _absorptionByGammaRows.Where(r => r.Length > 0 && string.Equals(r[0], instanceId, StringComparison.OrdinalIgnoreCase)));
            ExperimentalCsv.WriteRows(Path.Combine(instanceDir, "07_absorption_by_resource.csv"), ResourceAbsorptionHeaders(), _resourceAbsorptionRows.Where(r => r.Length > 0 && string.Equals(r[0], instanceId, StringComparison.OrdinalIgnoreCase)));
        }

        private RiskResultDto RunReplicationsAndConfidence(ExperimentalStudyConfig config, ProjectDataDto project, BaselineResultDto baseline, FrmResultDto frm, int nominalMakespan, string instanceId, string family, string baselineId)
        {
            var primaryCvars = new List<double>();
            var primaryP95s = new List<double>();
            var pairedCvars = new List<double>();
            var pairedP95s = new List<double>();
            var deltaCvars = new List<double>();
            var deltaP95s = new List<double>();
            var primaryResults = new List<RiskResultDto>();
            var pairedResults = new List<RiskResultDto>();

            for (int rep = 1; rep <= Math.Max(1, config.Replications); rep++)
            {
                int seed = DeriveSeed(config.Seed, baselineId, rep);
                var risk = _riskAnalyzer.Run(project, baseline, frm, BuildRiskOptions(config, seed));
                var pairedRisk = risk.PairedStructuralResult ?? risk.PairedUnilateralResult;
                bool hasPaired = pairedRisk != null;
                primaryResults.Add(risk);
                if (hasPaired) pairedResults.Add(pairedRisk);

                primaryCvars.Add(risk.CVaR95);
                primaryP95s.Add(risk.P95);
                if (hasPaired)
                {
                    pairedCvars.Add(pairedRisk.CVaR95);
                    pairedP95s.Add(pairedRisk.P95);
                    deltaCvars.Add(pairedRisk.CVaR95 - risk.CVaR95);
                    deltaP95s.Add(pairedRisk.P95 - risk.P95);
                }

                double primarySd = risk.MakespanSamples == null ? 0.0 : ExperimentalStatistics.StdDev(risk.MakespanSamples.Select(v => (double)v));
                int primaryMin = risk.MakespanSamples == null || risk.MakespanSamples.Count == 0 ? 0 : risk.MakespanSamples.Min();
                int primaryMax = risk.MakespanSamples == null || risk.MakespanSamples.Count == 0 ? 0 : risk.MakespanSamples.Max();
                double pairedSd = hasPaired && pairedRisk.MakespanSamples != null ? ExperimentalStatistics.StdDev(pairedRisk.MakespanSamples.Select(v => (double)v)) : 0.0;
                int pairedMin = hasPaired && pairedRisk.MakespanSamples != null && pairedRisk.MakespanSamples.Count > 0 ? pairedRisk.MakespanSamples.Min() : 0;
                int pairedMax = hasPaired && pairedRisk.MakespanSamples != null && pairedRisk.MakespanSamples.Count > 0 ? pairedRisk.MakespanSamples.Max() : 0;

                _replicationRows.Add(new[]
                {
                    instanceId, baselineId, ExperimentalCsv.S(rep), ExperimentalCsv.S(seed), F(config.Gamma),
                    F(risk.MeanMakespan), F(risk.P50), F(risk.P95), F(risk.CVaR95), F(primarySd), ExperimentalCsv.S(primaryMin), ExperimentalCsv.S(primaryMax),
                    ExperimentalCsv.S(hasPaired), hasPaired ? pairedRisk.SamplingMode : string.Empty,
                    hasPaired ? F(pairedRisk.MeanMakespan) : string.Empty,
                    hasPaired ? F(pairedRisk.P50) : string.Empty,
                    hasPaired ? F(pairedRisk.P95) : string.Empty,
                    hasPaired ? F(pairedRisk.CVaR95) : string.Empty,
                    hasPaired ? F(pairedSd) : string.Empty,
                    hasPaired ? ExperimentalCsv.S(pairedMin) : string.Empty,
                    hasPaired ? ExperimentalCsv.S(pairedMax) : string.Empty,
                    hasPaired ? F(pairedRisk.MeanMakespan - risk.MeanMakespan) : string.Empty,
                    hasPaired ? F(pairedRisk.P50 - risk.P50) : string.Empty,
                    hasPaired ? F(pairedRisk.P95 - risk.P95) : string.Empty,
                    hasPaired ? F(pairedRisk.CVaR95 - risk.CVaR95) : string.Empty
                });
            }

            double meanCvar = ExperimentalStatistics.Mean(primaryCvars);
            double sdCvar = ExperimentalStatistics.StdDev(primaryCvars);
            double meanP95 = ExperimentalStatistics.Mean(primaryP95s);
            double sdP95 = ExperimentalStatistics.StdDev(primaryP95s);
            double pairedMeanCvar = ExperimentalStatistics.Mean(pairedCvars);
            double pairedSdCvar = ExperimentalStatistics.StdDev(pairedCvars);
            double pairedMeanP95 = ExperimentalStatistics.Mean(pairedP95s);
            double pairedSdP95 = ExperimentalStatistics.StdDev(pairedP95s);
            double deltaMeanCvar = ExperimentalStatistics.Mean(deltaCvars);
            double deltaSdCvar = ExperimentalStatistics.StdDev(deltaCvars);
            double deltaMeanP95 = ExperimentalStatistics.Mean(deltaP95s);
            double deltaSdP95 = ExperimentalStatistics.StdDev(deltaP95s);
            string stability = primaryCvars.Count < 2
                ? "NA_INSUFFICIENT_REPLICATIONS"
                : (sdCvar <= Math.Max(1e-9, Math.Abs(meanCvar)) * 0.05 ? "ESTAVEL" : "INSTAVEL");

            _stabilityRows.Add(new[]
            {
                baselineId, F(config.Gamma),
                F(meanCvar), F(sdCvar), F(SafeDivide(sdCvar, Math.Abs(meanCvar))),
                F(meanP95), F(sdP95), F(SafeDivide(sdP95, Math.Abs(meanP95))),
                pairedCvars.Count > 0 ? F(pairedMeanCvar) : string.Empty,
                pairedCvars.Count > 0 ? F(pairedSdCvar) : string.Empty,
                pairedCvars.Count > 0 ? F(SafeDivide(pairedSdCvar, Math.Abs(pairedMeanCvar))) : string.Empty,
                pairedP95s.Count > 0 ? F(pairedMeanP95) : string.Empty,
                pairedP95s.Count > 0 ? F(pairedSdP95) : string.Empty,
                pairedP95s.Count > 0 ? F(SafeDivide(pairedSdP95, Math.Abs(pairedMeanP95))) : string.Empty,
                deltaCvars.Count > 0 ? F(deltaMeanCvar) : string.Empty,
                deltaCvars.Count > 0 ? F(deltaSdCvar) : string.Empty,
                deltaP95s.Count > 0 ? F(deltaMeanP95) : string.Empty,
                deltaP95s.Count > 0 ? F(deltaSdP95) : string.Empty,
                stability
            });

            AddConfidenceRows(baselineId, config.Gamma, "primary_cvar95", meanCvar, sdCvar, primaryCvars.Count);
            AddConfidenceRows(baselineId, config.Gamma, "primary_p95", meanP95, sdP95, primaryP95s.Count);
            if (pairedCvars.Count > 0)
            {
                AddConfidenceRows(baselineId, config.Gamma, "paired_cvar95", pairedMeanCvar, pairedSdCvar, pairedCvars.Count);
                AddConfidenceRows(baselineId, config.Gamma, "paired_p95", pairedMeanP95, pairedSdP95, pairedP95s.Count);
                AddConfidenceRows(baselineId, config.Gamma, "delta_modes_cvar95", deltaMeanCvar, deltaSdCvar, deltaCvars.Count);
                AddConfidenceRows(baselineId, config.Gamma, "delta_modes_p95", deltaMeanP95, deltaSdP95, deltaP95s.Count);
            }

            return AggregateReplicationResults(primaryResults, pairedResults, config.Seed, config.Gamma);
        }

        private void RunSensitivity(ExperimentalStudyConfig config, ProjectDataDto project, BaselineResultDto baseline, FrmResultDto frm, int nominalMakespan, string instanceId, string family, string baselineId)
        {


            var mandatoryGammas = new[] { 0.25, 0.50, 0.75, 1.00 };
            var gammaLevels = mandatoryGammas
                .Concat(config.SensitivityGammas ?? new List<double>())
                .Where(g => g >= 0.0 && g <= 1.0)
                .Distinct()
                .OrderBy(g => g)
                .ToList();

            int sensitivitySeed = DeriveSeed(config.Seed, baselineId + "_SENS", 0);
            string crnGroup = baselineId + "_SENS";

            foreach (double gamma in gammaLevels)
            {

                var sensitivityResults = new List<RiskResultDto>();
                var sensitivityPairedResults = new List<RiskResultDto>();
                for (int rep = 1; rep <= Math.Max(1, config.Replications); rep++)
                {
                    var roRep = BuildRiskOptions(config, DeriveSeed(sensitivitySeed, baselineId, rep));
                    roRep.Gamma = gamma;
                    var repRisk = _riskAnalyzer.Run(project, baseline, frm, roRep);
                    sensitivityResults.Add(repRisk);
                    var repPaired = repRisk.PairedStructuralResult ?? repRisk.PairedUnilateralResult;
                    if (repPaired != null) sensitivityPairedResults.Add(repPaired);
                }
                var risk = AggregateReplicationResults(sensitivityResults, sensitivityPairedResults, sensitivitySeed, gamma);
                var ro = BuildRiskOptions(config, sensitivitySeed);
                ro.Gamma = gamma;
                var delay = ComputeDelayMetrics(risk, nominalMakespan);
                var pairedRisk = risk.PairedStructuralResult ?? risk.PairedUnilateralResult;
                bool hasPaired = pairedRisk != null;
                var pairedDelay = hasPaired ? ComputeDelayMetrics(pairedRisk, nominalMakespan) : new DelayMetrics();

                double primaryRelP95 = SafeDivide(risk.P95, nominalMakespan);
                double primaryRelCvar = SafeDivide(risk.CVaR95, nominalMakespan);
                double pairedRelP95 = hasPaired ? SafeDivide(pairedRisk.P95, nominalMakespan) : 0.0;
                double pairedRelCvar = hasPaired ? SafeDivide(pairedRisk.CVaR95, nominalMakespan) : 0.0;

                _sensitivityRows.Add(new[]
                {
                    instanceId, family, baselineId, F(gamma), ExperimentalCsv.S(config.Iterations), ExperimentalCsv.S(config.Replications), ExperimentalCsv.S(config.Seed), ExperimentalCsv.S(ro.Seed), "MEAN_OF_INDEPENDENT_REPLICATIONS_FOR_SENSITIVITY", crnGroup,
                    F(frm.SifGlobal), ExperimentalCsv.S(nominalMakespan),
                    F(risk.P95), F(risk.CVaR95), F(primaryRelP95), F(primaryRelCvar),
                    F(delay.Probability), F(delay.Mean), F(delay.P95), F(delay.CVaR95), "EXECUTADO",
                    ExperimentalCsv.S(hasPaired), hasPaired ? pairedRisk.SamplingMode : string.Empty,
                    hasPaired ? F(pairedRisk.P95) : string.Empty,
                    hasPaired ? F(pairedRisk.CVaR95) : string.Empty,
                    hasPaired ? F(pairedRelP95) : string.Empty,
                    hasPaired ? F(pairedRelCvar) : string.Empty,
                    hasPaired ? F(pairedDelay.Probability) : string.Empty,
                    hasPaired ? F(pairedDelay.Mean) : string.Empty,
                    hasPaired ? F(pairedDelay.P95) : string.Empty,
                    hasPaired ? F(pairedDelay.CVaR95) : string.Empty,
                    hasPaired ? F(pairedRisk.P95 - risk.P95) : string.Empty,
                    hasPaired ? F(pairedRisk.CVaR95 - risk.CVaR95) : string.Empty,
                    hasPaired ? F(pairedRelCvar - primaryRelCvar) : string.Empty,
                    hasPaired ? F(pairedDelay.Probability - delay.Probability) : string.Empty,
                    hasPaired ? F(pairedDelay.Mean - delay.Mean) : string.Empty,
                    hasPaired ? F(pairedDelay.P95 - delay.P95) : string.Empty,
                    F(risk.MeanBalanceGenerated), F(risk.MeanBalanceConsumed), F(risk.MeanBalanceUsageRatio),
                    F(risk.MeanPositiveWorkDemand), F(risk.MeanUnabsorbedWork), F(risk.P95UnabsorbedWork),
                    F(risk.CVaR95UnabsorbedWork), F(risk.MeanUnabsorbedWorkRatio), F(risk.BalanceRuptureProbability),
                    F(risk.MinObservedBalance)
                });

                _absorptionByGammaRows.Add(new[]
                {
                    instanceId, family, baselineId, F(gamma), ExperimentalCsv.S(config.Iterations),
                    ExperimentalCsv.S(config.Replications), ExperimentalCsv.S(config.Seed), ExperimentalCsv.S(ro.Seed),
                    "MEAN_OF_INDEPENDENT_REPLICATIONS_FOR_SENSITIVITY", crnGroup, risk.SamplingMode,
                    F(risk.MeanBalanceGenerated), F(risk.MeanBalanceConsumed), F(risk.MeanBalanceUsage),
                    F(risk.MeanBalanceUsageRatio), F(risk.MeanPositiveWorkDemand), F(risk.MeanUnabsorbedWork),
                    F(risk.P95UnabsorbedWork), F(risk.CVaR95UnabsorbedWork), F(risk.MeanUnabsorbedWorkRatio),
                    F(risk.BalanceRuptureProbability), F(risk.MinObservedBalance),
                    risk.MeanPositiveWorkDemand > 1e-9 ? F(risk.MeanBalanceConsumed / risk.MeanPositiveWorkDemand) : string.Empty,
                    risk.MeanPositiveWorkDemand > 1e-9 ? F(Math.Max(0.0, risk.MeanPositiveWorkDemand - risk.MeanBalanceConsumed)) : F(risk.MeanUnabsorbedWork),
                    hasPaired ? F(pairedRisk.MeanBalanceGenerated) : string.Empty,
                    hasPaired ? F(pairedRisk.MeanBalanceConsumed) : string.Empty,
                    hasPaired ? F(pairedRisk.MeanUnabsorbedWork) : string.Empty,
                    hasPaired ? F(pairedRisk.MeanUnabsorbedWorkRatio) : string.Empty,
                    hasPaired ? F(pairedRisk.BalanceRuptureProbability) : string.Empty
                });
                AppendResourceAbsorptionRows(instanceId, family, baselineId, gamma, risk, "SENSITIVITY");
            }
        }

        private static RiskResultDto AggregateReplicationResults(IList<RiskResultDto> primaryResults, IList<RiskResultDto> pairedResults, int seedInput, double gamma)
        {
            if (primaryResults == null || primaryResults.Count == 0)
                return new RiskResultDto { Seed = seedInput, Gamma = gamma, SummaryText = "No replication result." };

            Func<Func<RiskResultDto, double>, double> avg = selector => primaryResults.Average(selector);
            var result = new RiskResultDto
            {
                Iterations = primaryResults[0].Iterations,
                Gamma = gamma,
                Seed = seedInput,
                RuntimeMs = primaryResults.Sum(r => r.RuntimeMs),
                SamplingMode = primaryResults[0].SamplingMode,
                ReferenceMakespan = avg(r => r.ReferenceMakespan),
                MeanMakespan = avg(r => r.MeanMakespan),
                P50 = avg(r => r.P50),
                P95 = avg(r => r.P95),
                CVaR95 = avg(r => r.CVaR95),
                MakespanCVaR95 = avg(r => r.MakespanCVaR95),
                DelayProbability = avg(r => r.DelayProbability),
                MeanDelay = avg(r => r.MeanDelay),
                P95Delay = avg(r => r.P95Delay),
                CVaR95Delay = avg(r => r.CVaR95Delay),
                MaxDelay = avg(r => r.MaxDelay),
                BalanceRuptureProbability = avg(r => r.BalanceRuptureProbability),
                MeanBalanceGenerated = avg(r => r.MeanBalanceGenerated),
                MeanBalanceConsumed = avg(r => r.MeanBalanceConsumed),
                MeanBalanceUsage = avg(r => r.MeanBalanceUsage),
                MeanBalanceUsageRatio = avg(r => r.MeanBalanceUsageRatio),
                MinObservedBalance = avg(r => r.MinObservedBalance),
                CVaR95GivenBalanceRupture = avg(r => r.CVaR95GivenBalanceRupture),
                MeanPositiveWorkDemand = avg(r => r.MeanPositiveWorkDemand),
                MeanUnabsorbedWork = avg(r => r.MeanUnabsorbedWork),
                P95UnabsorbedWork = avg(r => r.P95UnabsorbedWork),
                CVaR95UnabsorbedWork = avg(r => r.CVaR95UnabsorbedWork),
                MeanUnabsorbedWorkRatio = avg(r => r.MeanUnabsorbedWorkRatio),


                MakespanSamples = primaryResults
                    .Where(r => r.MakespanSamples != null)
                    .SelectMany(r => r.MakespanSamples)
                    .ToList(),
                ReplicationCount = primaryResults.Count,
                ReplicationSeeds = primaryResults.Select(r => r.Seed).ToList(),
                ReplicationP95 = primaryResults.Select(r => r.P95).ToList(),
                ReplicationCVaR95Delay = primaryResults.Select(r => r.CVaR95Delay).ToList(),
                ReplicationDelayProbability = primaryResults.Select(r => r.DelayProbability).ToList(),
                ReplicationMeanDelay = primaryResults.Select(r => r.MeanDelay).ToList(),
                SummaryText = "Official estimate: arithmetic mean of " + primaryResults.Count + " independent replication estimates. Raw samples are retained only for distribution diagnostics."
            };

            result.ResourceAbsorption = primaryResults
                .Where(r => r.ResourceAbsorption != null)
                .SelectMany(r => r.ResourceAbsorption)
                .GroupBy(x => x.ResourceId)
                .Select(g => new ResourceAbsorptionMetricDto
                {
                    ResourceId = g.Key,
                    MeanBalanceGenerated = g.Average(x => x.MeanBalanceGenerated),
                    MeanBalanceConsumed = g.Average(x => x.MeanBalanceConsumed),
                    MeanPositiveWorkDemand = g.Average(x => x.MeanPositiveWorkDemand),
                    MeanUnabsorbedWork = g.Average(x => x.MeanUnabsorbedWork),
                    MeanUnabsorbedWorkRatio = g.Average(x => x.MeanUnabsorbedWorkRatio),
                    RuptureProbability = g.Average(x => x.RuptureProbability),
                    MinObservedBalance = g.Average(x => x.MinObservedBalance)
                })
                .OrderBy(x => x.ResourceId)
                .ToList();

            if (pairedResults != null && pairedResults.Count == primaryResults.Count && pairedResults.Count > 0)
            {
                result.PairedComparisonMode = primaryResults[0].PairedComparisonMode;
                var paired = AggregateReplicationResults(pairedResults, null, seedInput, gamma);
                if (primaryResults[0].PairedStructuralResult != null)
                    result.PairedStructuralResult = paired;
                else
                    result.PairedUnilateralResult = paired;
            }
            return result;
        }

        private void AddConfidenceRows(string baselineId, double gamma, string metric, double mean, double sd, int n)
        {
            int count = Math.Max(0, n);
            if (count < 2)
            {
                _confidenceRows.Add(new[] { baselineId, F(gamma), metric, F(mean), string.Empty, string.Empty, string.Empty, ExperimentalCsv.S(count), count > 0 ? ExperimentalCsv.S(count - 1) : string.Empty, string.Empty, "mean_of_independent_replications_student_t", "NA_INSUFFICIENT_REPLICATIONS" });
                return;
            }

            double critical = ExperimentalStatistics.StudentTCritical975(count - 1);
            double margin = critical * sd / Math.Sqrt(count);
            _confidenceRows.Add(new[] { baselineId, F(gamma), metric, F(mean), F(mean - margin), F(mean + margin), F(2 * margin), ExperimentalCsv.S(count), ExperimentalCsv.S(count - 1), F(critical), "mean_of_independent_replications_student_t", margin <= Math.Max(1e-9, Math.Abs(mean)) * 0.05 ? "ESTAVEL" : "AMPLA" });
        }

        private void ApplyDominance()
        {
            var grouped = _integrated.GroupBy(s => s.InstanceId).ToList();
            foreach (var group in grouped)
            {
                var list = group.ToList();
                foreach (var candidate in list)
                {
                    candidate.Dominated = false;
                    candidate.DominatedBy = string.Empty;
                    candidate.PairedDominated = false;
                    candidate.PairedDominatedBy = string.Empty;
                    candidate.Observation = string.Empty;
                    candidate.PairedObservation = string.Empty;
                }

                foreach (var candidate in list)
                {
                    foreach (var other in list)
                    {
                        if (ReferenceEquals(candidate, other))
                            continue;

                        bool dominatesPrimary = other.Makespan <= candidate.Makespan
                                         && other.SifGlobal >= candidate.SifGlobal
                                         && other.Cvar95Relative <= candidate.Cvar95Relative
                                         && (other.Makespan < candidate.Makespan
                                             || other.SifGlobal > candidate.SifGlobal
                                             || other.Cvar95Relative < candidate.Cvar95Relative);
                        if (dominatesPrimary)
                        {
                            candidate.Dominated = true;
                            candidate.DominatedBy = other.ScenarioId;
                            candidate.Observation = "Dominated by " + other.ScenarioId;
                            break;
                        }
                    }
                }

                var pairedList = list.Where(x => x.HasPaired).ToList();
                foreach (var candidate in pairedList)
                {
                    foreach (var other in pairedList)
                    {
                        if (ReferenceEquals(candidate, other))
                            continue;

                        bool dominatesPaired = other.PairedMakespan <= candidate.PairedMakespan
                                         && other.PairedSifGlobal >= candidate.PairedSifGlobal
                                         && other.PairedCvar95Relative <= candidate.PairedCvar95Relative
                                         && (other.PairedMakespan < candidate.PairedMakespan
                                             || other.PairedSifGlobal > candidate.PairedSifGlobal
                                             || other.PairedCvar95Relative < candidate.PairedCvar95Relative);
                        if (dominatesPaired)
                        {
                            candidate.PairedDominated = true;
                            candidate.PairedDominatedBy = other.ScenarioId;
                            candidate.PairedObservation = "Paired dominated by " + other.ScenarioId;
                            break;
                        }
                    }
                }
            }
        }

        private void WriteConsolidated(ExperimentalStudyConfig config, TimeSpan elapsed)
        {
            string dir = Path.Combine(config.OutputDirectory, "03_consolidated");
            Directory.CreateDirectory(dir);

            ExperimentalCsv.WriteRows(Path.Combine(dir, "todos_baselines.csv"), BaselineHeaders(), _allBaselines);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "todos_frm.csv"), FrmHeaders(), _allFrm);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "todos_frm_detalhado.csv"), FrmDetailHeaders(), _allFrmDetails);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "todos_monte_carlo.csv"), RiskHeaders(), _allRisk);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "todos_crashing.csv"), CrashingHeaders(), _allCrashing);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "todos_crashing_candidates.csv"), CrashingCandidateHeaders(), _allCrashingCandidates);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "todos_integrated.csv"), IntegratedHeaders(), _integrated.Select(IntegratedRow));
            ExperimentalCsv.WriteRows(Path.Combine(dir, "excluded_scenarios.csv"), ExcludedHeaders(), _excludedRows);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "sensibilidade.csv"), SensitivityHeaders(), _sensitivityRows);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "monte_carlo_confidence_intervals.csv"), ConfidenceHeaders(), _confidenceRows);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "monte_carlo_replications.csv"), ReplicationHeaders(), _replicationRows);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "monte_carlo_stability.csv"), StabilityHeaders(), _stabilityRows);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "baseline_deduplication.csv"), BaselineDeduplicationHeaders(), _baselineDeduplicationRows);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "frm_absorption_by_gamma.csv"), AbsorptionByGammaHeaders(), _absorptionByGammaRows);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "frm_absorption_by_resource.csv"), ResourceAbsorptionHeaders(), _resourceAbsorptionRows);
            WriteWeightedChapter4Results(Path.Combine(dir, "chapter4_weighted_results.csv"));
            ExperimentalCsv.WriteRows(Path.Combine(dir, "instance_selection_rfrs.csv"), InstanceSelectionHeaders(), _instanceSelectionRows);

            WriteCorrelations(Path.Combine(dir, "correlacoes.csv"));
            WriteDominance(Path.Combine(dir, "dominance.csv"));
            WriteFamilyResults(Path.Combine(dir, "resultados_por_family.csv"));
            WriteSensitivityCorrelations(Path.Combine(dir, "sensitivity_correlations.csv"));
            WritePropositionAssessment(Path.Combine(dir, "avaliacao_proposition.csv"));
            WriteClassificationRules(Path.Combine(dir, "classification_rules.json"));
            WriteModifiedDhBbVsHeuristics(dir);


            WriteStratifiedCorrelations(Path.Combine(dir, "correlacoes_rfrs_estratificadas.csv"));


            if (_policyPairs.Count > 0)
            {
                ExperimentalCsv.WriteRows(Path.Combine(dir, "crashing_policy_comparison.csv"),
                    new[] { "instance_id", "baseline_id", "frri_frm_guided", "frri_critical_path", "frri_risk_driven", "diff_crf_vs_crt", "diff_crf_vs_rrd", "winner_crf_vs_crt", "winner_crf_vs_rrd", "paired_enabled", "paired_mode", "paired_frri_frm_guided", "paired_frri_critical_path", "paired_frri_risk_driven", "paired_diff_crf_vs_crt", "paired_diff_crf_vs_rrd", "delta_modes_frri_frm_guided", "delta_modes_frri_critical_path", "delta_modes_frri_risk_driven" },
                    _policyComparisonRows);
                WriteCrashingPolicyWilcoxonTest(Path.Combine(dir, "crashing_policy_wilcoxon_test.csv"));
            }

            var summaryRows = new List<string[]>();
            double pearson = Correlation(_integrated.Where(s => s.Type == "baseline"), true);
            double spearman = Correlation(_integrated.Where(s => s.Type == "baseline"), false);
            int totalExactBaselines = _allBaselines.Count(r => r.Length > 5 && string.Equals(r[5], "True", StringComparison.OrdinalIgnoreCase));
            int totalHeuristicBaselines = _allBaselines.Count - totalExactBaselines;
            int totalExactReferenceBaselines = _allBaselines.Count(r => r.Length > 11 && string.Equals(r[11], BaselineMethodClassifier.ExactReference, StringComparison.OrdinalIgnoreCase));
            int totalExactTimeLimitBaselines = _allBaselines.Count(r => r.Length > 11 && string.Equals(r[11], BaselineMethodClassifier.ExactTimeLimit, StringComparison.OrdinalIgnoreCase));
            int totalExactUnprovenBaselines = _allBaselines.Count(r => r.Length > 11 && string.Equals(r[11], BaselineMethodClassifier.ExactUnproven, StringComparison.OrdinalIgnoreCase));
            int totalExactFailedBaselines = _allBaselines.Count(r => r.Length > 11 && string.Equals(r[11], BaselineMethodClassifier.ExactFailed, StringComparison.OrdinalIgnoreCase));
            string gammaSet = config.SensitivityGammas == null ? string.Empty : string.Join(";", config.SensitivityGammas.Select(g => F(g)).ToArray());

            summaryRows.Add(new[]
            {
                config.ExperimentId,
                config.SamplingModeUi ?? NormalizeSamplingMode(config.SamplingMode),
                F(config.Gamma),
                gammaSet,
                ExperimentalCsv.S(config.RunSensitivity),
                ExperimentalCsv.S(config.MaxCombinationSize),
                ExperimentalCsv.S(config.MaxCrashingScenarios),
                ExperimentalCsv.S(_instanceSelectionRows.Count(r => r.Length > 8 && string.Equals(r[7], "True", StringComparison.OrdinalIgnoreCase))),
                ExperimentalCsv.S(_integrated.Select(r => r.InstanceId).Distinct().Count()),
                ExperimentalCsv.S(_errorRows.Select(r => r[1]).Distinct().Count()),
                ExperimentalCsv.S(_integrated.Select(s => s.InstanceId).Distinct().Count()),
                ExperimentalCsv.S(totalHeuristicBaselines),
                ExperimentalCsv.S(totalExactBaselines),
                ExperimentalCsv.S(totalExactReferenceBaselines),
                ExperimentalCsv.S(totalExactTimeLimitBaselines),
                ExperimentalCsv.S(totalExactUnprovenBaselines),
                ExperimentalCsv.S(totalExactFailedBaselines),
                ExperimentalCsv.S(_allBaselines.Count),
                ExperimentalCsv.S(_allFrm.Count),
                ExperimentalCsv.S(_allRisk.Count),
                ExperimentalCsv.S(_allCrashing.Count),
                ExperimentalCsv.S(_integrated.Count),
                elapsed.ToString(),
                BestScenario(s => s.Makespan, true),
                BestScenario(s => s.SifGlobal, false),
                BestScenario(s => s.Cvar95Relative, true),
                F(pearson),
                F(spearman),
                "COMPLETED"
            });
            ExperimentalCsv.WriteRows(Path.Combine(dir, "experiment_summary.csv"), new[] { "experiment_id", "sampling_mode", "main_gamma", "gamma_set", "sensitivity_enabled", "max_activities_per_combo", "max_crashing_scenarios", "total_files", "files_processados_ok", "files_com_erro", "total_valid_instances", "total_heuristic_baselines", "total_exact_baselines", "total_exact_reference_baselines", "total_exact_time_limit_baselines", "total_exact_unproven_baselines", "total_exact_failed_baselines", "total_processed_baselines", "total_frm", "total_monte_carlo_runs", "total_crashing_scenarios", "total_integrated_scenarios", "total_runtime", "best_makespan_scenario", "best_sif_scenario", "melhor_scenario_relative_cvar95", "sif_cvar95_pearson_correlation", "sif_cvar95_spearman_correlation", "final_status" }, summaryRows);
        }


        private void WriteModifiedDhBbVsHeuristics(string dir)
        {
            var comparisons = BuildModifiedDhBbInstanceComparisons();

            WriteExactClassificationAudit(Path.Combine(dir, "modified_dh_bb_classification_audit.csv"));

            ExperimentalCsv.WriteRows(
                Path.Combine(dir, "modified_dh_bb_vs_heuristics_by_instance.csv"),
                ModifiedDhBbByInstanceHeaders(),
                comparisons.Select(ModifiedDhBbByInstanceRow));

            WriteModifiedDhBbSummary(Path.Combine(dir, "modified_dh_bb_vs_heuristics_summary.csv"), comparisons);
            WriteModifiedDhBbByGamma(Path.Combine(dir, "modified_dh_bb_vs_heuristics_by_gamma.csv"));
            WriteModifiedDhBbCrashing(Path.Combine(dir, "modified_dh_bb_vs_heuristics_crashing.csv"));
        }

        private List<ExactVsHeuristicInstanceComparison> BuildModifiedDhBbInstanceComparisons()
        {
            var rows = new List<ExactVsHeuristicInstanceComparison>();
            var baselines = _integrated.Where(s => string.Equals(s.Type, "baseline", StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var instanceGroup in baselines.GroupBy(s => s.InstanceId).OrderBy(g => g.Key))
            {
                var all = instanceGroup.Where(s => IsExactReference(s) || IsHeuristicMethod(s)).ToList();
                var exacts = all.Where(IsExactReference).ToList();
                var heuristics = all.Where(IsHeuristicMethod).ToList();

                if (exacts.Count == 0 || heuristics.Count == 0)
                    continue;

                foreach (var exact in exacts)
                {
                    var record = new ExactVsHeuristicInstanceComparison
                    {
                        InstanceId = instanceGroup.Key,
                        Family = exact.Family,
                        ExactBaselineId = exact.BaselineId,
                        ExactMakespan = exact.Makespan,
                        ExactSif = exact.SifGlobal,
                        ExactDelayCvar95 = exact.Cvar95Delay,
                        ExactRelativeCvar95 = exact.Cvar95Relative,
                        ExactMeanUnabsorbedWork = exact.MeanUnabsorbedWork,
                        ExactUnabsorbedWorkRatio = exact.MeanUnabsorbedWorkRatio,
                        HeuristicsCount = heuristics.Count,
                        HeuristicBestMakespan = MinOrZero(heuristics.Select(h => (double)h.Makespan)),
                        HeuristicMeanMakespan = MeanOrZero(heuristics.Select(h => (double)h.Makespan)),
                        HeuristicMedianMakespan = Median(heuristics.Select(h => (double)h.Makespan).ToList()),
                        HeuristicBestSif = MaxOrZero(heuristics.Select(h => h.SifGlobal)),
                        HeuristicMeanSif = MeanOrZero(heuristics.Select(h => h.SifGlobal)),
                        HeuristicMedianSif = Median(heuristics.Select(h => h.SifGlobal).ToList()),
                        HeuristicBestDelayCvar95 = MinOrZero(heuristics.Select(h => h.Cvar95Delay)),
                        HeuristicMeanDelayCvar95 = MeanOrZero(heuristics.Select(h => h.Cvar95Delay)),
                        HeuristicMedianDelayCvar95 = Median(heuristics.Select(h => h.Cvar95Delay).ToList()),
                        HeuristicBestRelativeCvar95 = MinOrZero(heuristics.Select(h => h.Cvar95Relative)),
                        HeuristicMeanRelativeCvar95 = MeanOrZero(heuristics.Select(h => h.Cvar95Relative)),
                        HeuristicMedianRelativeCvar95 = Median(heuristics.Select(h => h.Cvar95Relative).ToList()),
                        HeuristicMeanUnabsorbedWork = MeanOrZero(heuristics.Select(h => h.MeanUnabsorbedWork)),
                        HeuristicMeanUnabsorbedWorkRatio = MeanOrZero(heuristics.Select(h => h.MeanUnabsorbedWorkRatio)),
                        ExactRankByMakespan = RankScenario(all, exact, s => s.Makespan, true),
                        ExactRankBySif = RankScenario(all, exact, s => s.SifGlobal, false),
                        ExactRankByDelayCvar95 = RankScenario(all, exact, s => s.Cvar95Delay, true),
                        ExactRankByRelativeCvar95 = RankScenario(all, exact, s => s.Cvar95Relative, true)
                    };

                    record.DeltaExactVsMeanMakespan = record.ExactMakespan - record.HeuristicMeanMakespan;
                    record.DeltaExactVsBestMakespan = record.ExactMakespan - record.HeuristicBestMakespan;
                    record.DeltaExactVsMeanSif = record.ExactSif - record.HeuristicMeanSif;
                    record.DeltaExactVsMeanDelayCvar95 = record.ExactDelayCvar95 - record.HeuristicMeanDelayCvar95;
                    record.DeltaExactVsMeanRelativeCvar95 = record.ExactRelativeCvar95 - record.HeuristicMeanRelativeCvar95;
                    record.DeltaExactVsMeanUnabsorbedWork = record.ExactMeanUnabsorbedWork - record.HeuristicMeanUnabsorbedWork;

                    rows.Add(record);
                }
            }

            return rows;
        }

        private void WriteExactClassificationAudit(string path)
        {
            var rows = new List<string[]>();
            foreach (var row in _allBaselines.Where(r => r != null && r.Length > 11 && string.Equals(r[5], "True", StringComparison.OrdinalIgnoreCase)))
            {
                rows.Add(new[]
                {
                    row.Length > 0 ? row[0] : string.Empty,
                    row.Length > 1 ? row[1] : string.Empty,
                    row.Length > 2 ? row[2] : string.Empty,
                    row.Length > 10 ? row[10] : string.Empty,
                    row.Length > 11 ? row[11] : string.Empty,
                    row.Length > 12 ? row[12] : string.Empty,
                    row.Length > 13 ? row[13] : string.Empty,
                    row.Length > 14 ? row[14] : string.Empty,
                    row.Length > 15 ? row[15] : string.Empty,
                    row.Length > 16 ? row[16] : string.Empty
                });
            }

            ExperimentalCsv.WriteRows(path,
                new[]
                {
                    "instance_id", "baseline_id", "algorithm", "run_status", "method_classification",
                    "bb_time_limit_seconds", "bb_time_limit_reached", "bb_optimality_proven", "bb_nodes_visited", "bb_trace"
                }, rows);
        }

        private static string[] ModifiedDhBbByInstanceHeaders()
        {
            return new[]
            {
                "instance_id", "instance_family", "exact_baseline_id",
                "exact_makespan", "exact_sif", "exact_delay_cvar95", "exact_relative_cvar95", "exact_mean_unabsorbed_work", "exact_unabsorbed_work_ratio",
                "heuristics_count", "heuristic_best_makespan", "heuristic_mean_makespan", "heuristic_median_makespan",
                "heuristic_best_sif", "heuristic_mean_sif", "heuristic_median_sif",
                "heuristic_best_delay_cvar95", "heuristic_mean_delay_cvar95", "heuristic_median_delay_cvar95",
                "heuristic_best_relative_cvar95", "heuristic_mean_relative_cvar95", "heuristic_median_relative_cvar95",
                "heuristic_mean_unabsorbed_work", "heuristic_mean_unabsorbed_work_ratio",
                "delta_exact_vs_mean_makespan", "delta_exact_vs_best_makespan", "delta_exact_vs_mean_sif", "delta_exact_vs_mean_delay_cvar95", "delta_exact_vs_mean_relative_cvar95", "delta_exact_vs_mean_unabsorbed_work",
                "exact_rank_by_makespan", "exact_rank_by_sif", "exact_rank_by_delay_cvar95", "exact_rank_by_relative_cvar95",
                "interpretation"
            };
        }

        private static string[] ModifiedDhBbByInstanceRow(ExactVsHeuristicInstanceComparison r)
        {
            return new[]
            {
                r.InstanceId, r.Family, r.ExactBaselineId,
                F(r.ExactMakespan), F(r.ExactSif), F(r.ExactDelayCvar95), F(r.ExactRelativeCvar95), F(r.ExactMeanUnabsorbedWork), F(r.ExactUnabsorbedWorkRatio),
                ExperimentalCsv.S(r.HeuristicsCount), F(r.HeuristicBestMakespan), F(r.HeuristicMeanMakespan), F(r.HeuristicMedianMakespan),
                F(r.HeuristicBestSif), F(r.HeuristicMeanSif), F(r.HeuristicMedianSif),
                F(r.HeuristicBestDelayCvar95), F(r.HeuristicMeanDelayCvar95), F(r.HeuristicMedianDelayCvar95),
                F(r.HeuristicBestRelativeCvar95), F(r.HeuristicMeanRelativeCvar95), F(r.HeuristicMedianRelativeCvar95),
                F(r.HeuristicMeanUnabsorbedWork), F(r.HeuristicMeanUnabsorbedWorkRatio),
                F(r.DeltaExactVsMeanMakespan), F(r.DeltaExactVsBestMakespan), F(r.DeltaExactVsMeanSif), F(r.DeltaExactVsMeanDelayCvar95), F(r.DeltaExactVsMeanRelativeCvar95), F(r.DeltaExactVsMeanUnabsorbedWork),
                ExperimentalCsv.S(r.ExactRankByMakespan), ExperimentalCsv.S(r.ExactRankBySif), ExperimentalCsv.S(r.ExactRankByDelayCvar95), ExperimentalCsv.S(r.ExactRankByRelativeCvar95),
                InterpretExactVsHeuristic(r)
            };
        }

        private static string InterpretExactVsHeuristic(ExactVsHeuristicInstanceComparison r)
        {
            bool nominalBetter = r.ExactMakespan <= r.HeuristicMeanMakespan;
            bool riskBetter = r.ExactRelativeCvar95 <= r.HeuristicMeanRelativeCvar95;
            bool absorptionBetter = r.ExactMeanUnabsorbedWork <= r.HeuristicMeanUnabsorbedWork;

            if (nominalBetter && riskBetter && absorptionBetter)
                return "exact_reference_better_or_equal_on_nominal_risk_and_absorption";
            if (nominalBetter && !riskBetter)
                return "exact_reference_better_nominal_but_higher_relative_risk";
            if (!nominalBetter && riskBetter)
                return "heuristics_better_nominal_but_exact_reference_lower_relative_risk";
            return "mixed_or_heuristics_favorable";
        }

        private void WriteModifiedDhBbSummary(string path, List<ExactVsHeuristicInstanceComparison> comparisons)
        {
            int n = comparisons == null ? 0 : comparisons.Count;
            var rows = new List<string[]>();

            rows.Add(new[]
            {
                ExperimentalCsv.S(n),
                ExperimentalCsv.S(comparisons.Count(r => r.ExactMakespan <= r.HeuristicMeanMakespan)),
                Percent(comparisons.Count(r => r.ExactMakespan <= r.HeuristicMeanMakespan), Math.Max(1, n)),
                ExperimentalCsv.S(comparisons.Count(r => r.ExactMakespan <= r.HeuristicBestMakespan)),
                Percent(comparisons.Count(r => r.ExactMakespan <= r.HeuristicBestMakespan), Math.Max(1, n)),
                ExperimentalCsv.S(comparisons.Count(r => r.ExactSif >= r.HeuristicMeanSif)),
                Percent(comparisons.Count(r => r.ExactSif >= r.HeuristicMeanSif), Math.Max(1, n)),
                ExperimentalCsv.S(comparisons.Count(r => r.ExactDelayCvar95 <= r.HeuristicMeanDelayCvar95)),
                Percent(comparisons.Count(r => r.ExactDelayCvar95 <= r.HeuristicMeanDelayCvar95), Math.Max(1, n)),
                ExperimentalCsv.S(comparisons.Count(r => r.ExactRelativeCvar95 <= r.HeuristicMeanRelativeCvar95)),
                Percent(comparisons.Count(r => r.ExactRelativeCvar95 <= r.HeuristicMeanRelativeCvar95), Math.Max(1, n)),
                ExperimentalCsv.S(comparisons.Count(r => r.ExactMeanUnabsorbedWork <= r.HeuristicMeanUnabsorbedWork)),
                Percent(comparisons.Count(r => r.ExactMeanUnabsorbedWork <= r.HeuristicMeanUnabsorbedWork), Math.Max(1, n)),
                F(MeanOrZero(comparisons.Select(r => r.DeltaExactVsMeanMakespan))),
                F(MeanOrZero(comparisons.Select(r => r.DeltaExactVsMeanSif))),
                F(MeanOrZero(comparisons.Select(r => r.DeltaExactVsMeanDelayCvar95))),
                F(MeanOrZero(comparisons.Select(r => r.DeltaExactVsMeanRelativeCvar95))),
                F(MeanOrZero(comparisons.Select(r => r.DeltaExactVsMeanUnabsorbedWork)))
            });

            ExperimentalCsv.WriteRows(path,
                new[]
                {
                    "total_instances_compared",
                    "exact_better_or_equal_than_mean_makespan_count", "exact_better_or_equal_than_mean_makespan_percent",
                    "exact_better_or_equal_than_best_makespan_count", "exact_better_or_equal_than_best_makespan_percent",
                    "exact_better_or_equal_than_mean_sif_count", "exact_better_or_equal_than_mean_sif_percent",
                    "exact_better_or_equal_than_mean_delay_cvar95_count", "exact_better_or_equal_than_mean_delay_cvar95_percent",
                    "exact_better_or_equal_than_mean_relative_cvar95_count", "exact_better_or_equal_than_mean_relative_cvar95_percent",
                    "exact_better_or_equal_than_mean_unabsorbed_work_count", "exact_better_or_equal_than_mean_unabsorbed_work_percent",
                    "mean_delta_makespan", "mean_delta_sif", "mean_delta_delay_cvar95", "mean_delta_relative_cvar95", "mean_delta_unabsorbed_work"
                }, rows);
        }

        private void WriteModifiedDhBbByGamma(string path)
        {
            var baselinesById = _integrated
                .Where(s => string.Equals(s.Type, "baseline", StringComparison.OrdinalIgnoreCase))
                .GroupBy(s => s.BaselineId)
                .ToDictionary(g => g.Key, g => g.First());

            var parsed = new List<SensitivityMethodRecord>();
            foreach (var row in _sensitivityRows)
            {
                if (row == null || row.Length < 20)
                    continue;

                string baselineId = row[2];
                ScenarioRecord baseline;
                if (!baselinesById.TryGetValue(baselineId, out baseline))
                    continue;

                bool isExact = IsExactReference(baseline);
                bool isHeuristic = IsHeuristicMethod(baseline);
                if (!isExact && !isHeuristic)
                    continue;

                parsed.Add(new SensitivityMethodRecord
                {
                    Gamma = Parse(row[3]),
                    IsExact = isExact,
                    DelayCvar95 = Parse(row[13]),
                    RelativeCvar95 = Parse(row[15]),
                    P95 = Parse(row[12]),
                    DelayProbability = Parse(row[16]),
                    MeanDelay = Parse(row[17])
                });
            }

            var rows = new List<string[]>();
            foreach (var gammaGroup in parsed.GroupBy(r => r.Gamma).OrderBy(g => g.Key))
            {
                var exact = gammaGroup.Where(r => r.IsExact).ToList();
                var heur = gammaGroup.Where(r => !r.IsExact).ToList();
                rows.Add(new[]
                {
                    F(gammaGroup.Key),
                    ExperimentalCsv.S(exact.Count), ExperimentalCsv.S(heur.Count),
                    F(MeanOrZero(exact.Select(r => r.DelayCvar95))), F(MeanOrZero(heur.Select(r => r.DelayCvar95))), F(MeanOrZero(exact.Select(r => r.DelayCvar95)) - MeanOrZero(heur.Select(r => r.DelayCvar95))),
                    F(MeanOrZero(exact.Select(r => r.RelativeCvar95))), F(MeanOrZero(heur.Select(r => r.RelativeCvar95))), F(MeanOrZero(exact.Select(r => r.RelativeCvar95)) - MeanOrZero(heur.Select(r => r.RelativeCvar95))),
                    F(MeanOrZero(exact.Select(r => r.P95))), F(MeanOrZero(heur.Select(r => r.P95))), F(MeanOrZero(exact.Select(r => r.P95)) - MeanOrZero(heur.Select(r => r.P95))),
                    F(MeanOrZero(exact.Select(r => r.DelayProbability))), F(MeanOrZero(heur.Select(r => r.DelayProbability))), F(MeanOrZero(exact.Select(r => r.DelayProbability)) - MeanOrZero(heur.Select(r => r.DelayProbability))),
                    F(MeanOrZero(exact.Select(r => r.MeanDelay))), F(MeanOrZero(heur.Select(r => r.MeanDelay))), F(MeanOrZero(exact.Select(r => r.MeanDelay)) - MeanOrZero(heur.Select(r => r.MeanDelay)))
                });
            }

            ExperimentalCsv.WriteRows(path,
                new[]
                {
                    "gamma", "exact_count", "heuristic_count",
                    "exact_mean_delay_cvar95", "heuristic_mean_delay_cvar95", "delta_delay_cvar95",
                    "exact_mean_relative_cvar95", "heuristic_mean_relative_cvar95", "delta_relative_cvar95",
                    "exact_mean_p95", "heuristic_mean_p95", "delta_p95",
                    "exact_mean_delay_probability", "heuristic_mean_delay_probability", "delta_delay_probability",
                    "exact_mean_delay", "heuristic_mean_delay", "delta_mean_delay"
                }, rows);
        }

        private void WriteModifiedDhBbCrashing(string path)
        {
            var baselinesById = _integrated
                .Where(s => string.Equals(s.Type, "baseline", StringComparison.OrdinalIgnoreCase))
                .GroupBy(s => s.BaselineId)
                .ToDictionary(g => g.Key, g => g.First());

            var crashing = _integrated.Where(s => string.Equals(s.Type, "crashing", StringComparison.OrdinalIgnoreCase)).ToList();
            var rows = new List<string[]>();

            var eligibleCrashing = crashing
                .Where(s =>
                {
                    string classification = GetMethodClassificationByBaselineId(s.BaselineId, baselinesById);
                    return string.Equals(classification, BaselineMethodClassifier.ExactReference, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(classification, BaselineMethodClassifier.Heuristic, StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            foreach (var group in eligibleCrashing.GroupBy(s => GetMethodClassificationByBaselineId(s.BaselineId, baselinesById)).OrderBy(g => g.Key))
            {
                var list = group.ToList();
                var deltasMakespan = new List<double>();
                var deltasSif = new List<double>();
                var deltasDelayCvar = new List<double>();
                var deltasRelativeCvar = new List<double>();
                var deltasUnabsorbed = new List<double>();
                var frri = new List<double>();

                foreach (var scenario in list)
                {
                    ScenarioRecord baseline;
                    if (!baselinesById.TryGetValue(scenario.BaselineId, out baseline))
                        continue;

                    deltasMakespan.Add(scenario.Makespan - baseline.Makespan);
                    deltasSif.Add(scenario.SifGlobal - baseline.SifGlobal);
                    deltasDelayCvar.Add(scenario.Cvar95Delay - baseline.Cvar95Delay);
                    deltasRelativeCvar.Add(scenario.Cvar95Relative - baseline.Cvar95Relative);
                    deltasUnabsorbed.Add(scenario.MeanUnabsorbedWork - baseline.MeanUnabsorbedWork);
                    frri.Add(scenario.Frri);
                }

                int total = list.Count;
                rows.Add(new[]
                {
                    group.Key,
                    ExperimentalCsv.S(total),
                    F(MeanOrZero(deltasMakespan)), F(MeanOrZero(deltasSif)), F(MeanOrZero(deltasDelayCvar)), F(MeanOrZero(deltasRelativeCvar)), F(MeanOrZero(frri)), F(MeanOrZero(deltasUnabsorbed)),
                    ExperimentalCsv.S(list.Count(s => string.Equals(s.Classification, "EFFICIENT", StringComparison.OrdinalIgnoreCase))), Percent(list.Count(s => string.Equals(s.Classification, "EFFICIENT", StringComparison.OrdinalIgnoreCase)), Math.Max(1, total)),
                    ExperimentalCsv.S(list.Count(s => string.Equals(s.Classification, "FAST_BUT_HIGH_RISK", StringComparison.OrdinalIgnoreCase))), Percent(list.Count(s => string.Equals(s.Classification, "FAST_BUT_HIGH_RISK", StringComparison.OrdinalIgnoreCase)), Math.Max(1, total)),
                    ExperimentalCsv.S(list.Count(s => string.Equals(s.Classification, "ROBUST_BUT_SLOW", StringComparison.OrdinalIgnoreCase))), Percent(list.Count(s => string.Equals(s.Classification, "ROBUST_BUT_SLOW", StringComparison.OrdinalIgnoreCase)), Math.Max(1, total)),
                    ExperimentalCsv.S(list.Count(s => string.Equals(s.Classification, "AMBIGUOUS", StringComparison.OrdinalIgnoreCase))), Percent(list.Count(s => string.Equals(s.Classification, "AMBIGUOUS", StringComparison.OrdinalIgnoreCase)), Math.Max(1, total)),
                    ExperimentalCsv.S(list.Count(s => string.Equals(s.Classification, "EFFICIENT_COM_TRADEOFF", StringComparison.OrdinalIgnoreCase))), Percent(list.Count(s => string.Equals(s.Classification, "EFFICIENT_COM_TRADEOFF", StringComparison.OrdinalIgnoreCase)), Math.Max(1, total))
                });
            }

            ExperimentalCsv.WriteRows(path,
                new[]
                {
                    "method_family", "total_crashing_scenarios",
                    "mean_delta_makespan", "mean_delta_sif", "mean_delta_delay_cvar95", "mean_delta_relative_cvar95", "mean_frri", "mean_delta_unabsorbed_work",
                    "efficient_count", "efficient_percent", "fast_but_high_risk_count", "fast_but_high_risk_percent", "robust_but_slow_count", "robust_but_slow_percent", "ambiguous_count", "ambiguous_percent", "efficient_com_tradeoff_count", "efficient_com_tradeoff_percent"
                }, rows);
        }

        private static bool IsExactReference(ScenarioRecord record)
        {
            if (record == null)
                return false;
            return string.Equals(record.MethodClassification, BaselineMethodClassifier.ExactReference, StringComparison.OrdinalIgnoreCase)
                && record.BranchAndBoundTimeLimitReached == false
                && record.BranchAndBoundOptimalityProven == true;
        }

        private static bool IsHeuristicMethod(ScenarioRecord record)
        {
            return record != null &&
                string.Equals(record.MethodClassification, BaselineMethodClassifier.Heuristic, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetMethodClassificationByBaselineId(string baselineId, Dictionary<string, ScenarioRecord> baselinesById)
        {
            ScenarioRecord baseline;
            if (baselinesById != null && baselinesById.TryGetValue(baselineId, out baseline) && baseline != null)
                return baseline.MethodClassification ?? string.Empty;
            return string.Empty;
        }

        private static int RankScenario(List<ScenarioRecord> list, ScenarioRecord target, Func<ScenarioRecord, double> selector, bool ascending)
        {
            if (list == null || target == null || selector == null)
                return 0;

            var ordered = ascending
                ? list.OrderBy(selector).ThenBy(s => s.BaselineId).ToList()
                : list.OrderByDescending(selector).ThenBy(s => s.BaselineId).ToList();

            for (int i = 0; i < ordered.Count; i++)
                if (ReferenceEquals(ordered[i], target) || string.Equals(ordered[i].ScenarioId, target.ScenarioId, StringComparison.OrdinalIgnoreCase))
                    return i + 1;
            return 0;
        }

        private static double MeanOrZero(IEnumerable<double> values)
        {
            if (values == null)
                return 0.0;
            var list = values.ToList();
            return list.Count == 0 ? 0.0 : ExperimentalStatistics.Mean(list);
        }

        private static double MinOrZero(IEnumerable<double> values)
        {
            if (values == null)
                return 0.0;
            var list = values.ToList();
            return list.Count == 0 ? 0.0 : list.Min();
        }

        private static double MaxOrZero(IEnumerable<double> values)
        {
            if (values == null)
                return 0.0;
            var list = values.ToList();
            return list.Count == 0 ? 0.0 : list.Max();
        }

        private static void WriteClassificationRules(string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"framework_mode\": \"FRM_WORKCONTENT_BILATERAL\",");
            sb.AppendLine("  \"risk_metric\": \"Delay_CVaR95 and Delay_CVaR95_Relative\",");
            sb.AppendLine("  \"frri_formula\": \"(Delay_CVaR95_original - Delay_CVaR95_crashing) / Delay_CVaR95_original\",");
            sb.AppendLine("  \"classes\": {");
            sb.AppendLine("    \"EFFICIENT\": \"delta_makespan < 0 and risk decreases and SIF is preserved or increased\",");
            sb.AppendLine("    \"EFFICIENT_COM_TRADEOFF\": \"delta_makespan < 0 and risk decreases, but SIF decreases\",");
            sb.AppendLine("    \"FAST_BUT_HIGH_RISK\": \"delta_makespan < 0 and risk does not decrease\",");
            sb.AppendLine("    \"ROBUST_BUT_SLOW\": \"delta_makespan >= 0 and risk decreases and SIF is preserved or increased\",");
            sb.AppendLine("    \"AMBIGUOUS\": \"all remaining combinations\",");
            sb.AppendLine("    \"BASELINE\": \"original uncompressed baseline scenario\"");
            sb.AppendLine("  },");
            sb.AppendLine("  \"dominance_primary\": \"makespan <=, SIF >=, relative Delay_CVaR95 <=, with at least one strict improvement\",");
            sb.AppendLine("  \"absorption_fields\": [\"balance_rupture_probability\", \"mean_balance_usage\", \"mean_unabsorbed_work\", \"mean_unabsorbed_work_ratio\"]");
            sb.AppendLine("}");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        private void WriteCorrelations(string path)
        {
            var rows = new List<string[]>();
            AddCorrelation(rows, "All", "ALL", "confirmatory", "SIF_global", "relative_CVaR95", _integrated.Where(s => s.Type == "baseline").ToList(), s => s.SifGlobal, s => s.Cvar95Relative);
            AddCorrelation(rows, "All", "ALL", "confirmatory", "SIF_global", "relative_P95", _integrated.Where(s => s.Type == "baseline").ToList(), s => s.SifGlobal, s => s.P95Relative);
            AddCorrelation(rows, "All", "ALL", "confirmatory", "SIF_global", "delay_CVaR95", _integrated.Where(s => s.Type == "baseline").ToList(), s => s.SifGlobal, s => s.Cvar95Delay);
            AddCorrelation(rows, "All", "ALL", "confirmatory", "SIF_global", "delay_P95", _integrated.Where(s => s.Type == "baseline").ToList(), s => s.SifGlobal, s => s.P95Delay);
            AddCorrelation(rows, "All", "ALL", "exploratory", "Makespan", "relative_CVaR95", _integrated.Where(s => s.Type == "baseline").ToList(), s => s.Makespan, s => s.Cvar95Relative);

            var pairedBaselines = _integrated.Where(s => s.Type == "baseline" && s.HasPaired).ToList();
            if (pairedBaselines.Count > 0)
            {
                AddCorrelation(rows, "All", "ALL", "confirmatory_paired", "paired_SIF_global", "paired_relative_CVaR95", pairedBaselines, s => s.PairedSifGlobal, s => s.PairedCvar95Relative);
                AddCorrelation(rows, "All", "ALL", "confirmatory_paired", "paired_SIF_global", "paired_delay_CVaR95", pairedBaselines, s => s.PairedSifGlobal, s => s.PairedCvar95Delay);
            }

            foreach (var family in _integrated.Where(s => s.Type == "baseline").GroupBy(s => s.Family))
            {
                AddCorrelation(rows, "Family", family.Key, "confirmatory", "SIF_global", "relative_CVaR95", family.ToList(), s => s.SifGlobal, s => s.Cvar95Relative);
                AddCorrelation(rows, "Family", family.Key, "confirmatory", "SIF_global", "delay_CVaR95", family.ToList(), s => s.SifGlobal, s => s.Cvar95Delay);
            }

            ExperimentalCsv.WriteRows(path, new[] { "group", "instance_family", "analysis_type", "x_variable", "y_variable", "pearson", "spearman", "n", "interpretation" }, rows);
        }

        private void AddCorrelation(List<string[]> rows, string group, string family, string type, string xName, string yName, List<ScenarioRecord> records, Func<ScenarioRecord, double> x, Func<ScenarioRecord, double> y)
        {
            var valid = records.Where(r => !double.IsNaN(x(r)) && !double.IsNaN(y(r))).ToList();
            var xs = valid.Select(x).ToList();
            var ys = valid.Select(y).ToList();
            var weights = BuildEqualInstanceBaselineWeights(valid);
            double p = ExperimentalStatistics.WeightedPearson(xs, ys, weights);
            double s = ExperimentalStatistics.WeightedSpearman(xs, ys, weights);
            rows.Add(new[] { group, family, type + "_weighted_instance_baseline", xName, yName, F(p), F(s), ExperimentalCsv.S(valid.Count), ExperimentalStatistics.InterpretCorrelation(s) });
        }

        private void WriteDominance(string path)
        {
            var rows = new List<string[]>();
            foreach (var s in _integrated)
                rows.Add(new[]
                {
                    s.InstanceId, s.ScenarioId,
                    ExperimentalCsv.S(s.Dominated), s.DominatedBy,
                    "primary:makespan<=;sif>=;relative_cvar95<=",
                    ExperimentalCsv.S(s.Makespan), F(s.SifGlobal), F(s.Cvar95Relative), s.Classification,
                    ExperimentalCsv.S(s.HasPaired), s.PairedMode,
                    ExperimentalCsv.S(s.PairedDominated), s.PairedDominatedBy,
                    "paired:makespan<=;sif>=;relative_cvar95<=",
                    ExperimentalCsv.S(s.PairedMakespan), F(s.PairedSifGlobal), F(s.PairedCvar95Relative),
                    F(s.DeltaModesSif), F(s.DeltaModesP95), F(s.DeltaModesCvar95), F(s.DeltaModesFrri)
                });
            ExperimentalCsv.WriteRows(path,
                new[] { "instance_id", "scenario_id", "dominated_primary", "dominated_by_primary", "dominance_criteria_primary", "makespan", "sif_global", "relative_cvar95", "class", "paired_enabled", "paired_mode", "dominated_paired", "dominated_by_paired", "dominance_criteria_paired", "paired_makespan", "paired_sif_global", "paired_relative_cvar95", "delta_modes_sif", "delta_modes_p95", "delta_modes_cvar95", "delta_modes_frri" },
                rows);
        }

        private void WriteFamilyResults(string path)
        {
            var rows = new List<string[]>();
            foreach (var family in _integrated.Where(s => s.Type == "baseline").GroupBy(s => s.Family))
            {
                var list = family.ToList();
                var weights = BuildEqualInstanceBaselineWeights(list);
                double spearmanCvar = ExperimentalStatistics.WeightedSpearman(list.Select(s => s.SifGlobal).ToList(), list.Select(s => s.Cvar95Relative).ToList(), weights);
                double spearmanP95 = ExperimentalStatistics.WeightedSpearman(list.Select(s => s.SifGlobal).ToList(), list.Select(s => s.P95Relative).ToList(), weights);
                double spearmanDelay = ExperimentalStatistics.WeightedSpearman(list.Select(s => s.SifGlobal).ToList(), list.Select(s => s.Cvar95Delay).ToList(), weights);
                rows.Add(new[] { family.Key, ExperimentalCsv.S(list.Select(s => s.InstanceId).Distinct().Count()), ExperimentalCsv.S(list.Count), F(ExperimentalStatistics.WeightedMean(list.Select(s => s.SifGlobal).ToList(), weights)), F(ExperimentalStatistics.WeightedMean(list.Select(s => s.P95Relative).ToList(), weights)), F(ExperimentalStatistics.WeightedMean(list.Select(s => s.Cvar95Relative).ToList(), weights)), F(ExperimentalStatistics.WeightedMean(list.Select(s => s.Cvar95Delay).ToList(), weights)), F(spearmanCvar), F(spearmanP95), F(spearmanDelay), ExperimentalStatistics.InterpretCorrelation(spearmanCvar) });
            }
            ExperimentalCsv.WriteRows(path, new[] { "instance_family", "n_instances", "n_baselines", "mean_sif", "mean_relative_p95", "mean_relative_cvar95", "mean_delay_cvar95", "spearman_sif_relative_cvar95", "spearman_sif_relative_p95", "spearman_sif_delay_cvar95", "interpretation" }, rows);
        }


        private void WriteStratifiedCorrelations(string path)
        {
            var baselines = _integrated.Where(s => s.Type == "baseline" && s.ResourceStrength > 0).ToList();
            if (baselines.Count == 0)
            {
                ExperimentalCsv.WriteRows(path, new[] { "stratum", "n", "spearman_sif_relative_cvar95", "spearman_sif_delay_cvar95", "interpretation" }, new List<string[]>());
                return;
            }

            var uniqueInstances = baselines.GroupBy(s => s.InstanceId).Select(g => g.First()).ToList();
            double rsMedian = _rsSelectionThreshold > 0.0 ? _rsSelectionThreshold : Median(uniqueInstances.Select(s => s.ResourceStrength).ToList());
            double rfMedian = _rfSelectionThreshold > 0.0 ? _rfSelectionThreshold : Median(uniqueInstances.Select(s => s.ResourceFactor).ToList());

            var rows = new List<string[]>();
            AddStratumRow(rows, "ALL", baselines, rfMedian, rsMedian);
            AddStratumRow(rows, "RS_HIGH", baselines.Where(s => s.ResourceStrength > rsMedian).ToList(), rfMedian, rsMedian);
            AddStratumRow(rows, "RS_LOW", baselines.Where(s => s.ResourceStrength <= rsMedian).ToList(), rfMedian, rsMedian);
            AddStratumRow(rows, "RF_HIGH", baselines.Where(s => s.ResourceFactor > rfMedian).ToList(), rfMedian, rsMedian);
            AddStratumRow(rows, "RF_LOW", baselines.Where(s => s.ResourceFactor <= rfMedian).ToList(), rfMedian, rsMedian);
            AddStratumRow(rows, "RF_HIGH_RS_HIGH", baselines.Where(s => s.ResourceStrength > rsMedian && s.ResourceFactor > rfMedian).ToList(), rfMedian, rsMedian);
            AddStratumRow(rows, "RF_LOW_RS_HIGH", baselines.Where(s => s.ResourceStrength > rsMedian && s.ResourceFactor <= rfMedian).ToList(), rfMedian, rsMedian);
            AddStratumRow(rows, "RF_HIGH_RS_LOW", baselines.Where(s => s.ResourceStrength <= rsMedian && s.ResourceFactor > rfMedian).ToList(), rfMedian, rsMedian);
            AddStratumRow(rows, "RF_LOW_RS_LOW", baselines.Where(s => s.ResourceStrength <= rsMedian && s.ResourceFactor <= rfMedian).ToList(), rfMedian, rsMedian);

            ExperimentalCsv.WriteRows(path, new[] { "stratum", "n", "rs_median_threshold", "rf_median_threshold", "spearman_sif_relative_cvar95", "spearman_sif_delay_cvar95", "interpretation_relative", "interpretation_delay" }, rows);
        }

        private static void AddStratumRow(List<string[]> rows, string stratum, List<ScenarioRecord> list, double rfThreshold, double rsThreshold)
        {
            if (list.Count == 0)
            {
                rows.Add(new[] { stratum, "0", F(rsThreshold), F(rfThreshold), "", "", "insufficient_data", "insufficient_data" });
                return;
            }
            var weights = BuildEqualInstanceBaselineWeights(list);
            double spRel = ExperimentalStatistics.WeightedSpearman(list.Select(s => s.SifGlobal).ToList(), list.Select(s => s.Cvar95Relative).ToList(), weights);
            double spDelay = ExperimentalStatistics.WeightedSpearman(list.Select(s => s.SifGlobal).ToList(), list.Select(s => s.Cvar95Delay).ToList(), weights);
            rows.Add(new[] { stratum, ExperimentalCsv.S(list.Count), F(rsThreshold), F(rfThreshold), F(spRel), F(spDelay), ExperimentalStatistics.InterpretCorrelation(spRel), ExperimentalStatistics.InterpretCorrelation(spDelay) });
        }

        private static double Median(List<double> values)
        {
            if (values == null || values.Count == 0)
                return 0.0;
            var sorted = values.OrderBy(v => v).ToList();
            int mid = sorted.Count / 2;
            return sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2.0 : sorted[mid];
        }


        private void WriteCrashingPolicyWilcoxonTest(string path)
        {
            var rows = new List<string[]>();


            var diffsCrfVsCrt = _policyPairs.Select(p => p.DiffCrfVsCrt).ToList();
            var resCrfVsCrt = ExperimentalStatistics.WilcoxonSignedRankTest(diffsCrfVsCrt);
            rows.Add(BuildWilcoxonRow("H2a", "FRRI(FRM_GUIDED) > FRRI(CRITICAL_PATH)", resCrfVsCrt, _policyPairs.Count));


            var diffsCrfVsRrd = _policyPairs.Select(p => p.DiffCrfVsRrd).ToList();
            var resCrfVsRrd = ExperimentalStatistics.WilcoxonSignedRankTest(diffsCrfVsRrd);
            rows.Add(BuildWilcoxonRow("H2b", "FRRI(FRM_GUIDED) > FRRI(RISK_DRIVEN)", resCrfVsRrd, _policyPairs.Count));

            ExperimentalCsv.WriteRows(path,
                new[] { "hypothesis", "comparison", "n_pairs", "n_zero_diff", "W_plus", "W_minus", "z_statistic", "p_value_onetailed", "effect_size_r", "significant_alpha05", "note" },
                rows);
        }

        private static string[] BuildWilcoxonRow(string hyp, string comparison, WilcoxonResult res, int totalPairs)
        {
            return new[]
            {
                hyp, comparison,
                ExperimentalCsv.S(res.N + res.NZeros),
                ExperimentalCsv.S(res.NZeros),
                F(res.WPlus), F(res.WMinus),
                F(res.Z), F(res.PValueOneTailed),
                F(res.EffectSizeR),
                ExperimentalCsv.S(res.Significant),
                res.Note
            };
        }


        private void RunThreePolicyCrashing(
            ExperimentalStudyConfig config,
            ProjectDataDto project,
            BaselineRunSummaryDto run,
            ExecutionSummary summary,
            string instanceId,
            string baselineId)
        {
            double frriCrf = BestFrri(summary.Crashing);

            var crashBase = BuildCrashingOptions(config);
            RiskResultDto pairedRisk = summary.Risk != null ? (summary.Risk.PairedStructuralResult ?? summary.Risk.PairedUnilateralResult) : null;
            bool hasPaired = pairedRisk != null;
            string pairedMode = hasPaired ? pairedRisk.SamplingMode : string.Empty;

            double frriCrt = 0.0;
            double frriRrd = 0.0;
            double pairedFrriCrf = hasPaired ? BestFrri(summary.Crashing != null ? (summary.Crashing.PairedStructuralResult ?? summary.Crashing.PairedUnilateralResult) : null) : 0.0;
            double pairedFrriCrt = 0.0;
            double pairedFrriRrd = 0.0;

            try
            {
                var crtOptions = CloneCrashingOptions(crashBase);
                crtOptions.CrashingPolicyMode = "CRITICAL_PATH";
                var crtResult = _crashingAnalyzer.Run(project, run.BaselineResult, summary.Frm, summary.Risk, crtOptions);
                frriCrt = BestFrri(crtResult);
                if (hasPaired)
                {
                    var pairedCrtResult = _crashingAnalyzer.Run(project, run.BaselineResult, summary.Frm, pairedRisk, crtOptions);
                    pairedFrriCrt = BestFrri(pairedCrtResult);
                }
            }
            catch (Exception ex)
            {
                AddError(instanceId, baselineId, string.Empty, "ThreePolicy_CRT", ex.GetType().Name, ex.Message, "CRT policy skipped for this baseline.");
            }

            try
            {
                var rrdOptions = CloneCrashingOptions(crashBase);
                rrdOptions.CrashingPolicyMode = "RISK_DRIVEN";
                var rrdResult = _crashingAnalyzer.Run(project, run.BaselineResult, summary.Frm, summary.Risk, rrdOptions);
                frriRrd = BestFrri(rrdResult);
                if (hasPaired)
                {
                    var pairedRrdResult = _crashingAnalyzer.Run(project, run.BaselineResult, summary.Frm, pairedRisk, rrdOptions);
                    pairedFrriRrd = BestFrri(pairedRrdResult);
                }
            }
            catch (Exception ex)
            {
                AddError(instanceId, baselineId, string.Empty, "ThreePolicy_RRD", ex.GetType().Name, ex.Message, "RISK_DRIVEN policy skipped for this baseline.");
            }

            string winnerCrfVsCrt = frriCrf > frriCrt + 1e-9 ? "CRF_WINS"
                                  : frriCrt > frriCrf + 1e-9 ? "CRT_WINS" : "TIE";
            string winnerCrfVsRrd = frriCrf > frriRrd + 1e-9 ? "CRF_WINS"
                                  : frriRrd > frriCrf + 1e-9 ? "RRD_WINS" : "TIE";

            _policyComparisonRows.Add(new[]
            {
                instanceId, baselineId,
                F(frriCrf), F(frriCrt), F(frriRrd),
                F(frriCrf - frriCrt), F(frriCrf - frriRrd),
                winnerCrfVsCrt, winnerCrfVsRrd,
                ExperimentalCsv.S(hasPaired), pairedMode,
                hasPaired ? F(pairedFrriCrf) : string.Empty,
                hasPaired ? F(pairedFrriCrt) : string.Empty,
                hasPaired ? F(pairedFrriRrd) : string.Empty,
                hasPaired ? F(pairedFrriCrf - pairedFrriCrt) : string.Empty,
                hasPaired ? F(pairedFrriCrf - pairedFrriRrd) : string.Empty,
                hasPaired ? F(pairedFrriCrf - frriCrf) : string.Empty,
                hasPaired ? F(pairedFrriCrt - frriCrt) : string.Empty,
                hasPaired ? F(pairedFrriRrd - frriRrd) : string.Empty
            });

            _policyPairs.Add(new PolicyPairRecord
            {
                InstanceId = instanceId,
                BaselineId = baselineId,
                FrriFrmGuided = frriCrf,
                FrriCriticalPath = frriCrt,
                FrriRiskDriven = frriRrd,
                DiffCrfVsCrt = frriCrf - frriCrt,
                DiffCrfVsRrd = frriCrf - frriRrd
            });
        }

        private static double BestFrri(CrashingResultDto crashing)
        {
            if (crashing == null || crashing.Scenarios == null || crashing.Scenarios.Count == 0)
                return 0.0;
            return crashing.Scenarios.Max(s => s.Frri);
        }

        private static CrashingOptionsDto CloneCrashingOptions(CrashingOptionsDto source)
        {
            return new CrashingOptionsDto
            {
                Enabled = source.Enabled,
                CrashingPolicyMode = source.CrashingPolicyMode,
                MaxActivitiesToCrash = source.MaxActivitiesToCrash,
                MaxCombinationSize = source.MaxCombinationSize,
                MaxScenarioCount = source.MaxScenarioCount,
                RecalculateRiskAfterCrash = source.RecalculateRiskAfterCrash,
                UseFrmGuidance = source.UseFrmGuidance,
                PrioritizeStructuralAcceptability = source.PrioritizeStructuralAcceptability,
                KeepProblematicScenariosVisible = source.KeepProblematicScenariosVisible,
                ScoreWeightMakespan = source.ScoreWeightMakespan,
                ScoreWeightP95 = source.ScoreWeightP95,
                ScoreWeightCVaR95 = source.ScoreWeightCVaR95,
                ScoreWeightFrmRobustness = source.ScoreWeightFrmRobustness,
                BranchAndBoundTimeLimitSeconds = source.BranchAndBoundTimeLimitSeconds,
                CandidatesOnly = source.CandidatesOnly,
                CandidateActivities = source.CandidateActivities == null
                    ? new List<CrashingCandidateActivityDto>()
                    : new List<CrashingCandidateActivityDto>(source.CandidateActivities)
            };
        }


        private static void ComputeRfRs(ProjectDataDto project, out double rf, out double rs)
        {
            rf = 0.0;
            rs = 0.0;

            if (project == null || project.Activities == null || project.Resources == null)
                return;

            var activities = project.Activities.Where(a => !a.IsSummary && !a.IsDummy).ToList();
            int n = activities.Count;
            int K = project.Resources.Count;

            if (n == 0 || K == 0)
                return;


            int totalPairs = 0;
            foreach (var a in activities)
                if (a.Assignments != null)
                    totalPairs += a.Assignments.Count(x => x.Units > 0);
            rf = (double)totalPairs / (n * K);


            var capById = project.Resources.ToDictionary(r => r.Id, r => r.Capacity);
            var maxDemandById = new Dictionary<int, double>();
            foreach (var r in project.Resources)
                maxDemandById[r.Id] = 0.0;

            foreach (var a in activities)
                if (a.Assignments != null)
                    foreach (var ass in a.Assignments)
                        if (capById.ContainsKey(ass.ResourceId) && ass.Units > maxDemandById[ass.ResourceId])
                            maxDemandById[ass.ResourceId] = ass.Units;

            double rsSum = 0.0;
            int rsCount = 0;
            foreach (var r in project.Resources)
            {
                double maxD;
                if (!maxDemandById.TryGetValue(r.Id, out maxD) || maxD <= 0.0)
                    continue;
                rsSum += r.Capacity / maxD;
                rsCount++;
            }

            rs = rsCount > 0 ? rsSum / rsCount : 0.0;
        }

        private void ApplyRfRs(ScenarioRecord record, string instanceId)
        {
            double rf, rs;
            _rfByInstance.TryGetValue(instanceId, out rf);
            _rsByInstance.TryGetValue(instanceId, out rs);
            record.ResourceFactor = rf;
            record.ResourceStrength = rs;
            record.RsRfBand = GetRfRsStratum(rf, rs, _rfSelectionThreshold, _rsSelectionThreshold);
        }

        private void WriteSensitivityCorrelations(string path)
        {
            var rows = new List<string[]>();

            var parsed = _sensitivityRows.Select(r => new { InstanceId = r[0], BaselineId = r[2], Gamma = r[3], Sif = Parse(r[10]), CvarRel = Parse(r[15]), CvarDelay = Parse(r[13]) }).ToList();
            foreach (var g in parsed.GroupBy(x => x.Gamma))
            {
                var list = g.ToList();
                var xs = list.Select(v => v.Sif).ToList();
                var ysRel = list.Select(v => v.CvarRel).ToList();
                var counts = list.GroupBy(v => v.InstanceId).ToDictionary(v => v.Key, v => v.Count());
                int nInstances = counts.Count;
                var weights = list.Select(v => nInstances == 0 ? 0.0 : 1.0 / (nInstances * counts[v.InstanceId])).ToList();
                double pRel = ExperimentalStatistics.WeightedPearson(xs, ysRel, weights);
                double sRel = ExperimentalStatistics.WeightedSpearman(xs, ysRel, weights);
                rows.Add(new[] { g.Key, string.Empty, string.Empty, "SIF_global", "relative_CVaR95", F(pRel), F(sRel), ExperimentalCsv.S(xs.Count), ExperimentalStatistics.InterpretCorrelation(sRel) });

                var ysDelay = list.Select(v => v.CvarDelay).ToList();
                double pDelay = ExperimentalStatistics.WeightedPearson(xs, ysDelay, weights);
                double sDelay = ExperimentalStatistics.WeightedSpearman(xs, ysDelay, weights);
                rows.Add(new[] { g.Key, string.Empty, string.Empty, "SIF_global", "delay_CVaR95", F(pDelay), F(sDelay), ExperimentalCsv.S(xs.Count), ExperimentalStatistics.InterpretCorrelation(sDelay) });
            }
            ExperimentalCsv.WriteRows(path, new[] { "gamma", "iterations", "replications", "x_variable", "y_variable", "pearson", "spearman", "n", "interpretation" }, rows);
        }

        private void WritePropositionAssessment(string path)
        {
            var baselines = _integrated.Where(s => s.Type == "baseline").ToList();
            var propositionWeights = BuildEqualInstanceBaselineWeights(baselines);
            double spearmanRel = ExperimentalStatistics.WeightedSpearman(baselines.Select(s => s.SifGlobal).ToList(), baselines.Select(s => s.Cvar95Relative).ToList(), propositionWeights);
            double spearmanDelay = ExperimentalStatistics.WeightedSpearman(baselines.Select(s => s.SifGlobal).ToList(), baselines.Select(s => s.Cvar95Delay).ToList(), propositionWeights);

            int consistentFamiliesRel = 0;
            int consistentFamiliesDelay = 0;
            foreach (var family in baselines.GroupBy(s => s.Family))
            {
                double sfRel = ExperimentalStatistics.Spearman(family.Select(s => s.SifGlobal).ToList(), family.Select(s => s.Cvar95Relative).ToList());
                double sfDelay = ExperimentalStatistics.Spearman(family.Select(s => s.SifGlobal).ToList(), family.Select(s => s.Cvar95Delay).ToList());
                if (sfRel <= -0.30) consistentFamiliesRel++;
                if (sfDelay <= -0.30) consistentFamiliesDelay++;
            }

            string classificationRel = ClassifyProposition(spearmanRel, consistentFamiliesRel);
            string classificationDelay = ClassifyProposition(spearmanDelay, consistentFamiliesDelay);

            ExperimentalCsv.WriteRows(path, new[] { "proposition", "criterion", "observed_result", "classification", "consistent_families", "consistent_gammas", "observation" }, new[]
            {
                new[] { "Higher SIF tends to lower relative CVaR95", "Spearman(SIF_global, relative_CVaR95)", F(spearmanRel), classificationRel, ExperimentalCsv.S(consistentFamiliesRel), "see sensitivity_correlations.csv", "Automatic assessment; review interpretation in Chapter 4." },
                new[] { "Higher SIF tends to lower delay CVaR95", "Spearman(SIF_global, delay_CVaR95)", F(spearmanDelay), classificationDelay, ExperimentalCsv.S(consistentFamiliesDelay), "see sensitivity_correlations.csv", "Delay metric added to measure delay risk directly; review interpretation in Chapter 4." }
            });
        }

        private static string ClassifyProposition(double spearman, int consistentFamilies)
        {
            if (spearman <= -0.60 && consistentFamilies >= 3)
                return "STRONGLY_SUPPORTED";
            if (spearman <= -0.30)
                return "PARTIALLY_SUPPORTED";
            if (spearman > 0.30)
                return "CONTRADICTED";
            return "NOT_SUPPORTED_OR_INCONCLUSIVE";
        }

        private void WriteGraphData(ExperimentalStudyConfig config)
        {
            string dir = Path.Combine(config.OutputDirectory, "04_graph_data");
            Directory.CreateDirectory(dir);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "chart_sif_cvar95.csv"), new[] { "scenario_id", "instance_id", "scenario_type", "sif_global", "relative_cvar95", "delay_cvar95", "delay_probability", "makespan", "integrated_classification" }, _integrated.Select(s => new[] { s.ScenarioId, s.InstanceId, s.Type, F(s.SifGlobal), F(s.Cvar95Relative), F(s.Cvar95Delay), F(s.DelayProbability), ExperimentalCsv.S(s.Makespan), s.Classification }));
            ExperimentalCsv.WriteRows(Path.Combine(dir, "chart_sif_delay_cvar95.csv"), new[] { "scenario_id", "instance_id", "scenario_type", "sif_global", "delay_cvar95", "delay_probability", "makespan", "integrated_classification" }, _integrated.Select(s => new[] { s.ScenarioId, s.InstanceId, s.Type, F(s.SifGlobal), F(s.Cvar95Delay), F(s.DelayProbability), ExperimentalCsv.S(s.Makespan), s.Classification }));
            ExperimentalCsv.WriteRows(Path.Combine(dir, "chart_sif_p95.csv"), new[] { "scenario_id", "instance_id", "scenario_type", "sif_global", "relative_p95", "delay_p95", "makespan", "integrated_classification" }, _integrated.Select(s => new[] { s.ScenarioId, s.InstanceId, s.Type, F(s.SifGlobal), F(s.P95Relative), F(s.P95Delay), ExperimentalCsv.S(s.Makespan), s.Classification }));
            ExperimentalCsv.WriteRows(Path.Combine(dir, "chart_makespan_cvar95.csv"), new[] { "scenario_id", "instance_id", "scenario_type", "makespan", "relative_cvar95", "delay_cvar95", "sif_global", "integrated_classification" }, _integrated.Select(s => new[] { s.ScenarioId, s.InstanceId, s.Type, ExperimentalCsv.S(s.Makespan), F(s.Cvar95Relative), F(s.Cvar95Delay), F(s.SifGlobal), s.Classification }));
            ExperimentalCsv.WriteRows(Path.Combine(dir, "chart_sif_cvar95_paired.csv"), new[] { "scenario_id", "instance_id", "scenario_type", "paired_sif_global", "paired_relative_cvar95", "paired_delay_cvar95", "paired_enabled", "integrated_classification" }, _integrated.Where(s => s.HasPaired).Select(s => new[] { s.ScenarioId, s.InstanceId, s.Type, F(s.PairedSifGlobal), F(s.PairedCvar95Relative), F(s.PairedCvar95Delay), ExperimentalCsv.S(s.HasPaired), s.Classification }));
            ExperimentalCsv.WriteRows(Path.Combine(dir, "chart_makespan_cvar95_paired.csv"), new[] { "scenario_id", "instance_id", "scenario_type", "paired_makespan", "paired_relative_cvar95", "paired_delay_cvar95", "paired_sif_global", "paired_enabled", "integrated_classification" }, _integrated.Where(s => s.HasPaired).Select(s => new[] { s.ScenarioId, s.InstanceId, s.Type, ExperimentalCsv.S(s.PairedMakespan), F(s.PairedCvar95Relative), F(s.PairedCvar95Delay), F(s.PairedSifGlobal), ExperimentalCsv.S(s.HasPaired), s.Classification }));
            ExperimentalCsv.WriteRows(Path.Combine(dir, "chart_delta_modes_risk.csv"), new[] { "scenario_id", "instance_id", "scenario_type", "delta_modes_p95", "delta_modes_cvar95", "delta_modes_frri", "paired_enabled" }, _integrated.Where(s => s.HasPaired).Select(s => new[] { s.ScenarioId, s.InstanceId, s.Type, F(s.DeltaModesP95), F(s.DeltaModesCvar95), F(s.DeltaModesFrri), ExperimentalCsv.S(s.HasPaired) }));
            ExperimentalCsv.WriteRows(Path.Combine(dir, "chart_scenario_frri.csv"), new[] { "scenario_id", "instance_id", "source_baseline", "frri", "integrated_classification" }, _integrated.Select(s => new[] { s.ScenarioId, s.InstanceId, s.BaselineId, F(s.Frri), s.Classification }));
            ExperimentalCsv.WriteRows(Path.Combine(dir, "grafico_crashing_delta.csv"), CrashingHeaders(), _allCrashing);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "grafico_sensibilidade_gamma.csv"), SensitivityHeaders(), _sensitivityRows);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "grafico_histograma_makespan.csv"), new[] { "scenario_id", "instance_id", "bin_inicio", "bin_fim", "frequencia", "p50", "p95", "cvar95" }, new List<string[]>());
        }

        private void WriteReportTables(ExperimentalStudyConfig config)
        {
            string dir = Path.Combine(config.OutputDirectory, "06_report_tables");
            Directory.CreateDirectory(dir);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "tabela_baselines.csv"), BaselineHeaders(), _allBaselines);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "tabela_frm.csv"), FrmHeaders(), _allFrm);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "tabela_frm_detalhado.csv"), FrmDetailHeaders(), _allFrmDetails);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "tabela_monte_carlo.csv"), RiskHeaders(), _allRisk);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "tabela_crashing.csv"), CrashingHeaders(), _allCrashing);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "tabela_crashing_candidates.csv"), CrashingCandidateHeaders(), _allCrashingCandidates);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "integrated_table.csv"), new[] { "Instance", "Scenario", "Type", "Makespan", "SIF", "P95_rel", "CVaR95_rel", "delay_prob", "delay_P95", "delay_CVaR95", "FRRI", "Paired", "Paired_Mode", "Paired_SIF", "Paired_CVaR95_rel", "Paired_FRRI", "Delta_Modes_SIF", "Delta_Modes_P95", "Delta_Modes_CVaR95", "Delta_Modes_FRRI", "Class" }, _integrated.Select(s => new[] { s.InstanceId, s.ScenarioId, s.Type, ExperimentalCsv.S(s.Makespan), F(s.SifGlobal), F(s.P95Relative), F(s.Cvar95Relative), F(s.DelayProbability), F(s.P95Delay), F(s.Cvar95Delay), F(s.Frri), ExperimentalCsv.S(s.HasPaired), s.PairedMode, F(s.PairedSifGlobal), F(s.PairedCvar95Relative), F(s.PairedFrri), F(s.DeltaModesSif), F(s.DeltaModesP95), F(s.DeltaModesCvar95), F(s.DeltaModesFrri), s.Classification }));
            ExperimentalCsv.WriteRows(Path.Combine(dir, "tabela_intervalos_confianca.csv"), ConfidenceHeaders(), _confidenceRows);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "tabela_sensibilidade.csv"), SensitivityHeaders(), _sensitivityRows);
            WriteWeightedChapter4Results(Path.Combine(dir, "tabela_resultados_ponderados_instancia_baseline.csv"));
            ExperimentalCsv.WriteRows(Path.Combine(dir, "tabela_selecao_instancias_rfrs.csv"), InstanceSelectionHeaders(), _instanceSelectionRows);
            WriteResumoTextual(Path.Combine(dir, "resumo_textual_experimento.txt"));
        }

        private void WriteLogs(ExperimentalStudyConfig config)
        {
            string dir = Path.Combine(config.OutputDirectory, "05_logs");
            Directory.CreateDirectory(dir);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "error_log.csv"), ErrorHeaders(), _errorRows);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "warnings.csv"), WarningHeaders(), _warningRows);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "pipeline_trace.csv"), PipelineHeaders(), _pipelineRows);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "crashing_sampling_policy.csv"), new[] { "baseline_id", "activities_elegiveis", "total_scenarios_possiveis", "total_scenarios_avaliados", "politica_selecao", "max_activities_por_combinacao", "max_atividades_candidatas", "max_cenarios_crashing", "percentual_coberto", "seed_amostragem", "justificativa" }, _crashingPolicyRows);
            File.WriteAllText(Path.Combine(dir, "execution_log.txt"), "Experimental execution generated at " + DateTime.Now.ToString("s") + Environment.NewLine, Encoding.UTF8);
        }

        private bool HasAllRfRsStrata()
        {
            var selected = _instanceSelectionRows.Where(r => r.Length > 8 && string.Equals(r[7], "True", StringComparison.OrdinalIgnoreCase)).Select(r => r[6]).Distinct().ToList();
            return new[] { "RF_LOW_RS_LOW", "RF_LOW_RS_HIGH", "RF_HIGH_RS_LOW", "RF_HIGH_RS_HIGH" }.All(selected.Contains);
        }

        private void WriteReadinessChecklist(ExperimentalStudyConfig config)
        {
            var rows = new List<string[]>
            {


                new[] { "SCIENTIFIC_HYPOTHESES", "H1 — SIF versus probabilistic risk", _integrated.Any(s => s.Type == "baseline") ? "ASSESSED" : "NOT_ASSESSED", "Use the weighted SIF-risk correlations and avaliacao_proposition.csv. Interpret absolute and relative risk separately." },
                new[] { "SCIENTIFIC_HYPOTHESES", "H2 — Gamma modulation of the SIF-risk relationship", _sensitivityRows.Count > 0 ? "ASSESSED" : "NOT_ASSESSED", "Use sensibilidade.csv and the weighted correlations by gamma. This hypothesis is unrelated to the optional crashing-policy comparison." },
                new[] { "SCIENTIFIC_HYPOTHESES", "H3 — Multicriteria discrimination of crashing scenarios", _allCrashing.Count > 0 && _integrated.Any(s => s.Type != "baseline") ? "ASSESSED" : "NOT_ASSESSED", "Use the integrated S0 versus Sj comparison, FRRI, confidence intervals and scenario classifications." },


                new[] { "OPERATIONAL_CHECKS", "Button btnExperimentalTese starts the experimental module", "OK", "The Ribbon opens FormExperimentalTese." },
                new[] { "OPERATIONAL_CHECKS", "Validated instances", _excludedRows.Any(r => r[3].Contains("instance")) ? "WARNING" : "OK", "See excluded_scenarios.csv." },
                new[] { "OPERATIONAL_CHECKS", "Feasible baselines", _allBaselines.Count > 0 ? "OK" : "WARNING", "See 02_baseline_validation.csv." },
                new[] { "OPERATIONAL_CHECKS", "FRM calculated", _allFrm.Count > 0 ? "OK" : "WARNING", "See todos_frm.csv." },
                new[] { "OPERATIONAL_CHECKS", "Monte Carlo executed", _allRisk.Count > 0 ? "OK" : "WARNING", "See todos_monte_carlo.csv." },
                new[] { "OPERATIONAL_CHECKS", "Independent replications executed", _replicationRows.Count > 0 ? "OK" : "WARNING", "See monte_carlo_replications.csv." },
                new[] { "OPERATIONAL_CHECKS", "Confidence intervals generated", _confidenceRows.Count > 0 ? "OK" : "WARNING", "See monte_carlo_confidence_intervals.csv." },
                new[] { "OPERATIONAL_CHECKS", "Gamma sensitivity executed", _sensitivityRows.Count > 0 ? "OK" : "WARNING", "See sensibilidade.csv." },
                new[] { "OPERATIONAL_CHECKS", "Crashing evaluated", _allCrashing.Count > 0 ? "OK" : "WARNING", "See todos_crashing.csv." },
                new[] { "OPERATIONAL_CHECKS", "Normalized metrics calculated", _integrated.Count > 0 ? "OK" : "WARNING", "See todos_integrated.csv." },
                new[] { "OPERATIONAL_CHECKS", "Dominance calculated", "OK", "See dominance.csv." },
                new[] { "OPERATIONAL_CHECKS", "Proposition assessment file generated", "OK", "See avaliacao_proposition.csv." },
                new[] { "OPERATIONAL_CHECKS", "Graph data generated", "OK", "See 04_graph_data." },
                new[] { "OPERATIONAL_CHECKS", "RF/RS stratified correlations", _rfByInstance.Count > 0 ? "OK" : "WARNING", "See correlacoes_rfrs_estratificadas.csv." },
                new[] { "OPERATIONAL_CHECKS", "RF/RS instance coverage", HasAllRfRsStrata() ? "OK" : "WARNING", "See instance_selection_rfrs.csv. Four strata are expected for complete structural coverage." },
                new[] { "OPERATIONAL_CHECKS", "Hierarchical instance/baseline weighting", _integrated.Any(s => s.Type == "baseline") ? "OK" : "WARNING", "See chapter4_weighted_results.csv and weighted correlations." },
                new[] { "OPERATIONAL_CHECKS", "Auxiliary crashing-policy comparison", _policyPairs.Count > 0 ? "OK" : "NOT_RUN", "Optional analysis. Enable RunThreePolicies=true to run. See crashing_policy_comparison.csv and crashing_policy_wilcoxon_test.csv. This operational check is separate from the scientific hypotheses." },
                new[] { "METHODOLOGICAL_NOTES", "SIF metric definition", "OK", "SifGlobal in code = min_k(SIF_k), SIF_k = sum(r_ik*slack_i): faithful to Faria eq.(3.24)/(3.25) and to thesis Sec.2.5.8. SIF* = sum_k(max(balance_k_min,0)) is an auxiliary conservative metric and is not the indicator used in the H1 correlations. The thesis text must keep these definitions distinct." }
            };
            ExperimentalCsv.WriteRows(Path.Combine(config.OutputDirectory, "06_report_tables", "chapter4_readiness_checklist.csv"), new[] { "Section", "Item", "Status", "Observation" }, rows);
        }

        private void WriteResumoTextual(string path)
        {
            var baselines = _integrated.Where(s => s.Type == "baseline").ToList();
            double spearman = ExperimentalStatistics.Spearman(baselines.Select(s => s.SifGlobal).ToList(), baselines.Select(s => s.Cvar95Relative).ToList());
            double spearmanDelay = ExperimentalStatistics.Spearman(baselines.Select(s => s.SifGlobal).ToList(), baselines.Select(s => s.Cvar95Delay).ToList());
            string text = "Automatic summary of the experimental study" + Environment.NewLine + Environment.NewLine
                + "Baselines gerados: " + _allBaselines.Count + Environment.NewLine
                + "Generated FRM diagnostics: " + _allFrm.Count + Environment.NewLine
                + "Main Monte Carlo runs: " + _allRisk.Count + Environment.NewLine
                + "Exported crashing scenarios: " + _allCrashing.Count + Environment.NewLine
                + "Spearman correlation between global SIF and relative CVaR95 in baselines: " + F(spearman) + Environment.NewLine
                + "Spearman correlation between global SIF and delay CVaR95 in baselines: " + F(spearmanDelay) + Environment.NewLine
                + "Automatic interpretation (relative CVaR): " + ExperimentalStatistics.InterpretCorrelation(spearman) + Environment.NewLine + Environment.NewLine
                + "This text is an automatic summary and must be reviewed before being incorporated into Chapter 4.";
            File.WriteAllText(path, text, Encoding.UTF8);
        }


        private static string[] ValidationHeaders() { return new[] { "instance_id", "source_file", "validation_status", "has_cycle", "activity_count", "resource_count", "precedence_count", "durations_invalid", "demands_invalid", "demands_above_capacity", "disconnected_activities", "message" }; }
        private static string[] BaselineHeaders() { return new[] { "instance_id", "baseline_id", "heuristic", "scheme", "direction", "exact_method", "makespan_nominal", "runtime_ms", "activity_order", "selected_for_frm", "status", "method_classification", "bb_time_limit_seconds", "bb_time_limit_reached", "bb_optimality_proven", "bb_nodes_visited", "bb_trace" }; }
        private static string[] BaselineValidationHeaders() { return new[] { "baseline_id", "feasible_precedence", "feasible_resource", "violation_count_precedence", "violation_count_resource", "max_violacao_resource", "activities_not_scheduled", "makespan_consistent", "status" }; }
        private static string[] FrmHeaders() { return new[] { "instance_id", "baseline_id", "sif_global", "min_resource_sif", "critical_sif_resource", "balance0_total", "balance_final", "min_balance_global", "flexible_activity_count", "crashing_eligible_activity_count", "critical_activity_count", "balance_limited_critical_count", "balance_fully_funded_critical_count", "balance_generated_total", "balance_requested_total", "balance_consumed_total", "balance_unabsorbed_total", "structural_classification" }; }
        private static string[] FrmDetailHeaders() { return new[] { "instance_id", "baseline_id", "activity", "resource", "slack_ik", "slack_i", "raw_score", "net_score", "balance", "nominal_duration", "physical_min_duration", "physical_max_duration", "structural_min_duration", "structural_max_duration", "structural_duration", "is_critical", "structural_duration_before_balance", "structural_duration_after_balance", "requested_negative_score", "applied_negative_score", "balance_before", "balance_generated", "balance_requested", "balance_consumed", "balance_after", "balance_limited", "limiting_resource", "duration_decision" }; }
        private static string[] RiskHeaders() { return new[] { "instance_id", "baseline_id", "iterations", "replications", "seed_input", "seed_effective", "seed_policy", "gamma", "distribution", "makespan_nominal", "mc_mean", "p50", "p95", "primary_delay_cvar95", "makespan_cvar95", "relative_p95", "relative_delay_cvar95", "p95_excess", "delay_cvar95", "p95_excess_percent", "delay_cvar95_percent", "delay_probability", "mean_delay", "delay_p95", "recalculated_delay_cvar95", "max_delay", "stddev", "simulated_min", "simulated_max", "runtime_ms", "balance_rupture_probability", "mean_balance_generated", "mean_balance_consumed", "mean_balance_usage", "mean_balance_usage_ratio", "min_observed_balance", "cvar95_given_balance_rupture", "mean_positive_work_demand", "mean_unabsorbed_work", "p95_unabsorbed_work", "cvar95_unabsorbed_work", "mean_unabsorbed_work_ratio", "sampling_mode_ui", "paired_enabled", "paired_mode", "paired_mean_makespan", "paired_p50", "paired_p95", "paired_delay_cvar95", "paired_makespan_cvar95", "paired_relative_p95", "paired_relative_delay_cvar95", "paired_p95_excess", "paired_p95_excess_percent", "paired_delay_cvar95_percent", "paired_delay_probability", "paired_mean_delay", "paired_delay_p95", "paired_recalculated_delay_cvar95", "paired_max_delay", "paired_stddev", "paired_simulated_min", "paired_simulated_max", "delta_modes_mean_makespan", "delta_modes_p50", "delta_modes_p95", "delta_modes_delay_cvar95", "delta_modes_relative_delay_cvar95", "delta_modes_delay_probability", "delta_modes_mean_delay", "delta_modes_delay_p95", "delta_modes_max_delay" }; }
        private void AppendResourceAbsorptionRows(string instanceId, string family, string baselineId, double gamma, RiskResultDto risk, string scope)
        {
            if (risk == null || risk.ResourceAbsorption == null)
                return;
            foreach (var item in risk.ResourceAbsorption.OrderBy(x => x.ResourceId))
            {
                _resourceAbsorptionRows.Add(new[]
                {
                    instanceId, family, baselineId, F(gamma), scope, ExperimentalCsv.S(item.ResourceId),
                    F(item.MeanBalanceGenerated), F(item.MeanBalanceConsumed),
                    F(item.MeanPositiveWorkDemand), F(item.MeanUnabsorbedWork),
                    F(item.MeanUnabsorbedWorkRatio), F(item.RuptureProbability),
                    F(item.MinObservedBalance)
                });
            }
        }

        private static string[] ResourceAbsorptionHeaders()
        {
            return new[] { "instance_id", "family", "baseline_id", "gamma", "scope", "resource_id", "mean_balance_generated", "mean_balance_consumed", "mean_positive_work_demand", "mean_unabsorbed_work", "mean_unabsorbed_work_ratio", "rupture_probability", "min_observed_balance" };
        }

        private static string[] CrashingHeaders() { return new[] { "instance_id", "baseline_id", "crashing_scenario_id", "activities_comprimidas", "crashed_activity_count", "makespan_original", "makespan_crashing", "delta_makespan", "sif_original", "sif_crashing", "delta_sif", "p95_original", "p95_crashing", "delta_p95", "cvar95_original", "cvar95_crashing", "delta_cvar95", "relative_p95_original", "relative_p95_crashing", "relative_cvar95_original", "relative_cvar95_crashing", "delta_relative_cvar95", "delay_p95_original", "delay_p95_crashing", "delay_cvar95_original", "delay_cvar95_crashing", "delta_delay_cvar95", "balance_rupture_original", "balance_rupture_crashing", "delta_balance_rupture", "mean_balance_usage_original", "mean_balance_usage_crashing", "delta_mean_balance_usage", "mean_unabsorbed_work_original", "mean_unabsorbed_work_crashing", "delta_mean_unabsorbed_work", "mean_unabsorbed_work_ratio_original", "mean_unabsorbed_work_ratio_crashing", "delta_mean_unabsorbed_work_ratio", "frri", "replication_count", "scenario_cvar95_sd", "scenario_cvar95_ci_lower", "scenario_cvar95_ci_upper", "frri_sd", "frri_ci_lower", "frri_ci_upper", "classification", "paired_enabled", "paired_mode", "paired_scenario_matched", "sif_crashing_paired", "delta_sif_paired", "delta_modes_sif", "p95_original_paired", "p95_crashing_paired", "delta_p95_paired", "cvar95_original_paired", "cvar95_crashing_paired", "delta_cvar95_paired", "relative_p95_original_paired", "relative_p95_crashing_paired", "relative_cvar95_original_paired", "relative_cvar95_crashing_paired", "delta_relative_cvar95_paired", "delay_p95_original_paired", "delay_p95_crashing_paired", "delay_cvar95_original_paired", "delay_cvar95_crashing_paired", "delta_delay_cvar95_paired", "frri_paired", "classification_paired", "delta_modes_p95", "delta_modes_cvar95", "delta_modes_frri" }; }
        private static string[] CrashingCandidateHeaders() { return new[] { "instance_id", "baseline_id", "candidate_rank", "activity", "activity_name", "use_candidate", "nominal_duration", "minimum_duration", "recommended_new_duration", "new_duration", "max_compression", "is_eligible", "is_dummy", "frm_slack_i", "frm_criticality", "frm_sensitivity", "frm_balance_risk", "frm_balance_risk_band", "frm_priority", "candidate_reason" }; }
        private static string[] CrashingDetailHeaders() { return new[] { "crashing_scenario_id", "activity", "original_duration", "crashed_duration", "delta_duration", "frm_eligible", "frm_min_duration", "frm_max_duration", "eligibility_reason" }; }
        private static string[] IntegratedHeaders() { return new[] { "instance_id", "instance_family", "scenario_id", "scenario_type", "source_baseline", "heuristic", "scheme", "direction", "makespan", "sif_global", "p50", "p95", "primary_delay_cvar95", "relative_p95", "relative_delay_cvar95", "p95_excess", "delay_cvar95", "delay_probability", "mean_delay", "delay_p95", "recalculated_delay_cvar95", "max_delay", "balance_rupture_probability", "mean_balance_usage", "mean_unabsorbed_work", "mean_unabsorbed_work_ratio", "delta_balance_rupture", "delta_mean_balance_usage", "delta_mean_unabsorbed_work", "delta_mean_unabsorbed_work_ratio", "frri", "paired_enabled", "paired_mode", "paired_makespan", "paired_sif_global", "paired_p95", "paired_delay_cvar95", "paired_relative_p95", "paired_relative_delay_cvar95", "paired_p95_excess", "paired_delay_probability", "paired_mean_delay", "paired_delay_p95", "paired_recalculated_delay_cvar95", "paired_max_delay", "paired_frri", "delta_modes_sif", "delta_modes_p95", "delta_modes_cvar95", "delta_modes_frri", "dominated_primary", "dominated_by_primary", "dominated_paired", "dominated_by_paired", "integrated_classification", "include_confirmatory_analysis", "observation", "paired_observation", "method_classification", "bb_time_limit_reached", "bb_optimality_proven" }; }
        private static string[] SensitivityHeaders() { return new[] { "instance_id", "instance_family", "baseline_id", "gamma", "iterations", "replications", "seed_input", "seed_effective", "seed_policy", "common_random_numbers_group", "sif_global", "makespan_nominal", "p95", "primary_delay_cvar95", "relative_p95", "relative_delay_cvar95", "delay_probability", "mean_delay", "delay_p95", "recalculated_delay_cvar95", "stability_classification", "paired_enabled", "paired_mode", "paired_p95", "paired_delay_cvar95", "paired_relative_p95", "paired_relative_delay_cvar95", "paired_delay_probability", "paired_mean_delay", "paired_delay_p95", "paired_recalculated_delay_cvar95", "delta_modes_p95", "delta_modes_delay_cvar95", "delta_modes_relative_delay_cvar95", "delta_modes_delay_probability", "delta_modes_mean_delay", "delta_modes_delay_p95", "mean_balance_generated", "mean_balance_consumed", "mean_balance_usage_ratio", "mean_positive_work_demand", "mean_unabsorbed_work", "p95_unabsorbed_work", "cvar95_unabsorbed_work", "mean_unabsorbed_work_ratio", "balance_rupture_probability", "min_observed_balance" }; }
        private static string[] BaselineDeduplicationHeaders() { return new[] { "instance_id", "unique_baseline_id", "schedule_signature_sha256", "generator_index", "heuristic", "scheme", "direction", "exact_method", "method_classification", "makespan", "selected_as_canonical", "equivalent_generator_count", "all_generators", "deduplication_reason" }; }
        private static string[] AbsorptionByGammaHeaders() { return new[] { "instance_id", "instance_family", "baseline_id", "gamma", "iterations", "replications", "seed_input", "seed_effective", "estimation_method", "common_random_numbers_group", "sampling_mode", "mean_balance_generated", "mean_balance_consumed", "mean_balance_usage", "mean_balance_usage_ratio", "mean_positive_work_demand", "mean_unabsorbed_work", "p95_unabsorbed_work", "cvar95_unabsorbed_work", "mean_unabsorbed_work_ratio", "balance_rupture_probability", "min_observed_balance", "absorbed_demand_ratio", "estimated_unabsorbed_work", "paired_mean_balance_generated", "paired_mean_balance_consumed", "paired_mean_unabsorbed_work", "paired_mean_unabsorbed_work_ratio", "paired_balance_rupture_probability" }; }
        private static string[] ConfidenceHeaders() { return new[] { "baseline_id", "gamma", "metric", "value", "ci95_lower", "ci95_upper", "ci_width", "replication_count", "degrees_of_freedom", "t_critical_0_975", "estimation_method", "stability" }; }
        private static string[] ReplicationHeaders() { return new[] { "instance_id", "baseline_id", "replication", "replication_seed", "gamma", "primary_mean_makespan", "primary_p50", "primary_p95", "primary_delay_cvar95", "primary_stddev", "primary_simulated_min", "primary_simulated_max", "paired_enabled", "paired_mode", "paired_mean_makespan", "paired_p50", "paired_p95", "paired_delay_cvar95", "paired_stddev", "paired_simulated_min", "paired_simulated_max", "delta_modes_mean_makespan", "delta_modes_p50", "delta_modes_p95", "delta_modes_delay_cvar95" }; }
        private static string[] StabilityHeaders() { return new[] { "baseline_id", "gamma", "media_delay_cvar95", "stddev_delay_cvar95", "cv_delay_cvar95", "mean_p95", "stddev_p95", "cv_p95", "paired_mean_delay_cvar95", "paired_stddev_delay_cvar95", "paired_cv_delay_cvar95", "paired_mean_p95", "paired_stddev_p95", "paired_cv_p95", "delta_modes_mean_delay_cvar95", "delta_modes_stddev_delay_cvar95", "delta_modes_mean_p95", "delta_modes_stddev_p95", "stability_classification" }; }
        private static string[] ErrorHeaders() { return new[] { "timestamp", "instance_id", "baseline_id", "scenario_id", "stage", "error_type", "message", "action_taken" }; }
        private static string[] WarningHeaders() { return new[] { "timestamp", "instance_id", "stage", "warning", "impact", "recommendation" }; }
        private static string[] PipelineHeaders() { return new[] { "instance_id", "baseline_id", "scenario_id", "import", "instance_validation", "baseline", "baseline_validation", "frm", "monte_carlo", "crashing", "integrated", "final_status" }; }
        private static string[] ExcludedHeaders() { return new[] { "scenario_id", "instance_id", "baseline_id", "exclusion_reason", "stage", "impact", "included_exploratory_analysis", "included_confirmatory_analysis" }; }

        private static string[] BaselineRow(string instanceId, string baselineId, BaselineRunSummaryDto run)
        {
            if (run == null)
                return new[] { instanceId, baselineId, string.Empty, string.Empty, string.Empty, "False", "0", string.Empty, string.Empty, "False", "Missing run", BaselineMethodClassifier.ExactFailed, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty };

            return new[]
            {
                instanceId, baselineId, run.Heuristic, run.Scheme, run.Direction,
                ExperimentalCsv.S(run.IsExact), ExperimentalCsv.S(run.Makespan), string.Empty,
                run.ScheduledOrderText, ExperimentalCsv.S(run.Success),
                run.Status + (string.IsNullOrWhiteSpace(run.ErrorMessage) ? string.Empty : ": " + run.ErrorMessage),
                string.IsNullOrWhiteSpace(run.MethodClassification)
                    ? BaselineMethodClassifier.Classify(run, run.Success)
                    : run.MethodClassification,
                run.BranchAndBoundTimeLimitSeconds.HasValue ? ExperimentalCsv.S(run.BranchAndBoundTimeLimitSeconds.Value) : string.Empty,
                FormatNullableBool(run.BranchAndBoundTimeLimitReached),
                FormatNullableBool(run.BranchAndBoundOptimalityProven),
                run.BranchAndBoundNodesVisited.HasValue ? ExperimentalCsv.S(run.BranchAndBoundNodesVisited.Value) : string.Empty,
                run.BranchAndBoundTrace ?? string.Empty
            };
        }

        private static string[] FrmRow(string instanceId, string baselineId, FrmResultDto frm)
        {
            double sifMin = frm.SifByResourceId == null || frm.SifByResourceId.Count == 0 ? 0.0 : frm.SifByResourceId.Values.Min();
            int resourceCrit = frm.SifByResourceId == null || frm.SifByResourceId.Count == 0 ? 0 : frm.SifByResourceId.OrderBy(k => k.Value).First().Key;
            int balance0 = frm.Balance0ByResourceId == null ? 0 : frm.Balance0ByResourceId.Values.Sum();
            int final = frm.ResourceDiagnostics == null ? 0 : frm.ResourceDiagnostics.Sum(r => r.BalanceFinal);
            int min = frm.Activities == null || frm.Activities.Count == 0 ? 0 : frm.Activities.SelectMany(a => a.BalanceByResourceId.Values).DefaultIfEmpty(0).Min();
            int flex = frm.Activities == null ? 0 : frm.Activities.Count(a => a.SlackI > 0);
            int crash = frm.Activities == null ? 0 : frm.Activities.Count(a => a.DSMin < a.DurationNominal);
            int critical = frm.Activities == null ? 0 : frm.Activities.Count(a => a.IsCritical);
            int limitedCritical = frm.Activities == null ? 0 : frm.Activities.Count(a => a.IsCritical && a.WasBalanceLimited);
            int fullyFundedCritical = frm.Activities == null ? 0 : frm.Activities.Count(a => a.IsCritical && !a.WasBalanceLimited && a.DNew < a.DurationNominal);
            int generated = frm.Activities == null ? 0 : frm.Activities.Sum(a => a.BalanceGeneratedByResourceId == null ? 0 : a.BalanceGeneratedByResourceId.Values.Sum());
            int requested = frm.Activities == null ? 0 : frm.Activities.Sum(a => a.BalanceRequestedByResourceId == null ? 0 : a.BalanceRequestedByResourceId.Values.Sum());
            int consumed = frm.Activities == null ? 0 : frm.Activities.Sum(a => a.BalanceConsumedByResourceId == null ? 0 : a.BalanceConsumedByResourceId.Values.Sum());
            int unabsorbed = Math.Max(0, requested - consumed);
            return new[]
            {
                instanceId, baselineId, F(frm.SifGlobal), F(sifMin), "R" + resourceCrit,
                ExperimentalCsv.S(balance0), ExperimentalCsv.S(final), ExperimentalCsv.S(min),
                ExperimentalCsv.S(flex), ExperimentalCsv.S(crash), ExperimentalCsv.S(critical),
                ExperimentalCsv.S(limitedCritical), ExperimentalCsv.S(fullyFundedCritical),
                ExperimentalCsv.S(generated), ExperimentalCsv.S(requested), ExperimentalCsv.S(consumed),
                ExperimentalCsv.S(unabsorbed), frm.IsStructurallyRobust ? "ROBUSTO" : "FRAGIL"
            };
        }

        private static IEnumerable<string[]> FrmDetailRows(string instanceId, string baselineId, FrmResultDto frm)
        {
            if (frm == null || frm.Activities == null)
                yield break;
            foreach (var a in frm.Activities)
            {
                var resIds = a.SlackIkByResourceId.Keys.Union(a.ScoreBrutoByResourceId.Keys).Union(a.ScoreIkByResourceId.Keys).Union(a.BalanceByResourceId.Keys).Distinct().OrderBy(x => x).ToList();
                foreach (int rid in resIds)
                {
                    int slackIk = Get(a.SlackIkByResourceId, rid);
                    int scoreB = Get(a.ScoreBrutoByResourceId, rid);
                    int score = Get(a.ScoreIkByResourceId, rid);
                    int bal = Get(a.BalanceByResourceId, rid);
                    int requestedNegativeScore = Math.Min(scoreB, 0);
                    int appliedNegativeScore = Math.Min(score, 0);
                    int balanceBefore = Get(a.BalanceBeforeByResourceId, rid);
                    int balanceGenerated = Get(a.BalanceGeneratedByResourceId, rid);
                    int balanceRequested = Get(a.BalanceRequestedByResourceId, rid);
                    int balanceConsumed = Get(a.BalanceConsumedByResourceId, rid);
                    yield return new[]
                    {
                        instanceId, baselineId, ExperimentalCsv.S(a.ActivityId), ExperimentalCsv.S(rid),
                        ExperimentalCsv.S(slackIk), ExperimentalCsv.S(a.SlackI), ExperimentalCsv.S(scoreB),
                        ExperimentalCsv.S(score), ExperimentalCsv.S(bal), ExperimentalCsv.S(a.DurationNominal),
                        ExperimentalCsv.S(a.DMin), ExperimentalCsv.S(a.DMax), ExperimentalCsv.S(a.DSMin),
                        ExperimentalCsv.S(a.DSMax), ExperimentalCsv.S(a.DNew), ExperimentalCsv.S(a.IsCritical),
                        ExperimentalCsv.S(a.StructuralDurationBeforeBalance), ExperimentalCsv.S(a.StructuralDurationAfterBalance),
                        ExperimentalCsv.S(requestedNegativeScore), ExperimentalCsv.S(appliedNegativeScore),
                        ExperimentalCsv.S(balanceBefore), ExperimentalCsv.S(balanceGenerated),
                        ExperimentalCsv.S(balanceRequested), ExperimentalCsv.S(balanceConsumed), ExperimentalCsv.S(bal),
                        ExperimentalCsv.S(a.WasBalanceLimited), a.LimitingResourceId.HasValue ? ExperimentalCsv.S(a.LimitingResourceId.Value) : string.Empty,
                        a.DurationDecision ?? string.Empty
                    };
                }
            }
        }

        private string[] RiskRow(string instanceId, string baselineId, int seedInput, int replications, RiskResultDto risk, int nominalMakespan)
        {
            var primaryDelay = ComputeDelayMetrics(risk, nominalMakespan);
            double primarySd = risk.MakespanSamples == null ? 0.0 : ExperimentalStatistics.StdDev(risk.MakespanSamples.Select(v => (double)v));
            int primaryMin = risk.MakespanSamples == null || risk.MakespanSamples.Count == 0 ? 0 : risk.MakespanSamples.Min();
            int primaryMax = risk.MakespanSamples == null || risk.MakespanSamples.Count == 0 ? 0 : risk.MakespanSamples.Max();

            var pairedRisk = risk.PairedStructuralResult ?? risk.PairedUnilateralResult;
            bool hasPaired = pairedRisk != null;
            string uiMode = hasPaired ? (risk.PairedComparisonMode ?? risk.SamplingMode) : risk.SamplingMode;
            var pairedDelay = hasPaired ? ComputeDelayMetrics(pairedRisk, nominalMakespan) : new DelayMetrics();
            double pairedSd = hasPaired && pairedRisk.MakespanSamples != null ? ExperimentalStatistics.StdDev(pairedRisk.MakespanSamples.Select(v => (double)v)) : 0.0;
            int pairedMin = hasPaired && pairedRisk.MakespanSamples != null && pairedRisk.MakespanSamples.Count > 0 ? pairedRisk.MakespanSamples.Min() : 0;
            int pairedMax = hasPaired && pairedRisk.MakespanSamples != null && pairedRisk.MakespanSamples.Count > 0 ? pairedRisk.MakespanSamples.Max() : 0;

            double primaryRelP95 = SafeDivide(risk.P95, nominalMakespan);
            double primaryRelCvar = SafeDivide(risk.CVaR95, nominalMakespan);
            double primaryExcessP95 = risk.P95 - nominalMakespan;
            double primaryRelExcessP95 = SafeDivide(primaryExcessP95, nominalMakespan);
            double primaryRelDelayCvar = SafeDivide(risk.CVaR95, nominalMakespan);

            double pairedRelP95 = hasPaired ? SafeDivide(pairedRisk.P95, nominalMakespan) : 0.0;
            double pairedRelCvar = hasPaired ? SafeDivide(pairedRisk.CVaR95, nominalMakespan) : 0.0;
            double pairedExcessP95 = hasPaired ? pairedRisk.P95 - nominalMakespan : 0.0;
            double pairedRelExcessP95 = hasPaired ? SafeDivide(pairedExcessP95, nominalMakespan) : 0.0;
            double pairedRelDelayCvar = hasPaired ? SafeDivide(pairedRisk.CVaR95, nominalMakespan) : 0.0;

            return new[]
            {
                instanceId, baselineId, ExperimentalCsv.S(risk.Iterations), ExperimentalCsv.S(replications), ExperimentalCsv.S(seedInput), ExperimentalCsv.S(risk.Seed), "MEAN_OF_INDEPENDENT_REPLICATIONS", F(risk.Gamma), DistributionLabel(risk.SamplingMode),
                ExperimentalCsv.S(nominalMakespan),
                F(risk.MeanMakespan), F(risk.P50), F(risk.P95), F(risk.CVaR95), F(risk.MakespanCVaR95),
                F(primaryRelP95), F(primaryRelCvar), F(primaryExcessP95), F(risk.CVaR95),
                F(primaryRelExcessP95), F(primaryRelDelayCvar),
                F(primaryDelay.Probability), F(primaryDelay.Mean), F(primaryDelay.P95), F(primaryDelay.CVaR95), F(primaryDelay.Max),
                F(primarySd), ExperimentalCsv.S(primaryMin), ExperimentalCsv.S(primaryMax), ExperimentalCsv.S(risk.RuntimeMs),
                F(risk.BalanceRuptureProbability), F(risk.MeanBalanceGenerated), F(risk.MeanBalanceConsumed), F(risk.MeanBalanceUsage), F(risk.MeanBalanceUsageRatio), F(risk.MinObservedBalance), F(risk.CVaR95GivenBalanceRupture), F(risk.MeanPositiveWorkDemand), F(risk.MeanUnabsorbedWork), F(risk.P95UnabsorbedWork), F(risk.CVaR95UnabsorbedWork), F(risk.MeanUnabsorbedWorkRatio),
                uiMode, ExperimentalCsv.S(hasPaired), hasPaired ? pairedRisk.SamplingMode : string.Empty,
                hasPaired ? F(pairedRisk.MeanMakespan) : string.Empty,
                hasPaired ? F(pairedRisk.P50) : string.Empty,
                hasPaired ? F(pairedRisk.P95) : string.Empty,
                hasPaired ? F(pairedRisk.CVaR95) : string.Empty,
                hasPaired ? F(pairedRisk.MakespanCVaR95) : string.Empty,
                hasPaired ? F(pairedRelP95) : string.Empty,
                hasPaired ? F(pairedRelCvar) : string.Empty,
                hasPaired ? F(pairedExcessP95) : string.Empty,
                hasPaired ? F(pairedRelExcessP95) : string.Empty,
                hasPaired ? F(pairedRelDelayCvar) : string.Empty,
                hasPaired ? F(pairedDelay.Probability) : string.Empty,
                hasPaired ? F(pairedDelay.Mean) : string.Empty,
                hasPaired ? F(pairedDelay.P95) : string.Empty,
                hasPaired ? F(pairedDelay.CVaR95) : string.Empty,
                hasPaired ? F(pairedDelay.Max) : string.Empty,
                hasPaired ? F(pairedSd) : string.Empty,
                hasPaired ? ExperimentalCsv.S(pairedMin) : string.Empty,
                hasPaired ? ExperimentalCsv.S(pairedMax) : string.Empty,
                hasPaired ? F(pairedRisk.MeanMakespan - risk.MeanMakespan) : string.Empty,
                hasPaired ? F(pairedRisk.P50 - risk.P50) : string.Empty,
                hasPaired ? F(pairedRisk.P95 - risk.P95) : string.Empty,
                hasPaired ? F(pairedRisk.CVaR95 - risk.CVaR95) : string.Empty,
                hasPaired ? F(pairedRelCvar - primaryRelCvar) : string.Empty,
                hasPaired ? F(pairedDelay.Probability - primaryDelay.Probability) : string.Empty,
                hasPaired ? F(pairedDelay.Mean - primaryDelay.Mean) : string.Empty,
                hasPaired ? F(pairedDelay.P95 - primaryDelay.P95) : string.Empty,
                hasPaired ? F(pairedDelay.Max - primaryDelay.Max) : string.Empty
            };
        }

        private static Dictionary<string, CrashingScenarioResultDto> BuildCrashingScenarioLookupBySignature(CrashingResultDto result)
        {
            var map = new Dictionary<string, CrashingScenarioResultDto>(StringComparer.OrdinalIgnoreCase);
            if (result == null || result.Scenarios == null)
                return map;

            foreach (var scenario in result.Scenarios)
            {
                string signature = BuildCrashingScenarioSignature(scenario);
                if (!string.IsNullOrWhiteSpace(signature) && !map.ContainsKey(signature))
                    map[signature] = scenario;
            }

            return map;
        }

        private static string BuildCrashingScenarioSignature(CrashingScenarioResultDto scenario)
        {
            if (scenario == null || scenario.CrashedDurations == null || scenario.CrashedDurations.Count == 0)
                return string.Empty;

            return string.Join("|", scenario.CrashedDurations
                .OrderBy(kv => kv.Key)
                .Select(kv => kv.Key.ToString(CultureInfo.InvariantCulture) + "->" + kv.Value.ToString(CultureInfo.InvariantCulture))
                .ToArray());
        }

        private static string[] CrashingRow(string instanceId, string baselineId, string scenarioId, CrashingScenarioResultDto sc, CrashingScenarioResultDto paired, int baseMakespan, double baseSif, RiskResultDto baseRisk, RiskResultDto pairedBaseRisk)
        {
            double scenarioP50 = Math.Max(sc.ScenarioMakespan, sc.ScenarioP50 > 0.0 ? sc.ScenarioP50 : sc.ScenarioMakespan);
            double scenarioP95 = Math.Max(sc.ScenarioMakespan, sc.ScenarioP95);
            double scenarioCvar = sc.ScenarioCVaR95;
            double scenarioDeltaP95 = scenarioP95 - baseRisk.P95;
            double scenarioDeltaCvar = scenarioCvar - baseRisk.CVaR95;

            double p95RelOriginal = SafeDivide(baseRisk.P95, baseMakespan);
            double p95RelCrash = SafeDivide(scenarioP95, sc.ScenarioMakespan);
            double cvarRelOriginal = SafeDivide(baseRisk.CVaR95, baseMakespan);
            double cvarRelCrash = SafeDivide(scenarioCvar, sc.ScenarioMakespan);
            double p95DelayOriginal = ComputeDelayMetrics(baseRisk, baseMakespan).P95;
            double p95DelayCrash = Math.Max(0.0, sc.ScenarioP95Delay);
            double cvarDelayOriginal = ComputeDelayMetrics(baseRisk, baseMakespan).CVaR95;
            double cvarDelayCrash = Math.Max(0.0, sc.ScenarioCVaR95Delay);
            string cls = Classify(sc.DeltaMakespan, sc.Sif - baseSif, scenarioDeltaCvar, cvarRelCrash - cvarRelOriginal);

            bool hasPaired = paired != null && pairedBaseRisk != null;
            double pairedScenarioP95 = hasPaired ? Math.Max(paired.ScenarioMakespan, paired.ScenarioP95) : 0.0;
            double pairedScenarioCvar = hasPaired ? paired.ScenarioCVaR95 : 0.0;
            double pairedDeltaP95 = hasPaired ? pairedScenarioP95 - pairedBaseRisk.P95 : 0.0;
            double pairedDeltaCvar = hasPaired ? pairedScenarioCvar - pairedBaseRisk.CVaR95 : 0.0;
            double pairedP95RelOriginal = hasPaired ? SafeDivide(pairedBaseRisk.P95, baseMakespan) : 0.0;
            double pairedP95RelCrash = hasPaired ? SafeDivide(pairedScenarioP95, paired.ScenarioMakespan) : 0.0;
            double pairedCvarRelOriginal = hasPaired ? SafeDivide(pairedBaseRisk.CVaR95, baseMakespan) : 0.0;
            double pairedCvarRelCrash = hasPaired ? SafeDivide(pairedScenarioCvar, paired.ScenarioMakespan) : 0.0;
            double pairedP95DelayOriginal = hasPaired ? ComputeDelayMetrics(pairedBaseRisk, baseMakespan).P95 : 0.0;
            double pairedP95DelayCrash = hasPaired ? Math.Max(0.0, paired.ScenarioP95Delay) : 0.0;
            double pairedCvarDelayOriginal = hasPaired ? ComputeDelayMetrics(pairedBaseRisk, baseMakespan).CVaR95 : 0.0;
            double pairedCvarDelayCrash = hasPaired ? Math.Max(0.0, paired.ScenarioCVaR95Delay) : 0.0;
            string pairedCls = hasPaired ? Classify(paired.DeltaMakespan, paired.Sif - baseSif, pairedDeltaCvar, pairedCvarRelCrash - pairedCvarRelOriginal) : string.Empty;
            string pairedMode = hasPaired ? pairedBaseRisk.SamplingMode : string.Empty;

            return new[]
            {
                instanceId, baselineId, scenarioId, ExperimentalCsv.JoinInts(sc.ActivityIds), ExperimentalCsv.S(sc.ActivityCount),
                ExperimentalCsv.S(baseMakespan), ExperimentalCsv.S(sc.ScenarioMakespan), ExperimentalCsv.S(sc.DeltaMakespan),
                F(baseSif), F(sc.Sif), F(sc.Sif - baseSif),
                F(baseRisk.P95), F(scenarioP95), F(scenarioDeltaP95),
                F(baseRisk.CVaR95), F(scenarioCvar), F(scenarioDeltaCvar),
                F(p95RelOriginal), F(p95RelCrash), F(cvarRelOriginal), F(cvarRelCrash), F(cvarRelCrash - cvarRelOriginal),
                F(p95DelayOriginal), F(p95DelayCrash), F(cvarDelayOriginal), F(cvarDelayCrash), F(cvarDelayCrash - cvarDelayOriginal),
                F(sc.BaselineBalanceRuptureProbability), F(sc.ScenarioBalanceRuptureProbability), F(sc.DeltaBalanceRuptureProbability),
                F(sc.BaselineMeanBalanceUsage), F(sc.ScenarioMeanBalanceUsage), F(sc.DeltaMeanBalanceUsage),
                F(sc.BaselineMeanUnabsorbedWork), F(sc.ScenarioMeanUnabsorbedWork), F(sc.DeltaMeanUnabsorbedWork),
                F(sc.BaselineMeanUnabsorbedWorkRatio), F(sc.ScenarioMeanUnabsorbedWorkRatio), F(sc.DeltaMeanUnabsorbedWorkRatio),
                F(sc.Frri), ExperimentalCsv.S(sc.ReplicationCount), F(sc.ScenarioCVaR95StdDev), F(sc.ScenarioCVaR95CiLower), F(sc.ScenarioCVaR95CiUpper), F(sc.FrriStdDev), F(sc.FrriCiLower), F(sc.FrriCiUpper), cls,
                ExperimentalCsv.S(hasPaired), pairedMode, ExperimentalCsv.S(hasPaired && string.Equals(BuildCrashingScenarioSignature(sc), BuildCrashingScenarioSignature(paired), StringComparison.OrdinalIgnoreCase)),
                hasPaired ? F(paired.Sif) : string.Empty,
                hasPaired ? F(paired.Sif - baseSif) : string.Empty,
                hasPaired ? F(paired.Sif - sc.Sif) : string.Empty,
                hasPaired ? F(pairedBaseRisk.P95) : string.Empty,
                hasPaired ? F(pairedScenarioP95) : string.Empty,
                hasPaired ? F(pairedDeltaP95) : string.Empty,
                hasPaired ? F(pairedBaseRisk.CVaR95) : string.Empty,
                hasPaired ? F(pairedScenarioCvar) : string.Empty,
                hasPaired ? F(pairedDeltaCvar) : string.Empty,
                hasPaired ? F(pairedP95RelOriginal) : string.Empty,
                hasPaired ? F(pairedP95RelCrash) : string.Empty,
                hasPaired ? F(pairedCvarRelOriginal) : string.Empty,
                hasPaired ? F(pairedCvarRelCrash) : string.Empty,
                hasPaired ? F(pairedCvarRelCrash - pairedCvarRelOriginal) : string.Empty,
                hasPaired ? F(pairedP95DelayOriginal) : string.Empty,
                hasPaired ? F(pairedP95DelayCrash) : string.Empty,
                hasPaired ? F(pairedCvarDelayOriginal) : string.Empty,
                hasPaired ? F(pairedCvarDelayCrash) : string.Empty,
                hasPaired ? F(pairedCvarDelayCrash - pairedCvarDelayOriginal) : string.Empty,
                hasPaired ? F(paired.Frri) : string.Empty,
                pairedCls,
                hasPaired ? F(pairedScenarioP95 - scenarioP95) : string.Empty,
                hasPaired ? F(pairedScenarioCvar - scenarioCvar) : string.Empty,
                hasPaired ? F(paired.Frri - sc.Frri) : string.Empty
            };
        }

        private static string[] CrashingCandidateRow(string instanceId, string baselineId, int rank, CrashingCandidateActivityDto c)
        {
            if (c == null)
                return new[] { instanceId, baselineId, ExperimentalCsv.S(rank), string.Empty, string.Empty, "False", "0", "0", "0", "0", "0", "False", "False", "0", "0", "0", "0", string.Empty, "0", "NULL_CANDIDATE" };

            int maxCompression = Math.Max(0, c.NominalDuration - c.MinimumDuration);
            string reason = c.IsDummy ? "DUMMY"
                : !c.IsEligible ? "NOT_ELIGIBLE"
                : maxCompression <= 0 ? "NO_COMPRESSION"
                : "ELIGIBLE";

            return new[]
            {
                instanceId, baselineId, ExperimentalCsv.S(rank), ExperimentalCsv.S(c.ActivityId), c.ActivityName,
                ExperimentalCsv.S(c.Use), ExperimentalCsv.S(c.NominalDuration), ExperimentalCsv.S(c.MinimumDuration),
                ExperimentalCsv.S(c.RecommendedNewDuration), ExperimentalCsv.S(c.NewDuration), ExperimentalCsv.S(maxCompression),
                ExperimentalCsv.S(c.IsEligible), ExperimentalCsv.S(c.IsDummy), ExperimentalCsv.S(c.FrmSlackI),
                F(c.FrmCriticality), F(c.FrmSensitivity), F(c.FrmBalanceRisk), c.FrmBalanceRiskBand, F(c.FrmPriority), reason
            };
        }

        private static IEnumerable<string[]> CrashingDetailRows(string scenarioId, CrashingScenarioResultDto sc, IList<CrashingCandidateActivityDto> candidates)
        {
            if (sc == null || sc.ActivityIds == null)
                yield break;
            var byId = candidates == null ? new Dictionary<int, CrashingCandidateActivityDto>() : candidates.ToDictionary(c => c.ActivityId, c => c);
            foreach (int id in sc.ActivityIds)
            {
                CrashingCandidateActivityDto c;
                byId.TryGetValue(id, out c);
                int original = c != null ? c.NominalDuration : 0;
                int crashed = sc.CrashedDurations != null && sc.CrashedDurations.ContainsKey(id) ? sc.CrashedDurations[id] : (c != null ? c.NewDuration : 0);
                yield return new[] { scenarioId, ExperimentalCsv.S(id), ExperimentalCsv.S(original), ExperimentalCsv.S(crashed), ExperimentalCsv.S(crashed - original), ExperimentalCsv.S(c != null && c.IsEligible), ExperimentalCsv.S(c != null ? c.MinimumDuration : 0), ExperimentalCsv.S(c != null ? c.RecommendedNewDuration : 0), c != null && c.IsEligible ? "FRM" : "NOT_ELIGIBLE" };
            }
        }

        private static string[] IntegratedRow(ScenarioRecord s)
        {
            return new[]
            {
                s.InstanceId, s.Family, s.ScenarioId, s.Type, s.BaselineId, s.Heuristic, s.Scheme, s.Direction,
                ExperimentalCsv.S(s.Makespan), F(s.SifGlobal), F(s.P50), F(s.P95), F(s.Cvar95),
                F(s.P95Relative), F(s.Cvar95Relative), F(s.ExcessP95), F(s.ExcessCvar95),
                F(s.DelayProbability), F(s.MeanDelay), F(s.P95Delay), F(s.Cvar95Delay), F(s.MaxDelay),
                F(s.BalanceRuptureProbability), F(s.MeanBalanceUsage), F(s.MeanUnabsorbedWork), F(s.MeanUnabsorbedWorkRatio),
                F(s.DeltaBalanceRupture), F(s.DeltaMeanBalanceUsage), F(s.DeltaMeanUnabsorbedWork), F(s.DeltaMeanUnabsorbedWorkRatio),
                F(s.Frri), ExperimentalCsv.S(s.HasPaired), s.PairedMode, ExperimentalCsv.S(s.PairedMakespan),
                F(s.PairedSifGlobal), F(s.PairedP95), F(s.PairedCvar95), F(s.PairedP95Relative), F(s.PairedCvar95Relative),
                F(s.PairedExcessP95), F(s.PairedDelayProbability), F(s.PairedMeanDelay), F(s.PairedP95Delay),
                F(s.PairedCvar95Delay), F(s.PairedMaxDelay), F(s.PairedFrri), F(s.DeltaModesSif), F(s.DeltaModesP95), F(s.DeltaModesCvar95), F(s.DeltaModesFrri),
                ExperimentalCsv.S(s.Dominated), s.DominatedBy, ExperimentalCsv.S(s.PairedDominated), s.PairedDominatedBy,
                s.Classification, ExperimentalCsv.S(s.Type == "baseline"), s.Observation, s.PairedObservation,
                s.MethodClassification, FormatNullableBool(s.BranchAndBoundTimeLimitReached), FormatNullableBool(s.BranchAndBoundOptimalityProven)
            };
        }

        private static ScenarioRecord BuildScenarioRecord(string instanceId, string family, string scenarioId, string type, string baselineId, BaselineRunSummaryDto run, FrmResultDto frm, RiskResultDto risk, double frri, string classification)
        {
            int makespan = run.Makespan;
            var delay = ComputeDelayMetrics(risk, makespan);
            var record = new ScenarioRecord
            {
                InstanceId = instanceId, Family = family, ScenarioId = scenarioId, Type = type, BaselineId = baselineId,
                Heuristic = run.Heuristic, Scheme = run.Scheme, Direction = run.Direction,
                MethodClassification = string.IsNullOrWhiteSpace(run.MethodClassification)
                    ? BaselineMethodClassifier.Classify(run, run.Success)
                    : run.MethodClassification,
                BranchAndBoundTimeLimitReached = run.BranchAndBoundTimeLimitReached,
                BranchAndBoundOptimalityProven = run.BranchAndBoundOptimalityProven,
                Makespan = makespan,
                SifGlobal = frm.SifGlobal, P50 = risk.P50, P95 = risk.P95, Cvar95 = risk.CVaR95,
                P95Relative = SafeDivide(risk.P95, makespan), Cvar95Relative = SafeDivide(risk.CVaR95, makespan),
                ExcessP95 = risk.P95 - makespan, ExcessCvar95 = risk.CVaR95,
                DelayProbability = delay.Probability, MeanDelay = delay.Mean, P95Delay = delay.P95, Cvar95Delay = delay.CVaR95, MaxDelay = delay.Max,
                BalanceRuptureProbability = risk.BalanceRuptureProbability,
                MeanBalanceUsage = risk.MeanBalanceUsage,
                MeanUnabsorbedWork = risk.MeanUnabsorbedWork,
                MeanUnabsorbedWorkRatio = risk.MeanUnabsorbedWorkRatio,
                DeltaBalanceRupture = 0.0,
                DeltaMeanBalanceUsage = 0.0,
                DeltaMeanUnabsorbedWork = 0.0,
                DeltaMeanUnabsorbedWorkRatio = 0.0,
                Frri = frri, Classification = classification
            };

            var pairedRisk = risk.PairedStructuralResult ?? risk.PairedUnilateralResult;
            if (pairedRisk != null)
            {
                var pairedDelay = ComputeDelayMetrics(pairedRisk, makespan);
                record.HasPaired = true;
                record.PairedMode = pairedRisk.SamplingMode;
                record.PairedMakespan = makespan;
                record.PairedSifGlobal = frm.SifGlobal;
                record.PairedP95 = pairedRisk.P95;
                record.PairedCvar95 = pairedRisk.CVaR95;
                record.PairedP95Relative = SafeDivide(pairedRisk.P95, makespan);
                record.PairedCvar95Relative = SafeDivide(pairedRisk.CVaR95, makespan);
                record.PairedExcessP95 = pairedRisk.P95 - makespan;
                record.PairedExcessCvar95 = pairedRisk.CVaR95;
                record.PairedDelayProbability = pairedDelay.Probability;
                record.PairedMeanDelay = pairedDelay.Mean;
                record.PairedP95Delay = pairedDelay.P95;
                record.PairedCvar95Delay = pairedDelay.CVaR95;
                record.PairedMaxDelay = pairedDelay.Max;
                record.PairedFrri = frri;
                record.DeltaModesSif = 0.0;
                record.DeltaModesP95 = pairedRisk.P95 - risk.P95;
                record.DeltaModesCvar95 = pairedRisk.CVaR95 - risk.CVaR95;
                record.DeltaModesFrri = 0.0;
            }

            return record;
        }

        private static ScenarioRecord BuildCrashingScenarioRecord(string instanceId, string family, string scenarioId, string baselineId, BaselineRunSummaryDto run, FrmResultDto frm, RiskResultDto risk, CrashingScenarioResultDto sc, CrashingScenarioResultDto paired, RiskResultDto pairedRisk)
        {
            double scenarioP50 = Math.Max(sc.ScenarioMakespan, sc.ScenarioP50 > 0.0 ? sc.ScenarioP50 : sc.ScenarioMakespan);
            double scenarioP95 = Math.Max(sc.ScenarioMakespan, sc.ScenarioP95);
            double scenarioCvar = sc.ScenarioCVaR95;
            double scenarioDeltaCvar = scenarioCvar - risk.CVaR95;
            double cvarRelOriginal = SafeDivide(risk.CVaR95, run.Makespan);
            double cvarRelCrash = SafeDivide(scenarioCvar, sc.ScenarioMakespan);
            double p95Delay = Math.Max(0.0, sc.ScenarioP95Delay);
            double cvarDelay = Math.Max(0.0, sc.ScenarioCVaR95Delay);
            string cls = Classify(sc.DeltaMakespan, sc.Sif - frm.SifGlobal, scenarioDeltaCvar, cvarRelCrash - cvarRelOriginal);
            var record = new ScenarioRecord
            {
                InstanceId = instanceId, Family = family, ScenarioId = scenarioId, Type = "crashing", BaselineId = baselineId,
                Heuristic = run.Heuristic, Scheme = run.Scheme, Direction = run.Direction,
                MethodClassification = string.IsNullOrWhiteSpace(run.MethodClassification)
                    ? BaselineMethodClassifier.Classify(run, run.Success)
                    : run.MethodClassification,
                BranchAndBoundTimeLimitReached = run.BranchAndBoundTimeLimitReached,
                BranchAndBoundOptimalityProven = run.BranchAndBoundOptimalityProven,
                Makespan = sc.ScenarioMakespan,
                SifGlobal = sc.Sif, P50 = scenarioP50, P95 = scenarioP95, Cvar95 = scenarioCvar,
                P95Relative = SafeDivide(scenarioP95, sc.ScenarioMakespan), Cvar95Relative = cvarRelCrash,
                ExcessP95 = scenarioP95 - sc.ScenarioMakespan, ExcessCvar95 = scenarioCvar,
                DelayProbability = sc.ScenarioDelayProbability,
                MeanDelay = sc.ScenarioMeanDelay,
                P95Delay = p95Delay,
                Cvar95Delay = cvarDelay,
                MaxDelay = Math.Max(sc.ScenarioMaxDelay, cvarDelay),
                BalanceRuptureProbability = sc.ScenarioBalanceRuptureProbability,
                MeanBalanceUsage = sc.ScenarioMeanBalanceUsage,
                MeanUnabsorbedWork = sc.ScenarioMeanUnabsorbedWork,
                MeanUnabsorbedWorkRatio = sc.ScenarioMeanUnabsorbedWorkRatio,
                DeltaBalanceRupture = sc.DeltaBalanceRuptureProbability,
                DeltaMeanBalanceUsage = sc.DeltaMeanBalanceUsage,
                DeltaMeanUnabsorbedWork = sc.DeltaMeanUnabsorbedWork,
                DeltaMeanUnabsorbedWorkRatio = sc.DeltaMeanUnabsorbedWorkRatio,
                Frri = sc.Frri, Classification = cls
            };

            if (paired != null && pairedRisk != null)
            {
                double pairedP95 = Math.Max(paired.ScenarioMakespan, paired.ScenarioP95);
                double pairedCvar = paired.ScenarioCVaR95;
                record.HasPaired = true;
                record.PairedMode = pairedRisk.SamplingMode;
                record.PairedMakespan = paired.ScenarioMakespan;
                record.PairedSifGlobal = paired.Sif;
                record.PairedP95 = pairedP95;
                record.PairedCvar95 = pairedCvar;
                record.PairedP95Relative = SafeDivide(pairedP95, paired.ScenarioMakespan);
                record.PairedCvar95Relative = SafeDivide(pairedCvar, paired.ScenarioMakespan);
                record.PairedExcessP95 = pairedP95 - paired.ScenarioMakespan;
                record.PairedExcessCvar95 = pairedCvar;
                record.PairedDelayProbability = paired.ScenarioDelayProbability;
                record.PairedMeanDelay = paired.ScenarioMeanDelay;
                record.PairedP95Delay = Math.Max(0.0, paired.ScenarioP95Delay);
                record.PairedCvar95Delay = Math.Max(0.0, paired.ScenarioCVaR95Delay);
                record.PairedMaxDelay = Math.Max(paired.ScenarioMaxDelay, record.PairedCvar95Delay);
                record.PairedFrri = paired.Frri;
                record.DeltaModesSif = paired.Sif - sc.Sif;
                record.DeltaModesP95 = pairedP95 - scenarioP95;
                record.DeltaModesCvar95 = pairedCvar - scenarioCvar;
                record.DeltaModesFrri = paired.Frri - sc.Frri;
            }

            return record;
        }

        private static string Classify(int deltaMakespan, double deltaSif, double deltaCvar, double deltaCvarRel)
        {
            bool reducesTime = deltaMakespan < 0;
            bool reducesRisk = deltaCvarRel < 0 || deltaCvar < 0;
            bool preservesSif = deltaSif >= 0;
            if (reducesTime && reducesRisk && preservesSif) return "EFFICIENT";
            if (reducesTime && reducesRisk && !preservesSif) return "EFFICIENT_COM_TRADEOFF";
            if (reducesTime && !reducesRisk) return "FAST_BUT_HIGH_RISK";
            if (!reducesTime && reducesRisk && preservesSif) return "ROBUST_BUT_SLOW";
            return "AMBIGUOUS";
        }

        private ProjectValidation ValidateProject(ProjectDataDto project)
        {
            var validation = new ProjectValidation { IsValid = true, Message = "Valid instance." };
            if (project == null)
                return ProjectValidation.Invalid("Null project.");
            if (project.Activities == null || project.Activities.Count == 0)
                return ProjectValidation.Invalid("Nenhuma activity encontrada.");
            if (project.Resources == null || project.Resources.Count == 0)
                return ProjectValidation.Invalid("Nenhum resource encontrado.");
            validation.InvalidDurations = project.Activities.Count(a => a.DurationDays < 0);
            validation.InvalidDemands = project.Activities.Sum(a => a.Assignments == null ? 0 : a.Assignments.Count(x => x.Units < 0));
            validation.DemandsAboveCapacity = CountDemandsAboveCapacity(project);
            validation.HasCycle = HasCycle(project);
            validation.DisconnectedActivities = project.Activities.Count(a => (a.PredecessorIds == null || a.PredecessorIds.Count == 0) && (a.SuccessorIds == null || a.SuccessorIds.Count == 0));
            validation.IsValid = validation.InvalidDurations == 0 && validation.InvalidDemands == 0 && validation.DemandsAboveCapacity == 0 && !validation.HasCycle;
            validation.Message = validation.IsValid ? "Valid instance." : "The instance contains inconsistencies.";
            return validation;
        }

        private BaselineValidation ValidateBaseline(ProjectDataDto project, BaselineResultDto baseline)
        {
            var v = new BaselineValidation { PrecedenceFeasible = true, ResourceFeasible = true, MakespanConsistent = true };
            if (project == null || baseline == null || baseline.Activities == null)
            {
                v.PrecedenceFeasible = false; v.ResourceFeasible = false; v.MakespanConsistent = false; v.MissingActivities = 9999; return v;
            }
            var scheduled = baseline.Activities.ToDictionary(a => a.ActivityId, a => a);
            v.MissingActivities = project.Activities.Count(a => !scheduled.ContainsKey(a.Id));
            foreach (var a in project.Activities)
            {
                if (!scheduled.ContainsKey(a.Id)) continue;
                foreach (int pred in a.PredecessorIds ?? new List<int>())
                {
                    if (scheduled.ContainsKey(pred) && scheduled[pred].Finish > scheduled[a.Id].Start)
                    {
                        v.PrecedenceViolations++; v.PrecedenceFeasible = false;
                    }
                }
            }
            int horizon = Math.Max(baseline.Makespan, baseline.Activities.Select(a => a.Finish).DefaultIfEmpty(0).Max()) + 1;
            var capacityById = project.Resources.ToDictionary(r => r.Id, r => (int)r.Capacity);
            for (int t = 0; t <= horizon; t++)
            {
                var use = new Dictionary<int, int>();
                foreach (var act in project.Activities)
                {
                    ScheduledActivityDto sa;
                    if (!scheduled.TryGetValue(act.Id, out sa)) continue;
                    if (t < sa.Start || t >= sa.Finish) continue;
                    foreach (var ass in act.Assignments ?? new List<ResourceAssignmentDto>())
                    {
                        int old; use.TryGetValue(ass.ResourceId, out old); use[ass.ResourceId] = old + (int)ass.Units;
                    }
                }
                foreach (var pair in use)
                {
                    int cap; capacityById.TryGetValue(pair.Key, out cap);
                    if (pair.Value > cap)
                    {
                        v.ResourceViolations++; v.ResourceFeasible = false; v.MaxResourceViolation = Math.Max(v.MaxResourceViolation, pair.Value - cap);
                    }
                }
            }
            int maxFinish = baseline.Activities.Select(a => a.Finish).DefaultIfEmpty(0).Max();
            v.MakespanConsistent = maxFinish == baseline.Makespan;
            return v;
        }

        private void WriteInstanceCsv(string dir, string instanceId, string family, string filePath, ProjectDataDto project)
        {
            int precedences = project.Activities.Sum(a => a.SuccessorIds == null ? 0 : a.SuccessorIds.Count);
            double density = project.Activities.Count <= 1 ? 0 : precedences / (double)(project.Activities.Count * (project.Activities.Count - 1));
            string capacities = string.Join(";", project.Resources.Select(r => r.Name + "=" + r.Capacity.ToString(CultureInfo.InvariantCulture)).ToArray());
            var rows = new[] { new[] { instanceId, Path.GetFileName(filePath), family, ExperimentalCsv.S(project.Activities.Count), ExperimentalCsv.S(project.Resources.Count), ExperimentalCsv.S(precedences), F(density), ExperimentalCsv.S(project.Activities.Sum(a => a.DurationDays)), F(project.Activities.Count == 0 ? 0 : project.Activities.Average(a => a.DurationDays)), ExperimentalCsv.S(project.Activities.Sum(a => a.DurationDays)), capacities, "OK" } };
            ExperimentalCsv.WriteRows(Path.Combine(dir, "01_instancia.csv"), new[] { "instance_id", "source_file", "instance_family", "activity_count", "resource_count", "precedence_count", "densidade_rede", "soma_durations", "media_durations", "horizonte_estimado", "capacidades_resources", "status_import" }, rows);
        }

        private FrmOptionsDto BuildFrmOptions(ExperimentalStudyConfig config) { return new FrmOptionsDto { Enabled = true, Mode = "NORMAL", PositiveFlexibilityPercent = config.PositiveFlexibilityPercent, NegativeFlexibilityPercent = config.NegativeFlexibilityPercent }; }
        private RiskOptionsDto BuildRiskOptions(ExperimentalStudyConfig config, int seed)
        {
            bool isPaired = config.RunPairedUnilateralStructural
                || string.Equals(config.SamplingModeUi, "UNILATERAL_STRUCTURAL", StringComparison.OrdinalIgnoreCase);
            string motorMode = isPaired ? "DELAY_UNILATERAL" : NormalizeSamplingMode(config.SamplingMode);
            return new RiskOptionsDto
            {
                Enabled = true,
                ScenarioCount = config.Iterations,
                Seed = seed,
                Gamma = config.Gamma,
                HistogramBinCount = config.HistogramBins,
                SamplingMode = motorMode,
                RunPairedUnilateralStructural = isPaired,
                UseCommonRandomNumbers = true
            };
        }
        private CrashingOptionsDto BuildCrashingOptions(ExperimentalStudyConfig config)
        {


            int configuredCandidateCap = config.MaxCandidateActivities > 0 ? config.MaxCandidateActivities : 20;
            int activeCandidateCap = Math.Max(config.MaxCombinationSize, configuredCandidateCap);
            return new CrashingOptionsDto
            {
                Enabled = true,
                UseFrmGuidance = true,
                RecalculateRiskAfterCrash = true,
                MaxCombinationSize = config.MaxCombinationSize,
                MaxScenarioCount = config.MaxCrashingScenarios,
                MaxActivitiesToCrash = activeCandidateCap,
                BranchAndBoundTimeLimitSeconds = config.BranchAndBoundTimeLimitSeconds
            };
        }

        private void WriteExperimentConfig(ExperimentalStudyConfig config)
        {
            string dir = Path.Combine(config.OutputDirectory, "00_config");
            Directory.CreateDirectory(dir);
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"experiment_id\": \"" + EscapeJson(config.ExperimentId) + "\",");
            sb.AppendLine("  \"start_datetime\": \"" + DateTime.Now.ToString("s") + "\",");
            sb.AppendLine("  \"iterations_monte_carlo\": " + config.Iterations + ",");
            sb.AppendLine("  \"replications\": " + config.Replications + ",");
            sb.AppendLine("  \"seed_global\": " + config.Seed + ",");
            sb.AppendLine("  \"seed_policy\": \"DERIVED_BY_BASELINE\",");
            sb.AppendLine("  \"gamma\": " + config.Gamma.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("  \"gamma_set\": \"" + EscapeJson(config.SensitivityGammas == null ? string.Empty : string.Join(";", config.SensitivityGammas.Select(g => F(g)).ToArray())) + "\",");
            sb.AppendLine("  \"sampling_mode_ui\": \"" + EscapeJson(config.SamplingModeUi ?? NormalizeSamplingMode(config.SamplingMode)) + "\",");
            sb.AppendLine("  \"sampling_mode\": \"" + EscapeJson(NormalizeSamplingMode(config.SamplingMode)) + "\",");
            sb.AppendLine("  \"paired_enabled\": " + (config.RunPairedUnilateralStructural ? "true" : "false") + ",");
            sb.AppendLine("  \"use_common_random_numbers\": true,");
            sb.AppendLine("  \"max_activities_per_combo\": " + config.MaxCombinationSize + ",");
            sb.AppendLine("  \"max_candidate_activities\": " + config.MaxCandidateActivities + ",");
            sb.AppendLine("  \"max_crashing_scenarios\": " + config.MaxCrashingScenarios + ",");
            sb.AppendLine("  \"positive_flexibility\": " + config.PositiveFlexibilityPercent.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("  \"negative_flexibility\": " + config.NegativeFlexibilityPercent.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("  \"rf_rs_stratified_selection\": " + (config.UseRfRsStratifiedSelection ? "true" : "false") + ",");
            sb.AppendLine("  \"rf_rs_target_instances\": " + config.RfRsTargetInstanceCount + ",");
            sb.AppendLine("  \"framework_source\": \"chapters_1_2_3_and_formbaseline\",");
            sb.AppendLine("  \"main_protocol\": \"FRM_WORKCONTENT_BILATERAL\",");
            sb.AppendLine("  \"modified_dh_bb_included\": true");
            sb.AppendLine("}");
            File.WriteAllText(Path.Combine(dir, "experiment_config.json"), sb.ToString(), Encoding.UTF8);
            ExperimentalCsv.WriteRows(Path.Combine(dir, "experiment_manifest.csv"), new[] { "field", "value" }, new[]
            {
                new[] { "experiment_id", config.ExperimentId },
                new[] { "start_datetime", DateTime.Now.ToString("s") },
                new[] { "selected_files", ExperimentalCsv.S(config.InputFiles.Count) },
                new[] { "output_folder", config.OutputDirectory },
                new[] { "iterations", ExperimentalCsv.S(config.Iterations) },
                new[] { "replications", ExperimentalCsv.S(config.Replications) },
                new[] { "seed_global", ExperimentalCsv.S(config.Seed) },
                new[] { "seed_policy", "DERIVED_BY_BASELINE" },
                new[] { "gamma", F(config.Gamma) },
                new[] { "gamma_set", config.SensitivityGammas == null ? string.Empty : string.Join(";", config.SensitivityGammas.Select(g => F(g)).ToArray()) },
                new[] { "sampling_mode_ui", config.SamplingModeUi ?? NormalizeSamplingMode(config.SamplingMode) },
                new[] { "sampling_mode", NormalizeSamplingMode(config.SamplingMode) },
                new[] { "paired_enabled", ExperimentalCsv.S(config.RunPairedUnilateralStructural) },
                new[] { "use_common_random_numbers", "true" },
                new[] { "max_activities_per_combo", ExperimentalCsv.S(config.MaxCombinationSize) },
                new[] { "max_candidate_activities", ExperimentalCsv.S(config.MaxCandidateActivities) },
                new[] { "max_crashing_scenarios", ExperimentalCsv.S(config.MaxCrashingScenarios) },
                new[] { "rf_rs_stratified_selection", ExperimentalCsv.S(config.UseRfRsStratifiedSelection) },
                new[] { "rf_rs_target_instances", ExperimentalCsv.S(config.RfRsTargetInstanceCount) }
            });
            ExperimentalCsv.WriteRows(Path.Combine(dir, "analysis_plan.csv"), new[] { "analysis", "tipo", "hypothesis", "x_variable", "y_variable", "criterion", "test" }, new[]
            {
                new[] { "H1", "Confirmatory", "Higher SIF reduces relative CVaR95", "SIF_global", "relative_CVaR95", "Spearman <= -0.60", "Spearman rank correlation" },
                new[] { "H1b", "Confirmatory", "H1 holds across RS/RF strata", "SIF_global", "relative_CVaR95_by_stratum", "Spearman <= -0.30 in all strata", "Stratified Spearman (see correlacoes_rfrs_estratificadas.csv)" },
                new[] { "H2", "Exploratory", "Crashing scenarios reconfigure time, FRM structure, absorption and delay risk", "delta_Cmax;delta_SIF;delta_unabsorbed_work", "FRRI;delta_delay_CVaR95", "multicriteria interpretation", "Integrated S0 x Sj comparison; policy test is optional" },
                new[] { "H3", "Exploratory", "FRRI captures risk beyond delta_Cmax alone", "delta_Cmax", "FRRI", "Scatter pattern differs from identity", "Visual + correlation (see chart_scenario_frri.csv)" },
                new[] { "E1", "Exploratory", "Schedule-risk relationship", "Makespan", "relative_CVaR95", "descriptive", "Pearson + Spearman" },
                new[] { "E2", "Exploratory", "Modified DH B&B as exact reference versus heuristic baselines", "method_family", "Makespan;SIF;Delay_CVaR95;relative_CVaR95;unabsorbed_work", "descriptive", "See modified_dh_bb_vs_heuristics_*.csv" }
            });
            ExperimentalCsv.WriteRows(Path.Combine(dir, "software_versions.csv"), new[] { "software_version", "batch_module_version", "rcp_importer_version", "frm_module_version", "risk_module_version", "crashing_module_version", "execution_date" }, new[] { new[] { "RCPSP-FRM", "1.0.0", "RcpProjectDataImporter 1.0.0", "CpuFrmCalculator", "CpuRiskAnalyzer", "CpuCrashingAnalyzer", DateTime.Now.ToString("s") } });
        }

        private void PrepareDirectories(string root)
        {
            foreach (string dir in new[] { "00_config", "01_raw", "02_processed", "03_consolidated", "04_graph_data", "05_logs", "06_report_tables" })
                Directory.CreateDirectory(Path.Combine(root, dir));
        }

        private void AddError(string instanceId, string baselineId, string scenarioId, string stage, string errorType, string message, string action)
        {
            _errorRows.Add(new[] { DateTime.Now.ToString("s"), instanceId, baselineId, scenarioId, stage, errorType, message, action });
        }
        private void AddExcluded(string instanceId, string baselineId, string scenarioId, string reason, string stage, string impact, bool exploratory, bool confirmatory)
        {
            _excludedRows.Add(new[] { scenarioId, instanceId, baselineId, reason, stage, impact, ExperimentalCsv.S(exploratory), ExperimentalCsv.S(confirmatory) });
        }


        private static string NormalizeSamplingMode(string value)
        {
            if (string.Equals(value, "FRM_WORKCONTENT_BILATERAL", StringComparison.OrdinalIgnoreCase))
                return "FRM_WORKCONTENT_BILATERAL";
            if (string.Equals(value, "DELAY_UNILATERAL", StringComparison.OrdinalIgnoreCase))
                return "DELAY_UNILATERAL";
            if (string.Equals(value, "DELAY_STRUCTURAL", StringComparison.OrdinalIgnoreCase))
                return "DELAY_STRUCTURAL";
            return "FRM_WORKCONTENT_BILATERAL";
        }

        private static string DistributionLabel(string samplingMode)
        {
            if (string.Equals(samplingMode, "FRM_WORKCONTENT_BILATERAL", StringComparison.OrdinalIgnoreCase))
                return "frm_workcontent_bilateral_balance";
            if (string.Equals(samplingMode, "DELAY_UNILATERAL", StringComparison.OrdinalIgnoreCase))
                return "structural_unilateral_delay";
            if (string.Equals(samplingMode, "DELAY_STRUCTURAL", StringComparison.OrdinalIgnoreCase))
                return "structural_unilateral_delay_dsmax";
            return "frm_workcontent_bilateral_balance";
        }

        private static void Report(Action<string, int> progress, string message, int percent) { if (progress != null) progress(message, Math.Max(0, Math.Min(100, percent))); }
        private static string F(double value) { return ExperimentalStatistics.Format(value); }
        private static string FormatNullableBool(bool? value) { return value.HasValue ? ExperimentalCsv.S(value.Value) : string.Empty; }
        private static double SafeDivide(double a, double b) { return Math.Abs(b) < 1e-9 ? 0.0 : a / b; }
        private static string Percent(double a, double b) { return F(100.0 * SafeDivide(a, b)); }
        private static int Get(Dictionary<int, int> dict, int key) { int value; return dict != null && dict.TryGetValue(key, out value) ? value : 0; }
        private static double Parse(string value) { double v; return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out v) ? v : 0.0; }
        private static int DeriveSeed(int seed, string text, int offset) { unchecked { int h = seed; foreach (char c in text ?? string.Empty) h = h * 31 + c; return Math.Abs(h + offset * 10007); } }
        private static string GetInstanceId(string path) { return Sanitize(Path.GetFileNameWithoutExtension(path)); }
        private static string Sanitize(string value) { if (string.IsNullOrWhiteSpace(value)) return "NA"; var chars = value.Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray(); return new string(chars); }
        private sealed class UniqueBaselineRun
        {
            public string UniqueBaselineId { get; set; }
            public string SignatureHash { get; set; }
            public BaselineRunSummaryDto CanonicalRun { get; set; }
            public List<BaselineRunSummaryDto> Generators { get; set; }
        }

        private List<UniqueBaselineRun> DeduplicateBaselineRuns(string instanceId, IList<BaselineRunSummaryDto> runs)
        {
            var validRuns = (runs ?? new List<BaselineRunSummaryDto>())
                .Where(r => r != null && r.Success && r.BaselineResult != null)
                .ToList();
            var invalidRuns = (runs ?? new List<BaselineRunSummaryDto>())
                .Where(r => r == null || !r.Success || r.BaselineResult == null)
                .ToList();

            var groups = validRuns.GroupBy(r => BuildScheduleSignature(r.BaselineResult), StringComparer.Ordinal).ToList();
            var result = new List<UniqueBaselineRun>();
            int uniqueIndex = 0;
            foreach (var group in groups)
            {
                uniqueIndex++;
                var generators = group.ToList();
                foreach (var generator in generators)
                    generator.MethodClassification = BaselineMethodClassifier.Classify(generator, true);

                var canonical = generators
                    .OrderByDescending(r => string.Equals(r.MethodClassification, BaselineMethodClassifier.ExactReference, StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(r => r.IsExact)
                    .ThenBy(r => r.Heuristic ?? string.Empty, StringComparer.Ordinal)
                    .First();
                string uniqueId = Sanitize(instanceId + "_UB" + uniqueIndex.ToString("D3", CultureInfo.InvariantCulture));
                string signatureHash = ComputeStringSha256(group.Key);
                string allGenerators = string.Join(" | ", generators.Select(DescribeBaselineGenerator).ToArray());

                for (int i = 0; i < generators.Count; i++)
                {
                    var generator = generators[i];
                    _baselineDeduplicationRows.Add(new[]
                    {
                        instanceId, uniqueId, signatureHash, ExperimentalCsv.S(i + 1), generator.Heuristic,
                        generator.Scheme, generator.Direction, ExperimentalCsv.S(generator.IsExact),
                        generator.MethodClassification, ExperimentalCsv.S(generator.Makespan),
                        ExperimentalCsv.S(object.ReferenceEquals(generator, canonical)), ExperimentalCsv.S(generators.Count),
                        allGenerators, "Equivalent activity start/finish/duration vector and execution sequence"
                    });
                }

                result.Add(new UniqueBaselineRun { UniqueBaselineId = uniqueId, SignatureHash = signatureHash, CanonicalRun = canonical, Generators = generators });
            }

            foreach (var invalid in invalidRuns)
            {
                string fallbackId = BuildBaselineId(instanceId, invalid);
                if (invalid != null)
                    invalid.MethodClassification = BaselineMethodClassifier.Classify(invalid, false);

                _baselineDeduplicationRows.Add(new[]
                {
                    instanceId, fallbackId, string.Empty, "1", invalid == null ? string.Empty : invalid.Heuristic,
                    invalid == null ? string.Empty : invalid.Scheme, invalid == null ? string.Empty : invalid.Direction,
                    ExperimentalCsv.S(invalid != null && invalid.IsExact), invalid == null ? BaselineMethodClassifier.ExactFailed : invalid.MethodClassification,
                    invalid == null ? "0" : ExperimentalCsv.S(invalid.Makespan), "False", "1",
                    invalid == null ? "Missing run" : DescribeBaselineGenerator(invalid), "Invalid run retained for classification audit but excluded from FRM/risk/crashing"
                });


                result.Add(new UniqueBaselineRun
                {
                    UniqueBaselineId = fallbackId,
                    SignatureHash = string.Empty,
                    CanonicalRun = invalid,
                    Generators = invalid == null
                        ? new List<BaselineRunSummaryDto>()
                        : new List<BaselineRunSummaryDto> { invalid }
                });
            }
            return result;
        }

        private static string BuildScheduleSignature(BaselineResultDto baseline)
        {
            if (baseline == null) return "NULL";
            var ids = (baseline.StartTimesByActivity ?? new Dictionary<int, int>()).Keys
                .Union((baseline.FinishTimesByActivity ?? new Dictionary<int, int>()).Keys)
                .Union((baseline.Activities ?? new List<ScheduledActivityDto>()).Select(a => a.ActivityId))
                .Distinct().OrderBy(id => id).ToList();
            var activityMap = (baseline.Activities ?? new List<ScheduledActivityDto>()).GroupBy(a => a.ActivityId).ToDictionary(g => g.Key, g => g.First());
            var parts = new List<string>();
            foreach (int id in ids)
            {
                int start = Get(baseline.StartTimesByActivity, id);
                int finish = Get(baseline.FinishTimesByActivity, id);
                ScheduledActivityDto activity;
                int duration = activityMap.TryGetValue(id, out activity) ? activity.Duration : Math.Max(0, finish - start);
                parts.Add(id.ToString(CultureInfo.InvariantCulture) + ":" + start.ToString(CultureInfo.InvariantCulture) + ":" + finish.ToString(CultureInfo.InvariantCulture) + ":" + duration.ToString(CultureInfo.InvariantCulture));
            }
            var sequence = baseline.Sequence != null && baseline.Sequence.Count > 0 ? baseline.Sequence : baseline.ScheduledOrder;
            return string.Join(";", parts.ToArray()) + "|SEQ=" + string.Join(",", (sequence ?? new List<int>()).Select(x => x.ToString(CultureInfo.InvariantCulture)).ToArray());
        }

        private static string DescribeBaselineGenerator(BaselineRunSummaryDto run)
        {
            if (run == null) return "NULL";
            return (run.Heuristic ?? string.Empty) + "/" + (run.Scheme ?? string.Empty) + "/" + (run.Direction ?? string.Empty) + "/" + (run.IsExact ? "EXACT" : "HEURISTIC") + "/" + (run.MethodClassification ?? string.Empty);
        }

        private static string ComputeStringSha256(string value)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty))).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string BuildBaselineId(string instanceId, BaselineRunSummaryDto run)
        {
            if (run == null) return Sanitize(instanceId + "_INVALID");
            return Sanitize(instanceId + "_" + run.Heuristic + "_" + run.Scheme + "_" + run.Direction + (run.IsExact ? "_EXACT" : string.Empty));
        }
        private static string DetectFamily(string instanceId) { string s = instanceId.ToLowerInvariant(); if (s.StartsWith("j30")) return "j30"; if (s.StartsWith("j60")) return "j60"; if (s.StartsWith("j90")) return "j90"; if (s.StartsWith("j120")) return "j120"; return "other"; }
        private static string EscapeJson(string value) { return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\""); }

        private static string ComputeSha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private int CountDemandsAboveCapacity(ProjectDataDto project)
        {
            var caps = project.Resources.ToDictionary(r => r.Id, r => r.Capacity);
            int count = 0;
            foreach (var a in project.Activities)
                foreach (var ass in a.Assignments ?? new List<ResourceAssignmentDto>())
                    if (caps.ContainsKey(ass.ResourceId) && ass.Units > caps[ass.ResourceId]) count++;
            return count;
        }

        private static bool HasCycle(ProjectDataDto project)
        {
            var ids = project.Activities.Select(a => a.Id).ToList();
            var indegree = ids.ToDictionary(id => id, id => 0);
            foreach (var a in project.Activities)
                foreach (int s in a.SuccessorIds ?? new List<int>())
                    if (indegree.ContainsKey(s)) indegree[s]++;
            var queue = new Queue<int>(indegree.Where(k => k.Value == 0).Select(k => k.Key));
            int visited = 0;
            var byId = project.Activities.ToDictionary(a => a.Id, a => a);
            while (queue.Count > 0)
            {
                int id = queue.Dequeue(); visited++;
                ActivityDto a; if (!byId.TryGetValue(id, out a)) continue;
                foreach (int s in a.SuccessorIds ?? new List<int>())
                    if (indegree.ContainsKey(s) && --indegree[s] == 0) queue.Enqueue(s);
            }
            return visited != ids.Count;
        }

        private string BestScenario(Func<ScenarioRecord, double> selector, bool ascending)
        {
            if (_integrated.Count == 0) return string.Empty;
            var ordered = ascending ? _integrated.OrderBy(selector) : _integrated.OrderByDescending(selector);
            return ordered.First().ScenarioId;
        }

        private double Correlation(IEnumerable<ScenarioRecord> records, bool pearson)
        {
            var list = records.ToList();
            var x = list.Select(s => s.SifGlobal).ToList();
            var y = list.Select(s => s.Cvar95Relative).ToList();
            var weights = BuildEqualInstanceBaselineWeights(list);
            return pearson ? ExperimentalStatistics.WeightedPearson(x, y, weights) : ExperimentalStatistics.WeightedSpearman(x, y, weights);
        }

        private static string BuildFinalMessage(ExperimentalRunResult result, TimeSpan elapsed)
        {
            return "Computational study completed." + Environment.NewLine + Environment.NewLine
                + "Selected files: " + result.FilesSelected + Environment.NewLine
                + "Valid files: " + result.FilesValid + Environment.NewLine
                + "Invalid files/errors: " + result.FilesInvalid + Environment.NewLine
                + "Exported baselines: " + result.BaselinesGenerated + Environment.NewLine
                + "Exported FRM diagnostics: " + result.FrmGenerated + Environment.NewLine
                + "Exported Monte Carlo runs: " + result.MonteCarloGenerated + Environment.NewLine
                + "Exported crashing scenarios: " + result.CrashingGenerated + Environment.NewLine
                + "Total time: " + elapsed + Environment.NewLine + Environment.NewLine
                + "Output folder:" + Environment.NewLine + result.OutputDirectory;
        }


        private static DelayMetrics ComputeDelayMetrics(RiskResultDto risk, int nominalMakespan)
        {
            if (risk == null)
                return new DelayMetrics();


            return new DelayMetrics
            {
                Probability = risk.DelayProbability,
                Mean = risk.MeanDelay,
                P95 = risk.P95Delay,
                CVaR95 = risk.CVaR95Delay,
                Max = risk.MaxDelay
            };
        }

        private static double Percentile(int[] sortedValues, double p)
        {
            if (sortedValues == null || sortedValues.Length == 0)
                return 0.0;
            if (p <= 0.0)
                return sortedValues[0];
            if (p >= 1.0)
                return sortedValues[sortedValues.Length - 1];
            double pos = (sortedValues.Length - 1) * p;
            int lower = (int)Math.Floor(pos);
            int upper = (int)Math.Ceiling(pos);
            if (lower == upper)
                return sortedValues[lower];
            double weight = pos - lower;
            return sortedValues[lower] + weight * (sortedValues[upper] - sortedValues[lower]);
        }

        private static double CVar(int[] sortedValues, double p)
        {
            if (sortedValues == null || sortedValues.Length == 0)
                return 0.0;
            double threshold = Percentile(sortedValues, p);
            long sum = 0;
            int count = 0;
            for (int i = 0; i < sortedValues.Length; i++)
            {
                int value = sortedValues[i];
                if (value >= threshold)
                {
                    sum += value;
                    count++;
                }
            }
            return count == 0 ? threshold : (double)sum / count;
        }


        private sealed class ExactVsHeuristicInstanceComparison
        {
            public string InstanceId;
            public string Family;
            public string ExactBaselineId;
            public double ExactMakespan;
            public double ExactSif;
            public double ExactDelayCvar95;
            public double ExactRelativeCvar95;
            public double ExactMeanUnabsorbedWork;
            public double ExactUnabsorbedWorkRatio;
            public int HeuristicsCount;
            public double HeuristicBestMakespan;
            public double HeuristicMeanMakespan;
            public double HeuristicMedianMakespan;
            public double HeuristicBestSif;
            public double HeuristicMeanSif;
            public double HeuristicMedianSif;
            public double HeuristicBestDelayCvar95;
            public double HeuristicMeanDelayCvar95;
            public double HeuristicMedianDelayCvar95;
            public double HeuristicBestRelativeCvar95;
            public double HeuristicMeanRelativeCvar95;
            public double HeuristicMedianRelativeCvar95;
            public double HeuristicMeanUnabsorbedWork;
            public double HeuristicMeanUnabsorbedWorkRatio;
            public double DeltaExactVsMeanMakespan;
            public double DeltaExactVsBestMakespan;
            public double DeltaExactVsMeanSif;
            public double DeltaExactVsMeanDelayCvar95;
            public double DeltaExactVsMeanRelativeCvar95;
            public double DeltaExactVsMeanUnabsorbedWork;
            public int ExactRankByMakespan;
            public int ExactRankBySif;
            public int ExactRankByDelayCvar95;
            public int ExactRankByRelativeCvar95;
        }

        private sealed class SensitivityMethodRecord
        {
            public double Gamma;
            public bool IsExact;
            public double DelayCvar95;
            public double RelativeCvar95;
            public double P95;
            public double DelayProbability;
            public double MeanDelay;
        }

        private sealed class DelayMetrics
        {
            public double Probability;
            public double Mean;
            public double P95;
            public double CVaR95;
            public double Max;
        }

        private sealed class ProjectValidation
        {
            public bool IsValid; public bool HasCycle; public int InvalidDurations; public int InvalidDemands; public int DemandsAboveCapacity; public int DisconnectedActivities; public string Message;
            public static ProjectValidation Invalid(string message) { return new ProjectValidation { IsValid = false, Message = message }; }
        }
        private sealed class BaselineValidation
        {
            public bool IsValid { get { return PrecedenceFeasible && ResourceFeasible && MakespanConsistent && MissingActivities == 0; } }
            public bool PrecedenceFeasible; public bool ResourceFeasible; public bool MakespanConsistent; public int PrecedenceViolations; public int ResourceViolations; public int MaxResourceViolation; public int MissingActivities;
        }
    }
}
