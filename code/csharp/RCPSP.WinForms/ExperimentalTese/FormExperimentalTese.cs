// Thesis traceability: user interface for configuring and executing the computational study.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RCPSP.WinForms.ExperimentalTese
{
    public sealed partial class FormExperimentalTese : Form
    {
        private readonly ListBox _lstFiles = new ListBox();
        private readonly TextBox _txtOutput = new TextBox();
        private readonly NumericUpDown _numIterations = new NumericUpDown();
        private readonly NumericUpDown _numReplications = new NumericUpDown();
        private readonly NumericUpDown _numSeed = new NumericUpDown();
        private readonly NumericUpDown _numGamma = new NumericUpDown();
        private readonly ComboBox _cmbSamplingMode = new ComboBox();
        private readonly NumericUpDown _numPositive = new NumericUpDown();
        private readonly NumericUpDown _numNegative = new NumericUpDown();
        private readonly NumericUpDown _numMaxCombination = new NumericUpDown();
        private readonly NumericUpDown _numMaxCandidateActivities = new NumericUpDown();
        private readonly NumericUpDown _numMaxScenarios = new NumericUpDown();
        private readonly NumericUpDown _numBbTimeLimit = new NumericUpDown();
        private readonly CheckBox _chkSensitivity = new CheckBox();
        private readonly CheckBox _chkRfRsStratifiedSelection = new CheckBox();
        private readonly NumericUpDown _numRfRsTargetInstances = new NumericUpDown();
        private readonly ProgressBar _progress = new ProgressBar();
        private readonly Label _lblStatus = new Label();
        private readonly Button _btnDryRun = new Button();
        private readonly Button _btnExecute = new Button();
        private readonly Button _btnAddFiles = new Button();
        private readonly Button _btnRemove = new Button();
        private readonly Button _btnOutput = new Button();

        public FormExperimentalTese()
        {
            Text = "Batch Experimental Execution Module - Thesis";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(980, 720);
            Size = new Size(1100, 780);
            BuildUi();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(10)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            _tabMain.Dock = DockStyle.Fill;
            _tabMain.TabPages.Clear();
            _tabExecution.Text = "Execution";
            _tabCharts.Text = "Analysis charts";
            _tabMain.TabPages.Add(_tabExecution);
            _tabMain.TabPages.Add(_tabCharts);
            Controls.Add(_tabMain);
            _tabExecution.Controls.Add(root);
            BuildChartsArea(_tabCharts);

            var title = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Batch Experimental Execution for Chapter 4",
                Font = new Font(Font.FontFamily, 14, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            root.Controls.Add(title, 0, 0);

            var filesGroup = new GroupBox { Dock = DockStyle.Fill, Text = "1. .rcp files and output folder" };
            root.Controls.Add(filesGroup, 0, 1);
            BuildFilesArea(filesGroup);

            var configGroup = new GroupBox { Dock = DockStyle.Fill, Text = "2. Experimental configuration" };
            root.Controls.Add(configGroup, 0, 2);
            BuildConfigArea(configGroup);

            var progressPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            progressPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            progressPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            _progress.Dock = DockStyle.Fill;
            _progress.Minimum = 0;
            _progress.Maximum = 100;
            _lblStatus.Dock = DockStyle.Fill;
            _lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            _lblStatus.Text = "Ready.";
            progressPanel.Controls.Add(_progress, 0, 0);
            progressPanel.Controls.Add(_lblStatus, 1, 0);
            root.Controls.Add(progressPanel, 0, 3);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            _btnExecute.Text = "Run full study";
            _btnExecute.Width = 180;
            _btnExecute.Height = 34;
            _btnExecute.Click += async (s, e) => await ExecuteAsync(false);
            _btnDryRun.Text = "Validate only / Dry Run";
            _btnDryRun.Width = 170;
            _btnDryRun.Height = 34;
            _btnDryRun.Click += async (s, e) => await ExecuteAsync(true);
            var btnClose = new Button { Text = "Close", Width = 100, Height = 34 };
            btnClose.Click += (s, e) => Close();
            buttons.Controls.Add(btnClose);
            buttons.Controls.Add(_btnExecute);
            buttons.Controls.Add(_btnDryRun);
            root.Controls.Add(buttons, 0, 4);
        }

        private void BuildFilesArea(Control parent)
        {
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(4) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 78));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            parent.Controls.Add(layout);

            _lstFiles.Dock = DockStyle.Fill;
            _lstFiles.HorizontalScrollbar = true;
            layout.Controls.Add(_lstFiles, 0, 0);

            var fileButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            _btnAddFiles.Text = "Add .rcp";
            _btnAddFiles.Width = 150;
            _btnAddFiles.Height = 28;
            _btnAddFiles.Click += BtnAddFiles_Click;
            _btnRemove.Text = "Remove";
            _btnRemove.Width = 150;
            _btnRemove.Height = 28;
            _btnRemove.Click += (s, e) => RemoveSelectedFiles();
            fileButtons.Controls.Add(_btnAddFiles);
            fileButtons.Controls.Add(_btnRemove);
            layout.Controls.Add(fileButtons, 1, 0);

            var outPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            outPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
            outPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            outPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            outPanel.Controls.Add(new Label { Text = "Output folder:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            _txtOutput.Dock = DockStyle.Fill;
            _txtOutput.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Chapter4_Results");
            outPanel.Controls.Add(_txtOutput, 1, 0);
            _btnOutput.Text = "Browse...";
            _btnOutput.Click += BtnOutput_Click;
            outPanel.Controls.Add(_btnOutput, 2, 0);
            layout.Controls.Add(outPanel, 0, 1);
            layout.SetColumnSpan(outPanel, 2);
        }

        private void BuildConfigArea(Control parent)
        {
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 9, Padding = new Padding(8, 6, 8, 4) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            parent.Controls.Add(grid);

            ConfigureNumeric(_numIterations, 1000, 1000000, 10000, 1000);
            ConfigureNumeric(_numReplications, 1, 50, 5, 1);
            ConfigureNumeric(_numSeed, 0, int.MaxValue, 123, 1);
            ConfigureNumeric(_numMaxCombination, 1, 10, 3, 1);
            ConfigureNumeric(_numMaxCandidateActivities, 1, 200, 20, 1);
            ConfigureNumeric(_numMaxScenarios, 1, 100000, 500, 50);
            ConfigureNumeric(_numBbTimeLimit, 1, 3600, 10, 5);
            ConfigureNumeric(_numRfRsTargetInstances, 0, 100000, 0, 1);
            ConfigureDecimalNumeric(_numGamma, 0m, 1m, 0.50m, 0.05m, 2);
            ConfigureDecimalNumeric(_numPositive, 0m, 100m, 25m, 1m, 2);
            ConfigureDecimalNumeric(_numNegative, 0m, 99m, 25m, 1m, 2);
            _cmbSamplingMode.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbSamplingMode.Items.Clear();
            _cmbSamplingMode.Items.Add("FRM_WORKCONTENT_BILATERAL");
            _cmbSamplingMode.Items.Add("DELAY_UNILATERAL");
            _cmbSamplingMode.Items.Add("DELAY_STRUCTURAL");
            _cmbSamplingMode.Items.Add("UNILATERAL_STRUCTURAL");
            _cmbSamplingMode.SelectedIndex = 0;
            _chkSensitivity.Text = "Run sensitivity analysis (gammas 0.25; 0.50; 0.75; 1.00)";
            _chkSensitivity.Checked = true;
            _chkSensitivity.AutoSize = true;
            _chkRfRsStratifiedSelection.Text = "Stratify instance selection by RF and RS";
            _chkRfRsStratifiedSelection.Checked = true;
            _chkRfRsStratifiedSelection.AutoSize = true;

            AddField(grid, 0, 0, "Iterations per replication", _numIterations);
            AddField(grid, 1, 0, "Independent replications", _numReplications);
            AddField(grid, 2, 0, "Seed global", _numSeed);
            AddField(grid, 3, 0, "Main gamma", _numGamma);

            AddField(grid, 0, 2, "Sampling mode", _cmbSamplingMode);
            AddField(grid, 1, 2, "Positive flexibility (%)", _numPositive);
            AddField(grid, 2, 2, "Negative flexibility (%)", _numNegative);
            AddField(grid, 3, 2, "Max activities/combo", _numMaxCombination);

            AddField(grid, 0, 4, "Max candidate activities", _numMaxCandidateActivities);
            AddField(grid, 1, 4, "Max crashing scenarios", _numMaxScenarios);

            AddField(grid, 0, 6, "B&&B time limit (s)", _numBbTimeLimit);
            AddField(grid, 1, 6, "RF/RS target instances (0 = all)", _numRfRsTargetInstances);
            grid.Controls.Add(_chkRfRsStratifiedSelection, 2, 6);
            grid.SetColumnSpan(_chkRfRsStratifiedSelection, 2);
            grid.SetRowSpan(_chkRfRsStratifiedSelection, 2);

            var info = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Text = string.Join(Environment.NewLine, new[]
                {
                    "This module is independent of the project currently open in MS Project. It imports multiple .rcp files and runs the thesis framework in batch: baseline -> FRM/SIF -> Monte Carlo by work-content deviations -> Balance absorption -> risk -> crashing -> recalculated FRM/risk -> integrated comparison. Raw outputs are saved first; consolidated analyses are generated only after the selected instances finish.",
                    "",
                    "Sampling modes:",
                    "  FRM_WORKCONTENT_BILATERAL - official thesis framework mode, equal to FormBaseline: bilateral work-content deviations, Balance absorption, non-absorbed excess converted into effective duration increase.",
                    "  DELAY_UNILATERAL - legacy/auxiliary direct duration perturbation.",
                    "  DELAY_STRUCTURAL - legacy ablation regime (upper bound = DSMax).",
                    "  UNILATERAL_STRUCTURAL - legacy paired ablation. Not the main thesis framework.",
                    "",
                    "Crashing controls:",
                    "  Max candidate activities - maximum number of FRM-eligible activities considered by the scenario generator.",
                    "  Max activities/combo - maximum number of activities compressed together in each scenario.",
                    "  Max crashing scenarios - maximum number of generated/evaluated scenarios.",
                    "  B&B time limit (s) - time limit per instance for the Modified DH B&B (reference baseline and exact re-evaluation during crashing). Default 10s, matching the prior hardcoded value.",
                    "",
                    "RF/RS selection:",
                    "  When enabled, the selected files form a candidate pool. The target count is distributed as evenly as possible over RF_LOW/RS_LOW, RF_LOW/RS_HIGH, RF_HIGH/RS_LOW and RF_HIGH/RS_HIGH. Zero processes all valid files but still exports the coverage audit."
                })
            };
            grid.Controls.Add(_chkSensitivity, 2, 4);
            grid.SetColumnSpan(_chkSensitivity, 2);
            grid.SetRowSpan(_chkSensitivity, 2);
            grid.Controls.Add(info, 0, 8);
            grid.SetColumnSpan(info, 4);
        }

        private static void ConfigureNumeric(NumericUpDown control, int min, int max, int value, int increment)
        {
            control.Minimum = min;
            control.Maximum = max;
            control.Value = value;
            control.Increment = increment;
            control.DecimalPlaces = 0;
            control.ThousandsSeparator = true;
            control.Dock = DockStyle.Fill;
        }

        private static void ConfigureDecimalNumeric(NumericUpDown control, decimal min, decimal max, decimal value, decimal increment, int decimals)
        {
            control.Minimum = min;
            control.Maximum = max;
            control.Value = Math.Min(Math.Max(value, min), max);
            control.Increment = increment;
            control.DecimalPlaces = decimals;
            control.Dock = DockStyle.Fill;
        }

        private static void AddField(TableLayoutPanel grid, int column, int row, string label, Control control)
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Margin = new Padding(4) };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            control.Dock = DockStyle.Fill;
            panel.Controls.Add(control, 0, 1);
            grid.Controls.Add(panel, column, row);
            grid.SetRowSpan(panel, 2);
        }

        private void BtnAddFiles_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Select PSPLIB .rcp files";
                dlg.Filter = "PSPLIB RCP (*.rcp)|*.rcp|All files (*.*)|*.*";
                dlg.Multiselect = true;
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                foreach (string file in dlg.FileNames)
                {
                    if (!_lstFiles.Items.Cast<string>().Any(x => string.Equals(x, file, StringComparison.OrdinalIgnoreCase)))
                        _lstFiles.Items.Add(file);
                }
            }
        }

        private void BtnOutput_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Select the output folder for Chapter 4 results";
                if (Directory.Exists(_txtOutput.Text))
                    dlg.SelectedPath = _txtOutput.Text;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    _txtOutput.Text = dlg.SelectedPath;
            }
        }

        private void RemoveSelectedFiles()
        {
            var selected = _lstFiles.SelectedItems.Cast<object>().ToList();
            foreach (var item in selected)
                _lstFiles.Items.Remove(item);
        }

        private async Task ExecuteAsync(bool dryRun)
        {
            try
            {
                var config = BuildConfig(dryRun);

                SetBusy(true);
                UpdateProgress(dryRun ? "Running preliminary validation..." : "Running full study...", 0);

                var runner = new BatchExperimentalStudyRunner();
                ExperimentalRunResult result = await Task.Run(() => runner.Run(config, UpdateProgress));

                UpdateProgress("Completed.", 100);
                if (!dryRun)
                    LoadExperimentalCharts(config.OutputDirectory);

                ShowMessageSafe(result.Message, "RCPSP-FRM | Experimental Study", MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                UpdateProgress("Error.", 0);
                ShowMessageSafe(ex.Message, "Error in the experimental module", MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private ExperimentalStudyConfig BuildConfig(bool dryRun)
        {
            var files = _lstFiles.Items.Cast<string>().ToList();
            if (files.Count == 0)
                throw new InvalidOperationException("Select at least one .rcp file.");

            string output = _txtOutput.Text;
            if (string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException("Provide the output folder.");

            string uiMode = Convert.ToString(_cmbSamplingMode.SelectedItem) ?? "FRM_WORKCONTENT_BILATERAL";
            bool isPaired = string.Equals(uiMode, "UNILATERAL_STRUCTURAL", StringComparison.OrdinalIgnoreCase);
            string motorMode = isPaired ? "DELAY_UNILATERAL" : NormalizeSamplingMode(uiMode);

            return new ExperimentalStudyConfig
            {
                InputFiles = files,
                OutputDirectory = output,
                Iterations = (int)_numIterations.Value,
                Replications = (int)_numReplications.Value,
                Seed = (int)_numSeed.Value,
                Gamma = Convert.ToDouble(_numGamma.Value, CultureInfo.InvariantCulture),
                SamplingModeUi = uiMode,
                SamplingMode = motorMode,
                RunPairedUnilateralStructural = isPaired,
                UseCommonRandomNumbers = true,
                UseRfRsStratifiedSelection = _chkRfRsStratifiedSelection.Checked,
                RfRsTargetInstanceCount = (int)_numRfRsTargetInstances.Value,
                PositiveFlexibilityPercent = Convert.ToDouble(_numPositive.Value, CultureInfo.InvariantCulture),
                NegativeFlexibilityPercent = Convert.ToDouble(_numNegative.Value, CultureInfo.InvariantCulture),
                MaxCombinationSize = (int)_numMaxCombination.Value,
                MaxCandidateActivities = (int)_numMaxCandidateActivities.Value,
                MaxCrashingScenarios = (int)_numMaxScenarios.Value,
                BranchAndBoundTimeLimitSeconds = (int)_numBbTimeLimit.Value,
                RunSensitivity = _chkSensitivity.Checked,
                RunThreePolicies = false,
                RunDryOnly = dryRun
            };
        }

        private void UpdateProgress(string message, int percent)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                if (IsHandleCreated)
                    BeginInvoke(new Action<string, int>(UpdateProgress), message, percent);
                return;
            }

            _progress.Style = ProgressBarStyle.Blocks;
            _progress.MarqueeAnimationSpeed = 0;
            _progress.Value = Math.Max(_progress.Minimum, Math.Min(_progress.Maximum, percent));
            _lblStatus.Text = message;
        }

        private void ShowMessageSafe(string message, string caption, MessageBoxIcon icon)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                if (IsHandleCreated)
                    BeginInvoke(new Action<string, string, MessageBoxIcon>(ShowMessageSafe), message, caption, icon);
                return;
            }

            MessageBox.Show(this, message, caption, MessageBoxButtons.OK, icon);
        }

        private void SetBusy(bool busy)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                if (IsHandleCreated)
                    BeginInvoke(new Action<bool>(SetBusy), busy);
                return;
            }

            _btnExecute.Enabled = !busy;
            _btnDryRun.Enabled = !busy;
            _btnAddFiles.Enabled = !busy;
            _btnRemove.Enabled = !busy;
            _btnOutput.Enabled = !busy;

            _lstFiles.Enabled = !busy;
            _txtOutput.Enabled = !busy;
            _numIterations.Enabled = !busy;
            _numReplications.Enabled = !busy;
            _numSeed.Enabled = !busy;
            _numGamma.Enabled = !busy;
            _cmbSamplingMode.Enabled = !busy;
            _numPositive.Enabled = !busy;
            _numNegative.Enabled = !busy;
            _numMaxCombination.Enabled = !busy;
            _numMaxCandidateActivities.Enabled = !busy;
            _numMaxScenarios.Enabled = !busy;
            _numBbTimeLimit.Enabled = !busy;
            _chkSensitivity.Enabled = !busy;

            _progress.Style = busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
            _progress.MarqueeAnimationSpeed = busy ? 30 : 0;

            if (!busy)
                _progress.Value = Math.Max(_progress.Minimum, Math.Min(_progress.Maximum, _progress.Value));

            _lblStatus.Text = busy ? "Running..." : "Ready.";
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
        private static double ParseDouble(string text, double fallback)
        {
            double value;
            if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                return value;
            if (double.TryParse(text, NumberStyles.Any, CultureInfo.GetCultureInfo("pt-BR"), out value))
                return value;
            return fallback;
        }
    }
}
