using Microsoft.Office.Interop.MSProject;
using Microsoft.Office.Tools.Ribbon;
using RCPSP.Contracts;
using RCPSP.WinForms;
using RCPSP.WinForms.ExperimentalTese;
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Application = Microsoft.Office.Interop.MSProject.Application;
using Exception = System.Exception;

namespace Main
{
    public partial class Ribbon1
    {
        private string _selectedHeuristic = "SPT";
        private string _selectedScheme = "SERIAL";
        private string _selectedDirection = "FORWARD";
        private double _selectedNegative = 25.0;
        private double _selectedPositive = 25.0;

        private void Ribbon1_Load(object sender, RibbonUIEventArgs e)
        {


            dropDownHeuristica.SelectionChanged += DropDownHeuristica_SelectionChanged;
            dropDownEsquema.SelectionChanged += DropDownEsquema_SelectionChanged;
            dropDownDirecao.SelectionChanged += DropDownDirecao_SelectionChanged;
            dropDownNegativa.SelectionChanged += DropDownNegativa_SelectionChanged;
            dropDownPositiva.SelectionChanged += DropDownPositiva_SelectionChanged;


            InitializeRibbonSelections();
            UpdateSchedulingControlsByHeuristic();
        }

        private void InitializeRibbonSelections()
        {
            if (dropDownHeuristica.Items.Count > 0)
            {
                dropDownHeuristica.SelectedItem = dropDownHeuristica.Items[0];
                _selectedHeuristic = NormalizeLabel(dropDownHeuristica.SelectedItem.Label);
            }

            if (dropDownEsquema.Items.Count > 0)
            {
                dropDownEsquema.SelectedItem = dropDownEsquema.Items[0];
                _selectedScheme = NormalizeLabel(dropDownEsquema.SelectedItem.Label);
            }

            if (dropDownDirecao.Items.Count > 0)
            {
                dropDownDirecao.SelectedItem = dropDownDirecao.Items[0];
                _selectedDirection = NormalizeLabel(dropDownDirecao.SelectedItem.Label);
            }

            if (dropDownNegativa.Items.Count > 0)
            {
                dropDownNegativa.SelectedItem = dropDownNegativa.Items[0];
                _selectedNegative = ParseDouble(dropDownNegativa.SelectedItem.Label, 25.0);
            }

            if (dropDownPositiva.Items.Count > 0)
            {
                dropDownPositiva.SelectedItem = dropDownPositiva.Items[0];
                _selectedPositive = ParseDouble(dropDownPositiva.SelectedItem.Label, 25.0);
            }
        }


