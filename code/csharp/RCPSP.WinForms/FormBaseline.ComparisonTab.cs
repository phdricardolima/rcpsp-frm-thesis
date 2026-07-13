using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using RCPSP.Contracts;

namespace RCPSP.WinForms
{
    public partial class FormBaseline : Form
    {
        private void InitializeComparisonLayout()
        {
            if (_tabComparison == null || _splitComparison == null)
                return;

            _tabComparison.Text = "Comparison";
            _tabComparison.UseVisualStyleBackColor = true;

            _splitComparison.Panel1.Controls.Clear();
            _splitComparison.Panel2.Controls.Clear();

            panelComparisonLeft = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8)
            };

            _groupComparisonControls = new GroupBox
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                Text = "Final S0 x Sj Comparison"
            };

            lblComparisonReference = new Label
            {
                AutoSize = true,
                Location = new Point(10, 28),
                Name = "lblComparisonReference",
                Size = new Size(100, 13),
                Text = "Selected baseline:"
            };

            _cmbComparisonReference = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(125, 24),
                Name = "_cmbComparisonReference",
                Size = new Size(215, 21)
            };

            _btnComparisonRun = new Button
            {
                Location = new Point(10, 62),
                Name = "_btnComparisonRun",
                Size = new Size(180, 30),
                Text = "Compare S0 x Sj"
            };

            _lblComparisonSummary = new Label
            {
                BackColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5F),
                Location = new Point(10, 108),
                Name = "_lblComparisonSummary",
                Padding = new Padding(8),
                Size = new Size(330, 292),
                Text = "Comparison not executed yet. Load a baseline, run crashing scenarios, and compare S0 against its Sj scenarios."
            };

            _groupComparisonControls.Controls.Add(lblComparisonReference);
            _groupComparisonControls.Controls.Add(_cmbComparisonReference);
            _groupComparisonControls.Controls.Add(_btnComparisonRun);
            _groupComparisonControls.Controls.Add(_lblComparisonSummary);
            panelComparisonLeft.Controls.Add(_groupComparisonControls);

            panelComparisonRight = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8)
            };

            splitComparisonRight = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 345,
                Name = "splitComparisonRight"
            };

            _groupComparisonState = new GroupBox
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                Text = "S0 x Sj decision indicators"
            };

            _groupComparisonTable = new GroupBox
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                Text = "Final S0 x Sj comparison table"
            };

            _tabComparisonCharts = new TabControl
            {
                Dock = DockStyle.Fill
            };

            _tabComparisonSifCvar = new TabPage
            {
                Text = "CVaR95 x SIF",
                UseVisualStyleBackColor = true
            };

            _tabComparisonMakespanCvar = new TabPage
            {
                Text = "CVaR95 x Makespan",
                UseVisualStyleBackColor = true
            };

            _tabComparisonDeltaCvar = new TabPage
            {
                Text = "ΔMk x ΔCVaR95",
                UseVisualStyleBackColor = true
            };

            _tabComparisonBalanceRupture = new TabPage
            {
                Text = "Unabsorbed work x SIF",
                UseVisualStyleBackColor = true
            };

            _tabComparisonPareto = new TabPage
            {
                Text = "Pareto",
                UseVisualStyleBackColor = true
            };

            _chartComparisonSifCvar = new Chart();
            ConfigureComparisonChart(_chartComparisonSifCvar, "SIF", "CVaR95 Delay", false);

            _chartComparisonMakespanCvar = new Chart();
            ConfigureComparisonChart(_chartComparisonMakespanCvar, "Makespan", "CVaR95 Delay", false);

            _chartComparisonDeltaCvar = new Chart();
            ConfigureComparisonChart(_chartComparisonDeltaCvar, "ΔMakespan", "ΔCVaR95", false);

            _chartComparisonBalanceRupture = new Chart();
            ConfigureComparisonChart(_chartComparisonBalanceRupture, "SIF", "Mean unabsorbed work", false);

            _chartComparisonParetoDominance = new Chart();
            ConfigureComparisonParetoChart(_chartComparisonParetoDominance);


            _chartComparisonTradeoff = _chartComparisonSifCvar;

            _tabComparisonSifCvar.Controls.Add(_chartComparisonSifCvar);
            _tabComparisonMakespanCvar.Controls.Add(_chartComparisonMakespanCvar);
            _tabComparisonDeltaCvar.Controls.Add(_chartComparisonDeltaCvar);
            _tabComparisonBalanceRupture.Controls.Add(_chartComparisonBalanceRupture);
            _tabComparisonPareto.Controls.Add(_chartComparisonParetoDominance);

            _tabComparisonCharts.TabPages.Add(_tabComparisonSifCvar);
            _tabComparisonCharts.TabPages.Add(_tabComparisonMakespanCvar);
            _tabComparisonCharts.TabPages.Add(_tabComparisonDeltaCvar);
            _tabComparisonCharts.TabPages.Add(_tabComparisonBalanceRupture);
            _tabComparisonCharts.TabPages.Add(_tabComparisonPareto);
            _groupComparisonState.Controls.Add(_tabComparisonCharts);

            _gridComparisonGrid = CreateReadOnlyGrid();
            _gridComparisonGrid.BackgroundColor = Color.White;
            _gridComparisonGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _gridComparisonGrid.MultiSelect = true;
            _gridComparisonGrid.ReadOnly = true;
            _gridComparisonGrid.AutoGenerateColumns = true;
            _gridComparisonGrid.SelectionChanged -= GridComparisonSelectionChanged;
            _gridComparisonGrid.SelectionChanged += GridComparisonSelectionChanged;
            _groupComparisonTable.Controls.Add(_gridComparisonGrid);

            splitComparisonRight.Panel1.Controls.Add(_groupComparisonState);
            splitComparisonRight.Panel2.Controls.Add(_groupComparisonTable);
            panelComparisonRight.Controls.Add(splitComparisonRight);

            _splitComparison.Panel1.Controls.Add(panelComparisonLeft);
            _splitComparison.Panel2.Controls.Add(panelComparisonRight);
        }

        private static void ConfigureComparisonChart(Chart chart, string xAxisTitle, string yAxisTitle, bool yAsPercent)
        {
            chart.Dock = DockStyle.Fill;
            chart.BackColor = Color.White;
            chart.ChartAreas.Clear();
            chart.Series.Clear();
            chart.Legends.Clear();
            chart.Titles.Clear();

            var area = new ChartArea("ComparisonArea");
            area.AxisX.Title = xAxisTitle;
            area.AxisY.Title = yAxisTitle;
            area.AxisX.Minimum = Double.NaN;
            area.AxisX.Maximum = Double.NaN;
            area.AxisX.MajorGrid.LineColor = Color.Gainsboro;
            area.AxisY.MajorGrid.LineColor = Color.Gainsboro;
            if (yAsPercent)
                area.AxisY.LabelStyle.Format = "0.00%";
            chart.ChartAreas.Add(area);

            var legend = new Legend("ComparisonLegend");
            legend.Docking = Docking.Top;
            chart.Legends.Add(legend);

            var runs = new Series("Baselines");
            runs.LegendText = "Crashing scenarios";
            runs.ChartType = SeriesChartType.Point;
            runs.MarkerSize = 9;
            runs.Legend = "ComparisonLegend";
            chart.Series.Add(runs);

            var selected = new Series("Selected");
            selected.ChartType = SeriesChartType.Point;
            selected.MarkerStyle = MarkerStyle.Circle;
            selected.MarkerSize = 12;
            selected.Legend = "ComparisonLegend";
            selected.IsVisibleInLegend = false;
            chart.Series.Add(selected);

            var reference = new Series("Reference");
            reference.LegendText = "Selected baseline S0";
            reference.ChartType = SeriesChartType.Point;
            reference.MarkerStyle = MarkerStyle.Star5;
            reference.MarkerSize = 16;
            reference.Color = Color.ForestGreen;
            reference.Legend = "ComparisonLegend";
            chart.Series.Add(reference);
        }

        private static void ConfigureComparisonParetoChart(Chart chart)
        {
            chart.Dock = DockStyle.Fill;
            chart.BackColor = Color.White;
            chart.ChartAreas.Clear();
            chart.Series.Clear();
            chart.Legends.Clear();
            chart.Titles.Clear();

            var area = new ChartArea("ComparisonParetoArea");
            area.AxisX.Title = "Makespan";
            area.AxisY.Title = "Relative CVaR95 Delay";
            area.AxisY.LabelStyle.Format = "0.00%";
            area.AxisX.MajorGrid.LineColor = Color.Gainsboro;
            area.AxisY.MajorGrid.LineColor = Color.Gainsboro;
            chart.ChartAreas.Add(area);

            var legend = new Legend("ComparisonParetoLegend");
            legend.Docking = Docking.Top;
            chart.Legends.Add(legend);

            var dominated = new Series("Dominated");
            dominated.LegendText = "Dominated Sj";
            dominated.ChartType = SeriesChartType.Point;
            dominated.MarkerStyle = MarkerStyle.Circle;
            dominated.MarkerSize = 8;
            dominated.Color = Color.LightGray;
            dominated.Legend = "ComparisonParetoLegend";
            chart.Series.Add(dominated);

            var nonDominated = new Series("Non-dominated");
            nonDominated.LegendText = "Non-dominated Sj";
            nonDominated.ChartType = SeriesChartType.Point;
            nonDominated.MarkerStyle = MarkerStyle.Circle;
            nonDominated.MarkerSize = 11;
            nonDominated.Color = Color.RoyalBlue;
            nonDominated.Legend = "ComparisonParetoLegend";
            chart.Series.Add(nonDominated);

            var selected = new Series("Selected");
            selected.ChartType = SeriesChartType.Point;
            selected.MarkerStyle = MarkerStyle.Star5;
            selected.MarkerSize = 15;
            selected.Color = Color.Black;
            selected.Legend = "ComparisonParetoLegend";
            selected.IsVisibleInLegend = false;
            chart.Series.Add(selected);

            var reference = new Series("Reference");
            reference.LegendText = "Selected baseline S0";
            reference.ChartType = SeriesChartType.Point;
            reference.MarkerStyle = MarkerStyle.Diamond;
            reference.MarkerSize = 14;
            reference.Color = Color.ForestGreen;
            reference.Legend = "ComparisonParetoLegend";
            chart.Series.Add(reference);

            var overlaps = new Series("Overlaps");
            overlaps.LegendText = "Overlapped points";
            overlaps.ChartType = SeriesChartType.Point;
            overlaps.MarkerStyle = MarkerStyle.Cross;
            overlaps.MarkerSize = 16;
            overlaps.Color = Color.DarkOrange;
            overlaps.BorderColor = Color.Black;
            overlaps.BorderWidth = 2;
            overlaps.Legend = "ComparisonParetoLegend";
            overlaps.IsVisibleInLegend = false;
            chart.Series.Add(overlaps);

            chart.Titles.Add("S0 x Sj multicriteria dominance projected on makespan x relative CVaR95");
            chart.GetToolTipText += (s, e) =>
            {
                if (e.HitTestResult == null || e.HitTestResult.PointIndex < 0 || e.HitTestResult.Series == null)
                    return;

                var point = e.HitTestResult.Series.Points[e.HitTestResult.PointIndex];
                if (!string.IsNullOrWhiteSpace(point.ToolTip))
                    e.Text = point.ToolTip;
            };
        }

        private void PopulateComparisonWorkspace(ExecutionSummary summary)
        {
            PopulateComparisonReferenceCombo(summary);

            int selectedRunIndex = summary != null ? summary.SelectedBaselineRunIndex : -1;
            var referenceRow = _comparisonRows != null ? _comparisonRows.FirstOrDefault(x => x != null && x.IsReference) : null;
            bool rowsBelongToCurrentBaseline = referenceRow != null && referenceRow.RunIndex == selectedRunIndex;

            if (_comparisonRows != null && _comparisonRows.Count > 0 && rowsBelongToCurrentBaseline)
            {
                BindComparisonGrid();
                PopulateComparisonChart();
                SelectComparisonRow(selectedRunIndex);
                UpdateComparisonSummaryFromSelection();
                return;
            }

            _comparisonRows = new BindingList<ComparisonRowView>();
            _comparisonAnalysesByRunIndex = new Dictionary<int, ExecutionSummary>();

            if (_gridComparisonGrid != null)
            {
                _gridComparisonGrid.AutoGenerateColumns = true;
                _gridComparisonGrid.DataSource = new[]
                {
                    new
                    {
                        SelectedBaseline = selectedRunIndex >= 0 ? BuildSelectedBaselineLabel(summary) : "-",
                        CrashingScenarios = summary != null && summary.Crashing != null && summary.Crashing.Scenarios != null ? summary.Crashing.Scenarios.Count : 0,
                        Comparison = summary != null && summary.Crashing != null && summary.Crashing.Scenarios != null && summary.Crashing.Scenarios.Count > 0
                            ? "Pending"
                            : "Unavailable: no Sj scenario generated"
                    }
                }.ToList();
            }

            ClearComparisonChart();
            if (_lblComparisonSummary != null)
            {
                bool hasScenarios = summary != null && summary.Crashing != null && summary.Crashing.Scenarios != null && summary.Crashing.Scenarios.Count > 0;
                _lblComparisonSummary.Text = hasScenarios
                    ? "Final comparison pending. It compares the selected baseline S0 against the crashing scenarios Sj generated from that same baseline."
                    : "Only S0 is available. Comparison unavailable because no Sj scenarios were generated for the selected baseline.";
            }
        }

        private async void BtnComparisonRun_Click(object sender, EventArgs e)
        {
            if (_currentSummary == null || _currentSummary.Baseline == null || _currentSummary.Risk == null || _currentSummary.Frm == null)
            {
                MessageBox.Show(
                    "Load a selected baseline with FRM, Risk and Crashing before running the final S0 x Sj comparison.",
                    "RCPSP-FRM",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (_currentSummary.Crashing == null || _currentSummary.Crashing.Scenarios == null || _currentSummary.Crashing.Scenarios.Count == 0)
            {
                if (_lblComparisonSummary != null)
                    _lblComparisonSummary.Text = "Only S0 is available. Comparison unavailable because no Sj scenarios were generated for the selected baseline.";
                MessageBox.Show(
                    "Only S0 is available. No Sj scenarios were generated for the selected baseline, so the final S0 x Sj comparison cannot be executed.",
                    "RCPSP-FRM",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                ToggleActionButtons(false);
                SetStatus("Running", "Comparison", "Building final S0 x Sj decision comparison", 35);

                var result = await Task.Run(() => BuildComparisonRowsForSelectedBaseline(_currentSummary));

                _comparisonRows = new BindingList<ComparisonRowView>(result.Rows
                    .OrderByDescending(x => x.IsReference)
                    .ThenBy(x => x.RunIndex)
                    .ToList());
                _comparisonAnalysesByRunIndex = result.Analyses;

                BindComparisonGrid();
                PopulateComparisonChart();
                SelectComparisonRow(_currentSummary != null ? _currentSummary.SelectedBaselineRunIndex : -1);
                UpdateComparisonSummaryFromSelection();
                SetStatus("Ready", "Comparison", "Final S0 x Sj comparison updated", 100);
            }
            catch (Exception ex)
            {
                SetStatus("Error", "Comparison", "S0 x Sj comparison failed", 0);
                MessageBox.Show(
                    ex.ToString(),
                    "RCPSP-FRM",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                ToggleActionButtons(true);
            }
        }

        private void GridComparisonSelectionChanged(object sender, EventArgs e)
        {
            UpdateComparisonSummaryFromSelection();
            UpdateComparisonChartSelectedPoints();
        }

        private void PopulateComparisonReferenceCombo(ExecutionSummary summary)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => PopulateComparisonReferenceCombo(summary)));
                return;
            }

            if (_cmbComparisonReference == null)
                return;

            int selectedRunIndex = summary != null ? summary.SelectedBaselineRunIndex : -1;
            var items = new List<ComparisonReferenceItem>();
            if (selectedRunIndex >= 0)
            {
                items.Add(new ComparisonReferenceItem
                {
                    RunIndex = selectedRunIndex,
                    Label = BuildSelectedBaselineLabel(summary)
                });
            }

            _cmbComparisonReference.DataSource = null;
            _cmbComparisonReference.Items.Clear();
            _cmbComparisonReference.DataSource = items;
            _cmbComparisonReference.Enabled = false;

            if (items.Count > 0)
                _cmbComparisonReference.SelectedIndex = 0;
        }

        private int GetSelectedComparisonReferenceRunIndex()
        {
            if (InvokeRequired)
                return (int)Invoke(new Func<int>(GetSelectedComparisonReferenceRunIndex));

            var item = _cmbComparisonReference != null ? _cmbComparisonReference.SelectedItem as ComparisonReferenceItem : null;
            return item != null ? item.RunIndex : -1;
        }

        private void BindComparisonGrid()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(BindComparisonGrid));
                return;
            }

            if (_gridComparisonGrid == null)
                return;

            _gridComparisonGrid.AutoGenerateColumns = true;
            _gridComparisonGrid.DataSource = _comparisonRows;

            string[] visibleColumns =
            {
                "RowType", "Scenario", "Classification", "Makespan", "DeltaMakespan", "StructuralStatus", "Sif",
                "P95", "CVaR95", "RelativeCVaR95", "DeltaP95", "DeltaCVaR95", "Frri",
                "BalanceRuptureProbability", "MeanBalanceUsage", "MinObservedBalance",
                "MeanUnabsorbedWork", "MeanUnabsorbedWorkRatio",
                "ParetoStatus", "DominatedBy", "IsReference"
            };

            foreach (DataGridViewColumn column in _gridComparisonGrid.Columns)
                column.Visible = visibleColumns.Contains(column.Name);

            SetComparisonHeader("RowType", "Type");
            SetComparisonHeader("Scenario", "Scenario");
            SetComparisonHeader("Classification", "Classification");
            SetComparisonHeader("Makespan", "Makespan");
            SetComparisonHeader("DeltaMakespan", "ΔMakespan");
            SetComparisonHeader("StructuralStatus", "Structural");
            SetComparisonHeader("Sif", "SIF");
            SetComparisonHeader("P95", "P95 Makespan");
            SetComparisonHeader("CVaR95", "CVaR95 Delay");
            SetComparisonHeader("RelativeCVaR95", "Relative CVaR95");
            SetComparisonHeader("DeltaP95", "ΔP95");
            SetComparisonHeader("DeltaCVaR95", "ΔCVaR95");
            SetComparisonHeader("Frri", "FRRI");
            SetComparisonHeader("BalanceRuptureProbability", "P(Balance rupture)");
            SetComparisonHeader("MeanBalanceUsage", "Mean Balance use");
            SetComparisonHeader("MeanBalanceUsageRatio", "Balance use ratio");
            SetComparisonHeader("MinObservedBalance", "Min Balance");
            SetComparisonHeader("CVaR95GivenBalanceRupture", "CVaR95 | rupture");
            SetComparisonHeader("MeanUnabsorbedWork", "Mean unabsorbed work");
            SetComparisonHeader("MeanUnabsorbedWorkRatio", "Unabsorbed work ratio");
            SetComparisonHeader("ParetoStatus", "Pareto");
            SetComparisonHeader("DominatedBy", "Dominated by");
            SetComparisonHeader("IsReference", "Reference");

            SetComparisonFormat("RelativeCVaR95", "0.00%");
            SetComparisonFormat("BalanceRuptureProbability", "0.00%");
            SetComparisonFormat("MeanBalanceUsageRatio", "0.00%");
            SetComparisonFormat("Sif", "0.###");
            SetComparisonFormat("P95", "0.##");
            SetComparisonFormat("CVaR95", "0.##");
            SetComparisonFormat("DeltaP95", "0.##");
            SetComparisonFormat("DeltaCVaR95", "0.##");
            SetComparisonFormat("Frri", "0.###");
            SetComparisonFormat("MeanBalanceUsage", "0.###");
            SetComparisonFormat("MinObservedBalance", "0.###");
            SetComparisonFormat("CVaR95GivenBalanceRupture", "0.##");
            SetComparisonFormat("MeanUnabsorbedWork", "0.###");
            SetComparisonFormat("MeanUnabsorbedWorkRatio", "0.00%");

            foreach (DataGridViewRow row in _gridComparisonGrid.Rows)
            {
                var item = row.DataBoundItem as ComparisonRowView;
                if (item == null)
                    continue;

                row.DefaultCellStyle.BackColor = GetStructuralStatusColor(item.StructuralStatus);
                row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 120, 215);
                row.DefaultCellStyle.SelectionForeColor = Color.White;
            }
        }

        private void SetComparisonHeader(string columnName, string headerText)
        {
            if (_gridComparisonGrid.Columns.Contains(columnName))
                _gridComparisonGrid.Columns[columnName].HeaderText = headerText;
        }

        private void SetComparisonFormat(string columnName, string format)
        {
            if (_gridComparisonGrid.Columns.Contains(columnName))
                _gridComparisonGrid.Columns[columnName].DefaultCellStyle.Format = format;
        }

        private void PopulateComparisonChart()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(PopulateComparisonChart));
                return;
            }

            PopulateComparisonChartCore(
                _chartComparisonSifCvar,
                row => row.Sif,
                row => row.CVaR95,
                "SIF",
                "CVaR95 Delay",
                "Structural flexibility (SIF) vs probabilistic risk (CVaR95 delay)",
                false);

            PopulateComparisonChartCore(
                _chartComparisonMakespanCvar,
                row => row.Makespan,
                row => row.CVaR95,
                "Makespan",
                "CVaR95 Delay",
                "Nominal makespan vs probabilistic risk (CVaR95 delay)",
                false);

            PopulateComparisonChartCore(
                _chartComparisonDeltaCvar,
                row => row.DeltaMakespan,
                row => row.DeltaCVaR95,
                "ΔMakespan",
                "ΔCVaR95",
                "Change versus reference: ΔMakespan vs ΔCVaR95",
                false);

            PopulateComparisonChartCore(
                _chartComparisonBalanceRupture,
                row => row.Sif,
                row => row.MeanUnabsorbedWork,
                "SIF",
                "Mean unabsorbed work",
                "Structural flexibility (SIF) vs unabsorbed work severity",
                false);

            PopulateComparisonParetoChart();
            UpdateComparisonChartSelectedPoints();
        }

        private void PopulateComparisonChartCore(
            Chart chart,
            Func<ComparisonRowView, double> xSelector,
            Func<ComparisonRowView, double> ySelector,
            string xAxisTitle,
            string yAxisTitle,
            string chartTitle,
            bool yAsPercent)
        {
            if (chart == null || chart.Series.Count < 3)
                return;

            var runsSeries = chart.Series["Baselines"];
            var selectedSeries = chart.Series["Selected"];
            var referenceSeries = chart.Series["Reference"];

            runsSeries.Points.Clear();
            selectedSeries.Points.Clear();
            selectedSeries.IsVisibleInLegend = false;
            referenceSeries.Points.Clear();
            chart.Titles.Clear();

            if (_comparisonRows == null || _comparisonRows.Count == 0)
                return;

            runsSeries.XValueType = ChartValueType.Double;
            runsSeries.YValueType = ChartValueType.Double;
            selectedSeries.XValueType = ChartValueType.Double;
            selectedSeries.YValueType = ChartValueType.Double;
            referenceSeries.XValueType = ChartValueType.Double;
            referenceSeries.YValueType = ChartValueType.Double;

            foreach (var row in _comparisonRows)
            {
                double xValue = xSelector(row);
                double yValue = ySelector(row);

                var targetSeries = row.IsReference ? referenceSeries : runsSeries;
                int pointIndex = targetSeries.Points.AddXY(xValue, yValue);
                var point = targetSeries.Points[pointIndex];

                point.Tag = row;
                point.Label = row.IsReference ? row.Scenario : string.Empty;
                point.ToolTip = BuildComparisonPointTooltip(row, yAxisTitle, yValue, yAsPercent);

                if (row.IsReference)
                {
                    point.Color = Color.ForestGreen;
                    point.MarkerSize = 16;
                    point.LabelForeColor = Color.DarkGreen;
                }
                else
                {
                    point.Color = GetStructuralStatusColor(row.StructuralStatus);
                    point.MarkerSize = 8;
                }
            }

            AutoScaleComparisonPointChart(chart, _comparisonRows.ToList(), xSelector, ySelector, xAxisTitle, yAxisTitle, yAsPercent);
            chart.Titles.Add(chartTitle);
        }

        private static string BuildComparisonPointTooltip(ComparisonRowView row, string yAxisTitle, double yValue, bool yAsPercent)
        {
            string yText = yAsPercent ? yValue.ToString("0.00%") : yValue.ToString("0.##");
            return string.Format(
                "{0} | {1} | {2} | Status={3} | Mk={4} | ΔMk={5} | SIF={6:0.###} | CVaR95={7:0.##} | ΔCVaR95={8:0.##} | FRRI={9:0.###} | P(Balance rupture)={10:0.00%} | Mean unabsorbed={11:0.###} | Unabsorbed ratio={12:0.00%} | {13}={14}",
                row.Scenario,
                row.RowType,
                row.Classification,
                row.StructuralStatus,
                row.Makespan,
                row.DeltaMakespan,
                row.Sif,
                row.CVaR95,
                row.DeltaCVaR95,
                row.Frri,
                row.BalanceRuptureProbability,
                row.MeanUnabsorbedWork,
                row.MeanUnabsorbedWorkRatio,
                yAxisTitle,
                yText);
        }

        private static void AutoScaleComparisonPointChart(
            Chart chart,
            List<ComparisonRowView> rows,
            Func<ComparisonRowView, double> xSelector,
            Func<ComparisonRowView, double> ySelector,
            string xAxisTitle,
            string yAxisTitle,
            bool yAsPercent)
        {
            if (chart == null || chart.ChartAreas.Count == 0 || rows == null || rows.Count == 0)
                return;

            double minX = rows.Min(r => xSelector(r));
            double maxX = rows.Max(r => xSelector(r));
            double minY = rows.Min(r => ySelector(r));
            double maxY = rows.Max(r => ySelector(r));

            if (Math.Abs(maxX - minX) < 0.0001)
            {
                minX -= 1.0;
                maxX += 1.0;
            }

            if (Math.Abs(maxY - minY) < 0.0001)
            {
                if (yAsPercent)
                {
                    minY = Math.Max(0.0, minY - 0.01);
                    maxY = Math.Min(1.0, maxY + 0.01);
                }
                else
                {
                    minY -= 1.0;
                    maxY += 1.0;
                }
            }

            double paddingX = Math.Max(1.0, (maxX - minX) * 0.10);
            double paddingY = yAsPercent ? Math.Max(0.01, (maxY - minY) * 0.10) : Math.Max(1.0, (maxY - minY) * 0.10);

            var area = chart.ChartAreas[0];
            area.AxisX.Title = xAxisTitle;
            area.AxisY.Title = yAxisTitle;
            area.AxisY.LabelStyle.Format = yAsPercent ? "0.00%" : string.Empty;

            area.AxisX.Minimum = Math.Floor(minX - paddingX);
            area.AxisX.Maximum = Math.Ceiling(maxX + paddingX);
            area.AxisY.Minimum = yAsPercent ? Math.Max(0.0, minY - paddingY) : Math.Floor(minY - paddingY);
            area.AxisY.Maximum = yAsPercent ? Math.Min(1.0, maxY + paddingY) : Math.Ceiling(maxY + paddingY);

            if (area.AxisY.Maximum <= area.AxisY.Minimum)
                area.AxisY.Maximum = area.AxisY.Minimum + (yAsPercent ? 0.01 : 1.0);

            area.AxisX.LabelStyle.Enabled = true;
            area.AxisY.LabelStyle.Enabled = true;
            area.AxisX.MajorGrid.Enabled = true;
            area.AxisY.MajorGrid.Enabled = true;
            area.AxisX.MajorTickMark.Enabled = true;
            area.AxisY.MajorTickMark.Enabled = true;

            double rangeX = area.AxisX.Maximum - area.AxisX.Minimum;
            area.AxisX.Interval = Math.Max(1.0, Math.Ceiling(rangeX / 6.0));

            double rangeY = area.AxisY.Maximum - area.AxisY.Minimum;
            area.AxisY.Interval = yAsPercent ? Math.Max(0.01, rangeY / 5.0) : Math.Max(1.0, Math.Ceiling(rangeY / 6.0));
        }

        private void ClearComparisonChart()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(ClearComparisonChart));
                return;
            }

            ClearComparisonChartCore(_chartComparisonSifCvar);
            ClearComparisonChartCore(_chartComparisonMakespanCvar);
            ClearComparisonChartCore(_chartComparisonDeltaCvar);
            ClearComparisonChartCore(_chartComparisonBalanceRupture);
            ClearComparisonChartCore(_chartComparisonParetoDominance);
        }

        private static void ClearComparisonChartCore(Chart chart)
        {
            if (chart == null)
                return;

            foreach (Series series in chart.Series)
                series.Points.Clear();

            chart.Titles.Clear();
            chart.Titles.Add("");
        }

        private void UpdateComparisonChartSelectedPoints()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(UpdateComparisonChartSelectedPoints));
                return;
            }

            UpdateComparisonChartSelectedPointsCore(_chartComparisonSifCvar, row => row.Sif, row => row.CVaR95, "CVaR95 Delay", false);
            UpdateComparisonChartSelectedPointsCore(_chartComparisonMakespanCvar, row => row.Makespan, row => row.CVaR95, "CVaR95 Delay", false);
            UpdateComparisonChartSelectedPointsCore(_chartComparisonDeltaCvar, row => row.DeltaMakespan, row => row.DeltaCVaR95, "ΔCVaR95", false);
            UpdateComparisonChartSelectedPointsCore(_chartComparisonBalanceRupture, row => row.Sif, row => row.MeanUnabsorbedWork, "Mean unabsorbed work", false);
            UpdateComparisonParetoSelectedPoint();
        }

        private void UpdateComparisonChartSelectedPointsCore(
            Chart chart,
            Func<ComparisonRowView, double> xSelector,
            Func<ComparisonRowView, double> ySelector,
            string yAxisTitle,
            bool yAsPercent)
        {
            if (chart == null || chart.Series.Count < 3)
                return;

            if (_gridComparisonGrid == null || _gridComparisonGrid.IsDisposed)
                return;

            var selectedSeries = chart.Series["Selected"];
            selectedSeries.Points.Clear();

            var selectedRows = _gridComparisonGrid.SelectedRows
                .Cast<DataGridViewRow>()
                .OrderBy(r => r.Index)
                .Select(r => r.DataBoundItem as ComparisonRowView)
                .Where(r => r != null && !r.IsReference)
                .ToList();

            foreach (var row in selectedRows)
            {
                double yValue = ySelector(row);
                int pointIndex = selectedSeries.Points.AddXY(xSelector(row), yValue);
                var point = selectedSeries.Points[pointIndex];
                point.Tag = row;
                point.Label = row.Scenario;
                point.ToolTip = BuildComparisonPointTooltip(row, yAxisTitle, yValue, yAsPercent);
                point.Color = GetStructuralStatusColor(row.StructuralStatus);
                point.BorderColor = Color.Black;
                point.BorderWidth = 1;
                point.MarkerStyle = MarkerStyle.Circle;
                point.MarkerSize = 12;
                point.LabelForeColor = Color.Black;
            }

            selectedSeries.IsVisibleInLegend = selectedSeries.Points.Count > 0;
        }

        private void PopulateComparisonParetoChart()
        {
            var chart = _chartComparisonParetoDominance;
            if (chart == null)
                return;

            foreach (var series in chart.Series)
                series.Points.Clear();
            chart.Titles.Clear();
            chart.Titles.Add("S0 x Sj multicriteria dominance projected on makespan x relative CVaR95");

            Series overlapSeries;
            try { overlapSeries = chart.Series["Overlaps"]; } catch { overlapSeries = null; }
            if (overlapSeries != null)
                overlapSeries.IsVisibleInLegend = false;

            var rows = (_comparisonRows ?? new BindingList<ComparisonRowView>()).Where(x => x != null).ToList();
            var overlapGroups = BuildParetoOverlapGroups(rows);

            foreach (var row in rows)
            {
                bool nonDominated = string.Equals(row.ParetoStatus, "Non-dominated", StringComparison.OrdinalIgnoreCase);
                string seriesName = row.IsReference ? "Reference" : (nonDominated ? "Non-dominated" : "Dominated");
                Series series;
                try { series = chart.Series[seriesName]; } catch { continue; }

                int pointIndex = series.Points.AddXY(row.Makespan, row.RelativeCVaR95);
                var point = series.Points[pointIndex];
                point.Tag = row;

                var group = FindParetoOverlapGroup(overlapGroups, row);
                bool overlapped = group != null && group.Count > 1;
                point.Label = overlapped ? string.Empty : (row.IsReference ? row.Scenario : string.Empty);
                point.ToolTip = BuildComparisonParetoTooltip(row, group);

                if (overlapped)
                {
                    point.BorderColor = Color.DarkOrange;
                    point.BorderWidth = 2;
                    point.MarkerSize = row.IsReference ? 15 : 12;
                }

                if (!row.IsReference && nonDominated)
                    point.Color = GetStructuralStatusColor(row.StructuralStatus);
            }

            if (overlapSeries != null)
            {
                foreach (var group in overlapGroups.Where(g => g.Count > 1))
                {
                    var first = group[0];
                    int pointIndex = overlapSeries.Points.AddXY(first.Makespan, first.RelativeCVaR95);
                    var point = overlapSeries.Points[pointIndex];
                    point.Tag = group;
                    point.Label = BuildParetoOverlapLabel(group);
                    point.ToolTip = BuildParetoOverlapTooltip(group);
                    point.LabelForeColor = Color.DarkOrange;
                }

                overlapSeries.IsVisibleInLegend = overlapSeries.Points.Count > 0;
            }

            AutoScaleComparisonParetoChart(chart);
        }

        private static List<List<ComparisonRowView>> BuildParetoOverlapGroups(IEnumerable<ComparisonRowView> rows)
        {
            if (rows == null)
                return new List<List<ComparisonRowView>>();

            return rows
                .Where(r => r != null)
                .GroupBy(r => new
                {
                    X = r.Makespan,
                    Y = Math.Round(r.RelativeCVaR95, 8)
                })
                .Select(g => g
                    .OrderByDescending(r => r.IsReference)
                    .ThenBy(r => r.Scenario ?? string.Empty)
                    .ToList())
                .ToList();
        }

        private static List<ComparisonRowView> FindParetoOverlapGroup(IEnumerable<List<ComparisonRowView>> groups, ComparisonRowView row)
        {
            if (groups == null || row == null)
                return null;

            double y = Math.Round(row.RelativeCVaR95, 8);
            return groups.FirstOrDefault(g => g.Any(r => r != null && r.Makespan == row.Makespan && Math.Round(r.RelativeCVaR95, 8) == y));
        }

        private static string BuildParetoOverlapLabel(List<ComparisonRowView> group)
        {
            if (group == null || group.Count == 0)
                return string.Empty;


            bool hasReference = group.Any(r => r != null && r.IsReference);
            if (!hasReference)
                return string.Empty;

            if (group.Count == 2)
                return BuildParetoShortScenarioLabel(group[0]) + " + " + BuildParetoShortScenarioLabel(group[1]);

            int sjCount = group.Count(r => r != null && !r.IsReference);
            return "S0 + " + sjCount.ToString() + " Sj";
        }

        private static string BuildParetoShortScenarioLabel(ComparisonRowView row)
        {
            if (row == null)
                return string.Empty;

            if (row.IsReference)
                return "S0";

            return Safe(row.Scenario);
        }

        private static string BuildComparisonParetoTooltip(ComparisonRowView row, List<ComparisonRowView> group)
        {
            string own = string.Format(
                "{0} | {1} | Mk={2} | SIF={3:0.###} | RelCVaR95={4:0.00%} | CVaR95={5:0.##} | P(Balance rupture)={6:0.00%} | Mean unabsorbed={7:0.###} | Dominated by={8}",
                Safe(row.Scenario),
                Safe(row.ParetoStatus),
                row.Makespan,
                row.Sif,
                row.RelativeCVaR95,
                row.CVaR95,
                row.BalanceRuptureProbability,
                row.MeanUnabsorbedWork,
                string.IsNullOrWhiteSpace(row.DominatedBy) ? "-" : row.DominatedBy);

            if (group == null || group.Count <= 1)
                return own;

            return own + Environment.NewLine + Environment.NewLine + BuildParetoOverlapTooltip(group);
        }

        private static string BuildParetoOverlapTooltip(List<ComparisonRowView> group)
        {
            if (group == null || group.Count == 0)
                return string.Empty;

            var first = group[0];
            var lines = new List<string>();
            lines.Add(string.Format(
                "Overlapped points at Mk={0}, RelCVaR95={1:0.00%}: {2}",
                first.Makespan,
                first.RelativeCVaR95,
                group.Count));

            foreach (var row in group)
            {
                lines.Add(string.Format(
                    "- {0} | {1} | Mean unabsorbed={2:0.###} | SIF={3:0.###} | {4}",
                    Safe(row.Scenario),
                    Safe(row.RowType),
                    row.MeanUnabsorbedWork,
                    row.Sif,
                    Safe(row.ParetoStatus)));
            }

            return string.Join(Environment.NewLine, lines.ToArray());
        }

        private void UpdateComparisonParetoSelectedPoint()
        {
            var chart = _chartComparisonParetoDominance;
            if (chart == null)
                return;

            Series selectedSeries;
            try { selectedSeries = chart.Series["Selected"]; } catch { return; }
            selectedSeries.Points.Clear();
            selectedSeries.IsVisibleInLegend = false;

            if (_gridComparisonGrid == null || _gridComparisonGrid.IsDisposed)
                return;

            var selectedRows = _gridComparisonGrid.SelectedRows
                .Cast<DataGridViewRow>()
                .OrderBy(r => r.Index)
                .Select(r => r.DataBoundItem as ComparisonRowView)
                .Where(r => r != null && !r.IsReference)
                .ToList();

            foreach (var row in selectedRows)
            {
                int pointIndex = selectedSeries.Points.AddXY(row.Makespan, row.RelativeCVaR95);
                var point = selectedSeries.Points[pointIndex];
                point.Tag = row;
                point.Label = row.Scenario;
                point.ToolTip = string.Format(
                    "{0} | {1} | Mk={2} | RelCVaR95={3:0.00%} | CVaR95={4:0.##} | P(Balance rupture)={5:0.00%} | Mean unabsorbed={6:0.###}",
                    row.Scenario,
                    row.ParetoStatus,
                    row.Makespan,
                    row.RelativeCVaR95,
                    row.CVaR95,
                    row.BalanceRuptureProbability,
                    row.MeanUnabsorbedWork);
                point.Color = Color.Black;
            }

            selectedSeries.IsVisibleInLegend = selectedSeries.Points.Count > 0;
        }

        private static void AutoScaleComparisonParetoChart(Chart chart)
        {
            if (chart == null || chart.ChartAreas.Count == 0)
                return;

            var points = chart.Series
                .Cast<Series>()
                .Where(s => string.Equals(s.Name, "Dominated", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(s.Name, "Non-dominated", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(s.Name, "Reference", StringComparison.OrdinalIgnoreCase))
                .SelectMany(s => s.Points.Cast<DataPoint>())
                .ToList();

            if (points.Count == 0)
                return;

            double minX = points.Min(p => p.XValue);
            double maxX = points.Max(p => p.XValue);
            double minY = points.Min(p => p.YValues.Length > 0 ? p.YValues[0] : 0.0);
            double maxY = points.Max(p => p.YValues.Length > 0 ? p.YValues[0] : 0.0);

            if (Math.Abs(maxX - minX) < 0.0001) { minX -= 1.0; maxX += 1.0; }
            if (Math.Abs(maxY - minY) < 0.0001) { minY = Math.Max(0.0, minY - 0.01); maxY += 0.01; }

            double padX = Math.Max(1.0, Math.Abs(maxX - minX) * 0.10);
            double padY = Math.Max(0.01, Math.Abs(maxY - minY) * 0.10);
            var area = chart.ChartAreas[0];
            area.AxisX.Minimum = Math.Floor(minX - padX);
            area.AxisX.Maximum = Math.Ceiling(maxX + padX);
            area.AxisY.Minimum = Math.Max(0.0, minY - padY);
            area.AxisY.Maximum = maxY + padY;
            area.AxisY.LabelStyle.Format = "0.00%";
        }

        private void SelectComparisonRow(int runIndex)
        {
            RunOnUiThread(() =>
            {
                if (_gridComparisonGrid == null || _gridComparisonGrid.IsDisposed)
                    return;

                BeginInvoke((Action)(() =>
                {
                    if (_gridComparisonGrid == null || _gridComparisonGrid.IsDisposed)
                        return;

                    _gridComparisonGrid.ClearSelection();

                    foreach (DataGridViewRow row in _gridComparisonGrid.Rows)
                    {
                        var item = row.DataBoundItem as ComparisonRowView;
                        if (item == null)
                            continue;

                        if (item.RunIndex == runIndex)
                        {
                            row.Selected = true;

                            DataGridViewCell firstVisibleCell = null;

                            foreach (DataGridViewColumn col in _gridComparisonGrid.Columns)
                            {
                                if (!col.Visible)
                                    continue;

                                firstVisibleCell = row.Cells[col.Index];
                                break;
                            }

                            if (firstVisibleCell != null && row.Visible)
                                _gridComparisonGrid.CurrentCell = firstVisibleCell;

                            if (row.Visible)
                                _gridComparisonGrid.FirstDisplayedScrollingRowIndex = row.Index;

                            break;
                        }
                    }
                }));
            });
        }

        private void UpdateComparisonSummaryFromSelection()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(UpdateComparisonSummaryFromSelection));
                return;
            }

            if (_lblComparisonSummary == null)
                return;

            var selected = _gridComparisonGrid != null && _gridComparisonGrid.CurrentRow != null
                ? _gridComparisonGrid.CurrentRow.DataBoundItem as ComparisonRowView
                : null;

            if (selected == null)
            {
                _lblComparisonSummary.Text = "Comparison not executed yet. Load a baseline, run crashing scenarios, and compare S0 against its Sj scenarios.";
                return;
            }

            string referenceLabel = BuildReferenceComparisonLabel();
            ComparisonRowView bestStructure = FindStrongestStructureRow();
            ComparisonRowView bestRisk = FindLowestRiskRow();
            ComparisonRowView bestMakespan = FindShortestMakespanRow();
            ComparisonRowView bestBalance = FindLowestUnabsorbedWorkRow();

            _lblComparisonSummary.Text =
                "Reference: " + referenceLabel + Environment.NewLine +
                "Selected: " + selected.Scenario + " | " + selected.RowType + Environment.NewLine +
                "Classification: " + selected.Classification + Environment.NewLine +
                "Nominal makespan: " + selected.Makespan + " | ΔMakespan=" + selected.DeltaMakespan + Environment.NewLine +
                "Structural quality: " + selected.StructuralStatus +
                " | SIF=" + selected.Sif.ToString("0.###") + Environment.NewLine +
                "Risk: P95=" + selected.P95.ToString("0.##") +
                " | CVaR95=" + selected.CVaR95.ToString("0.##") +
                " | Relative CVaR95=" + selected.RelativeCVaR95.ToString("0.00%") + Environment.NewLine +
                "Relative to S0: ΔCVaR95=" + selected.DeltaCVaR95.ToString("0.##") +
                " | FRRI=" + selected.Frri.ToString("0.###") + Environment.NewLine +
                "Balance: P(rupture)=" + selected.BalanceRuptureProbability.ToString("0.00%") +
                " | Mean use=" + selected.MeanBalanceUsage.ToString("0.###") +
                " | Mean unabsorbed=" + selected.MeanUnabsorbedWork.ToString("0.###") +
                " | Unabsorbed ratio=" + selected.MeanUnabsorbedWorkRatio.ToString("0.00%") +
                " | Min=" + selected.MinObservedBalance.ToString("0.###") + Environment.NewLine +
                "Pareto dominance: " + Safe(selected.ParetoStatus) +
                (string.IsNullOrWhiteSpace(selected.DominatedBy) ? string.Empty : " | dominated by=" + selected.DominatedBy) + Environment.NewLine +
                "Leaders: Structure=" + (bestStructure != null ? bestStructure.Scenario : "-") +
                " | Lowest CVaR95=" + (bestRisk != null ? bestRisk.Scenario : "-") +
                " | Lowest unabsorbed work=" + (bestBalance != null ? bestBalance.Scenario : "-") +
                " | Shortest makespan=" + (bestMakespan != null ? bestMakespan.Scenario : "-");
        }

        private string BuildReferenceComparisonLabel()
        {
            var reference = _comparisonRows != null ? _comparisonRows.FirstOrDefault(x => x.IsReference) : null;
            return reference != null ? reference.Scenario : "-";
        }

        private ComparisonRowView FindStrongestStructureRow()
        {
            return _comparisonRows != null
                ? _comparisonRows
                    .OrderByDescending(x => StructuralStatusRank(x.StructuralStatus))
                    .ThenByDescending(x => x.Sif)
                    .ThenBy(x => x.CVaR95)
                    .ThenBy(x => x.MeanUnabsorbedWork)
                    .ThenBy(x => x.BalanceRuptureProbability)
                    .ThenBy(x => x.Makespan)
                    .FirstOrDefault()
                : null;
        }

        private ComparisonRowView FindLowestRiskRow()
        {
            return _comparisonRows != null
                ? _comparisonRows
                    .OrderBy(x => x.CVaR95)
                    .ThenBy(x => x.P95)
                    .ThenBy(x => x.MeanUnabsorbedWork)
                    .ThenBy(x => x.BalanceRuptureProbability)
                    .ThenBy(x => x.Makespan)
                    .FirstOrDefault()
                : null;
        }

        private ComparisonRowView FindLowestUnabsorbedWorkRow()
        {
            return _comparisonRows != null
                ? _comparisonRows
                    .OrderBy(x => x.MeanUnabsorbedWork)
                    .ThenBy(x => x.MeanUnabsorbedWorkRatio)
                    .ThenBy(x => x.BalanceRuptureProbability)
                    .ThenBy(x => x.CVaR95)
                    .ThenBy(x => x.Makespan)
                    .FirstOrDefault()
                : null;
        }

        private ComparisonRowView FindShortestMakespanRow()
        {
            return _comparisonRows != null
                ? _comparisonRows
                    .OrderBy(x => x.Makespan)
                    .ThenBy(x => x.CVaR95)
                    .ThenBy(x => x.MeanUnabsorbedWork)
                    .ThenBy(x => x.BalanceRuptureProbability)
                    .ThenByDescending(x => StructuralStatusRank(x.StructuralStatus))
                    .FirstOrDefault()
                : null;
        }

        private static int StructuralStatusRank(string status)
        {
            if (string.Equals(status, "ROBUST", StringComparison.OrdinalIgnoreCase))
                return 4;
            if (string.Equals(status, "FEASIBLE", StringComparison.OrdinalIgnoreCase))
                return 3;
            if (string.Equals(status, "FRAGILE", StringComparison.OrdinalIgnoreCase))
                return 2;
            if (string.Equals(status, "INVIABLE", StringComparison.OrdinalIgnoreCase))
                return 1;
            return 0;
        }

        private static string BuildRunLabel(BaselineRunSummaryDto run)
        {
            if (run == null)
                return "-";

            if (run.IsExact)
                return Safe(run.Heuristic);

            return Safe(run.Heuristic) + " | " + Safe(run.Scheme) + " | " + Safe(run.Direction);
        }

        private static void ApplyComparisonParetoDominance(List<ComparisonRowView> rows)
        {
            var list = (rows ?? new List<ComparisonRowView>()).Where(x => x != null).ToList();
            foreach (var candidate in list)
            {
                candidate.ParetoStatus = "Non-dominated";
                candidate.DominatedBy = string.Empty;
            }

            foreach (var candidate in list)
            {
                foreach (var other in list)
                {
                    if (ReferenceEquals(candidate, other))
                        continue;

                    bool dominates = other.Makespan <= candidate.Makespan
                        && other.Sif >= candidate.Sif
                        && other.RelativeCVaR95 <= candidate.RelativeCVaR95
                        && other.MeanUnabsorbedWork <= candidate.MeanUnabsorbedWork
                        && (other.Makespan < candidate.Makespan
                            || other.Sif > candidate.Sif
                            || other.RelativeCVaR95 < candidate.RelativeCVaR95
                            || other.MeanUnabsorbedWork < candidate.MeanUnabsorbedWork);

                    if (dominates)
                    {
                        candidate.ParetoStatus = "Dominated";
                        candidate.DominatedBy = other.Scenario;
                        break;
                    }
                }
            }
        }

        private ComparisonBuildResult BuildComparisonRowsForSelectedBaseline(ExecutionSummary summary)
        {
            var analyses = new Dictionary<int, ExecutionSummary>();
            var rows = new List<ComparisonRowView>();

            if (summary == null)
                return new ComparisonBuildResult { Rows = rows, Analyses = analyses };

            int referenceRunIndex = summary.SelectedBaselineRunIndex;
            analyses[referenceRunIndex] = summary;

            var frm = summary.Frm ?? new FrmResultDto();
            var risk = summary.Risk ?? new RiskResultDto();
            var baseline = summary.Baseline ?? new BaselineResultDto();
            var diagnostics = frm.ResourceDiagnostics ?? new List<FrmResourceDiagnosticDto>();

            int referenceMakespan = baseline.Makespan;
            double referenceP95 = risk.P95;
            double referenceCVaR95 = risk.CVaR95;
            double referenceRelativeCVaR95 = referenceMakespan > 0 ? referenceCVaR95 / referenceMakespan : 0.0;
            double avgRobustness = diagnostics.Count > 0
                ? Clamp01(diagnostics.Average(x => Clamp01(x.RobustnessIndex)))
                : (frm.IsStructurallyRobust ? 1.0 : 0.0);
            string structuralStatus = ClassifyStructuralStatus(frm.IsStructurallyRobust, frm.SifGlobal, avgRobustness);

            rows.Add(new ComparisonRowView
            {
                RunIndex = referenceRunIndex,
                RowType = "Baseline S0",
                Scenario = "S0 - " + BuildSelectedBaselineLabel(summary),
                Classification = "Reference baseline",
                Heuristic = frm.Heuristic,
                Scheme = frm.Scheme,
                Direction = frm.Direction,
                Makespan = referenceMakespan,
                DeltaMakespan = 0,
                StructuralStatus = structuralStatus,
                Sif = frm.SifGlobal,
                P95 = referenceP95,
                CVaR95 = referenceCVaR95,
                RelativeCVaR95 = referenceRelativeCVaR95,
                DeltaP95 = 0.0,
                DeltaCVaR95 = 0.0,
                Frri = 0.0,
                BalanceRuptureProbability = risk.BalanceRuptureProbability,
                MeanBalanceUsage = risk.MeanBalanceUsage,
                MeanBalanceUsageRatio = risk.MeanBalanceUsageRatio,
                MinObservedBalance = risk.MinObservedBalance,
                CVaR95GivenBalanceRupture = risk.CVaR95GivenBalanceRupture,
                MeanUnabsorbedWork = risk.MeanUnabsorbedWork,
                MeanUnabsorbedWorkRatio = risk.MeanUnabsorbedWorkRatio,
                IsReference = true
            });

            var scenarios = summary.Crashing != null && summary.Crashing.Scenarios != null
                ? summary.Crashing.Scenarios
                : new List<CrashingScenarioResultDto>();

            foreach (var scenario in scenarios.OrderBy(x => x.Rank).ThenBy(x => x.ScenarioName))
            {
                if (scenario == null)
                    continue;

                int scenarioMakespan = scenario.ScenarioMakespan;
                double scenarioCVaR95 = scenario.ScenarioCVaR95;
                double scenarioP95 = scenario.ScenarioP95;
                double scenarioRelativeCVaR95 = scenarioMakespan > 0 ? scenarioCVaR95 / scenarioMakespan : 0.0;
                string scenarioName = string.IsNullOrWhiteSpace(scenario.ScenarioName)
                    ? "Sj"
                    : scenario.ScenarioName;
                string activitiesSuffix = string.IsNullOrWhiteSpace(scenario.ActivitiesLabel)
                    ? string.Empty
                    : " (" + scenario.ActivitiesLabel + ")";

                rows.Add(new ComparisonRowView
                {
                    RunIndex = 100000 + Math.Max(0, scenario.Rank),
                    RowType = "Crashing Sj",
                    Scenario = scenarioName + activitiesSuffix,
                    Classification = ClassifyComparisonScenario(scenario.DeltaMakespan, scenario.DeltaCVaR95, scenario.DeltaMeanUnabsorbedWork),
                    Heuristic = string.Empty,
                    Scheme = string.Empty,
                    Direction = string.Empty,
                    Makespan = scenarioMakespan,
                    DeltaMakespan = scenario.DeltaMakespan,
                    StructuralStatus = scenario.StructuralStatus,
                    Sif = scenario.Sif,
                    P95 = scenarioP95,
                    CVaR95 = scenarioCVaR95,
                    RelativeCVaR95 = scenarioRelativeCVaR95,
                    DeltaP95 = scenario.DeltaP95,
                    DeltaCVaR95 = scenario.DeltaCVaR95,
                    Frri = scenario.Frri,
                    BalanceRuptureProbability = scenario.ScenarioBalanceRuptureProbability,
                    MeanBalanceUsage = scenario.ScenarioMeanBalanceUsage,
                    MeanBalanceUsageRatio = 0.0,
                    MinObservedBalance = scenario.ScenarioMinObservedBalance,
                    CVaR95GivenBalanceRupture = 0.0,
                    MeanUnabsorbedWork = scenario.ScenarioMeanUnabsorbedWork,
                    MeanUnabsorbedWorkRatio = scenario.ScenarioMeanUnabsorbedWorkRatio,
                    IsReference = false
                });
            }

            ApplyComparisonParetoDominance(rows);

            return new ComparisonBuildResult
            {
                Rows = rows,
                Analyses = analyses
            };
        }

        private string BuildSelectedBaselineLabel(ExecutionSummary summary)
        {
            int index = summary != null ? summary.SelectedBaselineRunIndex : -1;
            if (_runs != null && index >= 0 && index < _runs.Count && _runs[index] != null)
                return BuildRunLabel(_runs[index]);

            if (summary != null && summary.Baseline != null && !string.IsNullOrWhiteSpace(summary.Baseline.RunLabel))
                return summary.Baseline.RunLabel;

            return "selected baseline";
        }

        private static string ClassifyComparisonScenario(int deltaMakespan, double deltaCVaR95, double deltaMeanUnabsorbedWork)
        {
            const double eps = 0.000001;

            if (deltaMakespan < 0 && deltaCVaR95 < -eps && deltaMeanUnabsorbedWork <= eps)
                return "Efficient compression";

            if (deltaMakespan < 0 && deltaCVaR95 > eps)
                return "Risky compression";

            if (deltaMakespan < 0 && deltaCVaR95 <= eps && deltaMeanUnabsorbedWork <= eps)
                return "Time gain without risk increase";

            if (deltaMakespan == 0 && deltaCVaR95 <= eps && deltaMeanUnabsorbedWork < -eps)
                return "Severity improvement only";

            if (deltaMakespan > 0 && deltaCVaR95 < -eps)
                return "Robustness trade-off";

            if (deltaMakespan >= 0 && deltaCVaR95 > eps)
                return "Unfavorable";

            if (Math.Abs(deltaCVaR95) <= eps && Math.Abs(deltaMeanUnabsorbedWork) <= eps && deltaMakespan == 0)
                return "Neutral";

            return "Mixed effect";
        }

        private static double ComputeFrri(double baselineRisk, double scenarioRisk)
        {
            if (baselineRisk <= 0.0)
                return 0.0;

            return (baselineRisk - scenarioRisk) / baselineRisk;
        }

        private static double Clamp01(double value)
        {
            if (value < 0.0)
                return 0.0;
            if (value > 1.0)
                return 1.0;
            return value;
        }
    }
}
