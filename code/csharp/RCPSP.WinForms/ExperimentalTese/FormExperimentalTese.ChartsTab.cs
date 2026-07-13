// Thesis traceability: complementary in-application visualization of consolidated experimental outputs.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace RCPSP.WinForms.ExperimentalTese
{
    public sealed partial class FormExperimentalTese : Form
    {
        private readonly TabControl _tabMain = new TabControl();
        private readonly TabPage _tabExecution = new TabPage("Execution");
        private readonly TabPage _tabCharts = new TabPage("Analysis charts");
        private readonly FlowLayoutPanel _chartsFlow = new FlowLayoutPanel();
        private readonly Label _lblChartsStatus = new Label();
        private readonly Button _btnReloadCharts = new Button();
        private readonly Button _btnOpenOutputFolder = new Button();

        private sealed class Agg
        {
            public int Count;
            public double Sum;
            public double Min = double.PositiveInfinity;
            public double Max = double.NegativeInfinity;
            public void Add(double value)
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                    return;
                Count++;
                Sum += value;
                if (value < Min) Min = value;
                if (value > Max) Max = value;
            }
            public double Mean { get { return Count == 0 ? 0.0 : Sum / Count; } }
        }

        private sealed class Point2
        {
            public double X;
            public double Y;
            public string Label;
            public string Class;
        }

        private void BuildChartsArea(Control parent)
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(8)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            parent.Controls.Add(root);

            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            _btnReloadCharts.Text = "Reload charts from output folder";
            _btnReloadCharts.Width = 240;
            _btnReloadCharts.Height = 30;
            _btnReloadCharts.Click += (s, e) => LoadExperimentalCharts(_txtOutput.Text);
            _btnOpenOutputFolder.Text = "Open output folder";
            _btnOpenOutputFolder.Width = 150;
            _btnOpenOutputFolder.Height = 30;
            _btnOpenOutputFolder.Click += (s, e) => OpenExperimentalOutputFolder();
            toolbar.Controls.Add(_btnReloadCharts);
            toolbar.Controls.Add(_btnOpenOutputFolder);

            var lblResizeHint = new Label
            {
                Text = "Tip: drag a chart's right edge to resize its width, or its bottom edge to resize its height.",
                AutoSize = true,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                ForeColor = System.Drawing.Color.DimGray,
                Margin = new Padding(18, 9, 4, 0)
            };
            toolbar.Controls.Add(lblResizeHint);
            root.Controls.Add(toolbar, 0, 0);

            _lblChartsStatus.Dock = DockStyle.Fill;
            _lblChartsStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            _lblChartsStatus.Text = "Run the experimental study or choose an output folder, then click Reload charts. The charts read the exported CSV files.";
            root.Controls.Add(_lblChartsStatus, 0, 1);

            _chartsFlow.Dock = DockStyle.Fill;
            _chartsFlow.AutoScroll = true;
            _chartsFlow.FlowDirection = FlowDirection.TopDown;
            _chartsFlow.WrapContents = false;
            root.Controls.Add(_chartsFlow, 0, 2);
        }

        private void OpenExperimentalOutputFolder()
        {
            try
            {
                string dir = _txtOutput.Text;
                if (!Directory.Exists(dir))
                    throw new DirectoryNotFoundException(dir);
                System.Diagnostics.Process.Start(dir);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not open the output folder.\r\n\r\n" + ex.Message, "RCPSP-FRM", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private static readonly object _chartsLogLock = new object();

        private static void LogChartsStep(string outputDirectory, string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(outputDirectory)) return;
                string dir = Path.Combine(outputDirectory, "04_charts");
                Directory.CreateDirectory(dir);
                string logPath = Path.Combine(dir, "charts_load_log.txt");
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "  " + message;
                lock (_chartsLogLock)
                {
                    File.AppendAllText(logPath, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {

            }
        }

        private async void LoadExperimentalCharts(string outputDirectory)
        {

            try
            {
                if (!string.IsNullOrWhiteSpace(outputDirectory) && Directory.Exists(outputDirectory))
                {
                    string dir = Path.Combine(outputDirectory, "04_charts");
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir, "charts_load_log.txt"),
                        "=== LoadExperimentalCharts started: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + " ===" + Environment.NewLine,
                        Encoding.UTF8);
                }
            }
            catch
            {

            }

            LogChartsStep(outputDirectory, "Entered LoadExperimentalCharts. InvokeRequired=" + InvokeRequired + " IsDisposed=" + IsDisposed + " Disposing=" + Disposing);

            try
            {
                if (InvokeRequired)
                {
                    LogChartsStep(outputDirectory, "InvokeRequired=true -> re-dispatching via BeginInvoke and returning.");
                    BeginInvoke(new Action<string>(LoadExperimentalCharts), outputDirectory);
                    return;
                }

                _chartsFlow.Controls.Clear();

                if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
                {
                    _lblChartsStatus.Text = "Output folder not found. Select the folder generated by the experimental study.";
                    LogChartsStep(outputDirectory, "ABORT: output folder not found or blank ('" + outputDirectory + "').");
                    return;
                }

                string consolidated = Path.Combine(outputDirectory, "03_consolidated");
                string logs = Path.Combine(outputDirectory, "05_logs");
                if (!Directory.Exists(consolidated))
                {
                    _lblChartsStatus.Text = "The selected folder does not contain 03_consolidated. Run the experiment first or select the correct output folder.";
                    LogChartsStep(outputDirectory, "ABORT: '03_consolidated' not found under '" + outputDirectory + "'.");
                    return;
                }

                LogChartsStep(outputDirectory, "Loading baseline metadata and Baseline/FRM/Risk/Sensitivity/Stability charts...");
                var baselineMap = LoadBaselineMetadata(Path.Combine(consolidated, "todos_baselines.csv"));
                AddBaselineCharts(consolidated, baselineMap);
                AddFrmCharts(consolidated, baselineMap);
                AddRiskCharts(consolidated, baselineMap);
                AddSensitivityCharts(consolidated);
                AddStabilityCharts(consolidated);
                LogChartsStep(outputDirectory, "Baseline/FRM/Risk/Sensitivity/Stability charts added. _chartsFlow.Controls.Count=" + _chartsFlow.Controls.Count);


                _lblChartsStatus.Text = "Loading large result sets (crashing, dominance)... this can take a while.";
                LogChartsStep(outputDirectory, "Starting AddCrashingCharts (background CSV parse of todos_crashing.csv)...");
                await AddCrashingCharts(consolidated).ConfigureAwait(true);
                LogChartsStep(outputDirectory, "AddCrashingCharts finished. IsDisposed=" + IsDisposed + " Disposing=" + Disposing + " Controls.Count=" + _chartsFlow.Controls.Count);

                LogChartsStep(outputDirectory, "Starting AddDominanceCharts (background CSV parse of dominance.csv)...");
                await AddDominanceCharts(consolidated).ConfigureAwait(true);
                LogChartsStep(outputDirectory, "AddDominanceCharts finished. IsDisposed=" + IsDisposed + " Disposing=" + Disposing + " Controls.Count=" + _chartsFlow.Controls.Count);

                LogChartsStep(outputDirectory, "Adding Modified DH B&B versus heuristics charts...");
                AddExactVsHeuristicsCharts(consolidated);
                LogChartsStep(outputDirectory, "Modified DH B&B versus heuristics charts added. Controls.Count=" + _chartsFlow.Controls.Count);

                if (IsDisposed || Disposing)
                {
                    LogChartsStep(outputDirectory, "ABORT after AddDominanceCharts: IsDisposed=" + IsDisposed + " Disposing=" + Disposing + ". Autosave will NOT run.");
                    return;
                }

                LogChartsStep(outputDirectory, "Adding MaxCombo/Correlation/StratifiedRisk/Proposition charts...");
                AddMaxComboCharts(logs);
                AddCorrelationCharts(consolidated);
                AddStratifiedRiskCharts(consolidated);
                AddPropositionCharts(consolidated);
                LogChartsStep(outputDirectory, "MaxCombo/Correlation/StratifiedRisk/Proposition charts added. Controls.Count=" + _chartsFlow.Controls.Count);

                AddBalanceDiagnosticsCharts(consolidated);
                LogChartsStep(outputDirectory, "AddBalanceDiagnosticsCharts added. Total Controls.Count=" + _chartsFlow.Controls.Count + ". Proceeding to autosave.");

                _lblChartsStatus.Text = "Charts loaded from: " + consolidated + ". Saving PNG/XLSX copies to 04_charts\\analysis_charts ...";

                string autoSaveDirectory;
                try
                {
                    int chartCountBefore = CountExperimentalCharts();
                    LogChartsStep(outputDirectory, "Calling AutoSaveAllExperimentalCharts. EnumerateExperimentalCharts() currently yields " + chartCountBefore + " chart(s). Target folder: " + Path.Combine(Path.Combine(outputDirectory, "04_charts"), "analysis_charts"));
                    autoSaveDirectory = AutoSaveAllExperimentalCharts(outputDirectory);
                    LogChartsStep(outputDirectory, "AutoSaveAllExperimentalCharts returned OK. Folder: " + autoSaveDirectory + ". Directory.Exists=" + Directory.Exists(autoSaveDirectory) + ". File count now=" + (Directory.Exists(autoSaveDirectory) ? Directory.GetFiles(autoSaveDirectory).Length : -1));
                }
                catch (Exception autoSaveEx)
                {
                    LogChartsStep(outputDirectory, "AUTOSAVE FAILED: " + autoSaveEx.GetType().FullName + " - " + autoSaveEx.Message + Environment.NewLine + autoSaveEx.StackTrace);
                    _lblChartsStatus.Text = "Charts loaded from: " + consolidated + ", but autosave to disk FAILED: " + autoSaveEx.GetType().Name + " - " + autoSaveEx.Message;
                    MessageBox.Show(this, "Charts were loaded, but saving them to 04_charts\\analysis_charts failed.\r\n\r\nException type: " + autoSaveEx.GetType().FullName + "\r\nMessage: " + autoSaveEx.Message + "\r\n\r\nStack trace:\r\n" + autoSaveEx.StackTrace + "\r\n\r\n(Full step-by-step log written to 04_charts\\charts_load_log.txt)", "RCPSP-FRM - Autosave failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    if (_tabMain != null && _tabCharts != null)
                        _tabMain.SelectedTab = _tabCharts;
                    return;
                }

                _lblChartsStatus.Text = "Charts loaded from: " + consolidated + ". Review Baseline, FRM/SIF, Risk, Sensitivity, Stability, Crashing, Dominance, Modified DH B&B vs heuristics, Max Activities/combo, Stratified, Proposition, and Balance diagnostics blocks. All charts were automatically saved to: " + autoSaveDirectory;
                if (_tabMain != null && _tabCharts != null)
                    _tabMain.SelectedTab = _tabCharts;
                LogChartsStep(outputDirectory, "=== LoadExperimentalCharts completed successfully. ===");
            }
            catch (Exception ex)
            {
                LogChartsStep(outputDirectory, "FATAL in LoadExperimentalCharts: " + ex.GetType().FullName + " - " + ex.Message + Environment.NewLine + ex.StackTrace);
                _lblChartsStatus.Text = "Could not load charts: " + ex.GetType().Name + " - " + ex.Message;
                MessageBox.Show(this, "Could not load experimental charts.\r\n\r\nException type: " + ex.GetType().FullName + "\r\nMessage: " + ex.Message + "\r\n\r\nStack trace:\r\n" + ex.StackTrace + "\r\n\r\n(Full step-by-step log written to 04_charts\\charts_load_log.txt, if that folder could be created)", "RCPSP-FRM", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private int CountExperimentalCharts()
        {
            int count = 0;
            foreach (var _ in EnumerateExperimentalCharts())
                count++;
            return count;
        }

        private Dictionary<string, string> LoadBaselineMetadata(string path)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path))
                return result;

            ForEachCsvRow(path, row =>
            {
                string baselineId = Get(row, "baseline_id");
                string heuristic = Get(row, "heuristic");
                if (!string.IsNullOrWhiteSpace(baselineId) && !result.ContainsKey(baselineId))
                    result[baselineId] = string.IsNullOrWhiteSpace(heuristic) ? InferHeuristicFromBaselineId(baselineId) : heuristic;
            });
            return result;
        }

        private void AddBaselineCharts(string consolidated, Dictionary<string, string> baselineMap)
        {
            string path = Path.Combine(consolidated, "todos_baselines.csv");
            if (!File.Exists(path)) return;

            var makespanByHeuristic = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var countByCombination = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var countByStatus = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);

            ForEachCsvRow(path, row =>
            {
                string heuristic = Get(row, "heuristic");
                if (string.IsNullOrWhiteSpace(heuristic)) heuristic = InferHeuristicFromBaselineId(Get(row, "baseline_id"));
                string scheme = Get(row, "scheme");
                string direction = Get(row, "direction");
                string combo = (string.IsNullOrWhiteSpace(scheme) ? "UNKNOWN" : scheme) + " / " + (string.IsNullOrWhiteSpace(direction) ? "UNKNOWN" : direction);
                string status = Get(row, "selected_for_frm");
                double makespan = Parse(Get(row, "makespan_nominal"));
                AddAgg(makespanByHeuristic, heuristic, makespan);
                AddAgg(countByCombination, combo, 1);
                AddAgg(countByStatus, string.Equals(status, "True", StringComparison.OrdinalIgnoreCase) ? "Valid" : "Excluded", 1);
            });

            AddBarChart("Baseline - Mean makespan by heuristic", "Lower values indicate shorter deterministic baselines.", makespanByHeuristic, a => a.Mean, "Mean makespan");
            AddBarChart("Baseline - Number of runs by scheme/direction", "Confirms whether serial/parallel and forward/backward combinations were generated.", countByCombination, a => a.Sum, "Runs");
            AddBarChart("Baseline - Valid versus excluded baselines", "Checks how many generated baselines entered the FRM/risk pipeline. Modified DH B&B is kept in the experimental module as an exact reference.", countByStatus, a => a.Sum, "Baselines");
        }

        private void AddFrmCharts(string consolidated, Dictionary<string, string> baselineMap)
        {
            string path = Path.Combine(consolidated, "todos_frm.csv");
            if (!File.Exists(path)) return;

            var sifByHeuristic = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var classCount = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var minBalanceByHeuristic = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var sifValues = new List<double>();

            ForEachCsvRow(path, row =>
            {
                string baselineId = Get(row, "baseline_id");
                string heuristic = baselineMap.ContainsKey(baselineId) ? baselineMap[baselineId] : InferHeuristicFromBaselineId(baselineId);
                double sif = Parse(Get(row, "sif_global"));
                AddAgg(sifByHeuristic, heuristic, sif);
                AddAgg(minBalanceByHeuristic, heuristic, Parse(Get(row, "min_balance_global")));
                AddAgg(classCount, Get(row, "structural_classification"), 1);
                sifValues.Add(sif);
            });

            AddBarChart("FRM/SIF - Mean SIF by heuristic", "Shows which baseline generation rules produced higher structural flexibility.", sifByHeuristic, a => a.Mean, "Mean SIF");
            AddBarChart("FRM/SIF - Mean minimum balance by heuristic", "Lower values identify baselines closer to structural exhaustion.", minBalanceByHeuristic, a => a.Mean, "Mean min balance");
            AddBarChart("FRM/SIF - Structural classification count", "Counts robust versus fragile baselines according to FRM diagnostics.", classCount, a => a.Sum, "Count");
            AddBarChart("FRM/SIF - SIF distribution", "Histogram-like grouped view of SIF values (bin width adapts to the observed range).", BuildAdaptiveDistribution(sifValues), a => a.Sum, "Count");
        }

        private void AddRiskCharts(string consolidated, Dictionary<string, string> baselineMap)
        {
            string riskPath = Path.Combine(consolidated, "todos_monte_carlo.csv");
            string frmPath = Path.Combine(consolidated, "todos_frm.csv");
            if (!File.Exists(riskPath)) return;

            var sifByBaseline = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(frmPath))
            {
                ForEachCsvRow(frmPath, row =>
                {
                    string baselineId = Get(row, "baseline_id");
                    if (!string.IsNullOrWhiteSpace(baselineId))
                        sifByBaseline[baselineId] = Parse(Get(row, "sif_global"));
                });
            }

            var cvarByHeuristic = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var p95ByHeuristic = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var delayProbByHeuristic = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var sifVsCvar = new List<Point2>();
            var makespanVsCvar = new List<Point2>();
            var sifVsRelativeCvar = new List<Point2>();
            var makespanVsRelativeCvar = new List<Point2>();
            var cvarValues = new List<double>();

            ForEachCsvRow(riskPath, row =>
            {
                string baselineId = Get(row, "baseline_id");
                string heuristic = baselineMap.ContainsKey(baselineId) ? baselineMap[baselineId] : InferHeuristicFromBaselineId(baselineId);
                double cvar = FirstNumber(row, "primary_delay_cvar95", "delay_cvar95", "recalculated_delay_cvar95");
                double relativeCvar = FirstNumber(row, "relative_delay_cvar95", "relative_cvar95");
                double p95Delay = FirstNumber(row, "delay_p95", "p95");
                double delayProb = Parse(Get(row, "delay_probability"));
                double makespan = Parse(Get(row, "makespan_nominal"));
                if (relativeCvar <= 0.0 && makespan > 0.0)
                    relativeCvar = cvar / makespan;
                AddAgg(cvarByHeuristic, heuristic, cvar);
                AddAgg(p95ByHeuristic, heuristic, p95Delay);
                AddAgg(delayProbByHeuristic, heuristic, delayProb);
                cvarValues.Add(cvar);
                double sif;
                if (sifByBaseline.TryGetValue(baselineId, out sif))
                {
                    AddSample(sifVsCvar, new Point2 { X = sif, Y = cvar, Label = baselineId, Class = heuristic }, 5000);
                    AddSample(sifVsRelativeCvar, new Point2 { X = sif, Y = relativeCvar, Label = baselineId, Class = heuristic }, 5000);
                }
                AddSample(makespanVsCvar, new Point2 { X = makespan, Y = cvar, Label = baselineId, Class = heuristic }, 5000);
                AddSample(makespanVsRelativeCvar, new Point2 { X = makespan, Y = relativeCvar, Label = baselineId, Class = heuristic }, 5000);
            });

            AddBarChart("Risk - Mean CVaR95(L) by heuristic", "Primary risk metric: average delay CVaR95 by baseline heuristic.", cvarByHeuristic, a => a.Mean, "Mean CVaR95(L)");
            AddBarChart("Risk - Mean P95 delay by heuristic", "Compares conservative delay percentiles across heuristics.", p95ByHeuristic, a => a.Mean, "Mean P95 delay");
            AddBarChart("Risk - Mean delay probability by heuristic", "Average probability that L > 0.", delayProbByHeuristic, a => a.Mean, "P(L > 0)");
            AddBarChart("Risk - CVaR95(L) distribution", "Histogram-like grouped view of tail delay risk (bin width adapts to the observed range).", BuildAdaptiveDistribution(cvarValues), a => a.Sum, "Count");
            AddScatterChart("Risk - SIF versus CVaR95(L)", "Core diagnostic using absolute delay tail risk.", "SIF", "CVaR95(L)", sifVsCvar);
            AddScatterChart("Risk - SIF versus relative CVaR95(L)", "Main H1 diagnostic: checks whether structural flexibility is associated with lower tail risk after normalizing by nominal makespan.", "SIF", "Relative CVaR95(L)", sifVsRelativeCvar);
            AddScatterChart("Risk - Makespan versus CVaR95(L)", "Checks whether nominal duration explains absolute risk more clearly than SIF.", "Makespan", "CVaR95(L)", makespanVsCvar);
            AddScatterChart("Risk - Makespan versus relative CVaR95(L)", "Compares nominal duration with normalized tail risk, supporting the thesis discussion on the role of makespan.", "Makespan", "Relative CVaR95(L)", makespanVsRelativeCvar);
        }

        private void AddSensitivityCharts(string consolidated)
        {
            string path = Path.Combine(consolidated, "sensibilidade.csv");
            if (!File.Exists(path)) return;

            var cvarByGamma = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var p95ByGamma = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var probByGamma = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            ForEachCsvRow(path, row =>
            {
                string gamma = NormalizeGamma(Get(row, "gamma"));
                AddAgg(cvarByGamma, gamma, FirstNumber(row, "primary_delay_cvar95", "recalculated_delay_cvar95"));
                AddAgg(p95ByGamma, gamma, FirstNumber(row, "delay_p95", "p95"));
                AddAgg(probByGamma, gamma, Parse(Get(row, "delay_probability")));
            });

            AddLineChart("Sensitivity - Gamma versus mean CVaR95(L)", "Shows how delay tail risk changes as perturbation intensity increases.", cvarByGamma, a => a.Mean, "Mean CVaR95(L)");
            AddLineChart("Sensitivity - Gamma versus mean P95 delay", "Shows whether conservative delay percentiles grow with gamma.", p95ByGamma, a => a.Mean, "Mean P95 delay");
            AddLineChart("Sensitivity - Gamma versus mean delay probability", "Shows how P(L > 0) changes under stronger perturbations.", probByGamma, a => a.Mean, "Mean P(L > 0)");

            string corrPath = Path.Combine(consolidated, "sensitivity_correlations.csv");
            if (File.Exists(corrPath))
            {
                var spearmanRel = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
                var spearmanDelay = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
                ForEachCsvRow(corrPath, row =>
                {
                    string gamma = NormalizeGamma(Get(row, "gamma"));
                    string y = Get(row, "y_variable");
                    double s = Parse(Get(row, "spearman"));
                    if (y.IndexOf("relative", StringComparison.OrdinalIgnoreCase) >= 0)
                        AddAgg(spearmanRel, gamma, s);
                    else if (y.IndexOf("delay", StringComparison.OrdinalIgnoreCase) >= 0 || y.IndexOf("CVaR", StringComparison.OrdinalIgnoreCase) >= 0)
                        AddAgg(spearmanDelay, gamma, s);
                });
                AddLineChart("Sensitivity - Gamma versus Spearman(SIF, relative CVaR95)", "Checks whether the SIF-risk relationship changes under different perturbation intensities.", spearmanRel, a => a.Mean, "Spearman");
                AddLineChart("Sensitivity - Gamma versus Spearman(SIF, delay CVaR95)", "Confirmatory sensitivity of the core SIF-risk proposition.", spearmanDelay, a => a.Mean, "Spearman");
            }
        }

        private void AddStabilityCharts(string consolidated)
        {
            string stabilityPath = Path.Combine(consolidated, "monte_carlo_stability.csv");
            if (File.Exists(stabilityPath))
            {
                var classCount = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
                var cvValues = new List<double>();
                ForEachCsvRow(stabilityPath, row =>
                {
                    AddAgg(classCount, Get(row, "stability_classification"), 1);
                    cvValues.Add(Parse(Get(row, "cv_delay_cvar95")));
                });
                AddBarChart("Stability - Stable versus unstable Monte Carlo results", "Shows how many baselines have stable CVaR95 estimates across independent replications.", classCount, a => a.Sum, "Count");
                AddBarChart("Stability - Coefficient of variation of CVaR95(L)", "Histogram-like view of seed/replication sensitivity for the tail risk metric (bin width adapts to the observed range).", BuildAdaptiveDistribution(cvValues), a => a.Sum, "Count");
            }

            string ciPath = Path.Combine(consolidated, "monte_carlo_confidence_intervals.csv");
            if (File.Exists(ciPath))
            {
                var widthValues = new List<double>();
                var widthByMetric = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
                ForEachCsvRow(ciPath, row =>
                {
                    string metric = Get(row, "metric");
                    double width = Parse(Get(row, "ci_width"));
                    AddAgg(widthByMetric, metric, width);
                    if (metric.IndexOf("cvar", StringComparison.OrdinalIgnoreCase) >= 0)
                        widthValues.Add(width);
                });
                AddBarChart("Stability - Mean confidence interval width by metric", "Compares uncertainty in P95 and CVaR95 estimates.", widthByMetric, a => a.Mean, "Mean CI width");
                AddBarChart("Stability - CVaR95 confidence interval width distribution", "Shows whether tail-risk estimates are narrow or broad (bin width adapts to the observed range).", BuildAdaptiveDistribution(widthValues), a => a.Sum, "Count");
            }
        }

        private async Task AddCrashingCharts(string consolidated)
        {
            string path = Path.Combine(consolidated, "todos_crashing.csv");
            if (!File.Exists(path)) return;

            var classCount = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);


            const int frriDistSampleCap = 200_000;
            var frriValues = new List<double>(frriDistSampleCap);
            var frriByClass = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var scatterDelta = new List<Point2>();
            var scatterSif = new List<Point2>();
            var frriByHeuristic = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var pairedFrriValues = new List<double>(frriDistSampleCap);
            var pairedFrriByClass = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var scatterDeltaPaired = new List<Point2>();
            var scatterRegimes = new List<Point2>();


            await Task.Run(() =>
            {
                ForEachCsvRow(path, row =>
                {
                    string baselineId = Get(row, "baseline_id");
                    string heuristic = InferHeuristicFromBaselineId(baselineId);
                    string cls = Get(row, "classification");
                    double frri = Parse(Get(row, "frri"));
                    double deltaMakespan = Parse(Get(row, "delta_makespan"));
                    double deltaCvar = FirstNumber(row, "delta_delay_cvar95", "delta_cvar95");
                    double deltaSif = Parse(Get(row, "delta_sif"));
                    AddAgg(classCount, cls, 1);
                    if (frriValues.Count < frriDistSampleCap) frriValues.Add(frri);
                    AddAgg(frriByClass, cls, frri);
                    AddAgg(frriByHeuristic, heuristic, frri);
                    AddSample(scatterDelta, new Point2 { X = deltaMakespan, Y = deltaCvar, Label = Get(row, "crashing_scenario_id"), Class = cls }, 6000);
                    AddSample(scatterSif, new Point2 { X = deltaSif, Y = deltaCvar, Label = Get(row, "crashing_scenario_id"), Class = cls }, 6000);

                    bool pairedEnabled = string.Equals(Get(row, "paired_enabled"), "True", StringComparison.OrdinalIgnoreCase);
                    if (pairedEnabled)
                    {
                        double pairedFrri = Parse(Get(row, "frri_paired"));
                        double pairedDeltaCvar = FirstNumber(row, "delta_delay_cvar95_paired", "delta_cvar95_paired");
                        string pairedCls = Get(row, "classification_paired");
                        if (pairedFrriValues.Count < frriDistSampleCap) pairedFrriValues.Add(pairedFrri);
                        AddAgg(pairedFrriByClass, string.IsNullOrWhiteSpace(pairedCls) ? cls : pairedCls, pairedFrri);
                        AddSample(scatterDeltaPaired, new Point2 { X = deltaMakespan, Y = pairedDeltaCvar, Label = Get(row, "crashing_scenario_id"), Class = string.IsNullOrWhiteSpace(pairedCls) ? cls : pairedCls }, 6000);
                        AddSample(scatterRegimes, new Point2 { X = frri, Y = pairedFrri, Label = Get(row, "crashing_scenario_id"), Class = cls }, 6000);
                    }
                });
            }).ConfigureAwait(true);

            if (IsDisposed || Disposing)
                return;

            AddBarChart("Crashing - Scenario classification count", "Shows efficient, risky, robust-but-slow, and ambiguous compression outcomes.", classCount, a => a.Sum, "Count");
            AddBarChart("Crashing - FRRI distribution", "Positive FRRI means the scenario reduced delay tail risk; negative FRRI means it aggravated risk. Bin width adapts to the observed range" + (frriValues.Count >= frriDistSampleCap ? "; counts are based on a representative sample of the first " + frriDistSampleCap.ToString("N0", CultureInfo.InvariantCulture) + " scenarios (the full file has millions of rows)." : "."), BuildAdaptiveDistribution(frriValues), a => a.Sum, "Count");
            AddBarChart("Crashing - Mean FRRI by classification", "Confirms whether the automatic classes align with actual risk reduction.", frriByClass, a => a.Mean, "Mean FRRI");
            AddBarChart("Crashing - Mean FRRI by heuristic", "Identifies which baseline heuristics produce better compression outcomes on average.", frriByHeuristic, a => a.Mean, "Mean FRRI");
            AddScatterChart("Crashing - Delta makespan versus delta CVaR95(L) primary", "Core intervention chart for the primary regime.", "Delta makespan", "Delta CVaR95(L) primary", scatterDelta);
            AddScatterChart("Crashing - Delta SIF versus delta CVaR95(L) primary", "Checks whether structural flexibility change is associated with primary-regime risk change.", "Delta SIF", "Delta CVaR95(L) primary", scatterSif);
            if (pairedFrriValues.Count > 0)
            {
                AddBarChart("Crashing - Paired FRRI distribution", "Same crashing scenarios evaluated under the paired regime.", BuildAdaptiveDistribution(pairedFrriValues), a => a.Sum, "Count");
                AddBarChart("Crashing - Mean paired FRRI by classification", "Checks paired-regime risk reduction by automatic class.", pairedFrriByClass, a => a.Mean, "Mean paired FRRI");
                AddScatterChart("Crashing - Delta makespan versus delta CVaR95(L) paired", "Same scenarios evaluated under the paired regime.", "Delta makespan", "Delta CVaR95(L) paired", scatterDeltaPaired);
                AddScatterChart("Crashing - FRRI primary versus FRRI paired", "Shows whether the same crashing scenario behaves consistently across the two regimes.", "FRRI primary", "FRRI paired", scatterRegimes);
            }
        }

        private async Task AddDominanceCharts(string consolidated)
        {
            string path = Path.Combine(consolidated, "dominance.csv");
            if (!File.Exists(path)) return;

            var domCount = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var nonDominatedByClass = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var paretoDominated = new List<Point2>();
            var paretoNonDominated = new List<Point2>();
            var paretoPaired = new List<Point2>();


            await Task.Run(() =>
            {
                ForEachCsvRow(path, row =>
                {
                    bool dominated = string.Equals(Get(row, "dominated_primary"), "True", StringComparison.OrdinalIgnoreCase);
                    string key = dominated ? "Dominated" : "Non-dominated";
                    AddAgg(domCount, key, 1);
                    if (!dominated)
                        AddAgg(nonDominatedByClass, Get(row, "class"), 1);

                    var p = new Point2
                    {
                        X = Parse(Get(row, "makespan")),
                        Y = Parse(Get(row, "relative_cvar95")),
                        Label = Get(row, "scenario_id"),
                        Class = key
                    };
                    if (dominated)
                        AddSample(paretoDominated, p, 3000);
                    else
                        AddSample(paretoNonDominated, p, 3000);

                    bool pairedEnabled = string.Equals(Get(row, "paired_enabled"), "True", StringComparison.OrdinalIgnoreCase);
                    if (pairedEnabled)
                    {
                        bool dominatedPaired = string.Equals(Get(row, "dominated_paired"), "True", StringComparison.OrdinalIgnoreCase);
                        AddSample(paretoPaired, new Point2
                        {
                            X = Parse(Get(row, "paired_makespan")),
                            Y = Parse(Get(row, "paired_relative_cvar95")),
                            Label = Get(row, "scenario_id"),
                            Class = dominatedPaired ? "Dominated paired" : "Non-dominated paired"
                        }, 3000);
                    }
                });
            }).ConfigureAwait(true);

            if (IsDisposed || Disposing)
                return;

            AddBarChart("Dominance - Dominated versus non-dominated scenarios", "Shows how selective the multicriteria frontier is.", domCount, a => a.Sum, "Count");
            AddBarChart("Dominance - Non-dominated scenarios by class", "Shows which scenario classes remain competitive after multicriteria filtering.", nonDominatedByClass, a => a.Sum, "Count");
            AddScatterChart("Dominance - Pareto view: makespan versus relative CVaR95 primary", "Lower-left points are preferable; non-dominated points form the decision frontier in the primary regime.", "Makespan", "Relative CVaR95 primary", paretoDominated, paretoNonDominated);
            if (paretoPaired.Count > 0)
                AddScatterChart("Dominance - Pareto view: makespan versus relative CVaR95 paired", "Lower-left points are preferable; point labels use the paired-regime dominance classification.", "Makespan paired", "Relative CVaR95 paired", paretoPaired);
        }


        private void AddExactVsHeuristicsCharts(string consolidated)
        {
            string byInstancePath = Path.Combine(consolidated, "modified_dh_bb_vs_heuristics_by_instance.csv");
            string summaryPath = Path.Combine(consolidated, "modified_dh_bb_vs_heuristics_summary.csv");
            string byGammaPath = Path.Combine(consolidated, "modified_dh_bb_vs_heuristics_by_gamma.csv");
            string crashingPath = Path.Combine(consolidated, "modified_dh_bb_vs_heuristics_crashing.csv");

            bool hasAnyFile = File.Exists(byInstancePath) || File.Exists(summaryPath) || File.Exists(byGammaPath) || File.Exists(crashingPath);
            if (!hasAnyFile)
            {
                AppendChartsNote("Modified DH B&B vs heuristics charts skipped: the derived files modified_dh_bb_vs_heuristics_*.csv were not found in 03_consolidated. Run the experimental analysis with Modified DH B&B enabled.");
                return;
            }

            AddSectionHeader("Modified DH B&B × heuristics", "Exact-reference charts for Chapter 4: deterministic quality, structural flexibility, tail risk, unabsorbed work, gamma sensitivity and crashing outcomes. Negative deltas in risk or makespan usually favour the exact reference; positive deltas in SIF favour the exact reference.");

            if (File.Exists(summaryPath))
                AddExactVsHeuristicsSummaryCharts(summaryPath);
            if (File.Exists(byInstancePath))
                AddExactVsHeuristicsByInstanceCharts(byInstancePath);
            if (File.Exists(byGammaPath))
                AddExactVsHeuristicsByGammaCharts(byGammaPath);
            if (File.Exists(crashingPath))
                AddExactVsHeuristicsCrashingCharts(crashingPath);
        }

        private void AddExactVsHeuristicsSummaryCharts(string path)
        {
            var percentages = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var meanDeltas = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);

            ForEachCsvRow(path, row =>
            {
                AddAgg(percentages, "Makespan <= heuristic mean", Parse(Get(row, "exact_better_or_equal_than_mean_makespan_percent")));
                AddAgg(percentages, "Makespan <= best heuristic", Parse(Get(row, "exact_better_or_equal_than_best_makespan_percent")));
                AddAgg(percentages, "SIF >= heuristic mean", Parse(Get(row, "exact_better_or_equal_than_mean_sif_percent")));
                AddAgg(percentages, "Delay CVaR95 <= heuristic mean", Parse(Get(row, "exact_better_or_equal_than_mean_delay_cvar95_percent")));
                AddAgg(percentages, "Relative CVaR95 <= heuristic mean", Parse(Get(row, "exact_better_or_equal_than_mean_relative_cvar95_percent")));
                AddAgg(percentages, "Unabsorbed work <= heuristic mean", Parse(Get(row, "exact_better_or_equal_than_mean_unabsorbed_work_percent")));

                AddAgg(meanDeltas, "Δ makespan", Parse(Get(row, "mean_delta_makespan")));
                AddAgg(meanDeltas, "Δ SIF", Parse(Get(row, "mean_delta_sif")));
                AddAgg(meanDeltas, "Δ delay CVaR95", Parse(Get(row, "mean_delta_delay_cvar95")));
                AddAgg(meanDeltas, "Δ relative CVaR95", Parse(Get(row, "mean_delta_relative_cvar95")));
                AddAgg(meanDeltas, "Δ unabsorbed work", Parse(Get(row, "mean_delta_unabsorbed_work")));
            });

            AddBarChart("DH B&B vs heuristics - Share of instances where exact reference is better or equal", "Percentage of instances where Modified DH B&B is at least as good as the heuristic benchmark for each criterion. For makespan, CVaR and unabsorbed work lower is better; for SIF higher is better.", percentages, a => a.Mean, "Instances (%)");
            AddBarChart("DH B&B vs heuristics - Mean deltas against heuristic mean", "Mean difference exact minus heuristic mean. Negative values favour Modified DH B&B for makespan/risk/work; positive values favour Modified DH B&B for SIF.", meanDeltas, a => a.Mean, "Mean delta");
        }

        private void AddExactVsHeuristicsByInstanceCharts(string path)
        {
            var deltaMakespanMean = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var deltaMakespanBest = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var deltaSif = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var deltaDelayCvar = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var deltaRelativeCvar = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var deltaUnabsorbed = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var rankByMakespan = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var rankByRisk = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var deltaMakespanValues = new List<double>();
            var deltaRelativeCvarValues = new List<double>();
            var deltaUnabsorbedValues = new List<double>();
            var tradeoffMakespanRisk = new List<Point2>();
            var tradeoffSifRisk = new List<Point2>();
            var tradeoffWorkRisk = new List<Point2>();

            ForEachCsvRow(path, row =>
            {
                string instance = Get(row, "instance_id");
                if (string.IsNullOrWhiteSpace(instance)) instance = Get(row, "exact_baseline_id");
                double dmMean = Parse(Get(row, "delta_exact_vs_mean_makespan"));
                double dmBest = Parse(Get(row, "delta_exact_vs_best_makespan"));
                double ds = Parse(Get(row, "delta_exact_vs_mean_sif"));
                double dc = Parse(Get(row, "delta_exact_vs_mean_delay_cvar95"));
                double dr = Parse(Get(row, "delta_exact_vs_mean_relative_cvar95"));
                double du = Parse(Get(row, "delta_exact_vs_mean_unabsorbed_work"));

                AddAgg(deltaMakespanMean, instance, dmMean);
                AddAgg(deltaMakespanBest, instance, dmBest);
                AddAgg(deltaSif, instance, ds);
                AddAgg(deltaDelayCvar, instance, dc);
                AddAgg(deltaRelativeCvar, instance, dr);
                AddAgg(deltaUnabsorbed, instance, du);

                deltaMakespanValues.Add(dmMean);
                deltaRelativeCvarValues.Add(dr);
                deltaUnabsorbedValues.Add(du);

                AddAgg(rankByMakespan, "Rank " + Get(row, "exact_rank_by_makespan"), 1);
                AddAgg(rankByRisk, "Rank " + Get(row, "exact_rank_by_relative_cvar95"), 1);
                AddSample(tradeoffMakespanRisk, new Point2 { X = dmMean, Y = dr, Label = instance, Class = "Exact vs mean heuristic" }, 5000);
                AddSample(tradeoffSifRisk, new Point2 { X = ds, Y = dr, Label = instance, Class = "Exact vs mean heuristic" }, 5000);
                AddSample(tradeoffWorkRisk, new Point2 { X = du, Y = dr, Label = instance, Class = "Exact vs mean heuristic" }, 5000);
            });

            AddBarChart("DH B&B vs heuristics - Distribution of Δ makespan against heuristic mean", "All compared instances summarized as bins. Negative bins mean Modified DH B&B is faster than the heuristic mean.", BuildAdaptiveDistribution(deltaMakespanValues), a => a.Sum, "Instances");
            AddBarChart("DH B&B vs heuristics - Distribution of Δ relative CVaR95 against heuristic mean", "All compared instances summarized as bins. Negative bins mean Modified DH B&B has lower normalized tail risk than the heuristic mean.", BuildAdaptiveDistribution(deltaRelativeCvarValues), a => a.Sum, "Instances");
            AddBarChart("DH B&B vs heuristics - Distribution of Δ unabsorbed work against heuristic mean", "All compared instances summarized as bins. Negative bins mean the exact-reference baseline leaves less work unabsorbed by Balance than the heuristic mean.", BuildAdaptiveDistribution(deltaUnabsorbedValues), a => a.Sum, "Instances");
            AddBarChart("DH B&B vs heuristics - Δ makespan against heuristic mean by instance", "Exact minus heuristic mean. Negative bars mean Modified DH B&B produced shorter deterministic baselines than the average heuristic baseline for that instance. For very large runs, the displayed category chart is capped for readability; use the distribution and CSV for the full set.", deltaMakespanMean, a => a.Mean, "Δ makespan");
            AddBarChart("DH B&B vs heuristics - Δ makespan against best heuristic by instance", "Exact minus best heuristic. Bars at or below zero mean Modified DH B&B matched or improved the best heuristic makespan.", deltaMakespanBest, a => a.Mean, "Δ makespan");
            AddBarChart("DH B&B vs heuristics - Δ SIF against heuristic mean by instance", "Exact minus heuristic mean. Positive bars mean the exact-reference baseline also had higher structural flexibility.", deltaSif, a => a.Mean, "Δ SIF");
            AddBarChart("DH B&B vs heuristics - Δ delay CVaR95 against heuristic mean by instance", "Exact minus heuristic mean. Negative bars mean the exact-reference baseline had lower absolute delay tail risk.", deltaDelayCvar, a => a.Mean, "Δ CVaR95(L)");
            AddBarChart("DH B&B vs heuristics - Δ relative CVaR95 against heuristic mean by instance", "Exact minus heuristic mean. Negative bars mean the exact-reference baseline had lower normalized tail risk, which is central to the thesis interpretation.", deltaRelativeCvar, a => a.Mean, "Δ relative CVaR95(L)");
            AddBarChart("DH B&B vs heuristics - Δ unabsorbed work against heuristic mean by instance", "Exact minus heuristic mean. Negative bars mean the exact-reference baseline left less simulated work unabsorbed by Balance.", deltaUnabsorbed, a => a.Mean, "Δ unabsorbed work");
            AddBarChart("DH B&B vs heuristics - Exact rank by makespan", "Rank position of Modified DH B&B among all baselines of the same instance. Rank 1 is best deterministic makespan.", rankByMakespan, a => a.Sum, "Instances");
            AddBarChart("DH B&B vs heuristics - Exact rank by relative CVaR95", "Rank position of Modified DH B&B among all baselines of the same instance. Rank 1 is lowest normalized tail risk.", rankByRisk, a => a.Sum, "Instances");
            AddScatterChart("DH B&B vs heuristics - Δ makespan versus Δ relative CVaR95", "Quadrant view. Left side means exact is faster; lower side means exact is less risky. The lower-left quadrant is the strongest evidence of exact-reference superiority beyond makespan.", "Δ makespan", "Δ relative CVaR95(L)", tradeoffMakespanRisk);
            AddScatterChart("DH B&B vs heuristics - Δ SIF versus Δ relative CVaR95", "Tests whether structural improvement from the exact reference is accompanied by risk reduction. Right-lower points are structurally and probabilistically favourable.", "Δ SIF", "Δ relative CVaR95(L)", tradeoffSifRisk);
            AddScatterChart("DH B&B vs heuristics - Δ unabsorbed work versus Δ relative CVaR95", "Checks whether reduction of work not absorbed by Balance moves together with reduction of normalized tail risk.", "Δ unabsorbed work", "Δ relative CVaR95(L)", tradeoffWorkRisk);
        }

        private void AddExactVsHeuristicsByGammaCharts(string path)
        {
            var delaySeries = new Dictionary<string, Dictionary<string, Agg>>(StringComparer.OrdinalIgnoreCase);
            var relativeSeries = new Dictionary<string, Dictionary<string, Agg>>(StringComparer.OrdinalIgnoreCase);
            var p95Series = new Dictionary<string, Dictionary<string, Agg>>(StringComparer.OrdinalIgnoreCase);
            var deltaDelay = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var deltaRelative = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var deltaMeanDelay = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var deltaProb = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);

            ForEachCsvRow(path, row =>
            {
                string gamma = NormalizeGamma(Get(row, "gamma"));
                AddSeriesAgg(delaySeries, "Modified DH B&B", gamma, Parse(Get(row, "exact_mean_delay_cvar95")));
                AddSeriesAgg(delaySeries, "Heuristic mean", gamma, Parse(Get(row, "heuristic_mean_delay_cvar95")));
                AddSeriesAgg(relativeSeries, "Modified DH B&B", gamma, Parse(Get(row, "exact_mean_relative_cvar95")));
                AddSeriesAgg(relativeSeries, "Heuristic mean", gamma, Parse(Get(row, "heuristic_mean_relative_cvar95")));
                AddSeriesAgg(p95Series, "Modified DH B&B", gamma, Parse(Get(row, "exact_mean_p95")));
                AddSeriesAgg(p95Series, "Heuristic mean", gamma, Parse(Get(row, "heuristic_mean_p95")));
                AddAgg(deltaDelay, gamma, Parse(Get(row, "delta_delay_cvar95")));
                AddAgg(deltaRelative, gamma, Parse(Get(row, "delta_relative_cvar95")));
                AddAgg(deltaMeanDelay, gamma, Parse(Get(row, "delta_mean_delay")));
                AddAgg(deltaProb, gamma, Parse(Get(row, "delta_delay_probability")));
            });

            AddMultiLineChart("DH B&B vs heuristics - Delay CVaR95 by gamma", "Compares exact-reference and heuristic mean tail risk across perturbation intensity. Separation that grows with gamma indicates that exact deterministic quality and probabilistic risk behave differently under stronger uncertainty.", delaySeries, a => a.Mean, "Mean CVaR95(L)");
            AddMultiLineChart("DH B&B vs heuristics - Relative CVaR95 by gamma", "Normalized tail-risk comparison across gamma. This is often more informative than absolute CVaR because it controls for the shorter makespan produced by exact baselines.", relativeSeries, a => a.Mean, "Mean relative CVaR95(L)");
            AddMultiLineChart("DH B&B vs heuristics - P95 by gamma", "Compares high percentile project duration between the exact reference and heuristic mean under increasing perturbation intensity.", p95Series, a => a.Mean, "Mean P95");
            AddLineChart("DH B&B vs heuristics - Δ delay CVaR95 by gamma", "Exact minus heuristic mean. Negative values favour Modified DH B&B; positive values indicate higher absolute tail risk for the exact-reference baselines.", deltaDelay, a => a.Mean, "Δ CVaR95(L)");
            AddLineChart("DH B&B vs heuristics - Δ relative CVaR95 by gamma", "Exact minus heuristic mean for normalized tail risk. This chart is central when discussing whether exact makespan quality translates into probabilistic robustness.", deltaRelative, a => a.Mean, "Δ relative CVaR95(L)");
            AddLineChart("DH B&B vs heuristics - Δ mean delay by gamma", "Exact minus heuristic mean for average delay under each perturbation intensity.", deltaMeanDelay, a => a.Mean, "Δ mean delay");
            AddLineChart("DH B&B vs heuristics - Δ delay probability by gamma", "Exact minus heuristic mean for P(L > 0).", deltaProb, a => a.Mean, "Δ P(L > 0)");
        }

        private void AddExactVsHeuristicsCrashingCharts(string path)
        {
            var frri = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var deltaMakespan = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var deltaSif = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var deltaDelay = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var deltaRelative = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var deltaUnabsorbed = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var outcomes = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);

            ForEachCsvRow(path, row =>
            {
                string family = Get(row, "method_family");
                if (string.IsNullOrWhiteSpace(family)) family = "UNKNOWN";
                AddAgg(frri, family, Parse(Get(row, "mean_frri")));
                AddAgg(deltaMakespan, family, Parse(Get(row, "mean_delta_makespan")));
                AddAgg(deltaSif, family, Parse(Get(row, "mean_delta_sif")));
                AddAgg(deltaDelay, family, Parse(Get(row, "mean_delta_delay_cvar95")));
                AddAgg(deltaRelative, family, Parse(Get(row, "mean_delta_relative_cvar95")));
                AddAgg(deltaUnabsorbed, family, Parse(Get(row, "mean_delta_unabsorbed_work")));
                AddAgg(outcomes, family + " / EFFICIENT", Parse(Get(row, "efficient_percent")));
                AddAgg(outcomes, family + " / FAST_BUT_HIGH_RISK", Parse(Get(row, "fast_but_high_risk_percent")));
                AddAgg(outcomes, family + " / ROBUST_BUT_SLOW", Parse(Get(row, "robust_but_slow_percent")));
                AddAgg(outcomes, family + " / AMBIGUOUS", Parse(Get(row, "ambiguous_percent")));
                AddAgg(outcomes, family + " / EFFICIENT_COM_TRADEOFF", Parse(Get(row, "efficient_com_tradeoff_percent")));
            });

            AddBarChart("DH B&B vs heuristics - Crashing mean FRRI by method family", "Compares whether compression scenarios built from exact-reference baselines reduce tail risk more or less than those built from heuristic baselines.", frri, a => a.Mean, "Mean FRRI");
            AddBarChart("DH B&B vs heuristics - Crashing mean Δ makespan by method family", "Negative values indicate stronger deterministic time reduction after crashing.", deltaMakespan, a => a.Mean, "Mean Δ makespan");
            AddBarChart("DH B&B vs heuristics - Crashing mean Δ SIF by method family", "Shows whether crashing tends to consume or preserve structural flexibility differently for exact-reference and heuristic baselines.", deltaSif, a => a.Mean, "Mean Δ SIF");
            AddBarChart("DH B&B vs heuristics - Crashing mean Δ delay CVaR95 by method family", "Negative values mean crashing reduced absolute delay tail risk on average.", deltaDelay, a => a.Mean, "Mean Δ CVaR95(L)");
            AddBarChart("DH B&B vs heuristics - Crashing mean Δ relative CVaR95 by method family", "Negative values mean crashing reduced normalized tail risk on average.", deltaRelative, a => a.Mean, "Mean Δ relative CVaR95(L)");
            AddBarChart("DH B&B vs heuristics - Crashing mean Δ unabsorbed work by method family", "Negative values indicate that crashing scenarios reduced the mean work not absorbed by Balance.", deltaUnabsorbed, a => a.Mean, "Mean Δ unabsorbed work");
            AddBarChart("DH B&B vs heuristics - Crashing outcome distribution by method family", "Percentage distribution of scenario classes by method family. This shows whether exact-reference baselines generate more efficient, risky or ambiguous compression outcomes.", outcomes, a => a.Mean, "Scenarios (%)");
        }

        private void AddSectionHeader(string title, string subtitle)
        {
            var group = new GroupBox
            {
                Text = title,
                Width = Math.Max(920, ClientSize.Width - 80),
                Height = 86,
                Padding = new Padding(10)
            };
            var titleLabel = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 28,
                Font = new System.Drawing.Font("Segoe UI", 11.0f, System.Drawing.FontStyle.Bold),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            var subtitleLabel = new Label
            {
                Text = subtitle,
                Dock = DockStyle.Fill,
                ForeColor = System.Drawing.Color.DimGray,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            group.Controls.Add(subtitleLabel);
            group.Controls.Add(titleLabel);
            _chartsFlow.Controls.Add(group);
        }

        private static void AddSeriesAgg(Dictionary<string, Dictionary<string, Agg>> data, string seriesName, string xKey, double value)
        {
            if (string.IsNullOrWhiteSpace(seriesName)) seriesName = "Series";
            Dictionary<string, Agg> series;
            if (!data.TryGetValue(seriesName, out series))
            {
                series = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
                data[seriesName] = series;
            }
            AddAgg(series, xKey, value);
        }

        private void AddMaxComboCharts(string logsDirectory)
        {
            string path = Path.Combine(logsDirectory ?? string.Empty, "crashing_sampling_policy.csv");
            if (!File.Exists(path)) return;

            var scenariosByMaxCombo = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var coverageByMaxCombo = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var possibleByMaxCombo = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            ForEachCsvRow(path, row =>
            {
                string maxCombo = Get(row, "max_activities_por_combinacao");
                if (string.IsNullOrWhiteSpace(maxCombo)) maxCombo = "Not recorded";
                AddAgg(scenariosByMaxCombo, maxCombo, Parse(Get(row, "total_scenarios_avaliados")));
                AddAgg(possibleByMaxCombo, maxCombo, Parse(Get(row, "total_scenarios_possiveis")));
                AddAgg(coverageByMaxCombo, maxCombo, Parse(Get(row, "percentual_coberto")));
            });


            int distinctMaxCombos = scenariosByMaxCombo.Keys
                .Concat(possibleByMaxCombo.Keys)
                .Concat(coverageByMaxCombo.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            if (distinctMaxCombos <= 1)
            {
                AppendChartsNote("Crashing policy - Max Activities/combo charts skipped: the run used a single fixed value (" +
                    (scenariosByMaxCombo.Keys.FirstOrDefault() ?? "n/a") +
                    "), so a per-combo comparison would show only one bar with no useful contrast.");
                return;
            }

            AddBarChart("Crashing policy - Evaluated scenarios by Max Activities/combo", "This is the key chart for detecting when Max Activities/combo changed the crashing search space.", scenariosByMaxCombo, a => a.Sum, "Evaluated scenarios");
            AddBarChart("Crashing policy - Possible scenarios by Max Activities/combo", "Shows the combinatorial growth before sampling limits are applied.", possibleByMaxCombo, a => a.Sum, "Possible scenarios");
            AddBarChart("Crashing policy - Mean coverage by Max Activities/combo", "Shows how much of the possible crashing space was actually evaluated.", coverageByMaxCombo, a => a.Mean, "Mean coverage (%)");
        }

        private void AddCorrelationCharts(string consolidated)
        {
            string path = Path.Combine(consolidated, "correlacoes.csv");
            if (!File.Exists(path)) return;

            var correlations = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            ForEachCsvRow(path, row =>
            {
                string group = Get(row, "group");
                string x = Get(row, "x_variable");
                string y = Get(row, "y_variable");
                string label = group + " | " + x + " vs " + y;
                AddAgg(correlations, label, Parse(Get(row, "spearman")));
            });
            AddBarChart("Correlation - Spearman coefficients", "Quick audit of confirmatory and exploratory relationships used in the thesis.", correlations, a => a.Mean, "Spearman");
        }


        private void AddStratifiedRiskCharts(string consolidated)
        {
            string path = Path.Combine(consolidated, "correlacoes_rfrs_estratificadas.csv");
            if (!File.Exists(path)) return;

            var relativeByStratum = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var delayByStratum = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);

            ForEachCsvRow(path, row =>
            {
                string stratum = Get(row, "stratum");
                AddAgg(relativeByStratum, stratum, Parse(Get(row, "spearman_sif_relative_cvar95")));
                AddAgg(delayByStratum, stratum, Parse(Get(row, "spearman_sif_delay_cvar95")));
            });

            AddBarChart("Stratified - Spearman(SIF, relative CVaR95) by RF/RS stratum",
                "Splits the SIF-versus-relative-CVaR95 correlation by resource strength (RS_LOW/RS_HIGH) versus the pooled value (ALL). If the bars point in different directions, the pooled coefficient shown in the Correlation block hides opposite local relationships and the proposition's support depends on the instance profile.",
                relativeByStratum, a => a.Mean, "Spearman");
            AddBarChart("Stratified - Spearman(SIF, delay CVaR95) by RF/RS stratum",
                "Same robustness check using delay CVaR95 as the risk measure. Compare the sign and magnitude of RS_LOW versus RS_HIGH against the pooled (ALL) bar.",
                delayByStratum, a => a.Mean, "Spearman");
        }


        private void AddPropositionCharts(string consolidated)
        {
            string path = Path.Combine(consolidated, "avaliacao_proposition.csv");
            if (!File.Exists(path)) return;

            var observedByCriterion = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var classifications = new List<string>();

            ForEachCsvRow(path, row =>
            {
                string criterion = Get(row, "criterion");
                AddAgg(observedByCriterion, criterion, Parse(Get(row, "observed_result")));
                string classification = Get(row, "classification");
                if (!string.IsNullOrWhiteSpace(classification) && !classifications.Contains(classification))
                    classifications.Add(classification);
            });

            string verdict = classifications.Count > 0
                ? " The pipeline's own automatic classification for these criteria is: " + string.Join(", ", classifications) + " -- a result that should be reported and discussed explicitly in Chapter 4's conclusions, not only the qualitative statement currently drafted there."
                : string.Empty;
            AddBarChart("Proposition - Observed Spearman coefficients for the central proposition",
                "Each bar is the Spearman coefficient the pipeline measured for one criterion of the thesis's central proposition (avaliacao_proposition.csv). A coefficient close to zero or positive does not support 'higher SIF -> lower tail risk' as stated; only a clearly negative value would." + verdict,
                observedByCriterion, a => a.Mean, "Spearman");
        }


        private void AddBalanceDiagnosticsCharts(string consolidated)
        {
            string path = Path.Combine(consolidated, "todos_frm.csv");
            if (!File.Exists(path)) return;

            var minBalanceByClass = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            var sifVsMinBalance = new List<Point2>();

            ForEachCsvRow(path, row =>
            {
                string classification = Get(row, "structural_classification");
                double sif = Parse(Get(row, "sif_global"));
                double minBalance = Parse(Get(row, "min_balance_global"));
                AddAgg(minBalanceByClass, classification, minBalance);
                AddSample(sifVsMinBalance, new Point2 { X = sif, Y = minBalance, Label = Get(row, "baseline_id"), Class = classification }, 5000);
            });

            AddBarChart("FRM/SIF - Mean minimum global balance by structural classification",
                "Checks whether the FRM's own structural classes (e.g. robust/fragile) line up with the minimum balance observed across resources -- the indicator the thesis singles out (Sec. 3, para. 1149) as able to reveal localized vulnerability that an aggregate SIF can mask.",
                minBalanceByClass, a => a.Mean, "Mean min balance");
            AddScatterChart("FRM/SIF - SIF global versus minimum global balance",
                "Direct visualization of the thesis's own caveat: do baselines with similar (even high) SIF still show very different, sometimes negative, minimum balances? Points low on the Y axis flag baselines whose admissibility margin (balance >= 0) is structurally thin regardless of their aggregate SIF.",
                "SIF global", "Minimum global balance", sifVsMinBalance);
        }

        private void AddBarChart(string title, string description, Dictionary<string, Agg> data, Func<Agg, double> selector, string yTitle)
        {
            var ordered = data.Where(k => k.Value != null && k.Value.Count > 0)
                .OrderBy(k => NaturalKey(k.Key))
                .Take(40)
                .ToList();
            if (ordered.Count == 0) return;

            bool horizontal = ordered.Count > 12;


            int height = horizontal ? Math.Max(360, 26 * ordered.Count + 90) : 360;

            var chart = CreateBaseChart(title, description, yTitle, height);
            Series s = chart.Series.Add(yTitle);
            s.ChartType = horizontal ? SeriesChartType.Bar : SeriesChartType.Column;


            s["PointWidth"] = "0.85";
            foreach (var kv in ordered)
                s.Points.AddXY(kv.Key, selector(kv.Value));
            ConfigureValueLabels(s);
            ConfigureValueAxisPadding(chart, s);
            ConfigureAxisForCategories(chart, ordered.Count);
            AddChartToFlow(title, description, chart);
        }


        private void AddMultiLineChart(string title, string description, Dictionary<string, Dictionary<string, Agg>> dataBySeries, Func<Agg, double> selector, string yTitle)
        {
            if (dataBySeries == null || dataBySeries.Count == 0)
                return;

            var xKeys = dataBySeries.Values
                .Where(d => d != null)
                .SelectMany(d => d.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(k => Parse(k))
                .ToList();
            if (xKeys.Count == 0)
                return;

            var chart = CreateBaseChart(title, description, yTitle);
            int seriesIndex = 0;
            foreach (var seriesEntry in dataBySeries.OrderBy(k => k.Key))
            {
                if (seriesEntry.Value == null || seriesEntry.Value.Count == 0)
                    continue;
                Series s = chart.Series.Add(seriesEntry.Key);
                s.ChartType = SeriesChartType.Line;
                s.MarkerStyle = MarkerStyle.Circle;
                s.MarkerSize = 7;
                s.BorderWidth = 2;
                if (seriesIndex % 2 == 1)
                    s.BorderDashStyle = ChartDashStyle.Dash;
                foreach (string x in xKeys)
                {
                    Agg agg;
                    if (seriesEntry.Value.TryGetValue(x, out agg) && agg != null && agg.Count > 0)
                        s.Points.AddXY(x, selector(agg));
                }
                ConfigureValueLabels(s);
                seriesIndex++;
            }

            if (chart.Series.Count == 0)
                return;

            ConfigureValueAxisPadding(chart, chart.Series[0]);
            ConfigureAxisForCategories(chart, xKeys.Count);
            AddChartToFlow(title, description, chart);
        }

        private void AddLineChart(string title, string description, Dictionary<string, Agg> data, Func<Agg, double> selector, string yTitle)
        {
            var ordered = data.Where(k => k.Value != null && k.Value.Count > 0)
                .OrderBy(k => Parse(k.Key))
                .ToList();
            if (ordered.Count == 0) return;

            var chart = CreateBaseChart(title, description, yTitle);
            Series s = chart.Series.Add(yTitle);
            s.ChartType = SeriesChartType.Line;
            s.MarkerStyle = MarkerStyle.Circle;
            s.MarkerSize = 7;
            s.BorderWidth = 2;
            foreach (var kv in ordered)
                s.Points.AddXY(kv.Key, selector(kv.Value));
            ConfigureValueLabels(s);
            ConfigureValueAxisPadding(chart, s);
            AddChartToFlow(title, description, chart);
        }

        private static void ConfigureValueLabels(Series series)
        {
            if (series == null) return;

            series.IsValueShownAsLabel = true;
            series.LabelFormat = "0.###";
            series.SmartLabelStyle.Enabled = true;
            series.SmartLabelStyle.AllowOutsidePlotArea = LabelOutsidePlotAreaStyle.Partial;
            series.SmartLabelStyle.IsOverlappedHidden = false;
            series.Font = new System.Drawing.Font("Segoe UI", 8.0f, System.Drawing.FontStyle.Regular);


            if (series.ChartType == SeriesChartType.Column || series.ChartType == SeriesChartType.Bar)
                series["BarLabelStyle"] = "Outside";
            foreach (DataPoint point in series.Points)
                point.Label = "#VALY{0.###}";
        }

        private static void ConfigureValueAxisPadding(Chart chart, Series series)
        {
            if (chart == null || chart.ChartAreas.Count == 0 || series == null || series.Points.Count == 0)
                return;

            var values = series.Points.Select(p => p.YValues != null && p.YValues.Length > 0 ? p.YValues[0] : 0.0).ToList();
            double min = values.Min();
            double max = values.Max();
            double span = Math.Abs(max - min);
            double magnitude = Math.Max(Math.Abs(min), Math.Abs(max));


            double pad = Math.Max(span * 0.15, magnitude * 0.12);
            if (pad <= 0) pad = magnitude > 0 ? magnitude * 0.1 : 0.1;
            var area = chart.ChartAreas[0];

            if (series.ChartType == SeriesChartType.Bar)
            {
                if (max >= 0) area.AxisX.Maximum = max + pad;
                if (min < 0) area.AxisX.Minimum = min - pad;
            }
            else
            {
                if (max >= 0) area.AxisY.Maximum = max + pad;
                if (min < 0) area.AxisY.Minimum = min - pad;
            }
        }

        private void AddScatterChart(string title, string description, string xTitle, string yTitle, List<Point2> points)
        {
            AddScatterChart(title, description, xTitle, yTitle, points, null);
        }

        private void AddScatterChart(string title, string description, string xTitle, string yTitle, List<Point2> pointsA, List<Point2> pointsB)
        {
            AddScatterChart(title, description, xTitle, yTitle, pointsA, "Dominated", pointsB, "Non-dominated");
        }

        private void AddScatterChart(string title, string description, string xTitle, string yTitle, List<Point2> pointsA, string nameA, List<Point2> pointsB, string nameB)
        {
            if ((pointsA == null || pointsA.Count == 0) && (pointsB == null || pointsB.Count == 0))
                return;

            var chart = CreateBaseChart(title, description, yTitle);
            ChartArea area = chart.ChartAreas[0];
            area.AxisX.Title = xTitle;
            area.AxisY.Title = yTitle;

            if (pointsA != null && pointsA.Count > 0)
            {
                Series s = chart.Series.Add(pointsB == null ? yTitle : nameA);
                s.ChartType = SeriesChartType.FastPoint;
                s.MarkerSize = 4;
                foreach (var p in pointsA)
                    s.Points.AddXY(p.X, p.Y);
            }
            if (pointsB != null && pointsB.Count > 0)
            {
                Series s2 = chart.Series.Add(nameB);
                s2.ChartType = SeriesChartType.Point;
                s2.MarkerSize = 6;
                foreach (var p in pointsB)
                    s2.Points.AddXY(p.X, p.Y);
            }
            AddChartToFlow(title, description, chart);
        }

        private Chart CreateBaseChart(string title, string description, string yTitle)
        {
            return CreateBaseChart(title, description, yTitle, 360);
        }

        private Chart CreateBaseChart(string title, string description, string yTitle, int height)
        {
            int width = Math.Max(920, ClientSize.Width - 80);
            int effectiveHeight = height;

            var chart = new Chart
            {
                Width = width,
                Height = effectiveHeight,
                Text = title,
                BorderlineDashStyle = ChartDashStyle.Solid,
                BorderlineWidth = 1,
                BorderlineColor = System.Drawing.Color.Gainsboro,
                BackColor = System.Drawing.Color.White,
                Palette = ChartColorPalette.BrightPastel,
                AntiAliasing = AntiAliasingStyles.All,
                TextAntiAliasingQuality = TextAntiAliasingQuality.High
            };
            var area = new ChartArea("Main");
            area.BackColor = System.Drawing.Color.White;
            area.BorderColor = System.Drawing.Color.Gainsboro;
            area.BorderDashStyle = ChartDashStyle.Solid;
            area.AxisX.MajorGrid.Enabled = false;
            area.AxisY.MajorGrid.LineColor = System.Drawing.Color.Gainsboro;
            area.AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            area.AxisX.LineColor = System.Drawing.Color.DimGray;
            area.AxisY.LineColor = System.Drawing.Color.DimGray;
            area.AxisX.LabelStyle.Angle = -35;
            area.AxisX.LabelStyle.Font = new System.Drawing.Font("Segoe UI", 8.0f);
            area.AxisY.LabelStyle.Font = new System.Drawing.Font("Segoe UI", 8.0f);
            area.AxisX.TitleFont = new System.Drawing.Font("Segoe UI", 9.0f, System.Drawing.FontStyle.Bold);
            area.AxisY.TitleFont = new System.Drawing.Font("Segoe UI", 9.0f, System.Drawing.FontStyle.Bold);
            area.AxisY.Title = yTitle;


            area.AxisX.ScaleView.Zoomable = true;
            area.AxisY.ScaleView.Zoomable = true;
            area.AxisX.ScrollBar.Enabled = true;
            area.AxisX.ScrollBar.IsPositionedInside = true;
            area.AxisX.ScrollBar.ButtonStyle = ScrollBarButtonStyles.SmallScroll;
            area.AxisY.ScrollBar.Enabled = true;
            area.AxisY.ScrollBar.IsPositionedInside = true;
            area.AxisY.ScrollBar.ButtonStyle = ScrollBarButtonStyles.SmallScroll;

            chart.ChartAreas.Add(area);
            chart.Titles.Add(new Title(title, Docking.Top, new System.Drawing.Font("Segoe UI", 11.0f, System.Drawing.FontStyle.Bold), System.Drawing.Color.FromArgb(32, 32, 32)));
            chart.Legends.Add(new Legend("Legend")
            {
                Docking = Docking.Bottom,
                BackColor = System.Drawing.Color.Transparent,
                Font = new System.Drawing.Font("Segoe UI", 8.0f)
            });
            AttachExperimentalChartExportContextMenu(chart);
            AttachDoubleClickToResetZoom(chart);
            return chart;
        }


        private static void AttachDoubleClickToResetZoom(Chart chart)
        {
            if (chart == null) return;
            chart.MouseDoubleClick += (s, e) =>
            {
                foreach (ChartArea area in chart.ChartAreas)
                {
                    area.AxisX.ScaleView.ZoomReset();
                    area.AxisY.ScaleView.ZoomReset();
                }
            };
        }

        private void ConfigureAxisForCategories(Chart chart, int count)
        {
            if (chart == null || chart.ChartAreas.Count == 0 || chart.Series.Count == 0) return;
            var area = chart.ChartAreas[0];
            var series = chart.Series[0];


            bool horizontal = series.ChartType == SeriesChartType.Bar;
            var categoryAxis = horizontal ? area.AxisY : area.AxisX;


            categoryAxis.Interval = 1;
            categoryAxis.IntervalType = DateTimeIntervalType.Number;
            categoryAxis.IntervalAutoMode = IntervalAutoMode.FixedCount;

            if (horizontal)
            {
                categoryAxis.LabelStyle.Angle = 0;
                categoryAxis.LabelStyle.Font = new System.Drawing.Font("Segoe UI", count > 25 ? 7.0f : 8.0f);
            }
            else if (count > 12)
            {
                categoryAxis.LabelStyle.Angle = 0;
                categoryAxis.LabelStyle.Font = new System.Drawing.Font("Segoe UI", 7.5f);
            }
            else
            {
                categoryAxis.LabelStyle.Angle = -35;
                categoryAxis.LabelStyle.Font = new System.Drawing.Font("Segoe UI", 8.0f);
            }
        }


        private void AppendChartsNote(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var group = new GroupBox
            {
                Text = "Note",
                Width = 480,
                Height = 90,
                Padding = new Padding(8)
            };
            var label = new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            group.Controls.Add(label);
            _chartsFlow.Controls.Add(group);
        }

        private void AddChartToFlow(string title, string description, Chart chart)
        {
            var group = new GroupBox
            {
                Text = title,
                Width = Math.Max(480, chart.Width + 40),
                Height = Math.Max(280, chart.Height + 70),
                Padding = new Padding(8)
            };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var label = new Label { Text = description, Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, AutoEllipsis = true };
            chart.Dock = DockStyle.Fill;
            layout.Controls.Add(label, 0, 0);
            layout.Controls.Add(chart, 0, 1);
            group.Controls.Add(layout);
            AttachManualResizeGrip(group, chart);
            _chartsFlow.Controls.Add(group);
        }


        private static void AttachManualResizeGrip(GroupBox group, Chart chart)
        {
            const int handleThickness = 6;

            var rightHandle = new Panel
            {
                Width = handleThickness,
                Cursor = Cursors.SizeWE,
                BackColor = System.Drawing.Color.Silver,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right
            };
            var bottomHandle = new Panel
            {
                Height = handleThickness,
                Cursor = Cursors.SizeNS,
                BackColor = System.Drawing.Color.Silver,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            Action layoutHandles = () =>
            {
                rightHandle.Location = new System.Drawing.Point(group.ClientSize.Width - handleThickness, 0);
                rightHandle.Height = group.ClientSize.Height;
                bottomHandle.Location = new System.Drawing.Point(0, group.ClientSize.Height - handleThickness);
                bottomHandle.Width = group.ClientSize.Width;
            };
            layoutHandles();

            bool draggingRight = false;
            bool draggingBottom = false;


            rightHandle.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                draggingRight = true;
                rightHandle.Capture = true;
            };
            rightHandle.MouseMove += (s, e) =>
            {
                if (!draggingRight) return;
                var mouseScreen = rightHandle.PointToScreen(e.Location);
                int chartLeftScreen = chart.PointToScreen(System.Drawing.Point.Empty).X;
                int desiredChartWidth = Math.Max(360, mouseScreen.X - chartLeftScreen);
                group.Width = Math.Max(480, desiredChartWidth + 40);
                layoutHandles();
            };
            rightHandle.MouseUp += (s, e) =>
            {
                draggingRight = false;
                rightHandle.Capture = false;
            };

            bottomHandle.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                draggingBottom = true;
                bottomHandle.Capture = true;
            };
            bottomHandle.MouseMove += (s, e) =>
            {
                if (!draggingBottom) return;
                var mouseScreen = bottomHandle.PointToScreen(e.Location);
                int chartTopScreen = chart.PointToScreen(System.Drawing.Point.Empty).Y;
                int desiredChartHeight = Math.Max(220, mouseScreen.Y - chartTopScreen);
                group.Height = Math.Max(280, desiredChartHeight + 70);
                layoutHandles();
            };
            bottomHandle.MouseUp += (s, e) =>
            {
                draggingBottom = false;
                bottomHandle.Capture = false;
            };

            group.Controls.Add(rightHandle);
            group.Controls.Add(bottomHandle);
            rightHandle.BringToFront();
            bottomHandle.BringToFront();
        }

        private void AttachExperimentalChartExportContextMenu(Chart chart)
        {
            var menu = new ContextMenuStrip();
            var export = new ToolStripMenuItem("Save chart as PNG + XLSX...");
            export.Click += (s, e) => SaveExperimentalChartAsPngAndXlsx(chart);
            menu.Items.Add(export);
            chart.ContextMenuStrip = menu;
        }

        private void SaveExperimentalChartAsPngAndXlsx(Chart chart)
        {
            if (chart == null) return;
            try
            {
                using (var dlg = new SaveFileDialog())
                {
                    dlg.Title = "Save chart as PNG + XLSX";
                    dlg.Filter = "Excel Workbook (*.xlsx)|*.xlsx";
                    dlg.DefaultExt = "xlsx";
                    dlg.AddExtension = true;
                    dlg.FileName = MakeSafeFileName(chart.Text) + ".xlsx";
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return;

                    string xlsxPath = dlg.FileName;
                    string pngPath = Path.ChangeExtension(xlsxPath, ".png");
                    chart.SaveImage(pngPath, ChartImageFormat.Png);
                    var sheets = RCPSP.WinForms.XlsxExportHelper.BuildSheetsFromChart(chart.Text, chart);
                    RCPSP.WinForms.XlsxExportHelper.SaveWorkbook(xlsxPath, sheets);
                    MessageBox.Show(this, "Export completed successfully.\r\n\r\nPNG:\r\n" + pngPath + "\r\n\r\nXLSX:\r\n" + xlsxPath, "RCPSP-FRM", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not save the chart as PNG + XLSX.\r\n\r\n" + ex.Message, "RCPSP-FRM", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string AutoSaveAllExperimentalCharts(string outputDirectory)
        {
            string chartsRoot = Path.Combine(outputDirectory, "04_charts");
            string chartsDirectory = Path.Combine(chartsRoot, "analysis_charts");
            Directory.CreateDirectory(chartsDirectory);

            int index = 1;
            foreach (Chart chart in EnumerateExperimentalCharts())
            {
                SaveExperimentalChartFiles(chart, chartsDirectory, index);
                index++;
            }

            WriteChartExportManifest(chartsDirectory, index - 1);
            return chartsDirectory;
        }

        private IEnumerable<Chart> EnumerateExperimentalCharts()
        {
            foreach (Control group in _chartsFlow.Controls)
            {
                foreach (Chart chart in EnumerateChartsRecursive(group))
                    yield return chart;
            }
        }

        private static IEnumerable<Chart> EnumerateChartsRecursive(Control parent)
        {
            if (parent == null)
                yield break;

            var chart = parent as Chart;
            if (chart != null)
                yield return chart;

            foreach (Control child in parent.Controls)
            {
                foreach (Chart nested in EnumerateChartsRecursive(child))
                    yield return nested;
            }
        }

        private void SaveExperimentalChartFiles(Chart chart, string chartsDirectory, int index)
        {
            if (chart == null)
                return;

            string baseName = index.ToString("000", CultureInfo.InvariantCulture) + "_" + MakeSafeFileName(chart.Text);
            string pngPath = Path.Combine(chartsDirectory, baseName + ".png");
            string xlsxPath = Path.Combine(chartsDirectory, baseName + ".xlsx");

            chart.SaveImage(pngPath, ChartImageFormat.Png);
            var sheets = RCPSP.WinForms.XlsxExportHelper.BuildSheetsFromChart(chart.Text, chart);
            RCPSP.WinForms.XlsxExportHelper.SaveWorkbook(xlsxPath, sheets);
        }

        private static void WriteChartExportManifest(string chartsDirectory, int chartCount)
        {
            string manifestPath = Path.Combine(chartsDirectory, "charts_export_manifest.txt");
            var lines = new List<string>
            {
                "Experimental analysis charts auto-export",
                "Generated at: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                "Charts exported: " + chartCount.ToString(CultureInfo.InvariantCulture),
                "Each chart is saved as a PNG image and an XLSX workbook with the underlying chart series data.",
                "Folder: " + chartsDirectory
            };
            File.WriteAllLines(manifestPath, lines, Encoding.UTF8);
        }

        private static void AddAgg(Dictionary<string, Agg> dict, string key, double value)
        {
            if (string.IsNullOrWhiteSpace(key)) key = "Unknown";
            Agg agg;
            if (!dict.TryGetValue(key, out agg))
            {
                agg = new Agg();
                dict[key] = agg;
            }
            agg.Add(value);
        }

        private static void AddSample(List<Point2> sample, Point2 point, int max)
        {
            if (point == null || sample == null || double.IsNaN(point.X) || double.IsNaN(point.Y))
                return;
            if (sample.Count < max)
            {
                sample.Add(point);
                return;
            }

            int idx = Math.Abs((point.Label ?? string.Empty).GetHashCode()) % max;
            if (idx >= 0 && idx < sample.Count)
                sample[idx] = point;
        }

        private static string Get(Dictionary<string, string> row, string key)
        {
            string value;
            return row != null && row.TryGetValue(key, out value) ? value : string.Empty;
        }

        private static double FirstNumber(Dictionary<string, string> row, params string[] keys)
        {
            if (keys == null) return 0.0;
            foreach (string key in keys)
            {
                string text = Get(row, key);
                if (string.IsNullOrWhiteSpace(text))
                    continue;
                return Parse(text);
            }
            return 0.0;
        }

        private static double Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0.0;
            double value;
            if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) return value;
            if (double.TryParse(text, NumberStyles.Any, CultureInfo.GetCultureInfo("pt-BR"), out value)) return value;
            return 0.0;
        }

        private static string NormalizeGamma(string gamma)
        {
            return Parse(gamma).ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string Bin(double value, double width)
        {
            if (width <= 0) width = 1.0;
            double start = Math.Floor(value / width) * width;
            double end = start + width;
            return start.ToString("0.##", CultureInfo.InvariantCulture) + "-" + end.ToString("0.##", CultureInfo.InvariantCulture);
        }


        private static Dictionary<string, Agg> BuildAdaptiveDistribution(IEnumerable<double> values, int desiredBins = 14)
        {
            var result = new Dictionary<string, Agg>(StringComparer.OrdinalIgnoreCase);
            if (values == null) return result;
            var clean = values.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();
            if (clean.Count == 0) return result;

            double min = clean.Min();
            double max = clean.Max();
            double range = max - min;
            double width = range > 0 ? NiceBinWidth(range / Math.Max(1, desiredBins)) : 1.0;

            foreach (double v in clean)
                AddAgg(result, Bin(v, width), 1);
            return result;
        }


        private static double NiceBinWidth(double raw)
        {
            if (raw <= 0 || double.IsNaN(raw) || double.IsInfinity(raw)) return 1.0;
            double exponent = Math.Floor(Math.Log10(raw));
            double fraction = raw / Math.Pow(10, exponent);
            double niceFraction = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10;
            return niceFraction * Math.Pow(10, exponent);
        }

        private static string InferHeuristicFromBaselineId(string baselineId)
        {
            if (string.IsNullOrWhiteSpace(baselineId)) return "Unknown";
            string upper = baselineId.ToUpperInvariant();
            if (upper.IndexOf("MODIFIED_DH") >= 0 || upper.IndexOf("DH_B") >= 0) return "Modified DH B&B";
            string[] names = { "GRWC", "MSLK", "SPT", "LPT", "EST", "EFT", "LST", "LFT", "MIS", "MTS" };
            foreach (string name in names)
            {
                if (upper.IndexOf("_" + name + "_") >= 0 || upper.EndsWith("_" + name))
                    return name;
            }
            return "Unknown";
        }

        private static string NaturalKey(string key)
        {
            if (key == null) return string.Empty;
            string first = key;


            int dash = key.IndexOf('-', 1);
            if (dash > 0) first = key.Substring(0, dash);
            double numeric;
            if (double.TryParse(first, NumberStyles.Any, CultureInfo.InvariantCulture, out numeric))
                return numeric.ToString("0000000000.000000", CultureInfo.InvariantCulture);
            return key;
        }

        private static string MakeSafeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "ExperimentalChart";
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            foreach (char ch in value)
                builder.Append(invalid.Contains(ch) ? '_' : ch);
            string result = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(result) ? "ExperimentalChart" : result;
        }

        private static void ForEachCsvRow(string path, Action<Dictionary<string, string>> action)
        {
            if (!File.Exists(path) || action == null) return;
            using (var reader = new StreamReader(path, Encoding.UTF8, true))
            {
                string headerLine = reader.ReadLine();
                if (headerLine == null) return;
                var headers = ParseCsvLine(headerLine).ToArray();
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length == 0) continue;
                    var values = ParseCsvLine(line).ToArray();
                    var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    int n = Math.Min(headers.Length, values.Length);
                    for (int i = 0; i < n; i++)
                    {
                        if (!row.ContainsKey(headers[i]))
                            row[headers[i]] = values[i];
                    }
                    action(row);
                }
            }
        }

        private static IEnumerable<string> ParseCsvLine(string line)
        {
            if (line == null) yield break;
            var sb = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (quoted)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++;
                        }
                        else
                        {
                            quoted = false;
                        }
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                }
                else
                {
                    if (ch == '"') quoted = true;
                    else if (ch == ',')
                    {
                        yield return sb.ToString();
                        sb.Length = 0;
                    }
                    else sb.Append(ch);
                }
            }
            yield return sb.ToString();
        }
    }
}