        private ExecutionRequest BuildExecutionRequest(ProjectDataDto project)
        {
            string heuristic = _selectedHeuristic;
            string scheme = _selectedScheme;
            string direction = _selectedDirection;

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
                    RunLabel = useExactEngine
                        ? "Modified DH B&B"
                        : string.Format("{0} | {1} | {2}", heuristic, scheme, direction)
                },
                Frm = new FrmOptionsDto
                {
                    PositiveFlexibilityPercent = _selectedPositive,
                    NegativeFlexibilityPercent = _selectedNegative,
                    Mode = "NORMAL",
                    Enabled = true
                },
                Risk = new RiskOptionsDto
                {
                    ScenarioCount = ParseInt(editBoxNScenarios != null ? editBoxNScenarios.Text : null, 1000),
                    Gamma = ParseDouble(editBoxGamma != null ? editBoxGamma.Text : null, 0.2),
                    Seed = ParseInt(editBoxSeed != null ? editBoxSeed.Text : null, 0),
                    HistogramBinCount = ParseInt(editBoxHistogramBin != null ? editBoxHistogramBin.Text : null, 20),
                    SamplingMode = "FRM_WORKCONTENT_BILATERAL",
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

        private void RCP_Click(object sender, RibbonControlEventArgs e)
        {
            try
            {
                openFileDialog1.Filter = "PSPLIB RCP (*.rcp)|*.rcp|All files (*.*)|*.*";
                openFileDialog1.Title = "Select PSPLIB .rcp file";
                openFileDialog1.Multiselect = false;

                if (openFileDialog1.ShowDialog() != DialogResult.OK)
                    return;

                var importer = CompositionRoot.BuildImporter();
                importer.ImportPsplibRcp(
                    openFileDialog1.FileName,
                    Globals.ThisAddIn.Application);

                MessageBox.Show(
                    ".rcp file successfully imported into Microsoft Project.",
                    "RCPSP-FRM",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error importing the .rcp file:\n\n" + ex.Message,
                    "RCPSP-FRM",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void DropDownHeuristica_SelectionChanged(object sender, RibbonControlEventArgs e)
        {
            if (dropDownHeuristica != null && dropDownHeuristica.SelectedItem != null)
                _selectedHeuristic = NormalizeLabel(dropDownHeuristica.SelectedItem.Label);

            UpdateSchedulingControlsByHeuristic();
        }

        private void DropDownEsquema_SelectionChanged(object sender, RibbonControlEventArgs e)
        {
            if (dropDownEsquema != null && dropDownEsquema.SelectedItem != null)
                _selectedScheme = NormalizeLabel(dropDownEsquema.SelectedItem.Label);
        }

        private void DropDownDirecao_SelectionChanged(object sender, RibbonControlEventArgs e)
        {
            if (dropDownDirecao != null && dropDownDirecao.SelectedItem != null)
                _selectedDirection = NormalizeLabel(dropDownDirecao.SelectedItem.Label);
        }

        private void DropDownNegativa_SelectionChanged(object sender, RibbonControlEventArgs e)
        {
            if (dropDownNegativa != null && dropDownNegativa.SelectedItem != null)
                _selectedNegative = ParseDouble(dropDownNegativa.SelectedItem.Label, 25.0);
        }

        private void DropDownPositiva_SelectionChanged(object sender, RibbonControlEventArgs e)
        {
            if (dropDownPositiva != null && dropDownPositiva.SelectedItem != null)
                _selectedPositive = ParseDouble(dropDownPositiva.SelectedItem.Label, 25.0);
        }

        private void UpdateSchedulingControlsByHeuristic()
        {
            bool disableSchemeAndDirection =
                _selectedHeuristic == "Modified DH B&B" ||
                _selectedHeuristic == "ALL";

            if (dropDownEsquema != null)
                dropDownEsquema.Enabled = !disableSchemeAndDirection;

            if (dropDownDirecao != null)
                dropDownDirecao.Enabled = !disableSchemeAndDirection;
        }


        private static int ParseInt(string text, int fallback)
        {
            int value;
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                ? value
                : fallback;
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

        private static string NormalizeLabel(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToUpperInvariant();
        }

        private static bool IsModifiedDhBranchAndBound(string heuristic)
        {
            string normalized = NormalizeLabel(heuristic);
            return normalized == "MODIFIED DH B&B"
                   || normalized == "B&B"
                   || normalized == "DHBB"
                   || normalized == "DH_BB"
                   || normalized == "ENHANCEFLEXIBILITY"
                   || normalized == "ENHANCE_FLEXIBILITY";
        }

        private void btnApply_Click(object sender, RibbonControlEventArgs e)
        {
            try
            {
                if (_selectedHeuristic == "ALL")
                {
                    MessageBox.Show(
                        "Select a specific rule.",
                        "RCPSP-FRM",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                Application app = Globals.ThisAddIn.Application;
                if (app == null || app.ActiveProject == null)
                {
                    MessageBox.Show(
                        "No active projects were found in MS Project.",
                        "RCPSP-FRM",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                var reader = CompositionRoot.BuildReader();
                ProjectDataDto project = reader.ReadActiveProject(app.ActiveProject);

                var request = BuildExecutionRequest(project);
                var pipelineRunner = CompositionRoot.BuildPipelineRunner();
                ExecutionSummary summary = pipelineRunner.Run(request);

                if (summary == null || summary.Baseline == null || summary.Baseline.Activities == null || summary.Baseline.Activities.Count == 0)
                    throw new InvalidOperationException("The baseline did not generate any activities suitable for use in MS Project.");

                var writer = CompositionRoot.BuildWriter();
                writer.WriteSchedule(app, summary);


            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error applying the schedule:\n\n" + ex.Message,
                    "RCPSP-FRM",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void btnExperimentalTese_Click(object sender, RibbonControlEventArgs e)
        {
            try
            {
                var form = new FormExperimentalTese();
                form.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error opening the thesis experimental module:\n\n" + ex.Message,
                    "RCPSP-FRM",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void btnRun_Click_1(object sender, RibbonControlEventArgs e)
        {

            try
            {
                var activeProject = Globals.ThisAddIn.Application.ActiveProject;
                if (activeProject == null)
                {
                    MessageBox.Show(
                        "No active project was found in MS Project.",
                        "RCPSP-FRM",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                var reader = CompositionRoot.BuildReader();
                var project = reader.ReadActiveProject(activeProject);

                var request = BuildExecutionRequest(project);
                request.Crashing.Enabled = true;

                var pipelineRunner = CompositionRoot.BuildPipelineRunner();
                var runAnalysisService = CompositionRoot.BuildRunAnalysisService();

                var form = new FormBaseline(
                    pipelineRunner,
                    runAnalysisService,
                    request);

                form.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "RCPSP-FRM",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private bool _corrigindo_Hist = false;

        private void editBoxHistogramBin_TextChanged(object sender, RibbonControlEventArgs e)
        {
            if (_corrigindo_Hist) return;

            string texto = editBoxHistogramBin.Text;
            string apenasNumeros = string.Empty;

            foreach (char c in texto)
            {
                if (char.IsDigit(c))
                    apenasNumeros += c;
            }

            if (texto != apenasNumeros)
            {
                _corrigindo_Hist = true;
                editBoxHistogramBin.Text = apenasNumeros;
                _corrigindo_Hist = false;

                MessageBox.Show("Please enter numbers only.", "Invalid value",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        private bool _corrigindo_Seed = false;
        private void editBoxSeed_TextChanged(object sender, RibbonControlEventArgs e)
        {
            if (_corrigindo_Seed) return;

            string texto = editBoxSeed.Text;
            string apenasNumeros = string.Empty;

            foreach (char c in texto)
            {
                if (char.IsDigit(c))
                    apenasNumeros += c;
            }

            if (texto != apenasNumeros)
            {
                _corrigindo_Seed = true;
                editBoxSeed.Text = apenasNumeros;
                _corrigindo_Seed = false;

                MessageBox.Show("Please enter numbers only.", "Invalid value",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        private bool _corrigindo_Scenarios = false;
        private void editBoxNScenarios_TextChanged(object sender, RibbonControlEventArgs e)
        {
            if (_corrigindo_Scenarios) return;

            string texto = editBoxNScenarios.Text;
            string apenasNumeros = string.Empty;

            foreach (char c in texto)
            {
                if (char.IsDigit(c))
                    apenasNumeros += c;
            }

            if (texto != apenasNumeros)
            {
                _corrigindo_Scenarios = true;
                editBoxNScenarios.Text = apenasNumeros;
                _corrigindo_Scenarios = false;

                MessageBox.Show("Please enter numbers only.", "Invalid value",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        private bool _corrigindo_Gamma = false;
        private void editBoxGamma_TextChanged(object sender, RibbonControlEventArgs e)
        {
            if (_corrigindo_Gamma) return;

            string texto = editBoxGamma.Text;
            string apenasNumeros = string.Empty;

            foreach (char c in texto)
            {
                if (char.IsDigit(c))
                    apenasNumeros += c;
            }

            if (texto != apenasNumeros)
            {
                _corrigindo_Gamma = true;
                editBoxGamma.Text = apenasNumeros;
                _corrigindo_Gamma = false;

                MessageBox.Show("Please enter numbers only.", "Invalid value",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
