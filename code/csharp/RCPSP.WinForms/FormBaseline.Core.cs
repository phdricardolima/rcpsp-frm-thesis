using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using RCPSP.Application;
using RCPSP.Contracts;
using RCPSP.Infrastructure.Cpu;
using System.Drawing;
using System.Windows.Forms.DataVisualization.Charting;

namespace RCPSP.WinForms
{
    public partial class FormBaseline : Form
    {
        private bool _isComputingAllExact;

        private async void FormBaseline_Shown(object sender, EventArgs e)
        {
            if (_request == null)
                return;

            PopulateRequestHeader();
            PopulateProjectGrids();
            InitializeEmptyResultAreas();

            if (_pipelineRunner == null)
            {
                SetStatus("Ready", "No pipeline", "Preview only", 0);
                return;
            }
            BeginInvoke(new Action(AdjustRiskSplitsSafe));
            await ExecutePipelineAsync("Initial run");
        }

        private async Task ExecutePipelineAsync(string runLabel)
        {
            if (_isRunning)
                return;

            try
            {
                _isRunning = true;

                await RunOnUiThreadAsync(() =>
                {
                    ToggleActionButtons(false);
                    ApplyCrashingParameterControlsToRequest();
                    SetStatus("Running", runLabel, "Preparing request", 10);
                });

                await Task.Yield();

                await RunOnUiThreadAsync(() =>
                {
                    SetStatus("Running", runLabel, "Executing pipeline", 35);
                });

                ExecutionSummary summary = IsSingleExactSchedulingMode()
                    ? CreatePendingSingleExactSummary()
                    : await Task.Run(() => _pipelineRunner.Run(_request));

                await RunOnUiThreadAsync(() =>
                {
                    SetStatus("Running", runLabel, "Binding results", 80);

                    _currentSummary = summary ?? new ExecutionSummary();
                    _runs = _currentSummary.BaselineRuns ?? new List<BaselineRunSummaryDto>();

                    PopulateSummary(_currentSummary);

                    int selectedIndex = _currentSummary.SelectedBaselineRunIndex;
                    if (_runs.Count > 0)
                    {
                        if (IsAllSchedulingMode() || IsSingleExactSchedulingMode())
                        {


                            if (tabControlMain != null && tabPageResults != null)
                                tabControlMain.SelectedTab = tabPageResults;

                            if (gridResultados != null)
                            {
                                gridResultados.Visible = true;
                                gridResultados.BringToFront();
                                gridResultados.Refresh();
                            }

                            if (IsSingleExactSchedulingMode())
                            {
                                _pendingExactRunIndexToLoadAfterCompletion = 0;
                                SelectResultsRow(0);
                            }
                        }
                        else
                        {
                            if (selectedIndex < 0 || selectedIndex >= _runs.Count)
                                selectedIndex = FindFirstSuccessfulRunIndex(_runs);

                            if (selectedIndex < 0)
                                selectedIndex = 0;

                            SelectResultsRow(selectedIndex);
                        }
                    }

                    bool deferredExactMode = IsAllSchedulingMode() || IsSingleExactSchedulingMode();
                    string statusMessage = IsAllSchedulingMode()
                        ? "Results: heuristics ready; calculating Modified DH B&B"
                        : IsSingleExactSchedulingMode()
                            ? "Results: Modified DH B&B queued; calculating exact baseline"
                            : "Pipeline executed";
                    SetStatus(deferredExactMode ? "Running" : "Ready", runLabel, statusMessage, deferredExactMode ? 85 : 100);
                });

                if (IsAllSchedulingMode() || IsSingleExactSchedulingMode())
                    await ComputeAllExactRowAsync(runLabel);
            }
            catch (Exception ex)
            {
                await RunOnUiThreadAsync(() =>
                {
                    SetStatus("Error", runLabel, "Execution failed", 0);

                    MessageBox.Show(
                        this,
                        ex.ToString(),
                        "RCPSP-FRM",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                });
            }
            finally
            {
                _isRunning = false;
                await RunOnUiThreadAsync(() => ToggleActionButtons(true));
            }
        }

        private void PopulateRequestHeader()
        {
            Text = string.Format("RCPSP-FRM | {0}", _request.Project.ProjectName);
            lblProjetoValor.Text = Safe(_request.Project.ProjectName);
            lblAtividadesValor.Text = (_request.Project.Activities ?? new List<ActivityDto>()).Count.ToString();
            lblRecursosValor.Text = (_request.Project.Resources ?? new List<ResourceDto>()).Count.ToString();

            string heuristic = _request.Scheduling != null ? Safe(_request.Scheduling.Heuristic) : string.Empty;
            lblHeuristicaValor.Text = heuristic;

            if (IsAllSchedulingMode())
            {
                lblEsquemaValor.Text = "ALL";
                lblDirecaoValor.Text = "ALL";
            }
            else if (IsModifiedDhBranchAndBoundLabel(heuristic))
            {
                lblEsquemaValor.Text = "-";
                lblDirecaoValor.Text = "-";
            }
            else
            {
                lblEsquemaValor.Text = _request.Scheduling != null ? Safe(_request.Scheduling.Scheme) : "-";
                lblDirecaoValor.Text = _request.Scheduling != null ? Safe(_request.Scheduling.Direction) : "-";
            }

            lblFlexPositivaValor.Text = _request.Frm.PositiveFlexibilityPercent + "%";
            lblFlexNegativaValor.Text = _request.Frm.NegativeFlexibilityPercent + "%";
            lblMonteCarloValor.Text = _request.Risk.ScenarioCount.ToString();
        }

        private void PopulateProjectGrids()
        {
            gridAtividades.AutoGenerateColumns = true;
            gridAtividades.DataSource = (_request.Project.Activities ?? new List<ActivityDto>())
                .Select(a => new
                {
                    a.Id,
                    a.OriginalId,
                    a.Name,
                    a.DurationDays,
                    a.RemainingDurationDays,
                    a.ExecutionState,
                    Predecessors = JoinInts(a.PredecessorIds),
                    Successors = JoinInts(a.SuccessorIds),
                    Assignments = JoinAssignments(a.Assignments)
                })
                .OrderBy(x => x.Id)
                .ToList();

            gridRecursos.AutoGenerateColumns = true;
            gridRecursos.DataSource = (_request.Project.Resources ?? new List<ResourceDto>())
                .Select(r => new
                {
                    r.Id,
                    r.Name,
                    r.Capacity
                })
                .OrderBy(x => x.Id)
                .ToList();

            ApplyUserGridPreferencesToAllGrids();
        }

        private void InitializeEmptyResultAreas()
        {

            if (gridResultados != null)
            {
                gridResultados.AutoGenerateColumns = true;
                gridResultados.DataSource = null;
                gridResultados.Rows.Clear();
                gridResultados.Columns.Clear();
            }


            if (_gridFrm != null)
            {
                _gridFrm.AutoGenerateColumns = false;
                _gridFrm.DataSource = null;
                _gridFrm.Rows.Clear();
                _gridFrm.Columns.Clear();
            }

            if (_lblFrmSummary != null)
                _lblFrmSummary.Text = "FRM not executed.";


            if (_lblRiskSummary != null)
                _lblRiskSummary.Text = string.Empty;

            if (_gridRisk != null)
            {
                _gridRisk.AutoGenerateColumns = false;
                _gridRisk.DataSource = null;
                _gridRisk.Rows.Clear();
                _gridRisk.Columns.Clear();
            }

            if (_gridRiskMetrics != null)
            {
                _gridRiskMetrics.DataSource = null;
                _gridRiskMetrics.Rows.Clear();
                _gridRiskMetrics.Columns.Clear();
            }

            if (_gridRiskBins != null)
            {
                _gridRiskBins.DataSource = null;
                _gridRiskBins.Rows.Clear();
                _gridRiskBins.Columns.Clear();
            }

            if (_chartRiskHistogram != null)
            {
                if (_chartRiskHistogram.Series.Count > 0)
                    _chartRiskHistogram.Series[0].Points.Clear();

                _chartRiskHistogram.Titles.Clear();

                if (_chartRiskHistogram.ChartAreas.Count > 0)
                    _chartRiskHistogram.ChartAreas[0].AxisX.StripLines.Clear();
            }


            _comparisonRows = new BindingList<ComparisonRowView>();
            _comparisonAnalysesByRunIndex = new Dictionary<int, ExecutionSummary>();
            if (_gridComparisonGrid != null)
            {
                _gridComparisonGrid.DataSource = null;
                _gridComparisonGrid.Rows.Clear();
                _gridComparisonGrid.Columns.Clear();
            }
            ClearComparisonChart();
            if (_lblComparisonSummary != null)
                _lblComparisonSummary.Text = "Comparison not executed yet. Load a baseline, run crashing scenarios, and compare S0 against its Sj scenarios.";

            UpdateStageTimingsView(null);
        }

        private void UpdateStageTimingsView(ExecutionSummary summary)
        {
            if (_txtStageTimings == null)
                return;

            _txtStageTimings.Text = FormatStageTimings(summary);
            _txtStageTimings.SelectionStart = 0;
            _txtStageTimings.SelectionLength = 0;
        }

        private string FormatStageTimings(ExecutionSummary summary)
        {
            var timings = summary != null ? summary.StageTimings : null;
            if (timings == null || timings.Count == 0)
                return "Stage timings: no timing data available for the current execution.";

            long total = 0;
            var lines = new List<string>(timings.Count + 2);
            lines.Add("Stage timings (ms)");

            for (int i = 0; i < timings.Count; i++)
            {
                var timing = timings[i];
                if (timing == null)
                    continue;

                total += timing.ElapsedMilliseconds;
                lines.Add(string.Format("{0,-18} {1,10:N0}", Safe(timing.StageName), timing.ElapsedMilliseconds));
            }

            lines.Add(string.Empty);
            lines.Add(string.Format("{0,-18} {1,10:N0}", "Total", total));
            return string.Join(Environment.NewLine, lines);
        }

        private void PopulateSummary(ExecutionSummary summary)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => PopulateSummary(summary)));
                return;
            }

            _comparisonRows = new BindingList<ComparisonRowView>();
            _comparisonAnalysesByRunIndex = new Dictionary<int, ExecutionSummary>();

            PopulateBaselineRuns(summary);
            UpdateStageTimingsView(summary);
            BindSelectedRunDetails(summary);
        }

        private void PopulateBaselineRuns(ExecutionSummary summary)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => PopulateBaselineRuns(summary)));
                return;
            }

            var runs = (summary != null && summary.BaselineRuns != null)
                ? summary.BaselineRuns
                : new List<BaselineRunSummaryDto>();

            var viewRows = new List<ResultsRunView>();
            for (int i = 0; i < runs.Count; i++)
            {
                var r = runs[i];
                if (r == null)
                    continue;

                viewRows.Add(new ResultsRunView
                {
                    RunIndex = i,
                    RunType = Safe(r.RunType),
                    Heuristic = Safe(r.Heuristic),
                    Scheme = Safe(r.Scheme),
                    Direction = Safe(r.Direction),
                    Status = Safe(r.Status),
                    MethodClassification = string.IsNullOrWhiteSpace(r.MethodClassification)
                        ? BaselineMethodClassifier.Classify(r, r.Success)
                        : r.MethodClassification,
                    Makespan = r.Makespan,
                    ScheduledActivities = r.ScheduledActivities,
                    BbTimeLimitSeconds = FormatNullableInt(r.BranchAndBoundTimeLimitSeconds),
                    BbTimeLimitReached = FormatNullableBool(r.BranchAndBoundTimeLimitReached),
                    BbOptimalityProven = FormatNullableBool(r.BranchAndBoundOptimalityProven),
                    BbNodesVisited = FormatNullableLong(r.BranchAndBoundNodesVisited),
                    BbSlackSum = FormatNullableDouble(r.BranchAndBoundSlackSum),
                    BbTrace = Safe(r.BranchAndBoundTrace),
                    PriorityList = Safe(r.PriorityListText),
                    ScheduledOrder = Safe(r.ScheduledOrderText),
                    ErrorMessage = Safe(r.ErrorMessage)
                });
            }

            if (viewRows.Count == 0 && summary != null && summary.Baseline != null)
            {
                viewRows.Add(new ResultsRunView
                {
                    RunIndex = 0,
                    RunType = "Selected",
                    Heuristic = Safe(summary.Frm != null ? summary.Frm.Heuristic : string.Empty),
                    Scheme = Safe(summary.Frm != null ? summary.Frm.Scheme : string.Empty),
                    Direction = Safe(summary.Frm != null ? summary.Frm.Direction : string.Empty),
                    Status = summary.Baseline.Activities != null && summary.Baseline.Activities.Count > 0 ? "Success" : "Unknown",
                    MethodClassification = string.Empty,
                    Makespan = summary.Baseline.Makespan,
                    ScheduledActivities = summary.Baseline.Activities != null ? summary.Baseline.Activities.Count : 0,
                    BbTimeLimitSeconds = string.Empty,
                    BbTimeLimitReached = string.Empty,
                    BbOptimalityProven = string.Empty,
                    BbNodesVisited = string.Empty,
                    BbSlackSum = string.Empty,
                    BbTrace = string.Empty,
                    PriorityList = JoinInts(summary.Baseline.PriorityList),
                    ScheduledOrder = JoinInts(summary.Baseline.ScheduledOrder),
                    ErrorMessage = string.Empty
                });
            }

            if (gridResultados == null)
                return;

            gridResultados.SuspendLayout();
            _suppressResultsSelectionAutoLoad = true;

            try
            {
                gridResultados.AutoGenerateColumns = true;
                gridResultados.DataSource = null;
                gridResultados.Columns.Clear();
                gridResultados.DataSource = new BindingList<ResultsRunView>(viewRows);
                if (gridResultados.Columns.Contains("RunIndex"))
                    gridResultados.Columns["RunIndex"].Visible = false;

                SetResultsHeader("BbTimeLimitSeconds", "B&B time limit (s)");
                SetResultsHeader("MethodClassification", "Method classification");
                SetResultsHeader("BbTimeLimitReached", "B&B time limit reached");
                SetResultsHeader("BbOptimalityProven", "B&B optimality proven");
                SetResultsHeader("BbNodesVisited", "B&B nodes visited");
                SetResultsHeader("BbSlackSum", "B&B Σslack");
                SetResultsHeader("BbTrace", "B&B trace");

                gridResultados.Refresh();
                ApplyUserGridPreferences(gridResultados);
            }
            finally
            {
                _suppressResultsSelectionAutoLoad = false;
                gridResultados.ResumeLayout();
            }
        }

        private void SetResultsHeader(string columnName, string headerText)
        {
            if (gridResultados != null && gridResultados.Columns.Contains(columnName))
                gridResultados.Columns[columnName].HeaderText = headerText;
        }

        private static string FormatNullableInt(int? value)
        {
            return value.HasValue ? value.Value.ToString() : string.Empty;
        }

        private static string FormatNullableLong(long? value)
        {
            return value.HasValue ? value.Value.ToString() : string.Empty;
        }

        private static string FormatNullableBool(bool? value)
        {
            return value.HasValue ? (value.Value ? "Yes" : "No") : string.Empty;
        }

        private static string FormatNullableDouble(double? value)
        {
            return value.HasValue ? value.Value.ToString("0.####") : string.Empty;
        }

        private void BindSelectedRunDetails(ExecutionSummary summary)
        {
            RunOnUiThread(() =>
            {
                PopulateFrm(summary);
                PopulateRisk(summary);
                PopulateCrashing(summary);
                PopulateComparisonWorkspace(summary);
                ApplyUserGridPreferencesToAllGrids();
            });
        }

        private void ToggleActionButtons(bool enabled)
        {
            RunOnUiThread(() =>
            {
                if (_btnCrashRunAll != null)
                    _btnCrashRunAll.Enabled = enabled;
                if (_btnCrashClear != null)
                    _btnCrashClear.Enabled = enabled;
                if (_btnComparisonRun != null)
                    _btnComparisonRun.Enabled = enabled;
            });
        }

        private void SetStatus(string main, string run, string step, int progress)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => SetStatus(main, run, step, progress)));
                return;
            }

            _lblStatusMain.Text = main;
            _lblStatusRun.Text = run;
            _lblStatusStep.Text = step;
            _progressStatus.Value = Math.Max(_progressStatus.Minimum, Math.Min(_progressStatus.Maximum, progress));
        }

        private async void gridResultados_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e == null || e.RowIndex < 0)
                return;

            await LoadRunByGridRowAsync(e.RowIndex);
        }

        private async void gridResultados_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e == null || e.RowIndex < 0)
                return;

            await LoadRunByGridRowAsync(e.RowIndex);
        }

        private async void gridResultados_RowHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e == null || e.RowIndex < 0)
                return;

            await LoadRunByGridRowAsync(e.RowIndex);
        }

        private async void gridResultados_SelectionChanged(object sender, EventArgs e)
        {
            if (_suppressResultsSelectionAutoLoad || _isLoadingSelectedRun)
                return;

            if (gridResultados == null || !gridResultados.Focused)
                return;

            int gridRowIndex = GetCurrentResultsGridRowIndex();
            if (gridRowIndex < 0)
                return;

            await LoadRunByGridRowAsync(gridRowIndex);
        }

        private async void gridResultados_KeyUp(object sender, KeyEventArgs e)
        {
            if (e == null)
                return;

            if (e.KeyCode != Keys.Up && e.KeyCode != Keys.Down &&
                e.KeyCode != Keys.PageUp && e.KeyCode != Keys.PageDown &&
                e.KeyCode != Keys.Home && e.KeyCode != Keys.End &&
                e.KeyCode != Keys.Enter)
                return;

            int gridRowIndex = GetCurrentResultsGridRowIndex();
            if (gridRowIndex < 0)
                return;

            await LoadRunByGridRowAsync(gridRowIndex);
        }

        private async void tabControlMain_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControlMain == null)
                return;


            if (tabControlMain.SelectedTab == tabPageResults || tabControlMain.SelectedTab == tabPageBaseline)
                return;


            int gridRowIndex = GetCurrentResultsGridRowIndex();
            if (gridRowIndex < 0)
                return;

            int runIndex = GetRunIndexFromGridRow(gridRowIndex);
            if (runIndex < 0)
                return;

            if (_currentSummary != null &&
                _currentSummary.SelectedBaselineRunIndex == runIndex &&
                HasRunDependentAnalysis(_currentSummary))
                return;

            await LoadRunByIndexAsync(runIndex);
        }

        private async Task LoadRunByGridRowAsync(int gridRowIndex)
        {
            int runIndex = GetRunIndexFromGridRow(gridRowIndex);
            if (runIndex < 0)
                return;

            await LoadRunByIndexAsync(runIndex);
        }

        private int GetCurrentResultsGridRowIndex()
        {
            if (gridResultados == null)
                return -1;

            if (gridResultados.CurrentCell != null)
                return gridResultados.CurrentCell.RowIndex;

            if (gridResultados.SelectedRows != null && gridResultados.SelectedRows.Count > 0)
                return gridResultados.SelectedRows[0].Index;

            if (gridResultados.SelectedCells != null && gridResultados.SelectedCells.Count > 0)
                return gridResultados.SelectedCells[0].RowIndex;

            return -1;
        }

        private int GetRunIndexFromGridRow(int gridRowIndex)
        {
            if (gridResultados == null || gridRowIndex < 0 || gridRowIndex >= gridResultados.Rows.Count)
                return -1;

            var row = gridResultados.Rows[gridRowIndex];
            if (row == null)
                return -1;

            var view = row.DataBoundItem as ResultsRunView;
            if (view != null)
                return view.RunIndex;


            return gridRowIndex;
        }

        private async Task LoadRunByIndexAsync(int rowIndex)
        {
            if (_isLoadingSelectedRun)
                return;

            if (_runs == null || rowIndex < 0 || rowIndex >= _runs.Count)
                return;

            var selectedRun = _runs[rowIndex];
            if (selectedRun == null)
                return;

            if (IsExactRunStillPending(selectedRun))
            {
                _pendingExactRunIndexToLoadAfterCompletion = rowIndex;
                SelectResultsRow(rowIndex);
                SetStatus("Running", Safe(selectedRun.Heuristic), "Exact baseline is still running", 90);
                MessageBox.Show(
                    this,
                    "The exact algorithm is still running. When this row is completed, the system will try to automatically load FRM/RISK/CRASHING/COMPARISON for it. You may also click again after Status is no longer Running/Pending.",
                    "Results",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (!HasValidBaselineForAnalysis(selectedRun))
            {
                SelectResultsRow(rowIndex);

                SetStatus("Warning", Safe(selectedRun.Heuristic), "Selected run has no valid baseline", 0);

                MessageBox.Show(
                    "The selected run does not have a valid baseline for FRM/RISK/CRASHING calculation.",
                    "Results",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            if (_currentSummary != null &&
                _currentSummary.SelectedBaselineRunIndex == rowIndex &&
                HasRunDependentAnalysis(_currentSummary))
            {
                SelectResultsRow(rowIndex);
                SetStatus("Ready", Safe(selectedRun.Heuristic), "Run already loaded", 100);
                return;
            }

            if (_runAnalysisService == null)
            {
                MessageBox.Show(
                    "The run analysis service has not been configured.",
                    "RCPSP-FRM",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            try
            {
                _isLoadingSelectedRun = true;
                ToggleActionButtons(false);

                SelectResultsRow(rowIndex);

                SetStatus("Running", Safe(selectedRun.Heuristic), "Loading selected run", 20);
                await Task.Yield();

                await RunOnUiThreadAsync(() =>
                {
                    ClearRunDependentState();
                    SetStatus("Running", Safe(selectedRun.Heuristic), "Recalculating FRM/RISK/CRASHING", 55);
                });


                ExecutionSummary analysis = await AnalyzeSelectedRunProgressivelyAsync(rowIndex, selectedRun);

                await RunOnUiThreadAsync(() =>
                {
                    _currentSummary = analysis ?? _currentSummary;
                    if (_currentSummary != null)
                    {
                        _currentSummary.BaselineRuns = _runs;
                        _currentSummary.SelectedBaselineRunIndex = rowIndex;
                        UpdateStageTimingsView(_currentSummary);
                        BindSelectedRunDetails(_currentSummary);
                    }

                    SetStatus("Ready", Safe(selectedRun.Heuristic), "Selected run loaded", 100);
                });
            }
            catch (Exception ex)
            {
                SetStatus("Error", Safe(selectedRun.Heuristic), "Run selection failed", 0);

                MessageBox.Show(
                    ex.ToString(),
                    "RCPSP-FRM",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                _isLoadingSelectedRun = false;
                ToggleActionButtons(true);
            }
        }

        private async Task<ExecutionSummary> AnalyzeSelectedRunProgressivelyAsync(int rowIndex, BaselineRunSummaryDto selectedRun)
        {
            if (selectedRun == null)
                throw new ArgumentNullException("selectedRun");

            if (selectedRun.BaselineResult == null ||
                selectedRun.BaselineResult.Activities == null ||
                selectedRun.BaselineResult.Activities.Count == 0)
                throw new InvalidOperationException("The selected run does not have a valid baseline.");

            BaselineResultDto baseline = selectedRun.BaselineResult;
            var timings = new List<StageTimingDto>();
            var working = new ExecutionSummary
            {
                Baseline = baseline,
                BaselineRuns = _runs,
                SelectedBaselineRunIndex = rowIndex,
                Frm = new FrmResultDto(),
                Risk = new RiskResultDto(),
                Crashing = new CrashingResultDto(),
                StageTimings = timings
            };

            await RunOnUiThreadAsync(() =>
            {
                _currentSummary = working;
                UpdateStageTimingsView(_currentSummary);
                BindSelectedRunDetails(_currentSummary);
            });

            var frmCalculator = new CpuFrmCalculator();
            var riskAnalyzer = new CpuRiskAnalyzer();
            var crashingAnalyzer = new CpuCrashingAnalyzer(
                new CpuBaselineScheduler(),
                new CpuExactBaselineScheduler(),
                frmCalculator,
                riskAnalyzer);

            var stopwatch = Stopwatch.StartNew();
            await RunOnUiThreadAsync(() =>
                SetStatus("Running", Safe(selectedRun.Heuristic), "Calculating FRM for selected run", 35));

            FrmResultDto frm = await Task.Run(() =>
                frmCalculator.Run(_request.Project, baseline, _request.Frm ?? new FrmOptionsDto()));

            stopwatch.Stop();
            timings.Add(new StageTimingDto { StageName = "FRM", ElapsedMilliseconds = stopwatch.ElapsedMilliseconds });
            working.Frm = frm;

            await RunOnUiThreadAsync(() =>
            {
                _currentSummary = working;
                UpdateStageTimingsView(_currentSummary);
                BindSelectedRunDetails(_currentSummary);
                SetStatus("Running", Safe(selectedRun.Heuristic), "FRM loaded; calculating RISK", 55);
            });

            stopwatch.Restart();
            RiskResultDto risk = await Task.Run(() =>
                riskAnalyzer.Run(_request.Project, baseline, frm, _request.Risk ?? new RiskOptionsDto()));

            stopwatch.Stop();
            timings.Add(new StageTimingDto { StageName = "Risk", ElapsedMilliseconds = stopwatch.ElapsedMilliseconds });
            working.Risk = risk;

            await RunOnUiThreadAsync(() =>
            {
                _currentSummary = working;
                UpdateStageTimingsView(_currentSummary);
                BindSelectedRunDetails(_currentSummary);
                SetStatus("Running", Safe(selectedRun.Heuristic), "RISK loaded; loading CRASHING candidates", 75);
            });


            stopwatch.Restart();
            CrashingOptionsDto candidateOnlyCrashingOptions = CloneCrashingOptionsForCandidatesOnly(_request != null ? _request.Crashing : null);
            CrashingResultDto crashing = await Task.Run(() =>
                crashingAnalyzer.Run(_request.Project, baseline, frm, risk, candidateOnlyCrashingOptions));

            stopwatch.Stop();
            timings.Add(new StageTimingDto { StageName = "Crashing candidates", ElapsedMilliseconds = stopwatch.ElapsedMilliseconds });
            working.Crashing = crashing;

            await RunOnUiThreadAsync(() =>
            {
                _currentSummary = working;
                UpdateStageTimingsView(_currentSummary);
                BindSelectedRunDetails(_currentSummary);
                SetStatus("Ready", Safe(selectedRun.Heuristic), "CRASHING candidates loaded; click Run Scenarios to generate Sj", 90);
            });

            await RunOnUiThreadAsync(() =>
            {
                _currentSummary = working;
                UpdateStageTimingsView(_currentSummary);
                BindSelectedRunDetails(_currentSummary);
                SetStatus("Ready", Safe(selectedRun.Heuristic), "FRM/RISK and candidates loaded; click Run Scenarios to generate Sj", 100);
            });

            return working;
        }

        private static CrashingOptionsDto CloneCrashingOptionsForCandidatesOnly(CrashingOptionsDto source)
        {
            var clone = new CrashingOptionsDto();
            if (source != null)
            {
                clone.Enabled = source.Enabled;
                clone.CrashingPolicyMode = source.CrashingPolicyMode;
                clone.MaxActivitiesToCrash = source.MaxActivitiesToCrash;
                clone.MaxCombinationSize = source.MaxCombinationSize;
                clone.MaxScenarioCount = source.MaxScenarioCount;
                clone.BranchAndBoundTimeLimitSeconds = source.BranchAndBoundTimeLimitSeconds;
                clone.RecalculateRiskAfterCrash = source.RecalculateRiskAfterCrash;
                clone.UseFrmGuidance = source.UseFrmGuidance;
                clone.PrioritizeStructuralAcceptability = source.PrioritizeStructuralAcceptability;
                clone.KeepProblematicScenariosVisible = source.KeepProblematicScenariosVisible;
                clone.ScoreWeightMakespan = source.ScoreWeightMakespan;
                clone.ScoreWeightP95 = source.ScoreWeightP95;
                clone.ScoreWeightCVaR95 = source.ScoreWeightCVaR95;
                clone.ScoreWeightFrmRobustness = source.ScoreWeightFrmRobustness;
                clone.CandidateActivities = source.CandidateActivities != null
                    ? new List<CrashingCandidateActivityDto>(source.CandidateActivities)
                    : new List<CrashingCandidateActivityDto>();
            }

            clone.CandidatesOnly = true;
            return clone;
        }

        private async Task RunCrashingScenariosOnlyAsync()
        {
            const string runLabel = "Run crash scenarios";

            if (_isRunning)
                return;

            if (_request == null || _request.Project == null)
            {
                MessageBox.Show(this, "Project data is not available.", "RCPSP-FRM", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_currentSummary == null || _currentSummary.Baseline == null || _currentSummary.Frm == null || _currentSummary.Risk == null)
            {
                MessageBox.Show(this, "Load a baseline with FRM and Risk before running crashing scenarios.", "RCPSP-FRM", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_currentSummary.Baseline.Activities == null || _currentSummary.Baseline.Activities.Count == 0)
            {
                MessageBox.Show(this, "The current baseline is not valid for crashing.", "RCPSP-FRM", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_request.Crashing == null)
                _request.Crashing = new CrashingOptionsDto();

            try
            {
                _isRunning = true;
                await RunOnUiThreadAsync(() =>
                {
                    ToggleActionButtons(false);
                    SetStatus("Running", runLabel, "Calculating crashing scenarios", 75);
                });

                var frmCalculator = new CpuFrmCalculator();
                var riskAnalyzer = new CpuRiskAnalyzer();
                var crashingAnalyzer = new CpuCrashingAnalyzer(
                    new CpuBaselineScheduler(),
                    new CpuExactBaselineScheduler(),
                    frmCalculator,
                    riskAnalyzer);

                BaselineResultDto baseline = _currentSummary.Baseline;
                FrmResultDto frm = _currentSummary.Frm;
                RiskResultDto risk = _currentSummary.Risk;
                CrashingOptionsDto options = _request.Crashing;
                if (options != null)
                    options.CandidatesOnly = false;

                var stopwatch = Stopwatch.StartNew();
                CrashingResultDto crashing = await Task.Run(() =>
                    crashingAnalyzer.Run(_request.Project, baseline, frm, risk, options));
                stopwatch.Stop();

                ReplaceStageTiming(_currentSummary, "Crashing", stopwatch.ElapsedMilliseconds);

                _currentSummary.Crashing = crashing ?? new CrashingResultDto();

                if (_request.Risk != null && _request.Risk.RunPairedUnilateralStructural
                    && risk != null && !string.IsNullOrEmpty(risk.PairedComparisonMode))
                {
                    RiskResultDto pairedRisk = risk.PairedStructuralResult ?? risk.PairedUnilateralResult;
                    if (pairedRisk != null)
                    {
                        await RunOnUiThreadAsync(() =>
                            SetStatus("Running", runLabel, "Calculating paired crashing scenarios", 90));

                        stopwatch.Restart();
                        CrashingResultDto pairedCrashing = await Task.Run(() =>
                            crashingAnalyzer.Run(_request.Project, baseline, frm, pairedRisk, options));
                        stopwatch.Stop();

                        ReplaceStageTiming(_currentSummary, "PairedCrashing", stopwatch.ElapsedMilliseconds);

                        ExecutionSummary pairedSummary = new ExecutionSummary
                        {
                            Baseline = baseline,
                            Frm = frm,
                            Risk = pairedRisk,
                            Crashing = pairedCrashing ?? new CrashingResultDto(),
                            StageTimings = new List<StageTimingDto>()
                        };

                        if (string.Equals(risk.PairedComparisonMode, "UNILATERAL_STRUCTURAL", StringComparison.OrdinalIgnoreCase))
                        {
                            _currentSummary.PairedStructuralSummary = pairedSummary;
                            _currentSummary.PairedUnilateralSummary = null;
                            _currentSummary.Crashing.PairedStructuralResult = pairedSummary.Crashing;
                            _currentSummary.Crashing.PairedUnilateralResult = null;
                        }
                        else
                        {
                            _currentSummary.PairedUnilateralSummary = pairedSummary;
                            _currentSummary.PairedStructuralSummary = null;
                            _currentSummary.Crashing.PairedUnilateralResult = pairedSummary.Crashing;
                            _currentSummary.Crashing.PairedStructuralResult = null;
                        }

                        _currentSummary.PairedComparisonMode = risk.PairedComparisonMode;
                        _currentSummary.Crashing.PairedComparisonMode = risk.PairedComparisonMode;
                    }
                }
                else if (_currentSummary.Crashing != null)
                {
                    _currentSummary.Crashing.PairedStructuralResult = null;
                    _currentSummary.Crashing.PairedUnilateralResult = null;
                    _currentSummary.Crashing.PairedComparisonMode = null;
                    _currentSummary.PairedStructuralSummary = null;
                    _currentSummary.PairedUnilateralSummary = null;
                    _currentSummary.PairedComparisonMode = null;
                }

                await RunOnUiThreadAsync(() =>
                {
                    UpdateStageTimingsView(_currentSummary);
                    PopulateCrashing(_currentSummary);
                    SetStatus("Ready", runLabel, "Crashing scenarios updated", 100);
                });
            }
            catch (Exception ex)
            {
                await RunOnUiThreadAsync(() =>
                {
                    SetStatus("Error", runLabel, "Crashing scenario calculation failed", 0);
                    MessageBox.Show(this, ex.ToString(), "RCPSP-FRM", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            }
            finally
            {
                _isRunning = false;
                await RunOnUiThreadAsync(() =>
                {
                    ToggleActionButtons(true);
                    KeepCrashingTabSelectedAfterScenarioRun();
                });
            }
        }

        private static void ReplaceStageTiming(ExecutionSummary summary, string stageName, long elapsedMilliseconds)
        {
            if (summary == null)
                return;

            if (summary.StageTimings == null)
                summary.StageTimings = new List<StageTimingDto>();

            summary.StageTimings.RemoveAll(x => string.Equals(x.StageName, stageName, StringComparison.OrdinalIgnoreCase));
            summary.StageTimings.Add(new StageTimingDto { StageName = stageName, ElapsedMilliseconds = elapsedMilliseconds });
        }

        private async Task ComputeAllExactRowAsync(string runLabel)
        {
            if (_isComputingAllExact)
                return;

            List<int> exactIndexes = FindExactPendingRunIndexes();
            if (exactIndexes.Count == 0)
            {
                await RunOnUiThreadAsync(() =>
                    SetStatus("Ready", runLabel, "Results ready; select a run to analyze", 100));
                return;
            }

            try
            {
                _isComputingAllExact = true;

                for (int pos = 0; pos < exactIndexes.Count; pos++)
                {
                    int exactIndex = exactIndexes[pos];
                    if (_runs == null || exactIndex < 0 || exactIndex >= _runs.Count)
                        continue;

                    BaselineRunSummaryDto pendingRun = _runs[exactIndex];
                    string exactLabel = pendingRun != null && !string.IsNullOrWhiteSpace(pendingRun.Heuristic)
                        ? pendingRun.Heuristic
                        : "Exact";

                    await RunOnUiThreadAsync(() =>
                        SetStatus("Running", runLabel, "Calculating " + exactLabel + " baseline", 90));

                    BaselineRunSummaryDto exactRun = await Task.Run(() => ExecuteExactForAll(pendingRun));

                    bool shouldLoadExactAfterCompletion = false;

                    await RunOnUiThreadAsync(() =>
                    {
                        if (_runs == null)
                            _runs = new List<BaselineRunSummaryDto>();

                        if (exactIndex >= 0 && exactIndex < _runs.Count)
                            _runs[exactIndex] = exactRun;
                        else
                            _runs.Add(exactRun);

                        if (_currentSummary == null)
                            _currentSummary = new ExecutionSummary();

                        _currentSummary.BaselineRuns = _runs;
                        _currentSummary.SelectedBaselineRunIndex = -1;

                        int selectedVisualRow = GetCurrentResultsGridRowIndex();
                        int selectedRunIndex = GetRunIndexFromGridRow(selectedVisualRow);

                        PopulateBaselineRuns(_currentSummary);

                        if (tabControlMain != null && tabPageResults != null)
                            tabControlMain.SelectedTab = tabPageResults;

                        shouldLoadExactAfterCompletion =
                            HasValidBaselineForAnalysis(exactRun) &&
                            (IsSingleExactSchedulingMode() ||
                             _pendingExactRunIndexToLoadAfterCompletion == exactIndex ||
                             selectedRunIndex == exactIndex);

                        if (shouldLoadExactAfterCompletion)
                            SelectResultsRow(exactIndex);
                    });

                    if (shouldLoadExactAfterCompletion)
                    {
                        _pendingExactRunIndexToLoadAfterCompletion = -1;
                        await LoadRunByIndexAsync(exactIndex);
                    }
                }

                await RunOnUiThreadAsync(() =>
                    SetStatus("Ready", runLabel, "Results complete; select a run to analyze", 100));
            }
            catch (Exception)
            {
                await RunOnUiThreadAsync(() =>
                {
                    if (_currentSummary != null)
                    {
                        _currentSummary.BaselineRuns = _runs;
                        PopulateBaselineRuns(_currentSummary);
                    }

                    SetStatus("Warning", runLabel, "Exact B&B failed; heuristic results available", 100);
                });
            }
            finally
            {
                _isComputingAllExact = false;
            }
        }

        private static bool IsExactRunStillPending(BaselineRunSummaryDto run)
        {
            if (run == null || !run.IsExact)
                return false;

            string status = run.Status ?? string.Empty;
            return status.Equals("Running", StringComparison.OrdinalIgnoreCase) ||
                   status.Equals("Pending", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasValidBaselineForAnalysis(BaselineRunSummaryDto run)
        {
            return run != null &&
                   run.BaselineResult != null &&
                   run.BaselineResult.Activities != null &&
                   run.BaselineResult.Activities.Count > 0;
        }

        private static bool HasRunDependentAnalysis(ExecutionSummary summary)
        {
            if (summary == null)
                return false;

            bool hasFrm = summary.Frm != null &&
                          summary.Frm.Activities != null &&
                          summary.Frm.Activities.Count > 0;

            bool hasRisk = summary.Risk != null &&
                           (summary.Risk.Iterations > 0 ||
                            summary.Risk.MakespanSamples != null && summary.Risk.MakespanSamples.Count > 0 ||
                            summary.Risk.HistogramCounts != null && summary.Risk.HistogramCounts.Count > 0);

            bool hasCrashing = summary.Crashing != null &&
                               (summary.Crashing.Candidates != null && summary.Crashing.Candidates.Count > 0 ||
                                summary.Crashing.Scenarios != null && summary.Crashing.Scenarios.Count > 0);

            return hasFrm || hasRisk || hasCrashing;
        }

        private List<int> FindExactPendingRunIndexes()
        {
            var result = new List<int>();
            if (_runs == null)
                return result;

            for (int i = 0; i < _runs.Count; i++)
            {
                var run = _runs[i];
                if (run == null)
                    continue;

                bool isPendingExact = run.IsExact &&
                    string.Equals(run.EngineKey, "DH_BB", StringComparison.OrdinalIgnoreCase) &&
                    (string.Equals(run.Status, "Running", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(run.Status, "Pending", StringComparison.OrdinalIgnoreCase));

                if (isPendingExact)
                    result.Add(i);
            }

            return result;
        }


        private BaselineRunSummaryDto ExecuteExactForAll(BaselineRunSummaryDto pendingRun)
        {
            string label = "Modified DH B&B";
            string engineKey = "DH_BB";

            var exactOptions = new SchedulingOptionsDto
            {
                Heuristic = label,
                Scheme = "EXACT",
                Direction = "EXACT",
                UseExactEngine = true,
                Engine = engineKey,
                RunLabel = label,
                BranchAndBoundTimeLimitSeconds = _request != null && _request.Scheduling != null
                    ? _request.Scheduling.BranchAndBoundTimeLimitSeconds
                    : null
            };

            var exactScheduler = new CpuExactBaselineScheduler();
            var exact = exactScheduler.RunModifiedDhBranchAndBoundDetailed(_request.Project, exactOptions);
            var baseline = exact != null ? exact.Baseline : null;

            var result = new BaselineRunSummaryDto
            {
                RunType = "Exact B&B",
                Heuristic = label,
                Scheme = "EXACT",
                Direction = "EXACT",
                IsExact = true,
                ExactMode = label,
                EngineKey = engineKey,
                Success = exact != null && exact.Success,
                Status = exact != null && !string.IsNullOrWhiteSpace(exact.Status) ? exact.Status : "Success",
                ErrorMessage = exact != null ? exact.Message : string.Empty,
                Makespan = baseline != null ? baseline.Makespan : 0,
                ScheduledActivities = baseline != null && baseline.Activities != null ? baseline.Activities.Count : 0,
                PriorityListText = "(exact)",
                ScheduledOrderText = JoinInts(baseline != null ? baseline.ScheduledOrder : null),
                BranchAndBoundTimeLimitSeconds = exact != null ? (int?)exact.TimeLimitSeconds : null,
                BranchAndBoundTimeLimitReached = exact != null ? (bool?)exact.TimeLimitReached : null,
                BranchAndBoundOptimalityProven = exact != null ? (bool?)exact.OptimalityProven : null,
                BranchAndBoundNodesVisited = exact != null ? (long?)exact.NodesVisited : null,
                BranchAndBoundSlackSum = exact != null ? (double?)exact.SlackSum : null,
                BranchAndBoundTrace = exact != null ? exact.Trace : string.Empty,
                BaselineResult = baseline ?? new BaselineResultDto { RunLabel = label }
            };

            result.MethodClassification = BaselineMethodClassifier.Classify(
                result,
                result.BaselineResult != null && result.BaselineResult.Activities != null && result.BaselineResult.Activities.Count > 0);
            return result;
        }

        private void ClearRunDependentState()
        {


            if (_gridFrm != null)
            {
                _gridFrm.DataSource = null;
                _gridFrm.Rows.Clear();
                _gridFrm.Columns.Clear();
            }

            if (_lblFrmSummary != null)
                _lblFrmSummary.Text = "FRM not executed.";


            if (_lblRiskSummary != null)
                _lblRiskSummary.Text = string.Empty;

            if (_gridRisk != null)
            {
                _gridRisk.DataSource = null;
                _gridRisk.Rows.Clear();
                _gridRisk.Columns.Clear();
            }

            if (_gridRiskMetrics != null)
            {
                _gridRiskMetrics.DataSource = null;
                _gridRiskMetrics.Rows.Clear();
                _gridRiskMetrics.Columns.Clear();
            }

            if (_gridRiskBins != null)
            {
                _gridRiskBins.DataSource = null;
                _gridRiskBins.Rows.Clear();
                _gridRiskBins.Columns.Clear();
            }

            if (_chartRiskHistogram != null)
            {
                if (_chartRiskHistogram.Series.Count > 0)
                    _chartRiskHistogram.Series[0].Points.Clear();

                _chartRiskHistogram.Titles.Clear();

                if (_chartRiskHistogram.ChartAreas.Count > 0)
                    _chartRiskHistogram.ChartAreas[0].AxisX.StripLines.Clear();
            }


            _comparisonRows = new BindingList<ComparisonRowView>();
            _comparisonAnalysesByRunIndex = new Dictionary<int, ExecutionSummary>();
            if (_gridComparisonGrid != null)
            {
                _gridComparisonGrid.DataSource = null;
                _gridComparisonGrid.Rows.Clear();
                _gridComparisonGrid.Columns.Clear();
            }
            ClearComparisonChart();
            if (_lblComparisonSummary != null)
                _lblComparisonSummary.Text = "Comparison not executed yet. Load a baseline, run crashing scenarios, and compare S0 against its Sj scenarios.";

            UpdateStageTimingsView(null);
        }

        private void SelectResultsRow(int runIndex)
        {
            if (gridResultados == null)
                return;

            if (runIndex < 0)
                return;

            RunOnUiThread(() =>
            {
                bool previousSuppress = _suppressResultsSelectionAutoLoad;
                _suppressResultsSelectionAutoLoad = true;
                try
                {
                int gridRowIndex = FindGridRowIndexByRunIndex(runIndex);

                if (gridRowIndex < 0)
                    gridRowIndex = runIndex;

                if (gridRowIndex < 0 || gridRowIndex >= gridResultados.Rows.Count)
                    return;

                var row = gridResultados.Rows[gridRowIndex];
                if (row == null || row.IsNewRow || !row.Visible)
                    return;

                DataGridViewCell firstVisibleCell = GetFirstVisibleCell(row);

                gridResultados.ClearSelection();
                row.Selected = true;


                if (firstVisibleCell != null)
                {
                    try
                    {
                        gridResultados.CurrentCell = firstVisibleCell;
                    }
                    catch (InvalidOperationException)
                    {


                    }
                }
                }
                finally
                {
                    _suppressResultsSelectionAutoLoad = previousSuppress;
                }
            });
        }

        private int FindGridRowIndexByRunIndex(int runIndex)
        {
            if (gridResultados == null)
                return -1;

            for (int i = 0; i < gridResultados.Rows.Count; i++)
            {
                var row = gridResultados.Rows[i];
                if (row == null || row.IsNewRow)
                    continue;

                var view = row.DataBoundItem as ResultsRunView;
                if (view != null && view.RunIndex == runIndex)
                    return i;
            }

            return -1;
        }

        private static DataGridViewCell GetFirstVisibleCell(DataGridViewRow row)
        {
            if (row == null || row.Cells == null)
                return null;

            foreach (DataGridViewCell cell in row.Cells)
            {
                if (cell != null && cell.Visible && cell.OwningColumn != null && cell.OwningColumn.Visible)
                    return cell;
            }

            return null;
        }

        private static int FindFirstSuccessfulRunIndex(List<BaselineRunSummaryDto> runs)
        {
            if (runs == null)
                return -1;

            for (int i = 0; i < runs.Count; i++)
            {
                if (runs[i] != null &&
                    runs[i].Success &&
                    runs[i].BaselineResult != null &&
                    runs[i].BaselineResult.Activities != null &&
                    runs[i].BaselineResult.Activities.Count > 0)
                {
                    return i;
                }
            }

            return -1;
        }

        private ExecutionSummary CreatePendingSingleExactSummary()
        {
            var pendingRun = new BaselineRunSummaryDto
            {
                RunType = "Exact B&B",
                Heuristic = "Modified DH B&B",
                Scheme = "EXACT",
                Direction = "EXACT",
                IsExact = true,
                ExactMode = "Modified DH B&B",
                EngineKey = "DH_BB",
                MethodClassification = BaselineMethodClassifier.ExactPending,
                Success = false,
                Status = "Running",
                ErrorMessage = "Modified DH B&B is running; wait until it finishes before loading FRM/RISK/CRASHING/COMPARISON.",
                Makespan = 0,
                ScheduledActivities = 0,
                BranchAndBoundTimeLimitSeconds = _request != null && _request.Scheduling != null
                    ? _request.Scheduling.BranchAndBoundTimeLimitSeconds
                    : null,
                BranchAndBoundTimeLimitReached = null,
                BranchAndBoundOptimalityProven = null,
                BranchAndBoundNodesVisited = null,
                BranchAndBoundSlackSum = null,
                BranchAndBoundTrace = string.Empty,
                PriorityListText = "(exact)",
                ScheduledOrderText = string.Empty,
                BaselineResult = new BaselineResultDto
                {
                    RunLabel = "Modified DH B&B"
                }
            };

            return new ExecutionSummary
            {
                Baseline = new BaselineResultDto(),
                BaselineRuns = new List<BaselineRunSummaryDto> { pendingRun },
                SelectedBaselineRunIndex = -1,
                Frm = new FrmResultDto(),
                Risk = new RiskResultDto(),
                Crashing = new CrashingResultDto(),
                StageTimings = new List<StageTimingDto>()
            };
        }

        private bool IsSingleExactSchedulingMode()
        {
            var heuristic = _request != null && _request.Scheduling != null
                ? _request.Scheduling.Heuristic
                : string.Empty;

            return IsModifiedDhBranchAndBoundLabel(heuristic);
        }

        private static bool IsModifiedDhBranchAndBoundLabel(string heuristic)
        {
            string normalized = string.IsNullOrWhiteSpace(heuristic)
                ? string.Empty
                : heuristic.Trim().ToUpperInvariant();

            return normalized == "MODIFIED DH B&B"
                   || normalized == "B&B"
                   || normalized == "DHBB"
                   || normalized == "DH_BB"
                   || normalized == "B&B EF"
                   || normalized == "BB_EF"
                   || normalized == "ENHANCEFLEXIBILITY"
                   || normalized == "ENHANCE_FLEXIBILITY";
        }

        private bool IsAllSchedulingMode()
        {
            var heuristic = _request != null && _request.Scheduling != null
                ? _request.Scheduling.Heuristic
                : string.Empty;

            return !string.IsNullOrWhiteSpace(heuristic)
                   && heuristic.Trim().Equals("ALL", StringComparison.OrdinalIgnoreCase);
        }


    }
}
