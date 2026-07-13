using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using RCPSP.Contracts;
using System.Drawing;
using System.Windows.Forms.DataVisualization.Charting;

namespace RCPSP.WinForms
{
    public partial class FormBaseline : Form
    {
        private void InitializeRiskLayout()
        {
            _panelRisk.SuspendLayout();
            _panelRisk.Controls.Clear();

            _riskRootSplit = new SplitContainer();
            _riskRightSplit = new SplitContainer();

            _grpRiskHistogram = new GroupBox();
            _grpRiskMetrics = new GroupBox();
            _grpRiskBins = new GroupBox();

            _chartRiskHistogram = new Chart();

            _gridRiskMetrics = CreateReadOnlyGrid();
            _gridRiskBins = CreateReadOnlyGrid();

            _riskRootSplit.Dock = DockStyle.Fill;
            _riskRootSplit.Orientation = Orientation.Vertical;

            _riskRightSplit.Dock = DockStyle.Fill;
            _riskRightSplit.Orientation = Orientation.Horizontal;

            _grpRiskHistogram.Text = "Monte Carlo Makespan Distribution";
            _grpRiskHistogram.Dock = DockStyle.Fill;
            _grpRiskHistogram.Padding = new Padding(8);

            _grpRiskMetrics.Text = "Risk and FRM Absorption Metrics";
            _grpRiskMetrics.Dock = DockStyle.Fill;
            _grpRiskMetrics.Padding = new Padding(8);

            _grpRiskBins.Text = "Histogram Bins";
            _grpRiskBins.Dock = DockStyle.Fill;
            _grpRiskBins.Padding = new Padding(8);

            ConfigureHistogramChart(_chartRiskHistogram);

            _grpRiskHistogram.Controls.Add(_chartRiskHistogram);
            _grpRiskMetrics.Controls.Add(_gridRiskMetrics);
            _grpRiskBins.Controls.Add(_gridRiskBins);

            _riskRightSplit.Panel1.Controls.Add(_grpRiskMetrics);
            _riskRightSplit.Panel2.Controls.Add(_grpRiskBins);

            _riskRootSplit.Panel1.Controls.Add(_grpRiskHistogram);
            _riskRootSplit.Panel2.Controls.Add(_riskRightSplit);

            _panelRisk.Controls.Add(_riskRootSplit);

            _panelRisk.ResumeLayout();
        }

        private void AdjustRiskSplitsSafe()
        {
            if (_riskRootSplit != null)
            {
                _riskRootSplit.Panel1MinSize = 300;
                _riskRootSplit.Panel2MinSize = 220;

                int total = _riskRootSplit.ClientSize.Width;
                int minLeft = _riskRootSplit.Panel1MinSize;
                int minRight = _riskRootSplit.Panel2MinSize;

                if (total > minLeft + minRight)
                {
                    int desired = (int)(total * 0.68);
                    desired = Math.Max(minLeft, Math.Min(desired, total - minRight));
                    _riskRootSplit.SplitterDistance = desired;
                }
            }

            if (_riskRightSplit != null)
            {
                _riskRightSplit.Panel1MinSize = 100;
                _riskRightSplit.Panel2MinSize = 140;

                int total = _riskRightSplit.ClientSize.Height;
                int minTop = _riskRightSplit.Panel1MinSize;
                int minBottom = _riskRightSplit.Panel2MinSize;

                if (total > minTop + minBottom)
                {
                    int desired = (int)(total * 0.45);
                    desired = Math.Max(minTop, Math.Min(desired, total - minBottom));
                    _riskRightSplit.SplitterDistance = desired;
                }
            }
        }

