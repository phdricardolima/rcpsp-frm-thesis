using RCPSP.Contracts;
using RCPSP.WinForms;
using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace RCPSP.Standalone
{
    internal sealed class FormSingleFileBaseline : Form
    {
        private readonly TextBox _txtFilePath;
        private readonly Button _btnBrowse;
        private readonly Button _btnRun;
        private readonly ComboBox _cmbHeuristic;
        private readonly ComboBox _cmbScheme;
        private readonly ComboBox _cmbDirection;
        private readonly ComboBox _cmbSamplingMode;
        private readonly NumericUpDown _numPositive;
        private readonly NumericUpDown _numNegative;
        private readonly NumericUpDown _numScenarios;
        private readonly NumericUpDown _numGamma;
        private readonly NumericUpDown _numSeed;
        private readonly NumericUpDown _numHistogramBins;
        private readonly NumericUpDown _numBbTimeLimit;
        private readonly Label _lblStatus;

        private static readonly Color AccentColor = Color.FromArgb(0, 120, 215);
        private static readonly Color SurfaceColor = Color.White;
        private static readonly Color MutedTextColor = Color.FromArgb(96, 96, 96);

        public FormSingleFileBaseline(string initialFile)
        {
            Text = "RCPSP-FRM Standalone | Single file baseline";
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Font;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            BackColor = SurfaceColor;
            MinimumSize = new Size(900, 600);
            Size = new Size(980, 660);

            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(18, 16, 18, 16);
            root.BackColor = SurfaceColor;
            root.ColumnCount = 1;
            root.RowCount = 6;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var filePanel = new TableLayoutPanel();
            filePanel.Dock = DockStyle.Top;
            filePanel.AutoSize = true;
            filePanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            filePanel.ColumnCount = 3;
            filePanel.RowCount = 1;
            filePanel.Margin = new Padding(0);
            filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            filePanel.Controls.Add(new Label { Text = "RCP file:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 0) }, 0, 0);
            _txtFilePath = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 2) };
            filePanel.Controls.Add(_txtFilePath, 1, 0);
            _btnBrowse = new Button { Text = "Browse...", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(10, 4, 10, 4), Margin = new Padding(8, 1, 0, 1) };
            _btnBrowse.Click += BtnBrowse_Click;
            filePanel.Controls.Add(_btnBrowse, 2, 0);
            root.Controls.Add(CreateGroup("Input file", filePanel), 0, 1);

            _cmbHeuristic = CreateCombo("SPT", "LPT", "EST", "EFT", "LST", "LFT", "MSLK", "MTS", "MIS", "GRWC", "ALL", "Modified DH B&B");
            _cmbScheme = CreateCombo("SERIAL", "PARALLEL");
            _cmbDirection = CreateCombo("FORWARD", "BACKWARD");
            _cmbSamplingMode = CreateCombo("FRM_WORKCONTENT_BILATERAL", "DELAY_UNILATERAL", "DELAY_STRUCTURAL", "UNILATERAL_STRUCTURAL");
            _cmbSamplingMode.SelectedIndex = 0;
            _numPositive = CreatePercentNumeric(25);
            _numNegative = CreateDecimalNumeric(25m, 0m, 99m, 2, 1m);
            _numScenarios = CreateIntegerNumeric(1000, 1, 1000000);
            _numGamma = CreateDecimalNumeric(1.00m, 0m, 1m, 3, 0.05m);
            _numSeed = CreateIntegerNumeric(123, 0, int.MaxValue);
            _numHistogramBins = CreateIntegerNumeric(20, 5, 500);
            _numBbTimeLimit = CreateIntegerNumeric(10, 1, 3600);

            var scheduling = CreateOptionsGrid();
            AddOption(scheduling, 0, "Heuristic", _cmbHeuristic);
            AddOption(scheduling, 1, "Scheme", _cmbScheme);
            AddOption(scheduling, 2, "Direction", _cmbDirection);
            AddOption(scheduling, 3, "B&B time limit (s)", _numBbTimeLimit);
            root.Controls.Add(CreateGroup("Scheduling", scheduling), 0, 2);

            var risk = CreateOptionsGrid();
            AddOption(risk, 0, "Sampling mode", _cmbSamplingMode);
            AddOption(risk, 1, "Positive FRM (%)", _numPositive);
            AddOption(risk, 2, "Negative FRM (%)", _numNegative);
            AddOption(risk, 3, "Monte Carlo scenarios", _numScenarios);
            AddOption(risk, 4, "Seed", _numSeed);
            AddOption(risk, 5, "Gamma", _numGamma);
            AddOption(risk, 6, "Histogram bins", _numHistogramBins);
            root.Controls.Add(CreateGroup("Risk and flexibility parameters", risk), 0, 3);

            _cmbHeuristic.SelectedIndexChanged += delegate { UpdateSchedulingControlsByHeuristic(); };

            var bottom = new TableLayoutPanel();
            bottom.Dock = DockStyle.Fill;
            bottom.AutoSize = true;
            bottom.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            bottom.Margin = new Padding(0, 4, 0, 0);
            bottom.ColumnCount = 2;
            bottom.RowCount = 1;
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            root.Controls.Add(bottom, 0, 5);

            _lblStatus = new Label { AutoSize = true, Text = "Ready.", Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 8, 0), ForeColor = MutedTextColor };
            bottom.Controls.Add(_lblStatus, 0, 0);

            _btnRun = new Button
            {
                Text = "Load and run FormBaseline",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Anchor = AnchorStyles.Right,
                Padding = new Padding(18, 8, 18, 8),
                Margin = new Padding(0),
                FlatStyle = FlatStyle.Flat,
                BackColor = AccentColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnRun.FlatAppearance.BorderSize = 0;
            _btnRun.Click += BtnRun_Click;
            bottom.Controls.Add(_btnRun, 1, 0);

            if (!string.IsNullOrWhiteSpace(initialFile))
                _txtFilePath.Text = initialFile;

            UpdateSchedulingControlsByHeuristic();
        }

        private static GroupBox CreateGroup(string title, Control content)
        {
            var group = new GroupBox
            {
                Text = title,
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(45, 45, 45),
                Padding = new Padding(14, 8, 14, 12),
                Margin = new Padding(0, 0, 0, 14)
            };

            content.Dock = DockStyle.Top;
            content.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            content.ForeColor = SystemColors.ControlText;
            group.Controls.Add(content);
            return group;
        }

        private static TableLayoutPanel CreateOptionsGrid()
        {
            var grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Top;
            grid.AutoSize = true;
            grid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            grid.ColumnCount = 4;
            grid.Margin = new Padding(0);
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            return grid;
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "PSPLIB RCP (*.rcp)|*.rcp|All files (*.*)|*.*";
                dialog.Title = "Select one PSPLIB .rcp file";
                dialog.Multiselect = false;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    _txtFilePath.Text = dialog.FileName;
            }
        }

        private void BtnRun_Click(object sender, EventArgs e)
        {
            try
            {
                string filePath = _txtFilePath.Text == null ? string.Empty : _txtFilePath.Text.Trim();
                if (string.IsNullOrWhiteSpace(filePath))
                    throw new InvalidOperationException("Select one .rcp file before running the analysis.");
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("Selected file was not found.", filePath);

                _lblStatus.Text = "Loading file...";
                var project = new RcpProjectDataImporter().Import(filePath);
                var request = BuildExecutionRequest(project);

                var form = new FormBaseline(
                    CompositionRoot.BuildPipelineRunner(),
                    CompositionRoot.BuildRunAnalysisService(),
                    request);

                _lblStatus.Text = "FormBaseline opened for " + project.ProjectName + ".";
                form.Show(this);
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "Error.";
                MessageBox.Show(this, ex.ToString(), "RCPSP-FRM Standalone", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private ExecutionRequest BuildExecutionRequest(ProjectDataDto project)
        {
            string heuristic = Convert.ToString(_cmbHeuristic.SelectedItem, CultureInfo.InvariantCulture);
            string scheme = Convert.ToString(_cmbScheme.SelectedItem, CultureInfo.InvariantCulture);
            string direction = Convert.ToString(_cmbDirection.SelectedItem, CultureInfo.InvariantCulture);
            string uiMode = Convert.ToString(_cmbSamplingMode.SelectedItem, CultureInfo.InvariantCulture);
            bool isPaired = string.Equals(uiMode, "UNILATERAL_STRUCTURAL", StringComparison.OrdinalIgnoreCase);
            string samplingMode = isPaired ? "DELAY_UNILATERAL" : NormalizeSamplingMode(uiMode);
            bool useExactEngine = IsModifiedDhBranchAndBound(heuristic);

            return new ExecutionRequest
            {
                Project = project,
                Scheduling = new SchedulingOptionsDto
                {
                    Heuristic = heuristic,
                    Scheme = scheme,
                    Direction = direction,
                    UseExactEngine = useExactEngine,
                    Engine = useExactEngine ? "DH_BB" : "HEURISTIC",
                    RunLabel = useExactEngine ? "Modified DH B&B" : string.Format("{0} | {1} | {2}", heuristic, scheme, direction),
                    BranchAndBoundTimeLimitSeconds = Convert.ToInt32(_numBbTimeLimit.Value, CultureInfo.InvariantCulture)
                },
                Frm = new FrmOptionsDto
                {
                    PositiveFlexibilityPercent = Convert.ToDouble(_numPositive.Value, CultureInfo.InvariantCulture),
                    NegativeFlexibilityPercent = Convert.ToDouble(_numNegative.Value, CultureInfo.InvariantCulture),
                    Mode = "NORMAL",
                    Enabled = true
                },
                Risk = new RiskOptionsDto
                {
                    ScenarioCount = Convert.ToInt32(_numScenarios.Value, CultureInfo.InvariantCulture),
                    Gamma = Convert.ToDouble(_numGamma.Value, CultureInfo.InvariantCulture),
                    Seed = Convert.ToInt32(_numSeed.Value, CultureInfo.InvariantCulture),
                    HistogramBinCount = Convert.ToInt32(_numHistogramBins.Value, CultureInfo.InvariantCulture),
                    SamplingMode = samplingMode,
                    RunPairedUnilateralStructural = isPaired,
                    UseCommonRandomNumbers = true,
                    Enabled = true
                },
                Crashing = new CrashingOptionsDto
                {
                    Enabled = true,
                    UseFrmGuidance = true,
                    RecalculateRiskAfterCrash = true,
                    MaxCombinationSize = 3,
                    MaxScenarioCount = 1000,
                    MaxActivitiesToCrash = 20
                }
            };
        }

        private static string NormalizeSamplingMode(string value)
        {
            if (string.Equals(value, "FRM_WORKCONTENT_BILATERAL", StringComparison.OrdinalIgnoreCase))
                return "FRM_WORKCONTENT_BILATERAL";
            if (string.Equals(value, "DELAY_STRUCTURAL", StringComparison.OrdinalIgnoreCase))
                return "DELAY_STRUCTURAL";

            return "DELAY_UNILATERAL";
        }

        private void UpdateSchedulingControlsByHeuristic()
        {
            string heuristic = Convert.ToString(_cmbHeuristic.SelectedItem, CultureInfo.InvariantCulture);
            bool isAll = string.Equals(heuristic, "ALL", StringComparison.OrdinalIgnoreCase);
            bool isBranchAndBound = IsModifiedDhBranchAndBound(heuristic);
            bool disable = isBranchAndBound || isAll;
            _cmbScheme.Enabled = !disable;
            _cmbDirection.Enabled = !disable;
            _numBbTimeLimit.Enabled = isBranchAndBound || isAll;
        }

        private static bool IsModifiedDhBranchAndBound(string heuristic)
        {
            return string.Equals(heuristic, "Modified DH B&B", StringComparison.OrdinalIgnoreCase)
                || string.Equals(heuristic, "DH_BB", StringComparison.OrdinalIgnoreCase)
                || string.Equals(heuristic, "EF B&B", StringComparison.OrdinalIgnoreCase);
        }

        private static ComboBox CreateCombo(params string[] values)
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            combo.Items.AddRange(values);
            if (combo.Items.Count > 0)
                combo.SelectedIndex = 0;
            return combo;
        }

        private static NumericUpDown CreatePercentNumeric(decimal value)
        {
            return CreateDecimalNumeric(value, 0m, 100m, 2, 1m);
        }

        private static NumericUpDown CreateIntegerNumeric(int value, int min, int max)
        {
            var numeric = new NumericUpDown();
            numeric.Dock = DockStyle.Fill;
            numeric.Minimum = min;
            numeric.Maximum = max;
            numeric.DecimalPlaces = 0;
            numeric.Increment = 1;
            numeric.Value = Math.Min(Math.Max(value, min), max);
            numeric.ThousandsSeparator = true;
            return numeric;
        }

        private static NumericUpDown CreateDecimalNumeric(decimal value, decimal min, decimal max, int decimals, decimal increment)
        {
            var numeric = new NumericUpDown();
            numeric.Dock = DockStyle.Fill;
            numeric.Minimum = min;
            numeric.Maximum = max;
            numeric.DecimalPlaces = decimals;
            numeric.Increment = increment;
            numeric.Value = value;
            return numeric;
        }

        private static void AddOption(TableLayoutPanel panel, int index, string label, Control control)
        {
            int row = index / 2;
            int labelColumn = (index % 2) == 0 ? 0 : 2;
            int controlColumn = labelColumn + 1;

            while (panel.RowStyles.Count <= row)
                panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            int leftPad = labelColumn == 0 ? 0 : 18;
            panel.Controls.Add(new Label { Text = label + ":", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(leftPad, 9, 8, 4) }, labelColumn, row);
            control.Margin = new Padding(0, 5, 0, 4);
            panel.Controls.Add(control, controlColumn, row);
        }
    }
}
