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
        private readonly IPipelineRunner _pipelineRunner;
        private readonly IRunAnalysisService _runAnalysisService;
        private readonly ExecutionRequest _request;

        private ExecutionSummary _currentSummary;
        private List<BaselineRunSummaryDto> _runs = new List<BaselineRunSummaryDto>();
        private TextBox _txtStageTimings;
        private TableLayoutPanel _resultsLayout;

        private bool _isRunning;
        private bool _isLoadingSelectedRun;
        private bool _suppressResultsSelectionAutoLoad;
        private int _pendingExactRunIndexToLoadAfterCompletion = -1;

        private SplitContainer _riskRootSplit;
        private SplitContainer _riskRightSplit;

        private GroupBox _grpRiskHistogram;
        private GroupBox _grpRiskMetrics;
        private GroupBox _grpRiskBins;

        private Chart _chartRiskHistogram;

        private DataGridView _gridRiskMetrics;
        private DataGridView _gridRiskBins;

        private BindingList<CrashingCandidateActivityDto> _crashCandidates = new BindingList<CrashingCandidateActivityDto>();
        private List<CrashingScenarioResultDto> _allCrashScenarios = new List<CrashingScenarioResultDto>();
        private List<CrashScenarioRowView> _allCrashScenarioRows = new List<CrashScenarioRowView>();
        private string _crashFilterStatus = "ALL";
        private bool _suppressCrashSelectionSync;
        private TableLayoutPanel _crashRightLayout;
        private FlowLayoutPanel _crashCardsPanel;
        private FlowLayoutPanel _crashFilterPanel;
        private Label _lblCrashBestGlobalCard;
        private Label _lblCrashBestAcceptableCard;
        private Label _lblCrashDistributionCard;
        private Chart _chartCrashingTradeoff;
        private Chart _chartCrashingDeltaCvar;
        private Chart _chartCrashingFrriPareto;
        private Chart _chartCrashingBalanceRupture;
        private TabControl _tabCrashingParetoCharts;
        private TabPage _tabCrashingDeltaCvar;
        private TabPage _tabCrashingFrriPareto;
        private TabPage _tabCrashingBalanceRupture;
        private Button _btnCrashFilterAll;
        private Button _btnCrashFilterRobust;
        private Button _btnCrashFilterFeasible;
        private Button _btnCrashFilterFragile;
        private Button _btnCrashFilterInviable;

        private Chart _chartComparisonTradeoff;
        private Chart _chartComparisonSifCvar;
        private Chart _chartComparisonMakespanCvar;
        private Chart _chartComparisonDeltaCvar;
        private Chart _chartComparisonParetoDominance;
        private Chart _chartComparisonBalanceRupture;
        private TabControl _tabComparisonCharts;
        private TabPage _tabComparisonSifCvar;
        private TabPage _tabComparisonMakespanCvar;
        private TabPage _tabComparisonDeltaCvar;
        private TabPage _tabComparisonPareto;
        private TabPage _tabComparisonBalanceRupture;
        private Panel panelComparisonLeft;
        private Panel panelComparisonRight;
        private SplitContainer splitComparisonRight;
        private GroupBox _groupComparisonControls;
        private GroupBox _groupComparisonState;
        private GroupBox _groupComparisonTable;
        private Label lblComparisonReference;
        private ComboBox _cmbComparisonReference;
        private Button _btnComparisonRun;
        private Label _lblComparisonSummary;
        private DataGridView _gridComparisonGrid;
        private BindingList<ComparisonRowView> _comparisonRows = new BindingList<ComparisonRowView>();
        private Dictionary<int, ExecutionSummary> _comparisonAnalysesByRunIndex = new Dictionary<int, ExecutionSummary>();

        private sealed class ResultsRunView
        {
            public int RunIndex { get; set; }
            public string RunType { get; set; } = string.Empty;
            public string Heuristic { get; set; } = string.Empty;
            public string Scheme { get; set; } = string.Empty;
            public string Direction { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string MethodClassification { get; set; } = string.Empty;
            public int Makespan { get; set; }
            public int ScheduledActivities { get; set; }
            public string BbTimeLimitSeconds { get; set; } = string.Empty;
            public string BbTimeLimitReached { get; set; } = string.Empty;
            public string BbOptimalityProven { get; set; } = string.Empty;
            public string BbNodesVisited { get; set; } = string.Empty;
            public string BbSlackSum { get; set; } = string.Empty;
            public string BbTrace { get; set; } = string.Empty;
            public string PriorityList { get; set; } = string.Empty;
            public string ScheduledOrder { get; set; } = string.Empty;
            public string ErrorMessage { get; set; } = string.Empty;
        }

        private sealed class ComparisonReferenceItem
        {
            public int RunIndex { get; set; }
            public string Label { get; set; } = string.Empty;
            public override string ToString() { return Label; }
        }


        private sealed class ComparisonBuildResult
        {
            public List<ComparisonRowView> Rows { get; set; } = new List<ComparisonRowView>();
            public Dictionary<int, ExecutionSummary> Analyses { get; set; } = new Dictionary<int, ExecutionSummary>();
        }

        private sealed class ComparisonRowView
        {
            public int RunIndex { get; set; }
            public string RowType { get; set; } = string.Empty;
            public string Scenario { get; set; } = string.Empty;
            public string Classification { get; set; } = string.Empty;
            public string Heuristic { get; set; } = string.Empty;
            public string Scheme { get; set; } = string.Empty;
            public string Direction { get; set; } = string.Empty;
            public int Makespan { get; set; }
            public int DeltaMakespan { get; set; }
            public string StructuralStatus { get; set; } = string.Empty;
            public double Sif { get; set; }
            public double P95 { get; set; }
            public double CVaR95 { get; set; }
            public double RelativeCVaR95 { get; set; }
            public double DeltaP95 { get; set; }
            public double DeltaCVaR95 { get; set; }
            public double Frri { get; set; }
            public double BalanceRuptureProbability { get; set; }
            public double MeanBalanceUsage { get; set; }
            public double MeanBalanceUsageRatio { get; set; }
            public double MinObservedBalance { get; set; }
            public double CVaR95GivenBalanceRupture { get; set; }
            public double MeanUnabsorbedWork { get; set; }
            public double MeanUnabsorbedWorkRatio { get; set; }
            public string ParetoStatus { get; set; } = string.Empty;
            public string DominatedBy { get; set; } = string.Empty;
            public bool IsReference { get; set; }
        }

        private sealed class CrashScenarioRowView
        {
            public CrashingScenarioResultDto Primary { get; set; }
            public CrashingScenarioResultDto Paired { get; set; }
            public double BaselineSif { get; set; }
            public string PairingStatus { get; set; } = string.Empty;
            public bool HasPaired { get { return Paired != null; } }
            public int Rank { get { return Primary != null ? Primary.Rank : 0; } }
            public string ScenarioName { get { return Primary != null ? Primary.ScenarioName : string.Empty; } }
            public string StructuralStatus { get { return Primary != null ? Primary.StructuralStatus : string.Empty; } }
            public string PairedStructuralStatus { get { return Paired != null ? Paired.StructuralStatus : string.Empty; } }
            public bool IsStructurallyAcceptable { get { return Primary != null && Primary.IsStructurallyAcceptable; } }
            public bool PairedIsStructurallyAcceptable { get { return Paired != null && Paired.IsStructurallyAcceptable; } }
            public int DeltaMakespan { get { return Primary != null ? Primary.DeltaMakespan : 0; } }
            public int PairedDeltaMakespan { get { return Paired != null ? Paired.DeltaMakespan : 0; } }
            public double BaselineP95 { get { return Primary != null ? Primary.BaselineP95 : 0.0; } }
            public double ScenarioP95 { get { return Primary != null ? Primary.ScenarioP95 : 0.0; } }
            public double DeltaP95 { get { return Primary != null ? Primary.DeltaP95 : 0.0; } }
            public double PairedBaselineP95 { get { return Paired != null ? Paired.BaselineP95 : 0.0; } }
            public double PairedScenarioP95 { get { return Paired != null ? Paired.ScenarioP95 : 0.0; } }
            public double PairedDeltaP95 { get { return Paired != null ? Paired.DeltaP95 : 0.0; } }
            public double DeltaCVaR95 { get { return Primary != null ? Primary.DeltaCVaR95 : 0.0; } }
            public double Frri { get { return Primary != null ? Primary.Frri : 0.0; } }
            public double ScenarioCVaR95 { get { return Primary != null ? Primary.ScenarioCVaR95 : 0.0; } }
            public double BaselineCVaR95 { get { return Primary != null ? Primary.BaselineCVaR95 : 0.0; } }
            public double PairedScenarioCVaR95 { get { return Paired != null ? Paired.ScenarioCVaR95 : 0.0; } }
            public double PairedBaselineCVaR95 { get { return Paired != null ? Paired.BaselineCVaR95 : 0.0; } }
            public double PairedDeltaCVaR95 { get { return Paired != null ? Paired.DeltaCVaR95 : 0.0; } }
            public double PairedFrri { get { return Paired != null ? Paired.Frri : 0.0; } }
            public double ScenarioBalanceRuptureProbability { get { return Primary != null ? Primary.ScenarioBalanceRuptureProbability : 0.0; } }
            public double DeltaBalanceRuptureProbability { get { return Primary != null ? Primary.DeltaBalanceRuptureProbability : 0.0; } }
            public double ScenarioMeanBalanceUsage { get { return Primary != null ? Primary.ScenarioMeanBalanceUsage : 0.0; } }
            public double DeltaMeanBalanceUsage { get { return Primary != null ? Primary.DeltaMeanBalanceUsage : 0.0; } }
            public double ScenarioMinObservedBalance { get { return Primary != null ? Primary.ScenarioMinObservedBalance : 0.0; } }
            public double DeltaMinObservedBalance { get { return Primary != null ? Primary.DeltaMinObservedBalance : 0.0; } }
            public double ScenarioMeanUnabsorbedWork { get { return Primary != null ? Primary.ScenarioMeanUnabsorbedWork : 0.0; } }
            public double DeltaMeanUnabsorbedWork { get { return Primary != null ? Primary.DeltaMeanUnabsorbedWork : 0.0; } }
            public double ScenarioMeanUnabsorbedWorkRatio { get { return Primary != null ? Primary.ScenarioMeanUnabsorbedWorkRatio : 0.0; } }
            public double DeltaMeanUnabsorbedWorkRatio { get { return Primary != null ? Primary.DeltaMeanUnabsorbedWorkRatio : 0.0; } }
            public double DeltaModesP95 { get { return Paired != null && Primary != null ? Paired.ScenarioP95 - Primary.ScenarioP95 : 0.0; } }
            public double DeltaModesScenarioCVaR95 { get { return Paired != null && Primary != null ? Paired.ScenarioCVaR95 - Primary.ScenarioCVaR95 : 0.0; } }
            public double DeltaModesFrri { get { return Paired != null && Primary != null ? Paired.Frri - Primary.Frri : 0.0; } }
            public double Sif { get { return Primary != null ? Primary.Sif : 0.0; } }
            public double PrimarySif { get { return Primary != null ? Primary.Sif : 0.0; } }
            public double DeltaSifPrimary { get { return Primary != null ? Primary.Sif - BaselineSif : 0.0; } }
            public double PairedSif { get { return Paired != null ? Paired.Sif : 0.0; } }
            public double DeltaSifPaired { get { return Paired != null ? Paired.Sif - BaselineSif : 0.0; } }
            public double DeltaModesSif { get { return Paired != null && Primary != null ? Paired.Sif - Primary.Sif : 0.0; } }
            public double RobustnessIndex { get { return Primary != null ? Primary.RobustnessIndex : 0.0; } }
            public double PairedRobustnessIndex { get { return Paired != null ? Paired.RobustnessIndex : 0.0; } }
            public string ActivitiesLabel { get { return Primary != null ? Primary.ActivitiesLabel : string.Empty; } }
            public int ActivityCount { get { return Primary != null ? Primary.ActivityCount : 0; } }
            public bool IsParetoDeltaCvar { get; set; }
            public bool IsParetoPairedDeltaCvar { get; set; }
            public bool IsParetoFrri { get; set; }
            public bool IsParetoBalanceRupture { get; set; }
            public bool IsParetoPairedFrri { get; set; }
            public bool IsParetoAllRegimes { get; set; }
            public string DominatedByDeltaCvar { get; set; } = string.Empty;
            public string DominatedByPairedDeltaCvar { get; set; } = string.Empty;
            public string DominatedByFrri { get; set; } = string.Empty;
            public string DominatedByBalanceRupture { get; set; } = string.Empty;
            public string DominatedByPairedFrri { get; set; } = string.Empty;
            public string DominatedByAllRegimes { get; set; } = string.Empty;
            public string ParetoDeltaCvar { get { return IsParetoDeltaCvar ? "Non-dominated" : "Dominated"; } }
            public string ParetoPairedDeltaCvar { get { return !HasPaired ? "N/A" : (IsParetoPairedDeltaCvar ? "Non-dominated" : "Dominated"); } }
            public string ParetoFrri { get { return IsParetoFrri ? "Non-dominated" : "Dominated"; } }
            public string ParetoBalanceRupture { get { return IsParetoBalanceRupture ? "Non-dominated" : "Dominated"; } }
            public string ParetoPairedFrri { get { return !HasPaired ? "N/A" : (IsParetoPairedFrri ? "Non-dominated" : "Dominated"); } }
            public string ParetoAllRegimes { get { return !HasPaired ? "N/A" : (IsParetoAllRegimes ? "Non-dominated" : "Dominated"); } }
        }

        public FormBaseline()
            : this(null, null, null)
        {
        }


        private void InitializeStageTimingsPanel()
        {
            if (panelResults == null || _txtStageTimings != null || gridResultados == null)
                return;

            _txtStageTimings = new TextBox();
            _txtStageTimings.Multiline = true;
            _txtStageTimings.ReadOnly = true;
            _txtStageTimings.ScrollBars = ScrollBars.Vertical;
            _txtStageTimings.BorderStyle = BorderStyle.FixedSingle;
            _txtStageTimings.Dock = DockStyle.Fill;
            _txtStageTimings.Margin = new Padding(0);
            _txtStageTimings.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point, ((byte)(0)));
            _txtStageTimings.BackColor = SystemColors.Window;
            _txtStageTimings.Text = "Stage timings will appear here after execution.";

            _resultsLayout = new TableLayoutPanel();
            _resultsLayout.Dock = DockStyle.Fill;
            _resultsLayout.Margin = new Padding(0);
            _resultsLayout.Padding = new Padding(0);
            _resultsLayout.ColumnCount = 1;
            _resultsLayout.RowCount = 2;
            _resultsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _resultsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96F));
            _resultsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            panelResults.SuspendLayout();
            try
            {
                panelResults.Controls.Clear();
                _resultsLayout.Controls.Add(_txtStageTimings, 0, 0);
                gridResultados.Dock = DockStyle.Fill;
                gridResultados.Margin = new Padding(0);
                _resultsLayout.Controls.Add(gridResultados, 0, 1);
                panelResults.Controls.Add(_resultsLayout);
            }
            finally
            {
                panelResults.ResumeLayout();
            }
        }

        public FormBaseline(
                            IPipelineRunner pipelineRunner,
                            IRunAnalysisService runAnalysisService,
                            ExecutionRequest request)
        {
            InitializeComponent();
            InitializeStageTimingsPanel();
            InitializeRiskLayout();
            InitializeCrashingLayout();
            InitializeCrashingParameterControls();
            InitializeComparisonLayout();
            ConfigureCrashingLabels();
            InitializeExportContextMenus();

            _pipelineRunner = pipelineRunner;
            _runAnalysisService = runAnalysisService;
            _request = request;
            SyncCrashingParameterControlsFromRequest();

            Shown += FormBaseline_Shown;

            _btnCrashClear.Click += BtnCrashClear_Click;
            _btnCrashRunAll.Click += BtnCrashRunAll_Click;
            if (_btnComparisonRun != null)
                _btnComparisonRun.Click += BtnComparisonRun_Click;
            _gridCrashingHistory.SelectionChanged += GridCrashingHistory_SelectionChanged;
            _gridCrashingScenario.CurrentCellDirtyStateChanged += GridCrashingScenario_CurrentCellDirtyStateChanged;
            _gridCrashingScenario.CellValueChanged += GridCrashingScenario_CellValueChanged;
            _gridCrashingScenario.SelectionChanged += GridCrashingScenario_SelectionChanged;
            _gridCrashingScenario.DataBindingComplete += GridCrashingScenario_DataBindingComplete;

            gridResultados.CellClick += gridResultados_CellClick;
            gridResultados.CellDoubleClick += gridResultados_CellDoubleClick;
            gridResultados.RowHeaderMouseClick += gridResultados_RowHeaderMouseClick;
            gridResultados.SelectionChanged += gridResultados_SelectionChanged;
            gridResultados.KeyUp += gridResultados_KeyUp;
            if (tabControlMain != null)
                tabControlMain.SelectedIndexChanged += tabControlMain_SelectedIndexChanged;
        }

    }
}