        private static void ConfigureHistogramChart(Chart chart)
        {
            chart.Dock = DockStyle.Fill;
            chart.BackColor = Color.White;

            chart.ChartAreas.Clear();
            chart.Series.Clear();
            chart.Legends.Clear();
            chart.Titles.Clear();
            chart.Annotations.Clear();

            var area = new ChartArea("HistogramArea");
            area.AxisX.Title = "Makespan (days)";
            area.AxisY.Title = "Frequency";
            area.AxisX.MajorGrid.LineColor = Color.Gainsboro;
            area.AxisY.MajorGrid.LineColor = Color.Gainsboro;
            area.AxisY.LabelStyle.Format = "0";
            chart.ChartAreas.Add(area);

            var legend = new Legend("MainLegend");
            legend.Docking = Docking.Top;
            chart.Legends.Add(legend);

            var histogramSeries = new Series("Histogram");
            histogramSeries.ChartType = SeriesChartType.Column;
            histogramSeries.XValueType = ChartValueType.Double;
            histogramSeries.IsValueShownAsLabel = false;
            histogramSeries.LabelForeColor = Color.Black;
            histogramSeries["PointWidth"] = "0.55";
            histogramSeries.Legend = "MainLegend";
            chart.Series.Add(histogramSeries);

            var p50Series = new Series("P50");
            p50Series.ChartType = SeriesChartType.Line;
            p50Series.BorderWidth = 2;
            p50Series.BorderDashStyle = ChartDashStyle.Dash;
            p50Series.Color = Color.Green;
            p50Series.Legend = "MainLegend";
            chart.Series.Add(p50Series);

            var p95Series = new Series("P95");
            p95Series.ChartType = SeriesChartType.Line;
            p95Series.BorderWidth = 2;
            p95Series.BorderDashStyle = ChartDashStyle.Dash;
            p95Series.Color = Color.Red;
            p95Series.Legend = "MainLegend";
            chart.Series.Add(p95Series);
        }

        private void PopulateRisk(ExecutionSummary summary)
        {
            var risk = summary != null && summary.Risk != null
                ? summary.Risk
                : new RiskResultDto();

            FillRiskMetricsGrid(summary, risk);
            FillRiskBinsGrid(risk);
            FillRiskHistogramChart(summary, risk);
        }

        private void FillRiskMetricsGrid(ExecutionSummary summary, RiskResultDto risk)
        {
            _gridRiskMetrics.Columns.Clear();
            _gridRiskMetrics.Rows.Clear();

            int baselineMakespan = summary != null && summary.Baseline != null
                ? summary.Baseline.Makespan
                : 0;

            _gridRiskMetrics.Columns.Add("Metric", "Metric");
            _gridRiskMetrics.Columns.Add("Value", "Value");

            AddRiskMetricRow("N Scenarios", risk.Iterations);
            AddRiskMetricRow("Gamma", risk.Gamma, "0.###");
            AddRiskMetricRow("Seed", risk.Seed);
            AddRiskMetricRow("Baseline Makespan", baselineMakespan);
            AddRiskMetricRow("Mean Makespan", risk.MeanMakespan);
            AddRiskMetricRow("P50 Makespan", risk.P50);
            AddRiskMetricRow("P95 Makespan", risk.P95);
            AddRiskMetricRow("Mean Delay", risk.MeanDelay);
            AddRiskMetricRow("P(L > 0)", risk.DelayProbability, "0.###");
            AddRiskMetricRow("P95 Delay", risk.P95Delay);
            AddRiskMetricRow("CVaR95 Delay", risk.CVaR95Delay);
            AddRiskMetricRow("Relative CVaR95 Delay", SafeDivide(risk.CVaR95Delay, baselineMakespan), "0.####");
            AddRiskMetricRow("Max Delay", risk.MaxDelay);
            AddRiskMetricRow("P(Balance rupture)", risk.BalanceRuptureProbability, "0.###");
            AddRiskMetricRow("Mean Balance generated", risk.MeanBalanceGenerated);
            AddRiskMetricRow("Mean Balance consumed", risk.MeanBalanceConsumed);
            AddRiskMetricRow("Mean Balance usage", risk.MeanBalanceUsage);
            AddRiskMetricRow("Mean Balance usage ratio", risk.MeanBalanceUsageRatio, "0.###");
            AddRiskMetricRow("Min observed Balance", risk.MinObservedBalance);
            AddRiskMetricRow("CVaR95 Delay | Balance rupture", risk.CVaR95GivenBalanceRupture);
            AddRiskMetricRow("Mean unabsorbed work", risk.MeanUnabsorbedWork);
            AddRiskMetricRow("Mean unabsorbed work ratio", risk.MeanUnabsorbedWorkRatio, "0.###");

            foreach (DataGridViewColumn column in _gridRiskMetrics.Columns)
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
        }

