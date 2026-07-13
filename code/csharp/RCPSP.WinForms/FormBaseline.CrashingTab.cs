using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using RCPSP.Application;
using RCPSP.Contracts;
using System.Drawing;
using System.Windows.Forms.DataVisualization.Charting;

namespace RCPSP.WinForms
{
    public partial class FormBaseline : Form
    {
        private readonly NumericUpDown _numCrashCandidateLimit = new NumericUpDown();
        private readonly NumericUpDown _numCrashCombinationLimit = new NumericUpDown();
        private readonly NumericUpDown _numCrashScenarioLimit = new NumericUpDown();
        private readonly Label _lblCrashCandidateLimit = new Label();
        private readonly Label _lblCrashCombinationLimit = new Label();
        private readonly Label _lblCrashScenarioLimit = new Label();
        private readonly Label _lblCrashCombinationTotal = new Label();

        private void InitializeCrashingLayout()
        {
            _groupCrashSummary.SuspendLayout();
            _groupCrashSummary.Controls.Clear();

            _crashRightLayout = new TableLayoutPanel();
            _crashCardsPanel = new FlowLayoutPanel();
            _crashFilterPanel = new FlowLayoutPanel();
            _tabCrashingParetoCharts = new TabControl();
            _tabCrashingDeltaCvar = new TabPage();
            _tabCrashingFrriPareto = new TabPage();
            _tabCrashingBalanceRupture = new TabPage();
            _chartCrashingDeltaCvar = new Chart();
            _chartCrashingFrriPareto = new Chart();
            _chartCrashingBalanceRupture = new Chart();
            _chartCrashingTradeoff = _chartCrashingDeltaCvar;

            _lblCrashBestGlobalCard = CreateCrashCardLabel();
            _lblCrashBestAcceptableCard = CreateCrashCardLabel();
            _lblCrashDistributionCard = CreateCrashCardLabel();

            _btnCrashFilterAll = CreateCrashFilterButton("All", "ALL", Color.WhiteSmoke);
            _btnCrashFilterRobust = CreateCrashFilterButton("Robust", "ROBUST", Color.FromArgb(210, 242, 210));
            _btnCrashFilterFeasible = CreateCrashFilterButton("Feasible", "FEASIBLE", Color.FromArgb(255, 243, 176));
            _btnCrashFilterFragile = CreateCrashFilterButton("Fragile", "FRAGILE", Color.FromArgb(255, 214, 153));
            _btnCrashFilterInviable = CreateCrashFilterButton("Inviable", "INVIABLE", Color.FromArgb(255, 199, 206));

            _crashRightLayout.Dock = DockStyle.Fill;
            _crashRightLayout.ColumnCount = 1;
            _crashRightLayout.RowCount = 3;
            _crashRightLayout.Padding = new Padding(8);
            _crashRightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            _crashRightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));
            _crashRightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));

            _crashFilterPanel.Dock = DockStyle.Fill;
            _crashFilterPanel.FlowDirection = FlowDirection.LeftToRight;
            _crashFilterPanel.WrapContents = false;
            _crashFilterPanel.Padding = new Padding(0);
            _crashFilterPanel.Margin = new Padding(0, 0, 0, 4);
            _crashFilterPanel.Controls.Add(_btnCrashFilterAll);
            _crashFilterPanel.Controls.Add(_btnCrashFilterRobust);
            _crashFilterPanel.Controls.Add(_btnCrashFilterFeasible);
            _crashFilterPanel.Controls.Add(_btnCrashFilterFragile);
            _crashFilterPanel.Controls.Add(_btnCrashFilterInviable);

            _tabCrashingParetoCharts.Dock = DockStyle.Fill;
            _tabCrashingDeltaCvar.Text = "ΔCVaR95";
            _tabCrashingDeltaCvar.UseVisualStyleBackColor = true;
            _tabCrashingFrriPareto.Text = "FRRI";
            _tabCrashingFrriPareto.UseVisualStyleBackColor = true;
            _tabCrashingBalanceRupture.Text = "Unabsorbed work";
            _tabCrashingBalanceRupture.UseVisualStyleBackColor = true;

            ConfigureCrashParetoChart(_chartCrashingDeltaCvar, "ΔMakespan", "ΔCVaR95", "Crashing Pareto - prazo x risco");
            ConfigureCrashParetoChart(_chartCrashingFrriPareto, "ΔMakespan", "FRRI", "Crashing Pareto - prazo x redução de risco");
            ConfigureCrashParetoChart(_chartCrashingBalanceRupture, "ΔMakespan", "ΔMean unabsorbed work", "Crashing Pareto - prazo x severidade não absorvida");

            _tabCrashingDeltaCvar.Controls.Add(_chartCrashingDeltaCvar);
            _tabCrashingFrriPareto.Controls.Add(_chartCrashingFrriPareto);
            _tabCrashingBalanceRupture.Controls.Add(_chartCrashingBalanceRupture);
            _tabCrashingParetoCharts.TabPages.Add(_tabCrashingDeltaCvar);
            _tabCrashingParetoCharts.TabPages.Add(_tabCrashingFrriPareto);
            _tabCrashingParetoCharts.TabPages.Add(_tabCrashingBalanceRupture);

            _gridCrashingHistory.Dock = DockStyle.Fill;
            _gridCrashingHistory.AllowUserToResizeColumns = true;
            _gridCrashingHistory.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            _gridCrashingHistory.MultiSelect = true;
            _gridCrashingHistory.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            _crashRightLayout.Controls.Add(_crashFilterPanel, 0, 0);
            _crashRightLayout.Controls.Add(_tabCrashingParetoCharts, 0, 1);
            _crashRightLayout.Controls.Add(_gridCrashingHistory, 0, 2);

            _groupCrashSummary.Controls.Add(_crashRightLayout);
            _groupCrashSummary.ResumeLayout();
            UpdateCrashFilterButtons();
        }

        private static Label CreateCrashCardLabel()
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(6)
            };
        }


        private Button CreateCrashFilterButton(string text, string status, Color backColor)
        {
            var button = new Button();
            button.Text = text;
            button.Tag = status;
            button.AutoSize = false;
            button.Width = 110;
            button.Height = 40;
            button.Margin = new Padding(0, 0, 8, 0);
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.BackColor = backColor;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.DarkGray;
            button.Click += (s, e) =>
            {
                _crashFilterStatus = status;
                UpdateCrashFilterButtons();
                ApplyCrashingScenarioFilter();
            };
            return button;
        }


        private static void ConfigureCrashParetoChart(Chart chart, string xAxisTitle, string yAxisTitle, string title)
        {
            chart.Dock = DockStyle.Fill;
            chart.BackColor = Color.White;
            chart.ChartAreas.Clear();
            chart.Series.Clear();
            chart.Legends.Clear();
            chart.Titles.Clear();

            var area = new ChartArea("CrashParetoArea");
            area.AxisX.Title = xAxisTitle;
            area.AxisY.Title = yAxisTitle;
            area.AxisX.MajorGrid.LineColor = Color.Gainsboro;
            area.AxisY.MajorGrid.LineColor = Color.Gainsboro;
            chart.ChartAreas.Add(area);

            var legend = new Legend("CrashParetoLegend");
            legend.Docking = Docking.Top;
            chart.Legends.Add(legend);

            var dominated = new Series("Dominated");
            dominated.ChartType = SeriesChartType.Point;
            dominated.MarkerStyle = MarkerStyle.Circle;
            dominated.MarkerSize = 8;
            dominated.Color = Color.LightGray;
            dominated.Legend = "CrashParetoLegend";
            chart.Series.Add(dominated);

            var nonDominated = new Series("Non-dominated");
            nonDominated.ChartType = SeriesChartType.Point;
            nonDominated.MarkerStyle = MarkerStyle.Circle;
            nonDominated.MarkerSize = 10;
            nonDominated.Color = Color.RoyalBlue;
            nonDominated.Legend = "CrashParetoLegend";
            chart.Series.Add(nonDominated);

            var frontier = new Series("Frontier");
            frontier.ChartType = SeriesChartType.Line;
            frontier.BorderWidth = 2;
            frontier.MarkerStyle = MarkerStyle.None;
            frontier.Color = Color.RoyalBlue;
            frontier.Legend = "CrashParetoLegend";
            chart.Series.Add(frontier);

            var selected = new Series("Selected");
            selected.ChartType = SeriesChartType.Point;
            selected.MarkerStyle = MarkerStyle.Star5;
            selected.MarkerSize = 15;
            selected.Color = Color.Black;
            selected.Legend = "CrashParetoLegend";
            selected.IsVisibleInLegend = false;
            chart.Series.Add(selected);

            chart.Titles.Add(title);
            chart.GetToolTipText += (s, e) =>
            {
                if (e.HitTestResult == null || e.HitTestResult.PointIndex < 0 || e.HitTestResult.Series == null)
                    return;

                var point = e.HitTestResult.Series.Points[e.HitTestResult.PointIndex];
                if (!string.IsNullOrWhiteSpace(point.ToolTip))
                    e.Text = point.ToolTip;
            };
        }

        private void ApplyCrashingScenarioFilter()
        {
            var scenarioRows = (_allCrashScenarioRows ?? new List<CrashScenarioRowView>()).ToList();
            if (!string.Equals(_crashFilterStatus, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                scenarioRows = scenarioRows
                    .Where(x => string.Equals(x.StructuralStatus, _crashFilterStatus, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            _gridCrashingHistory.AutoGenerateColumns = true;
            _gridCrashingHistory.DataSource = scenarioRows;
            ApplyCrashingHistoryGridHeaders();
            PopulateCrashingScatter(scenarioRows);

            if (scenarioRows.Count > 0)
            {
                _suppressCrashSelectionSync = true;
                try
                {
                    _gridCrashingHistory.ClearSelection();
                    if (_gridCrashingHistory.Rows.Count > 0)
                        _gridCrashingHistory.Rows[0].Selected = true;
                    BindCrashingScenarioDetails(scenarioRows[0]);
                }
                finally
                {
                    _suppressCrashSelectionSync = false;
                }
            }
            else
            {
                _lblCrashClassification.Text = "Classification: no scenario matches the current filter";
            }
        }

        private void PopulateCrashingScatter(List<CrashScenarioRowView> scenarioRows)
        {
            var rows = scenarioRows ?? new List<CrashScenarioRowView>();

            PopulateCrashParetoChart(
                _chartCrashingDeltaCvar,
                rows,
                row => row.DeltaMakespan,
                row => row.DeltaCVaR95,
                row => row.IsParetoDeltaCvar,
                row => row.DominatedByDeltaCvar,
                "ΔMakespan",
                "ΔCVaR95",
                "Crashing Pareto - menor prazo e menor risco");

            PopulateCrashParetoChart(
                _chartCrashingFrriPareto,
                rows,
                row => row.DeltaMakespan,
                row => row.Frri,
                row => row.IsParetoFrri,
                row => row.DominatedByFrri,
                "ΔMakespan",
                "FRRI",
                "Crashing Pareto - menor prazo e maior redução de risco");

            PopulateCrashParetoChart(
                _chartCrashingBalanceRupture,
                rows,
                row => row.DeltaMakespan,
                row => row.DeltaMeanUnabsorbedWork,
                row => row.IsParetoBalanceRupture,
                row => row.DominatedByBalanceRupture,
                "ΔMakespan",
                "ΔMean unabsorbed work",
                "Crashing Pareto - menor prazo e menor severidade não absorvida");
        }

        private void PopulateCrashParetoChart(
            Chart chart,
            List<CrashScenarioRowView> scenarioRows,
            Func<CrashScenarioRowView, double> xSelector,
            Func<CrashScenarioRowView, double> ySelector,
            Func<CrashScenarioRowView, bool> isParetoSelector,
            Func<CrashScenarioRowView, string> dominatedBySelector,
            string xAxisTitle,
            string yAxisTitle,
            string chartTitle)
        {
            if (chart == null)
                return;

            foreach (var series in chart.Series)
                series.Points.Clear();
            chart.Titles.Clear();
            chart.Titles.Add(chartTitle);

            if (chart.ChartAreas.Count > 0)
            {
                chart.ChartAreas[0].AxisX.Title = xAxisTitle;
                chart.ChartAreas[0].AxisY.Title = yAxisTitle;
            }

            var rows = (scenarioRows ?? new List<CrashScenarioRowView>())
                .Where(x => x != null && x.Primary != null)
                .ToList();

            foreach (var row in rows)
            {
                bool pareto = isParetoSelector(row);
                string seriesName = pareto ? "Non-dominated" : "Dominated";
                Series series;
                try { series = chart.Series[seriesName]; }
                catch { continue; }

                int pointIndex = series.Points.AddXY(xSelector(row), ySelector(row));
                var point = series.Points[pointIndex];
                point.Tag = row;
                point.ToolTip = BuildCrashParetoTooltip(row, pareto, dominatedBySelector(row));

                point.Label = string.Empty;
                point.Color = pareto ? GetStructuralStatusColor(row.StructuralStatus) : Color.LightGray;
                point.MarkerSize = pareto ? 10 : 7;
            }

            Series frontier = null;
            try { frontier = chart.Series["Frontier"]; } catch { frontier = null; }
            if (frontier != null)
            {
                var frontRows = rows
                    .Where(x => isParetoSelector(x))
                    .OrderBy(x => xSelector(x))
                    .ThenBy(x => ySelector(x))
                    .ToList();
                foreach (var row in frontRows)
                {
                    int pointIndex = frontier.Points.AddXY(xSelector(row), ySelector(row));
                    frontier.Points[pointIndex].ToolTip = BuildCrashParetoTooltip(row, true, string.Empty);
                }
            }

            Series selected = null;
            try { selected = chart.Series["Selected"]; } catch { selected = null; }
            if (selected != null)
                selected.IsVisibleInLegend = selected.Points.Count > 0;

            AutoScaleChartAxes(chart);
        }

        private static string BuildCrashParetoTooltip(CrashScenarioRowView row, bool pareto, string dominatedBy)
        {
            if (row == null)
                return string.Empty;

            return string.Format(
                "{0} | {1} | Status={2} | ΔMk={3} | SIF={4:0.##} | ΔSIF={5:0.##} | ΔCVaR95={6:0.##} | FRRI={7:0.###} | P(Balance rupture)={8:0.###} | Mean Balance use={9:0.##} | Mean unabsorbed={10:0.##} | ΔMean unabsorbed={11:0.##} | Dominated by={12}",
                row.ScenarioName,
                pareto ? "Non-dominated" : "Dominated",
                row.StructuralStatus,
                row.DeltaMakespan,
                row.Sif,
                row.DeltaSifPrimary,
                row.DeltaCVaR95,
                row.Frri,
                row.ScenarioBalanceRuptureProbability,
                row.ScenarioMeanBalanceUsage,
                row.ScenarioMeanUnabsorbedWork,
                row.DeltaMeanUnabsorbedWork,
                string.IsNullOrWhiteSpace(dominatedBy) ? "-" : dominatedBy);
        }

        private static void AutoScaleChartAxes(Chart chart)
        {
            if (chart == null || chart.ChartAreas.Count == 0)
                return;

            var points = chart.Series
                .Cast<Series>()
                .Where(s => s.ChartType != SeriesChartType.Line)
                .SelectMany(s => s.Points.Cast<DataPoint>())
                .ToList();
            if (points.Count == 0)
                return;

            double minX = points.Min(p => p.XValue);
            double maxX = points.Max(p => p.XValue);
            double minY = points.Min(p => p.YValues.Length > 0 ? p.YValues[0] : 0.0);
            double maxY = points.Max(p => p.YValues.Length > 0 ? p.YValues[0] : 0.0);
            if (Math.Abs(maxX - minX) < 0.0001) { minX -= 1.0; maxX += 1.0; }
            if (Math.Abs(maxY - minY) < 0.0001) { minY -= 1.0; maxY += 1.0; }
            double padX = Math.Max(1.0, Math.Abs(maxX - minX) * 0.10);
            double padY = Math.Max(1.0, Math.Abs(maxY - minY) * 0.10);

            var area = chart.ChartAreas[0];
            area.AxisX.Minimum = Math.Floor(minX - padX);
            area.AxisX.Maximum = Math.Ceiling(maxX + padX);
            area.AxisY.Minimum = Math.Floor(minY - padY);
            area.AxisY.Maximum = Math.Ceiling(maxY + padY);
        }

        private List<CrashScenarioRowView> GetCurrentCrashFilterScenarios()
        {
            var scenarioRows = (_allCrashScenarioRows ?? new List<CrashScenarioRowView>()).ToList();
            if (!string.Equals(_crashFilterStatus, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                scenarioRows = scenarioRows
                    .Where(x => string.Equals(x.StructuralStatus, _crashFilterStatus, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            return scenarioRows;
        }

        private void UpdateCrashingScatterFromGridSelection()
        {
            if (_suppressCrashSelectionSync)
                return;

            var selected = _gridCrashingHistory.SelectedRows
                .Cast<DataGridViewRow>()
                .Select(r => r.DataBoundItem as CrashScenarioRowView)
                .Where(x => x != null)
                .ToList();


            PopulateCrashingScatter(GetCurrentCrashFilterScenarios());

            if (selected.Count > 0)
                BindCrashingScenarioDetails(selected[0]);
        }

        private void ApplyCrashingHistoryGridHeaders()
        {
            string[] visible =
            {
                "Rank", "ScenarioName", "StructuralStatus", "IsStructurallyAcceptable",
                "DeltaMakespan", "BaselineSif", "Sif", "DeltaSifPrimary",
                "BaselineP95", "ScenarioP95", "DeltaP95",
                "BaselineCVaR95", "ScenarioCVaR95", "DeltaCVaR95", "Frri",
                "ScenarioBalanceRuptureProbability", "DeltaBalanceRuptureProbability",
                "ScenarioMeanBalanceUsage", "DeltaMeanBalanceUsage",
                "ScenarioMinObservedBalance", "DeltaMinObservedBalance",
                "ScenarioMeanUnabsorbedWork", "DeltaMeanUnabsorbedWork",
                "ScenarioMeanUnabsorbedWorkRatio", "DeltaMeanUnabsorbedWorkRatio",
                "ParetoDeltaCvar", "ParetoFrri", "ParetoBalanceRupture", "RobustnessIndex",
                "ActivitiesLabel", "ActivityCount"
            };

            foreach (DataGridViewColumn column in _gridCrashingHistory.Columns)
                column.Visible = visible.Contains(column.Name);

            SetHistoryHeader("ScenarioName", "Scenario");
            SetHistoryHeader("StructuralStatus", "Structural Status");
            SetHistoryHeader("IsStructurallyAcceptable", "Acceptable");
            SetHistoryHeader("DeltaMakespan", "ΔMk");
            SetHistoryHeader("BaselineSif", "SIF Baseline");
            SetHistoryHeader("Sif", "SIF Scenario");
            SetHistoryHeader("DeltaSifPrimary", "ΔSIF");
            SetHistoryHeader("BaselineP95", "P95 Baseline");
            SetHistoryHeader("ScenarioP95", "P95 Scenario");
            SetHistoryHeader("DeltaP95", "ΔP95");
            SetHistoryHeader("BaselineCVaR95", "CVaR95 Baseline");
            SetHistoryHeader("ScenarioCVaR95", "CVaR95 Scenario");
            SetHistoryHeader("DeltaCVaR95", "ΔCVaR95");
            SetHistoryHeader("Frri", "FRRI");
            SetHistoryHeader("ScenarioBalanceRuptureProbability", "P(Balance rupture)");
            SetHistoryHeader("DeltaBalanceRuptureProbability", "ΔP(Bal. rupture)");
            SetHistoryHeader("ScenarioMeanBalanceUsage", "Mean Balance use");
            SetHistoryHeader("DeltaMeanBalanceUsage", "ΔMean Balance use");
            SetHistoryHeader("ScenarioMinObservedBalance", "Min Balance");
            SetHistoryHeader("DeltaMinObservedBalance", "ΔMin Balance");
            SetHistoryHeader("ScenarioMeanUnabsorbedWork", "Mean unabsorbed work");
            SetHistoryHeader("DeltaMeanUnabsorbedWork", "ΔMean unabsorbed work");
            SetHistoryHeader("ScenarioMeanUnabsorbedWorkRatio", "Unabsorbed work ratio");
            SetHistoryHeader("DeltaMeanUnabsorbedWorkRatio", "ΔUnabsorbed ratio");
            SetHistoryHeader("ParetoDeltaCvar", "Pareto ΔCVaR");
            SetHistoryHeader("ParetoFrri", "Pareto FRRI");
            SetHistoryHeader("ParetoBalanceRupture", "Pareto unabsorbed");
            SetHistoryHeader("RobustnessIndex", "Robustness");
            SetHistoryHeader("ActivitiesLabel", "Activities");
            SetHistoryHeader("ActivityCount", "Count");

            foreach (DataGridViewRow row in _gridCrashingHistory.Rows)
            {
                var scenario = row.DataBoundItem as CrashScenarioRowView;
                if (scenario == null)
                    continue;
                row.DefaultCellStyle.BackColor = GetStructuralStatusColor(scenario.StructuralStatus);
            }
        }

        private void SetHistoryHeader(string columnName, string header)
        {
            if (_gridCrashingHistory.Columns[columnName] != null)
                _gridCrashingHistory.Columns[columnName].HeaderText = header;
        }

        private static List<CrashingCandidateActivityDto> FilterEffectiveCrashingCandidates(IEnumerable<CrashingCandidateActivityDto> candidates)
        {
            if (candidates == null)
                return new List<CrashingCandidateActivityDto>();

            return candidates
                .Where(IsEffectiveCrashingCandidate)
                .OrderByDescending(x => x.FrmPriority)
                .ThenBy(x => x.ActivityId)
                .ToList();
        }

        private static bool IsEffectiveCrashingCandidate(CrashingCandidateActivityDto candidate)
        {
            return candidate != null
                && !candidate.IsDummy
                && candidate.IsEligible
                && candidate.NewDuration < candidate.NominalDuration
                && candidate.RecommendedNewDuration < candidate.NominalDuration
                && candidate.Reduction > 0;
        }

        private void PopulateCrashing(ExecutionSummary summary)
        {
            var crashing = summary.Crashing ?? new CrashingResultDto();
            var candidates = crashing.Candidates ?? new List<CrashingCandidateActivityDto>();
            var scenarios = (crashing.Scenarios ?? new List<CrashingScenarioResultDto>())
                .Select(x =>
                {
                    if (string.IsNullOrWhiteSpace(x.StructuralStatus))
                        x.StructuralStatus = ClassifyStructuralStatus(x.IsStructurallyRobust, x.Sif, x.RobustnessIndex);
                    x.IsStructurallyAcceptable = string.Equals(x.StructuralStatus, "ROBUST", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(x.StructuralStatus, "FEASIBLE", StringComparison.OrdinalIgnoreCase);
                    return x;
                })
                .ToList();

            if (candidates.Count == 0)
                candidates = BuildFallbackCrashingCandidates(summary);

            candidates = FilterEffectiveCrashingCandidates(candidates);
            bool hasEffectiveCrashingCandidates = candidates.Count > 0;

            _crashCandidates = new BindingList<CrashingCandidateActivityDto>(candidates.ToList());
            AutoSelectDefaultCrashingCandidates();

            _gridCrashingScenario.AutoGenerateColumns = true;
            _gridCrashingScenario.EditMode = DataGridViewEditMode.EditOnEnter;
            _gridCrashingScenario.AllowUserToResizeColumns = true;
            _gridCrashingScenario.DataSource = _crashCandidates;
            ApplyCrashingCandidateGridHeaders();
            UpdateCrashingCombinationEstimate();

            _allCrashScenarios = scenarios;
            _allCrashScenarioRows = BuildCrashScenarioRows(crashing, scenarios, summary != null && summary.Frm != null ? summary.Frm.SifGlobal : 0.0);

            var bestGlobal = _allCrashScenarios.OrderBy(x => x.Rank).FirstOrDefault();
            var bestAcceptable = _allCrashScenarios.FirstOrDefault(x => x.IsStructurallyAcceptable);
            _lblCrashBestGlobalCard.Text = bestGlobal != null
                ? string.Format("{0}\r\nStatus: {1}\r\nΔMk: {2}\r\nΔCVaR95: {3:0.##}", bestGlobal.ScenarioName, bestGlobal.StructuralStatus, bestGlobal.DeltaMakespan, bestGlobal.DeltaCVaR95)
                : "No scenario was executed.";
            _lblCrashBestAcceptableCard.Text = bestAcceptable != null
                ? string.Format("{0}\r\nStatus: {1}\r\nΔMk: {2}\r\nΔCVaR95: {3:0.##}", bestAcceptable.ScenarioName, bestAcceptable.StructuralStatus, bestAcceptable.DeltaMakespan, bestAcceptable.DeltaCVaR95)
                : "No structurally acceptable scenario.";
            _lblCrashDistributionCard.Text = string.Format(
                "Total: {0}\r\nRobust: {1}\r\nFeasible: {2}\r\nFragile: {3}\r\nInviable: {4}",
                _allCrashScenarios.Count,
                _allCrashScenarios.Count(x => string.Equals(x.StructuralStatus, "ROBUST", StringComparison.OrdinalIgnoreCase)),
                _allCrashScenarios.Count(x => string.Equals(x.StructuralStatus, "FEASIBLE", StringComparison.OrdinalIgnoreCase)),
                _allCrashScenarios.Count(x => string.Equals(x.StructuralStatus, "FRAGILE", StringComparison.OrdinalIgnoreCase)),
                _allCrashScenarios.Count(x => string.Equals(x.StructuralStatus, "INVIABLE", StringComparison.OrdinalIgnoreCase)));

            _lblCrashSummary.Text = hasEffectiveCrashingCandidates
                ? Safe(crashing.SummaryText)
                : "No effective crashing candidates were found for the selected baseline. The candidates grid and scenario grid remain empty because no activity has admissible structural compression.";

            _crashFilterStatus = "ALL";
            UpdateCrashFilterButtons();
            ApplyCrashingScenarioFilter();

            if (_allCrashScenarios.Count == 0)
            {
                if (!hasEffectiveCrashingCandidates)
                {
                    _lblCrashClassification.Text = "Classification: no effective crashing candidates";
                    _lblCrashBestGlobalCard.Text = "No Sj scenario generated.";
                    _lblCrashBestAcceptableCard.Text = "No structurally acceptable scenario.";
                    _lblCrashDistributionCard.Text = "Total: 0\r\nNo effective candidates.";
                }
                else
                {
                    _lblCrashClassification.Text = "Classification: FRM-guided candidate";
                    if (_crashCandidates.Count > 0)
                        BindCrashingCandidateDetails(_crashCandidates[0]);
                }
            }

            ConfigureCrashingLabels();
        }


        private List<CrashScenarioRowView> BuildCrashScenarioRows(CrashingResultDto crashing, List<CrashingScenarioResultDto> primaryScenarios, double baselineSif)
        {
            var result = new List<CrashScenarioRowView>();

            foreach (var scenario in primaryScenarios ?? new List<CrashingScenarioResultDto>())
            {
                if (scenario == null)
                    continue;

                result.Add(new CrashScenarioRowView
                {
                    Primary = scenario,
                    BaselineSif = baselineSif
                });
            }

            ApplyCrashParetoDominance(result);
            return result;
        }

        private static string BuildCrashingScenarioSignature(CrashingScenarioResultDto scenario)
        {
            if (scenario == null || scenario.CrashedDurations == null || scenario.CrashedDurations.Count == 0)
                return string.Empty;

            return string.Join("|", scenario.CrashedDurations
                .OrderBy(kv => kv.Key)
                .Select(kv => kv.Key.ToString(System.Globalization.CultureInfo.InvariantCulture) + "->" + kv.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .ToArray());
        }

        private static void ApplyCrashParetoDominance(List<CrashScenarioRowView> rows)
        {
            var list = (rows ?? new List<CrashScenarioRowView>()).Where(x => x != null && x.Primary != null).ToList();
            foreach (var candidate in list)
            {
                candidate.IsParetoDeltaCvar = true;
                candidate.IsParetoFrri = true;
                candidate.IsParetoBalanceRupture = true;
                candidate.DominatedByDeltaCvar = string.Empty;
                candidate.DominatedByFrri = string.Empty;
                candidate.DominatedByBalanceRupture = string.Empty;
            }

            foreach (var candidate in list)
            {
                foreach (var other in list)
                {
                    if (ReferenceEquals(candidate, other))
                        continue;

                    bool dominatesDeltaCvar = other.DeltaMakespan <= candidate.DeltaMakespan
                        && other.DeltaCVaR95 <= candidate.DeltaCVaR95
                        && (other.DeltaMakespan < candidate.DeltaMakespan || other.DeltaCVaR95 < candidate.DeltaCVaR95);
                    if (dominatesDeltaCvar)
                    {
                        candidate.IsParetoDeltaCvar = false;
                        candidate.DominatedByDeltaCvar = other.ScenarioName;
                    }

                    bool dominatesFrri = other.DeltaMakespan <= candidate.DeltaMakespan
                        && other.Frri >= candidate.Frri
                        && (other.DeltaMakespan < candidate.DeltaMakespan || other.Frri > candidate.Frri);
                    if (dominatesFrri)
                    {
                        candidate.IsParetoFrri = false;
                        candidate.DominatedByFrri = other.ScenarioName;
                    }

                    bool dominatesBalanceRupture = other.DeltaMakespan <= candidate.DeltaMakespan
                        && other.DeltaMeanUnabsorbedWork <= candidate.DeltaMeanUnabsorbedWork
                        && (other.DeltaMakespan < candidate.DeltaMakespan || other.DeltaMeanUnabsorbedWork < candidate.DeltaMeanUnabsorbedWork);
                    if (dominatesBalanceRupture)
                    {
                        candidate.IsParetoBalanceRupture = false;
                        candidate.DominatedByBalanceRupture = other.ScenarioName;
                    }
                }
            }
        }

        private void AutoSelectDefaultCrashingCandidates()
        {
            if (_crashCandidates == null || _crashCandidates.Count == 0)
                return;

            bool alreadySelected = _crashCandidates.Any(x => x != null && x.Use && x.IsEligible && !x.IsDummy && x.NewDuration < x.NominalDuration);
            if (alreadySelected)
                return;

            foreach (var candidate in _crashCandidates
                .Where(x => x != null && x.IsEligible && !x.IsDummy && x.NewDuration < x.NominalDuration)
                .OrderByDescending(x => x.FrmPriority)
                .ThenBy(x => x.ActivityId))
            {
                candidate.Use = true;
            }
        }

        private bool EnsureCrashingSelectionBeforeRun()
        {
            if (_crashCandidates == null || _crashCandidates.Count == 0)
            {
                _lblCrashSummary.Text = "No effective crashing candidates were found for the selected baseline. No Sj scenario can be generated.";
                MessageBox.Show(
                    "No effective crashing candidates were found for the selected baseline. No Sj scenario can be generated.",
                    "Crashing",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }

            int eligibleCount = _crashCandidates.Count(x => x != null && x.IsEligible && !x.IsDummy && x.NewDuration < x.NominalDuration);
            if (eligibleCount == 0)
            {
                _lblCrashSummary.Text = "No FRM-eligible crashing candidate was found for this baseline. The scenarios grid will remain empty because there is no activity with admissible structural compression.";
                MessageBox.Show(
                    "No FRM-eligible crashing candidate was found for this baseline. The scenarios grid will remain empty because there is no activity with admissible structural compression.",
                    "Crashing",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }

            int selectedCount = _crashCandidates.Count(x => x != null && x.Use && x.IsEligible && !x.IsDummy && x.NewDuration < x.NominalDuration);
            if (selectedCount == 0)
            {
                AutoSelectDefaultCrashingCandidates();
                _gridCrashingScenario.Refresh();
                selectedCount = _crashCandidates.Count(x => x != null && x.Use && x.IsEligible && !x.IsDummy && x.NewDuration < x.NominalDuration);
            }

            if (selectedCount == 0)
            {
                _lblCrashSummary.Text = "Select at least one eligible activity in the Use column before running crashing scenarios.";
                MessageBox.Show(
                    "Select at least one eligible activity in the Use column before running crashing scenarios.",
                    "Crashing",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }

            return true;
        }

        private void GridCrashingScenario_SelectionChanged(object sender, EventArgs e)
        {
            if (_gridCrashingScenario.CurrentRow == null)
                return;

            int rowIndex = _gridCrashingScenario.CurrentRow.Index;
            if (rowIndex < 0 || _crashCandidates == null || rowIndex >= _crashCandidates.Count)
                return;

            BindCrashingCandidateDetails(_crashCandidates[rowIndex]);
        }

        private void GridCrashingHistory_SelectionChanged(object sender, EventArgs e)
        {
            if (_suppressCrashSelectionSync)
                return;

            UpdateCrashingScatterFromGridSelection();
        }

        private void GridCrashingScenario_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (_gridCrashingScenario.IsCurrentCellDirty)
                _gridCrashingScenario.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void GridCrashingScenario_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e == null || e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            if (_crashCandidates == null || e.RowIndex >= _crashCandidates.Count)
                return;

            var candidate = _crashCandidates[e.RowIndex];
            if (candidate == null)
                return;

            if (candidate.NewDuration < candidate.MinimumDuration)
                candidate.NewDuration = candidate.MinimumDuration;

            if (candidate.NewDuration > candidate.NominalDuration)
                candidate.NewDuration = candidate.RecommendedNewDuration;

            if (!candidate.IsEligible)
            {
                candidate.Use = false;
                candidate.NewDuration = candidate.NominalDuration;
            }

            _gridCrashingScenario.Refresh();
            UpdateCrashingCombinationEstimate();
            BindCrashingCandidateDetails(candidate);
        }

        private void BindCrashingCandidateDetails(CrashingCandidateActivityDto candidate)
        {
            if (candidate == null)
                return;


            _lblCrashClassification.Text = candidate.IsEligible
                ? "Classification: FRM-guided candidate"
                : "Classification: not eligible";
        }

        private void BindCrashingScenarioDetails(CrashScenarioRowView row)
        {
            if (row == null || row.Primary == null)
                return;

            var scenario = row.Primary;
            string status = string.IsNullOrWhiteSpace(scenario.StructuralStatus)
                ? ClassifyStructuralStatus(scenario.IsStructurallyRobust, scenario.Sif, scenario.RobustnessIndex)
                : scenario.StructuralStatus;

            _lblCrashClassification.Text = string.Format(
                "Classification: {0} | Acceptable={1} | ΔMk={2} | ΔSIF={3:0.###} | ΔP95={4:0.##} | ΔCVaR95={5:0.##} | FRRI={6:0.###} | P(Bal rupture)={7:0.###} | Mean Bal use={8:0.##} | Mean unabsorbed={9:0.##} | Unabsorbed ratio={10:0.###} | Min Bal={11:0.##}",
                status,
                scenario.IsStructurallyAcceptable ? "YES" : "NO",
                scenario.DeltaMakespan,
                row.DeltaSifPrimary,
                scenario.DeltaP95,
                scenario.DeltaCVaR95,
                scenario.Frri,
                scenario.ScenarioBalanceRuptureProbability,
                scenario.ScenarioMeanBalanceUsage,
                scenario.ScenarioMeanUnabsorbedWork,
                scenario.ScenarioMeanUnabsorbedWorkRatio,
                scenario.ScenarioMinObservedBalance);
        }

        private void BindCrashingScenarioDetails(CrashingScenarioResultDto scenario)
        {
            if (scenario == null)
                return;
            BindCrashingScenarioDetails(new CrashScenarioRowView { Primary = scenario, BaselineSif = 0.0 });
        }

        private async void BtnCrashRunAll_Click(object sender, EventArgs e)
        {
            if (!EnsureCrashingSelectionBeforeRun())
                return;

            PrepareCrashingOptionsFromGrid();
            _request.Crashing.MaxScenarioCount = Math.Max(1, (int)_numCrashScenarioLimit.Value);
            _request.Crashing.MaxCombinationSize = Math.Max(1, ParseCrashCombinationSize());

            await RunCrashingScenariosOnlyAsync();

            KeepCrashingTabSelectedAfterScenarioRun();
        }

        private void KeepCrashingTabSelectedAfterScenarioRun()
        {
            if (tabControlMain == null || _tabCrashing == null)
                return;

            if (tabControlMain.TabPages.Contains(_tabCrashing))
                tabControlMain.SelectedTab = _tabCrashing;

            if (_gridCrashingHistory != null)
                _gridCrashingHistory.Focus();
        }

        private void BtnCrashClear_Click(object sender, EventArgs e)
        {
            foreach (var candidate in _crashCandidates)
            {
                candidate.Use = false;
                candidate.NewDuration = candidate.RecommendedNewDuration;
            }

            _gridCrashingScenario.Refresh();
            UpdateCrashingCombinationEstimate();
            _allCrashScenarios = new List<CrashingScenarioResultDto>();
            _allCrashScenarioRows = new List<CrashScenarioRowView>();
            _gridCrashingHistory.DataSource = null;
            PopulateCrashingScatter(_allCrashScenarioRows);
            _lblCrashClassification.Text = "Classification: FRM-guided candidate";
            _lblCrashSummary.Text = "Scenario designer reset.";

            if (_crashCandidates.Count > 0)
                BindCrashingCandidateDetails(_crashCandidates[0]);
        }

        private void InitializeCrashingParameterControls()
        {
            if (panelCrashToolbar == null)
                return;

            panelCrashToolbar.SuspendLayout();
            panelCrashToolbar.Height = 96;

            _btnCrashClear.Location = new Point(5, 6);
            _btnCrashClear.Size = new Size(100, 30);
            _btnCrashRunAll.Location = new Point(113, 6);
            _btnCrashRunAll.Size = new Size(160, 30);

            ConfigureCrashNumeric(_numCrashCandidateLimit, 1, 200, 20, 1);
            ConfigureCrashNumeric(_numCrashCombinationLimit, 1, 10, 3, 1);
            ConfigureCrashNumeric(_numCrashScenarioLimit, 1, 100000, 1000, 50);

            ConfigureCrashLabel(_lblCrashCandidateLimit, "Candidates", new Point(5, 47), new Size(70, 18));
            ConfigureCrashNumericLocation(_numCrashCandidateLimit, new Point(76, 44), new Size(52, 24));

            ConfigureCrashLabel(_lblCrashCombinationLimit, "Act./scn", new Point(134, 47), new Size(58, 18));
            ConfigureCrashNumericLocation(_numCrashCombinationLimit, new Point(194, 44), new Size(46, 24));

            ConfigureCrashLabel(_lblCrashScenarioLimit, "Scenarios", new Point(246, 47), new Size(65, 18));
            ConfigureCrashNumericLocation(_numCrashScenarioLimit, new Point(313, 44), new Size(66, 24));

            ConfigureCrashLabel(_lblCrashCombinationTotal, "Total combinations: 0 | Will run: 0", new Point(5, 70), new Size(374, 20));
            _lblCrashCombinationTotal.Font = new Font("Segoe UI", 8.25F, FontStyle.Bold);

            string toolTipText = "Crashing limits used by Run Scenarios. Candidates = max selected FRM-eligible activities considered. Act./scn = max activities compressed together in each scenario. Scenarios = max generated/evaluated scenarios.";
            string totalToolTipText = "Total combinations is calculated from the selected effective candidates after applying the Candidates limit and Act./scn limit. Will run is also capped by the Scenarios limit.";
            var tip = new ToolTip();
            foreach (Control c in new Control[] { _lblCrashCandidateLimit, _numCrashCandidateLimit, _lblCrashCombinationLimit, _numCrashCombinationLimit, _lblCrashScenarioLimit, _numCrashScenarioLimit })
                tip.SetToolTip(c, toolTipText);
            tip.SetToolTip(_lblCrashCombinationTotal, totalToolTipText);

            if (!panelCrashToolbar.Controls.Contains(_lblCrashCandidateLimit)) panelCrashToolbar.Controls.Add(_lblCrashCandidateLimit);
            if (!panelCrashToolbar.Controls.Contains(_numCrashCandidateLimit)) panelCrashToolbar.Controls.Add(_numCrashCandidateLimit);
            if (!panelCrashToolbar.Controls.Contains(_lblCrashCombinationLimit)) panelCrashToolbar.Controls.Add(_lblCrashCombinationLimit);
            if (!panelCrashToolbar.Controls.Contains(_numCrashCombinationLimit)) panelCrashToolbar.Controls.Add(_numCrashCombinationLimit);
            if (!panelCrashToolbar.Controls.Contains(_lblCrashScenarioLimit)) panelCrashToolbar.Controls.Add(_lblCrashScenarioLimit);
            if (!panelCrashToolbar.Controls.Contains(_numCrashScenarioLimit)) panelCrashToolbar.Controls.Add(_numCrashScenarioLimit);
            if (!panelCrashToolbar.Controls.Contains(_lblCrashCombinationTotal)) panelCrashToolbar.Controls.Add(_lblCrashCombinationTotal);

            _numCrashCandidateLimit.ValueChanged += (s, e) => { ApplyCrashingParameterControlsToRequest(); UpdateCrashingCombinationEstimate(); };
            _numCrashCombinationLimit.ValueChanged += (s, e) => { ApplyCrashingParameterControlsToRequest(); UpdateCrashingCombinationEstimate(); };
            _numCrashScenarioLimit.ValueChanged += (s, e) => { ApplyCrashingParameterControlsToRequest(); UpdateCrashingCombinationEstimate(); };

            UpdateCrashingCombinationEstimate();
            panelCrashToolbar.ResumeLayout(false);
        }

        private static void ConfigureCrashNumeric(NumericUpDown control, int min, int max, int value, int increment)
        {
            control.Minimum = min;
            control.Maximum = max;
            control.Value = Math.Max(min, Math.Min(max, value));
            control.Increment = increment;
            control.DecimalPlaces = 0;
            control.ThousandsSeparator = false;
        }

        private static void ConfigureCrashLabel(Label label, string text, Point location, Size size)
        {
            label.Text = text;
            label.Location = location;
            label.Size = size;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.AutoSize = false;
        }

        private static void ConfigureCrashNumericLocation(NumericUpDown control, Point location, Size size)
        {
            control.Location = location;
            control.Size = size;
            control.TextAlign = HorizontalAlignment.Right;
        }

        private void SyncCrashingParameterControlsFromRequest()
        {
            if (_request == null || _request.Crashing == null)
                return;

            SetCrashNumericValue(_numCrashCandidateLimit, Math.Max(1, _request.Crashing.MaxActivitiesToCrash));
            SetCrashNumericValue(_numCrashCombinationLimit, Math.Max(1, _request.Crashing.MaxCombinationSize));
            SetCrashNumericValue(_numCrashScenarioLimit, Math.Max(1, _request.Crashing.MaxScenarioCount));
        }

        private static void SetCrashNumericValue(NumericUpDown control, int value)
        {
            if (control == null)
                return;
            int bounded = Math.Max((int)control.Minimum, Math.Min((int)control.Maximum, value));
            if ((int)control.Value != bounded)
                control.Value = bounded;
        }

        private void UpdateCrashingCombinationEstimate()
        {
            if (_lblCrashCombinationTotal == null)
                return;

            int activeCount;
            bool capped;
            long total = EstimateCrashingCombinationTotal(out activeCount, out capped);
            long scenarioLimit = Math.Max(1, _numCrashScenarioLimit != null ? (long)_numCrashScenarioLimit.Value : 1L);
            long willRun = capped ? scenarioLimit : Math.Min(total, scenarioLimit);

            string totalText = capped
                ? "more than " + FormatCrashCount(total)
                : FormatCrashCount(total);

            _lblCrashCombinationTotal.Text = string.Format(
                "Total combinations: {0} | Will run: {1} | Active: {2}",
                totalText,
                FormatCrashCount(willRun),
                activeCount);
        }

        private long EstimateCrashingCombinationTotal(out int activeCount, out bool capped)
        {
            const long DisplayCap = 999999999999L;
            capped = false;
            activeCount = 0;

            if (_crashCandidates == null || _crashCandidates.Count == 0)
                return 0L;

            int maxCandidates = Math.Max(1, _numCrashCandidateLimit != null ? (int)_numCrashCandidateLimit.Value : 1);
            int maxCombinationSizeConfigured = Math.Max(1, _numCrashCombinationLimit != null ? (int)_numCrashCombinationLimit.Value : 1);

            var activeReductionLevels = (_crashCandidates ?? new BindingList<CrashingCandidateActivityDto>())
                .Where(x => x != null && x.Use && x.IsEligible && !x.IsDummy && GetCrashReductionLevelCount(x) > 0)
                .OrderByDescending(x => x.FrmPriority)
                .ThenBy(x => x.ActivityId)
                .Take(maxCandidates)
                .Select(GetCrashReductionLevelCount)
                .Where(x => x > 0)
                .ToList();

            activeCount = activeReductionLevels.Count;
            if (activeCount == 0)
                return 0L;

            int maxK = Math.Min(maxCombinationSizeConfigured, activeCount);
            var dp = new long[maxK + 1];
            dp[0] = 1L;

            foreach (int levels in activeReductionLevels)
            {
                for (int k = maxK; k >= 1; k--)
                {
                    bool multCapped;
                    long contribution = SaturatingMultiply(dp[k - 1], levels, DisplayCap, out multCapped);
                    bool addCapped;
                    dp[k] = SaturatingAdd(dp[k], contribution, DisplayCap, out addCapped);
                    capped = capped || multCapped || addCapped;
                }
            }

            long total = 0L;
            for (int k = 1; k <= maxK; k++)
            {
                bool addCapped;
                total = SaturatingAdd(total, dp[k], DisplayCap, out addCapped);
                capped = capped || addCapped;
            }

            return total;
        }

        private static int GetCrashReductionLevelCount(CrashingCandidateActivityDto candidate)
        {
            if (candidate == null)
                return 0;

            int nominal = Math.Max(0, candidate.NominalDuration);
            if (nominal <= 0)
                return 0;

            int lowerBound = candidate.MinimumDuration;
            if (lowerBound <= 0)
                lowerBound = candidate.RecommendedNewDuration;
            if (lowerBound <= 0)
                lowerBound = 1;
            if (lowerBound > nominal)
                lowerBound = nominal;

            return Math.Max(0, nominal - lowerBound);
        }

        private static long SaturatingAdd(long a, long b, long cap, out bool capped)
        {
            if (a >= cap || b >= cap || a > cap - b)
            {
                capped = true;
                return cap;
            }

            capped = false;
            return a + b;
        }

        private static long SaturatingMultiply(long a, long b, long cap, out bool capped)
        {
            if (a == 0L || b == 0L)
            {
                capped = false;
                return 0L;
            }

            if (a >= cap || b >= cap || a > cap / b)
            {
                capped = true;
                return cap;
            }

            capped = false;
            return a * b;
        }

        private static string FormatCrashCount(long value)
        {
            return value.ToString("N0", System.Globalization.CultureInfo.CurrentCulture);
        }

        private void ApplyCrashingParameterControlsToRequest()
        {
            if (_request == null)
                return;

            if (_request.Crashing == null)
                _request.Crashing = new CrashingOptionsDto();

            _request.Crashing.MaxActivitiesToCrash = Math.Max(1, (int)_numCrashCandidateLimit.Value);
            _request.Crashing.MaxCombinationSize = Math.Max(1, (int)_numCrashCombinationLimit.Value);
            _request.Crashing.MaxScenarioCount = Math.Max(1, (int)_numCrashScenarioLimit.Value);
        }

        private void ConfigureCrashingLabels()
        {
            _groupCrashScenario.Text = "Crash Candidates";
            _groupCrashSummary.Text = "Chapter 3 - integrated crashing assessment";
            _btnCrashRunAll.Text = "Run Scenarios";
            _btnCrashClear.Text = "Reset";
        }

        private int ParseCrashCombinationSize()
        {
            int selectedCount = (_crashCandidates ?? new BindingList<CrashingCandidateActivityDto>())
                .Count(x => x.Use && x.IsEligible && x.NewDuration < x.NominalDuration);

            int configured = Math.Max(1, (int)_numCrashCombinationLimit.Value);
            return Math.Max(1, Math.Min(configured, Math.Max(1, selectedCount)));
        }

        private void PrepareCrashingOptionsFromGrid()
        {
            if (_request == null)
                return;

            if (_request.Crashing == null)
                _request.Crashing = new CrashingOptionsDto();

            _request.Crashing.Enabled = true;
            _request.Crashing.UseFrmGuidance = true;
            _request.Crashing.RecalculateRiskAfterCrash = true;
            ApplyCrashingParameterControlsToRequest();
            _request.Crashing.MaxScenarioCount = Math.Max(1, (int)_numCrashScenarioLimit.Value);
            _request.Crashing.MaxCombinationSize = ParseCrashCombinationSize();
            _request.Crashing.CandidateActivities = (_crashCandidates ?? new BindingList<CrashingCandidateActivityDto>())
                .Select(x => new CrashingCandidateActivityDto
                {
                    Use = x.Use && x.IsEligible && x.NewDuration < x.NominalDuration,
                    ActivityId = x.ActivityId,
                    ActivityName = x.ActivityName,
                    NominalDuration = x.NominalDuration,
                    MinimumDuration = x.RecommendedNewDuration,
                    NewDuration = x.RecommendedNewDuration,
                    RecommendedNewDuration = x.RecommendedNewDuration,
                    IsEligible = x.IsEligible,
                    IsDummy = x.IsDummy,
                    FrmSlackI = x.FrmSlackI,
                    FrmCriticality = x.FrmCriticality,
                    FrmSensitivity = x.FrmSensitivity,
                    FrmBalanceRisk = x.FrmBalanceRisk,
                    FrmPriority = x.FrmPriority
                })
                .ToList();

            _request.Crashing.MaxActivitiesToCrash = Math.Max(1, Math.Min((int)_numCrashCandidateLimit.Value, _request.Crashing.CandidateActivities.Count(x => x.Use)));
        }

        private List<CrashingCandidateActivityDto> BuildFallbackCrashingCandidates(ExecutionSummary summary)
        {
            var frmById = (summary != null && summary.Frm != null ? summary.Frm.Activities : new List<FrmActivityResultDto>())
                .GroupBy(x => x.ActivityId)
                .ToDictionary(g => g.Key, g => g.First());

            var resourceDiagnostics = (summary != null && summary.Frm != null ? summary.Frm.ResourceDiagnostics : new List<FrmResourceDiagnosticDto>())
                .GroupBy(x => x.ResourceId)
                .ToDictionary(g => g.Key, g => g.First());

            var projectById = (_request != null && _request.Project != null ? _request.Project.Activities : new List<ActivityDto>())
                .Where(x => x != null)
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First());

            var candidates = (summary != null && summary.Baseline != null ? summary.Baseline.Activities : new List<ScheduledActivityDto>())
                .OrderBy(a => a.Start)
                .ThenBy(a => a.Finish)
                .ThenBy(a => a.ActivityId)
                .Select(a =>
                {
                    FrmActivityResultDto frm;
                    frmById.TryGetValue(a.ActivityId, out frm);

                    ActivityDto activity;
                    projectById.TryGetValue(a.ActivityId, out activity);

                    int nominal = a.Duration;
                    int frmFixedDuration = nominal;
                    if (frm != null && frm.DSMin > 0 && frm.DSMin < nominal)
                        frmFixedDuration = frm.DSMin;
                    else if (frm != null && frm.DMin > 0 && frm.DMin < nominal)
                        frmFixedDuration = frm.DMin;
                    else if (frm != null && frm.DNew > 0 && frm.DNew < nominal)
                        frmFixedDuration = frm.DNew;

                    bool hasStructuralReduction = frm != null && frmFixedDuration > 0 && frmFixedDuration < nominal;
                    int fixedDuration = hasStructuralReduction ? frmFixedDuration : nominal;
                    bool hasSufficientFrmBalance = HasSufficientFrmBalanceForCompression(activity, frm, resourceDiagnostics, fixedDuration);
                    int slack = frm != null ? frm.SlackI : nominal;
                    double sensitivity = frm != null && frm.ScoreBrutoByResourceId != null
                        ? frm.ScoreBrutoByResourceId.Values.Sum(v => Math.Abs(v))
                        : 0.0;
                    double criticality = slack <= 0 ? 1.0 : 1.0 / (1.0 + slack);
                    double balanceRisk = 0.0;

                    if (activity != null && activity.Assignments != null && activity.Assignments.Count > 0)
                    {
                        double sumRisk = 0.0;
                        int countRisk = 0;

                        foreach (var assignment in activity.Assignments)
                        {
                            FrmResourceDiagnosticDto diagnostic;
                            if (assignment == null || !resourceDiagnostics.TryGetValue(assignment.ResourceId, out diagnostic) || diagnostic == null)
                                continue;

                            double resourceRisk = 1.0 - Math.Max(0.0, Math.Min(1.0, diagnostic.RobustnessIndex));
                            if (!diagnostic.IsRobust)
                                resourceRisk = Math.Min(1.0, resourceRisk + 0.35);

                            sumRisk += resourceRisk;
                            countRisk++;
                        }

                        if (countRisk > 0)
                            balanceRisk = Math.Max(0.0, Math.Min(1.0, sumRisk / countRisk));
                    }

                    double reductionNorm = nominal > 0 ? (double)Math.Max(0, nominal - fixedDuration) / nominal : 0.0;
                    double priority = Math.Max(0.0, Math.Min(1.0,
                        (0.35 * reductionNorm) +
                        (0.35 * criticality) +
                        (0.20 * Math.Min(1.0, sensitivity > 0 ? sensitivity / (sensitivity + 1.0) : 0.0)) +
                        (0.10 * balanceRisk)));

                    return new CrashingCandidateActivityDto
                    {
                        Use = false,
                        ActivityId = a.ActivityId,
                        ActivityName = a.Name,
                        NominalDuration = nominal,
                        MinimumDuration = fixedDuration,
                        NewDuration = fixedDuration,
                        RecommendedNewDuration = fixedDuration,
                        IsEligible = hasStructuralReduction && hasSufficientFrmBalance,
                        IsDummy = activity != null && activity.IsDummy,
                        FrmSlackI = slack,
                        FrmCriticality = criticality,
                        FrmSensitivity = sensitivity,
                        FrmBalanceRisk = balanceRisk,
                        FrmPriority = priority
                    };
                })
                .ToList();

            return candidates;
        }


        private static bool HasSufficientFrmBalanceForCompression(
            ActivityDto activity,
            FrmActivityResultDto frmActivity,
            Dictionary<int, FrmResourceDiagnosticDto> resourceDiagnostics,
            int compressedDuration)
        {
            if (activity == null || frmActivity == null || resourceDiagnostics == null)
                return false;
            if (activity.Assignments == null || activity.Assignments.Count == 0)
                return false;

            int nominal = Math.Max(0, frmActivity.DurationNominal);
            if (nominal <= 0 || compressedDuration <= 0 || compressedDuration >= nominal)
                return false;

            bool hasCheckedResource = false;

            foreach (var assignment in activity.Assignments)
            {
                if (assignment == null)
                    continue;

                int demand = (int)Math.Round(assignment.Units, MidpointRounding.AwayFromZero);
                if (demand <= 0)
                    continue;

                FrmResourceDiagnosticDto diagnostic;
                if (!resourceDiagnostics.TryGetValue(assignment.ResourceId, out diagnostic) || diagnostic == null)
                    return false;

                if (!diagnostic.IsRobust)
                    return false;

                int requiredBalance = demand * Math.Max(0, nominal - compressedDuration);
                if (diagnostic.BalanceFinal - requiredBalance < 0)
                    return false;

                int activityBalance;
                if (frmActivity.BalanceByResourceId != null &&
                    frmActivity.BalanceByResourceId.TryGetValue(assignment.ResourceId, out activityBalance) &&
                    activityBalance < 0)
                    return false;

                hasCheckedResource = true;
            }

            return hasCheckedResource;
        }

        private void ApplyCrashingCandidateGridHeaders()
        {
            if (_gridCrashingScenario.Columns["Use"] != null)
                _gridCrashingScenario.Columns["Use"].HeaderText = "Use";

            if (_gridCrashingScenario.Columns["ActivityId"] != null)
                _gridCrashingScenario.Columns["ActivityId"].HeaderText = "Act.";

            if (_gridCrashingScenario.Columns["ActivityName"] != null)
                _gridCrashingScenario.Columns["ActivityName"].HeaderText = "Name";

            if (_gridCrashingScenario.Columns["NominalDuration"] != null)
                _gridCrashingScenario.Columns["NominalDuration"].HeaderText = "Dur. Nom.";

            if (_gridCrashingScenario.Columns["NewDuration"] != null)
                _gridCrashingScenario.Columns["NewDuration"].HeaderText = "Dur. New";

            if (_gridCrashingScenario.Columns["Reduction"] != null)
                _gridCrashingScenario.Columns["Reduction"].HeaderText = "Reduction";

            if (_gridCrashingScenario.Columns["FrmPriority"] != null)
            {
                _gridCrashingScenario.Columns["FrmPriority"].HeaderText = "Prio. FRM";
                _gridCrashingScenario.Columns["FrmPriority"].DefaultCellStyle.Format = "0.###";
                _gridCrashingScenario.Columns["FrmPriority"].ReadOnly = true;
            }

            if (_gridCrashingScenario.Columns["FrmBalanceRiskDisplay"] != null)
            {
                _gridCrashingScenario.Columns["FrmBalanceRiskDisplay"].HeaderText = "Balance Risk";
                _gridCrashingScenario.Columns["FrmBalanceRiskDisplay"].ReadOnly = true;
            }

            string[] hidden = { "MinimumDuration", "IsEligible", "IsDummy", "RecommendedNewDuration", "FrmSlackI", "FrmCriticality", "FrmSensitivity", "FrmBalanceRisk", "FrmBalanceRiskBand" };
            foreach (string name in hidden)
            {
                if (_gridCrashingScenario.Columns[name] != null)
                    _gridCrashingScenario.Columns[name].Visible = false;
            }

            if (_gridCrashingScenario.Columns["ActivityName"] != null)
            {
                _gridCrashingScenario.Columns["ActivityName"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                _gridCrashingScenario.Columns["ActivityName"].Width = 260;
                _gridCrashingScenario.Columns["ActivityName"].Resizable = DataGridViewTriState.True;
            }

            if (_gridCrashingScenario.Columns["NewDuration"] != null)
                _gridCrashingScenario.Columns["NewDuration"].ReadOnly = true;

            if (_gridCrashingScenario.Columns["Reduction"] != null)
                _gridCrashingScenario.Columns["Reduction"].ReadOnly = true;

            ConfigureCrashingCandidateGridRows();
        }

        private void GridCrashingScenario_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            ConfigureCrashingCandidateGridRows();
        }

        private void ConfigureCrashingCandidateGridRows()
        {
            if (_gridCrashingScenario.Rows == null || _gridCrashingScenario.Columns["Use"] == null)
                return;

            foreach (DataGridViewRow row in _gridCrashingScenario.Rows)
            {
                var candidate = row.DataBoundItem as CrashingCandidateActivityDto;
                if (candidate == null)
                    continue;

                var useCell = row.Cells["Use"] as DataGridViewCheckBoxCell;
                if (useCell == null)
                    continue;

                bool canSelect = candidate.IsEligible && !candidate.IsDummy;
                useCell.ReadOnly = !canSelect;
                useCell.Style.BackColor = canSelect ? Color.White : Color.Gainsboro;
                useCell.Style.SelectionBackColor = canSelect ? _gridCrashingScenario.DefaultCellStyle.SelectionBackColor : Color.Gainsboro;
                useCell.Style.SelectionForeColor = canSelect ? _gridCrashingScenario.DefaultCellStyle.SelectionForeColor : Color.DimGray;
                useCell.ToolTipText = canSelect
                    ? "FRM identified an admissible structural interval for this activity."
                    : "Selection disabled because FRM did not identify an admissible structural interval for this activity.";

                if (!canSelect)
                    candidate.Use = false;
            }
        }


    }
}