        private void AddRiskMetricRow(string metric, string value)
        {
            _gridRiskMetrics.Rows.Add(metric, value ?? string.Empty);
        }

        private void AddRiskMetricRow(string metric, int value)
        {
            _gridRiskMetrics.Rows.Add(metric, value.ToString());
        }

        private void AddRiskMetricRow(string metric, double value, string format = "0.##")
        {
            _gridRiskMetrics.Rows.Add(metric, value.ToString(format));
        }


        private static double SafeDivide(double numerator, double denominator)
        {
            return Math.Abs(denominator) > 0.0000001 ? numerator / denominator : 0.0;
        }

        private void FillRiskBinsGrid(RiskResultDto risk)
        {
            _gridRiskBins.Columns.Clear();
            _gridRiskBins.Rows.Clear();

            _gridRiskBins.Columns.Add("Bin", "Bin");
            _gridRiskBins.Columns.Add("EdgeLeft", "Edge Left");
            _gridRiskBins.Columns.Add("EdgeRight", "Edge Right");
            _gridRiskBins.Columns.Add("Count", "Count");

            AddRiskBinsRows(risk);

            foreach (DataGridViewColumn column in _gridRiskBins.Columns)
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
        }

        private void AddRiskBinsRows(RiskResultDto risk)
        {
            var edges = risk != null ? (risk.HistogramEdges ?? new List<double>()) : new List<double>();
            var counts = risk != null ? (risk.HistogramCounts ?? new List<int>()) : new List<int>();
            int rowCount = Math.Min(counts.Count, Math.Max(0, edges.Count - 1));

            for (int i = 0; i < rowCount; i++)
            {
                _gridRiskBins.Rows.Add(
                    i + 1,
                    edges[i].ToString("0.##"),
                    edges[i + 1].ToString("0.##"),
                    counts[i]);
            }
        }

        private void FillRiskHistogramChart(ExecutionSummary summary, RiskResultDto risk)
        {
            if (_chartRiskHistogram == null)
                return;

            EnsureRiskHistogramSeries();

            foreach (Series series in _chartRiskHistogram.Series)
                series.Points.Clear();

            _chartRiskHistogram.Titles.Clear();
            _chartRiskHistogram.Annotations.Clear();

            AddRiskHistogramToChart(risk);

            _chartRiskHistogram.Titles.Add(
                "Makespan distribution" +
                " - N=" + (risk != null ? risk.Iterations.ToString() : "0") +
                ", gamma=" + (risk != null ? risk.Gamma.ToString("0.###") : "0") +
                ", seed=" + (risk != null ? risk.Seed.ToString() : "0"));

            var allEdges = new List<double>();
            var allCounts = new List<int>();
            CollectHistogramBounds(risk, allEdges, allCounts);

            double minX = allEdges.Count > 0 ? allEdges.Min() : 0.0;
            double maxX = allEdges.Count > 0 ? allEdges.Max() : 1.0;
            int maxCount = allCounts.Count > 0 ? Math.Max(1, allCounts.Max()) : 1;

            if (maxX <= minX)
                maxX = minX + 1.0;

            double displayMinX = Math.Floor(minX + 0.5) - 1.0;
            if (displayMinX >= minX)
                displayMinX = minX - 1.0;

            var area = _chartRiskHistogram.ChartAreas[0];
            area.AxisX.Minimum = displayMinX;
            area.AxisX.Maximum = maxX;
            area.AxisY.Minimum = 0;
            area.AxisY.Maximum = Math.Ceiling(maxCount * 1.20);

            AddRiskPercentileLines(risk, area.AxisY.Maximum);
            UpdateRiskPercentileLegendText(risk);
        }

        private void EnsureRiskHistogramSeries()
        {
            RemoveSeriesIfExists("Paired Histogram");
            RemoveSeriesIfExists("Paired P50");
            RemoveSeriesIfExists("Paired P95");

            if (_chartRiskHistogram.Series.FindByName("Histogram") == null)
            {
                var series = new Series("Histogram");
                series.ChartType = SeriesChartType.Column;
                series.XValueType = ChartValueType.Double;
                series.IsValueShownAsLabel = false;
                series["PointWidth"] = "0.55";
                series.Legend = "MainLegend";
                _chartRiskHistogram.Series.Add(series);
            }

            EnsureLineSeries("P50", ChartDashStyle.Dash, Color.Green);
            EnsureLineSeries("P95", ChartDashStyle.Dash, Color.Red);
        }

        private void RemoveSeriesIfExists(string name)
        {
            if (_chartRiskHistogram == null)
                return;

            var series = _chartRiskHistogram.Series.FindByName(name);
            if (series != null)
                _chartRiskHistogram.Series.Remove(series);
        }

        private void EnsureLineSeries(string name, ChartDashStyle dashStyle, Color color)
        {
            if (_chartRiskHistogram.Series.FindByName(name) != null)
                return;

            var series = new Series(name);
            series.ChartType = SeriesChartType.Line;
            series.BorderWidth = 2;
            series.BorderDashStyle = dashStyle;
            series.Color = color;
            series.Legend = "MainLegend";
            _chartRiskHistogram.Series.Add(series);
        }

        private void AddRiskHistogramToChart(RiskResultDto risk)
        {
            if (risk == null || _chartRiskHistogram.Series.FindByName("Histogram") == null)
                return;

            var histogramSeries = _chartRiskHistogram.Series["Histogram"];
            var edges = risk.HistogramEdges ?? new List<double>();
            var counts = risk.HistogramCounts ?? new List<int>();
            int rowCount = Math.Min(counts.Count, Math.Max(0, edges.Count - 1));

            for (int i = 0; i < rowCount; i++)
            {
                double x = (edges[i] + edges[i + 1]) / 2.0;
                int y = counts[i];
                AddHistogramPoint(histogramSeries, i, x, y);
            }
        }

        private static void AddHistogramPoint(Series histogramSeries, int binIndex, double x, int y)
        {
            int pointIndex = histogramSeries.Points.AddXY(x, y);
            var point = histogramSeries.Points[pointIndex];
            point.ToolTip = "Bin " + (binIndex + 1) + " | count=" + y;

            if (y > 0)
            {
                point.Label = y.ToString();
                point.LabelForeColor = Color.Black;
                point.Font = new Font("Segoe UI", 8f, FontStyle.Regular);
            }
            else
            {
                point.Label = string.Empty;
            }
        }

        private static void CollectHistogramBounds(RiskResultDto risk, List<double> edges, List<int> counts)
        {
            if (risk == null)
                return;
            if (risk.HistogramEdges != null)
                edges.AddRange(risk.HistogramEdges);
            if (risk.HistogramCounts != null)
                counts.AddRange(risk.HistogramCounts);
        }

        private void AddRiskPercentileLines(RiskResultDto risk, double maxY)
        {
            if (risk == null)
                return;

            if (_chartRiskHistogram.Series.FindByName("P50") != null)
            {
                var p50Series = _chartRiskHistogram.Series["P50"];
                p50Series.Points.AddXY(risk.P50, 0);
                p50Series.Points.AddXY(risk.P50, maxY);
            }

            if (_chartRiskHistogram.Series.FindByName("P95") != null)
            {
                var p95Series = _chartRiskHistogram.Series["P95"];
                p95Series.Points.AddXY(risk.P95, 0);
                p95Series.Points.AddXY(risk.P95, maxY);
            }
        }

        private void UpdateRiskPercentileLegendText(RiskResultDto risk)
        {
            SetLegendText("Histogram", "Histogram");
            SetLegendText("P50", risk != null ? "P50 = " + risk.P50.ToString("0.##") : "P50");
            SetLegendText("P95", risk != null ? "P95 = " + risk.P95.ToString("0.##") : "P95");
        }

        private void SetLegendText(string seriesName, string legendText)
        {
            if (_chartRiskHistogram == null)
                return;

            var series = _chartRiskHistogram.Series.FindByName(seriesName);
            if (series != null)
                series.LegendText = legendText;
        }
    }
}
