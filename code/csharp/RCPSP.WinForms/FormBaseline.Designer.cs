using System;
using System.Windows.Forms;

namespace RCPSP.WinForms
{
    partial class FormBaseline
{
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.TabControl tabControlMain;
        private System.Windows.Forms.TabPage tabPageBaseline;
        private System.Windows.Forms.TabPage tabPageResults;
        private System.Windows.Forms.TabPage _tabPageFrm;
        private System.Windows.Forms.TabPage _tabRisk;
        private System.Windows.Forms.TabPage _tabCrashing;
        private System.Windows.Forms.TabPage _tabComparison;

        private System.Windows.Forms.Panel panelHeader;
        private System.Windows.Forms.Panel panelBody;
        private System.Windows.Forms.Panel panelResults;
        private System.Windows.Forms.Panel _panelFrm;
        private System.Windows.Forms.Panel _panelRisk;
        private System.Windows.Forms.SplitContainer _splitCrashing;
        private System.Windows.Forms.SplitContainer _splitComparison;

        private System.Windows.Forms.Label lblProjeto;
        private System.Windows.Forms.Label lblProjetoValor;
        private System.Windows.Forms.Label lblAtividades;
        private System.Windows.Forms.Label lblAtividadesValor;
        private System.Windows.Forms.Label lblRecursos;
        private System.Windows.Forms.Label lblRecursosValor;
        private System.Windows.Forms.Label lblHeuristica;
        private System.Windows.Forms.Label lblHeuristicaValor;
        private System.Windows.Forms.Label lblEsquema;
        private System.Windows.Forms.Label lblEsquemaValor;
        private System.Windows.Forms.Label lblDirecao;
        private System.Windows.Forms.Label lblDirecaoValor;
        private System.Windows.Forms.Label lblFlexPositiva;
        private System.Windows.Forms.Label lblFlexPositivaValor;
        private System.Windows.Forms.Label lblFlexNegativa;
        private System.Windows.Forms.Label lblFlexNegativaValor;
        private System.Windows.Forms.Label lblMonteCarlo;
        private System.Windows.Forms.Label lblMonteCarloValor;
        private System.Windows.Forms.TextBox _lblFrmSummary;
        private System.Windows.Forms.Label _lblRiskSummary;

        private System.Windows.Forms.SplitContainer splitContainerMain;
        private System.Windows.Forms.GroupBox groupBoxAtividades;
        private System.Windows.Forms.GroupBox groupBoxRecursos;

        private System.Windows.Forms.DataGridView gridAtividades;
        private System.Windows.Forms.DataGridView gridRecursos;
        private System.Windows.Forms.DataGridView gridResultados;
        private System.Windows.Forms.DataGridView _gridFrm;
        private System.Windows.Forms.DataGridView _gridRisk;

        private System.Windows.Forms.StatusStrip _statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel _lblStatusMain;
        private System.Windows.Forms.ToolStripStatusLabel _toolStripSep1;
        private System.Windows.Forms.ToolStripStatusLabel _lblStatusRun;
        private System.Windows.Forms.ToolStripStatusLabel _toolStripSep2;
        private System.Windows.Forms.ToolStripStatusLabel _lblStatusStep;
        private System.Windows.Forms.ToolStripStatusLabel _toolStripSep3;
        private System.Windows.Forms.ToolStripProgressBar _progressStatus;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();

            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.panelCrashingLeft = new System.Windows.Forms.Panel();
            this._groupCrashScenario = new System.Windows.Forms.GroupBox();
            this._gridCrashingScenario = new System.Windows.Forms.DataGridView();
            this.panelCrashToolbar = new System.Windows.Forms.Panel();
            this._btnCrashClear = new System.Windows.Forms.Button();
            this._btnCrashRunAll = new System.Windows.Forms.Button();
            this.tabControlMain = new System.Windows.Forms.TabControl();
            this.tabPageBaseline = new System.Windows.Forms.TabPage();
            this.panelBody = new System.Windows.Forms.Panel();
            this.splitContainerMain = new System.Windows.Forms.SplitContainer();
            this.groupBoxAtividades = new System.Windows.Forms.GroupBox();
            this.gridAtividades = new System.Windows.Forms.DataGridView();
            this.groupBoxRecursos = new System.Windows.Forms.GroupBox();
            this.gridRecursos = new System.Windows.Forms.DataGridView();
            this.panelHeader = new System.Windows.Forms.Panel();
            this.lblProjeto = new System.Windows.Forms.Label();
            this.lblProjetoValor = new System.Windows.Forms.Label();
            this.lblAtividades = new System.Windows.Forms.Label();
            this.lblAtividadesValor = new System.Windows.Forms.Label();
            this.lblRecursos = new System.Windows.Forms.Label();
            this.lblRecursosValor = new System.Windows.Forms.Label();
            this.lblHeuristica = new System.Windows.Forms.Label();
            this.lblHeuristicaValor = new System.Windows.Forms.Label();
            this.lblEsquema = new System.Windows.Forms.Label();
            this.lblEsquemaValor = new System.Windows.Forms.Label();
            this.lblDirecao = new System.Windows.Forms.Label();
            this.lblDirecaoValor = new System.Windows.Forms.Label();
            this.lblFlexPositiva = new System.Windows.Forms.Label();
            this.lblFlexPositivaValor = new System.Windows.Forms.Label();
            this.lblFlexNegativa = new System.Windows.Forms.Label();
            this.lblFlexNegativaValor = new System.Windows.Forms.Label();
            this.lblMonteCarlo = new System.Windows.Forms.Label();
            this.lblMonteCarloValor = new System.Windows.Forms.Label();
            this.tabPageResults = new System.Windows.Forms.TabPage();
            this.panelResults = new System.Windows.Forms.Panel();
            this.gridResultados = new System.Windows.Forms.DataGridView();
            this._tabPageFrm = new System.Windows.Forms.TabPage();
            this._panelFrm = new System.Windows.Forms.Panel();
            this._gridFrm = new System.Windows.Forms.DataGridView();
            this._lblFrmSummary = new System.Windows.Forms.TextBox();
            this._tabRisk = new System.Windows.Forms.TabPage();
            this._panelRisk = new System.Windows.Forms.Panel();
            this._gridRisk = new System.Windows.Forms.DataGridView();
            this._lblRiskSummary = new System.Windows.Forms.Label();
            this._tabCrashing = new System.Windows.Forms.TabPage();
            this._splitCrashing = new System.Windows.Forms.SplitContainer();
            this._groupCrashSummary = new System.Windows.Forms.GroupBox();
            this._gridCrashingHistory = new System.Windows.Forms.DataGridView();
            this._lblCrashClassification = new System.Windows.Forms.Label();
            this._lblCrashSummary = new System.Windows.Forms.Label();
            this._tabComparison = new System.Windows.Forms.TabPage();
            this._splitComparison = new System.Windows.Forms.SplitContainer();
            this._statusStrip = new System.Windows.Forms.StatusStrip();
            this._lblStatusMain = new System.Windows.Forms.ToolStripStatusLabel();
            this._toolStripSep1 = new System.Windows.Forms.ToolStripStatusLabel();
            this._lblStatusRun = new System.Windows.Forms.ToolStripStatusLabel();
            this._toolStripSep2 = new System.Windows.Forms.ToolStripStatusLabel();
            this._lblStatusStep = new System.Windows.Forms.ToolStripStatusLabel();
            this._toolStripSep3 = new System.Windows.Forms.ToolStripStatusLabel();
            this._progressStatus = new System.Windows.Forms.ToolStripProgressBar();
            this.panelCrashingLeft.SuspendLayout();
            this._groupCrashScenario.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._gridCrashingScenario)).BeginInit();
            this.panelCrashToolbar.SuspendLayout();
            this.tabControlMain.SuspendLayout();
            this.tabPageBaseline.SuspendLayout();
            this.panelBody.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).BeginInit();
            this.splitContainerMain.Panel1.SuspendLayout();
            this.splitContainerMain.Panel2.SuspendLayout();
            this.splitContainerMain.SuspendLayout();
            this.groupBoxAtividades.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridAtividades)).BeginInit();
            this.groupBoxRecursos.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridRecursos)).BeginInit();
            this.panelHeader.SuspendLayout();
            this.tabPageResults.SuspendLayout();
            this.panelResults.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridResultados)).BeginInit();
            this._tabPageFrm.SuspendLayout();
            this._panelFrm.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._gridFrm)).BeginInit();
            this._tabRisk.SuspendLayout();
            this._panelRisk.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._gridRisk)).BeginInit();
            this._tabCrashing.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._splitCrashing)).BeginInit();
            this._splitCrashing.Panel1.SuspendLayout();
            this._splitCrashing.Panel2.SuspendLayout();
            this._splitCrashing.SuspendLayout();
            this._groupCrashSummary.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._gridCrashingHistory)).BeginInit();
            this._tabComparison.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._splitComparison)).BeginInit();
            this._splitComparison.SuspendLayout();
            this._statusStrip.SuspendLayout();
            this.SuspendLayout();


            this.panelCrashingLeft.Controls.Add(this._groupCrashScenario);
            this.panelCrashingLeft.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelCrashingLeft.Location = new System.Drawing.Point(0, 0);
            this.panelCrashingLeft.Name = "panelCrashingLeft";
            this.panelCrashingLeft.Padding = new System.Windows.Forms.Padding(8);
            this.panelCrashingLeft.Size = new System.Drawing.Size(415, 706);
            this.panelCrashingLeft.TabIndex = 0;


            this._groupCrashScenario.Controls.Add(this._gridCrashingScenario);
            this._groupCrashScenario.Controls.Add(this.panelCrashToolbar);
            this._groupCrashScenario.Dock = System.Windows.Forms.DockStyle.Fill;
            this._groupCrashScenario.Location = new System.Drawing.Point(8, 8);
            this._groupCrashScenario.Name = "_groupCrashScenario";
            this._groupCrashScenario.Padding = new System.Windows.Forms.Padding(8);
            this._groupCrashScenario.Size = new System.Drawing.Size(399, 690);
            this._groupCrashScenario.TabIndex = 0;
            this._groupCrashScenario.TabStop = false;
            this._groupCrashScenario.Text = "Crashing Scenario";


            this._gridCrashingScenario.AllowUserToAddRows = false;
            this._gridCrashingScenario.AllowUserToDeleteRows = false;
            this._gridCrashingScenario.AllowUserToResizeRows = false;
            this._gridCrashingScenario.BackgroundColor = System.Drawing.Color.White;
            this._gridCrashingScenario.Dock = System.Windows.Forms.DockStyle.Fill;
            this._gridCrashingScenario.Location = new System.Drawing.Point(8, 63);
            this._gridCrashingScenario.MultiSelect = false;
            this._gridCrashingScenario.Name = "_gridCrashingScenario";
            this._gridCrashingScenario.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this._gridCrashingScenario.Size = new System.Drawing.Size(383, 619);
            this._gridCrashingScenario.TabIndex = 0;


            this.panelCrashToolbar.Controls.Add(this._btnCrashClear);
            this.panelCrashToolbar.Controls.Add(this._btnCrashRunAll);
            this.panelCrashToolbar.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelCrashToolbar.Location = new System.Drawing.Point(8, 21);
            this.panelCrashToolbar.Name = "panelCrashToolbar";
            this.panelCrashToolbar.Size = new System.Drawing.Size(383, 42);
            this.panelCrashToolbar.TabIndex = 1;


            this._btnCrashClear.Location = new System.Drawing.Point(5, 6);
            this._btnCrashClear.Name = "_btnCrashClear";
            this._btnCrashClear.Size = new System.Drawing.Size(100, 30);
            this._btnCrashClear.TabIndex = 1;
            this._btnCrashClear.Text = "Reset Scenario";


            this._btnCrashRunAll.Location = new System.Drawing.Point(113, 6);
            this._btnCrashRunAll.Name = "_btnCrashRunAll";
            this._btnCrashRunAll.Size = new System.Drawing.Size(160, 30);
            this._btnCrashRunAll.TabIndex = 3;
            this._btnCrashRunAll.Text = "Run All Scenarios";


            this.tabControlMain.Controls.Add(this.tabPageBaseline);
            this.tabControlMain.Controls.Add(this.tabPageResults);
            this.tabControlMain.Controls.Add(this._tabPageFrm);
            this.tabControlMain.Controls.Add(this._tabRisk);
            this.tabControlMain.Controls.Add(this._tabCrashing);
            this.tabControlMain.Controls.Add(this._tabComparison);
            this.tabControlMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControlMain.Location = new System.Drawing.Point(0, 0);
            this.tabControlMain.Margin = new System.Windows.Forms.Padding(2);
            this.tabControlMain.Name = "tabControlMain";
            this.tabControlMain.SelectedIndex = 0;
            this.tabControlMain.Size = new System.Drawing.Size(1260, 738);
            this.tabControlMain.TabIndex = 0;


            this.tabPageBaseline.Controls.Add(this.panelBody);
            this.tabPageBaseline.Controls.Add(this.panelHeader);
            this.tabPageBaseline.Location = new System.Drawing.Point(4, 22);
            this.tabPageBaseline.Margin = new System.Windows.Forms.Padding(2);
            this.tabPageBaseline.Name = "tabPageBaseline";
            this.tabPageBaseline.Padding = new System.Windows.Forms.Padding(2);
            this.tabPageBaseline.Size = new System.Drawing.Size(1252, 712);
            this.tabPageBaseline.TabIndex = 0;
            this.tabPageBaseline.Text = "Baseline";
            this.tabPageBaseline.UseVisualStyleBackColor = true;


            this.panelBody.Controls.Add(this.splitContainerMain);
            this.panelBody.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelBody.Location = new System.Drawing.Point(2, 124);
            this.panelBody.Margin = new System.Windows.Forms.Padding(2);
            this.panelBody.Name = "panelBody";
            this.panelBody.Padding = new System.Windows.Forms.Padding(6);
            this.panelBody.Size = new System.Drawing.Size(1248, 586);
            this.panelBody.TabIndex = 1;


            this.splitContainerMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerMain.Location = new System.Drawing.Point(6, 6);
            this.splitContainerMain.Margin = new System.Windows.Forms.Padding(2);
            this.splitContainerMain.Name = "splitContainerMain";
            this.splitContainerMain.Orientation = System.Windows.Forms.Orientation.Horizontal;


            this.splitContainerMain.Panel1.Controls.Add(this.groupBoxAtividades);


            this.splitContainerMain.Panel2.Controls.Add(this.groupBoxRecursos);
            this.splitContainerMain.Size = new System.Drawing.Size(1236, 574);
            this.splitContainerMain.SplitterDistance = 394;
            this.splitContainerMain.TabIndex = 0;


            this.groupBoxAtividades.Controls.Add(this.gridAtividades);
            this.groupBoxAtividades.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxAtividades.Location = new System.Drawing.Point(0, 0);
            this.groupBoxAtividades.Margin = new System.Windows.Forms.Padding(2);
            this.groupBoxAtividades.Name = "groupBoxAtividades";
            this.groupBoxAtividades.Padding = new System.Windows.Forms.Padding(6);
            this.groupBoxAtividades.Size = new System.Drawing.Size(1236, 394);
            this.groupBoxAtividades.TabIndex = 0;
            this.groupBoxAtividades.TabStop = false;
            this.groupBoxAtividades.Text = "Imported Activities";


            this.gridAtividades.AllowUserToAddRows = false;
            this.gridAtividades.AllowUserToDeleteRows = false;
            this.gridAtividades.AllowUserToOrderColumns = true;
            this.gridAtividades.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridAtividades.BackgroundColor = System.Drawing.SystemColors.Window;
            this.gridAtividades.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridAtividades.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridAtividades.Location = new System.Drawing.Point(6, 19);
            this.gridAtividades.Margin = new System.Windows.Forms.Padding(2);
            this.gridAtividades.MultiSelect = false;
            this.gridAtividades.Name = "gridAtividades";
            this.gridAtividades.ReadOnly = true;
            this.gridAtividades.RowHeadersWidth = 51;
            this.gridAtividades.RowTemplate.Height = 24;
            this.gridAtividades.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridAtividades.Size = new System.Drawing.Size(1224, 369);
            this.gridAtividades.TabIndex = 0;


            this.groupBoxRecursos.Controls.Add(this.gridRecursos);
            this.groupBoxRecursos.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxRecursos.Location = new System.Drawing.Point(0, 0);
            this.groupBoxRecursos.Margin = new System.Windows.Forms.Padding(2);
            this.groupBoxRecursos.Name = "groupBoxRecursos";
            this.groupBoxRecursos.Padding = new System.Windows.Forms.Padding(6);
            this.groupBoxRecursos.Size = new System.Drawing.Size(1236, 176);
            this.groupBoxRecursos.TabIndex = 0;
            this.groupBoxRecursos.TabStop = false;
            this.groupBoxRecursos.Text = "Imported Resources";


            this.gridRecursos.AllowUserToAddRows = false;
            this.gridRecursos.AllowUserToDeleteRows = false;
            this.gridRecursos.AllowUserToOrderColumns = true;
            this.gridRecursos.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridRecursos.BackgroundColor = System.Drawing.SystemColors.Window;
            this.gridRecursos.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridRecursos.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridRecursos.Location = new System.Drawing.Point(6, 19);
            this.gridRecursos.Margin = new System.Windows.Forms.Padding(2);
            this.gridRecursos.MultiSelect = false;
            this.gridRecursos.Name = "gridRecursos";
            this.gridRecursos.ReadOnly = true;
            this.gridRecursos.RowHeadersWidth = 51;
            this.gridRecursos.RowTemplate.Height = 24;
            this.gridRecursos.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridRecursos.Size = new System.Drawing.Size(1224, 151);
            this.gridRecursos.TabIndex = 0;


            this.panelHeader.Controls.Add(this.lblProjeto);
            this.panelHeader.Controls.Add(this.lblProjetoValor);
            this.panelHeader.Controls.Add(this.lblAtividades);
            this.panelHeader.Controls.Add(this.lblAtividadesValor);
            this.panelHeader.Controls.Add(this.lblRecursos);
            this.panelHeader.Controls.Add(this.lblRecursosValor);
            this.panelHeader.Controls.Add(this.lblHeuristica);
            this.panelHeader.Controls.Add(this.lblHeuristicaValor);
            this.panelHeader.Controls.Add(this.lblEsquema);
            this.panelHeader.Controls.Add(this.lblEsquemaValor);
            this.panelHeader.Controls.Add(this.lblDirecao);
            this.panelHeader.Controls.Add(this.lblDirecaoValor);
            this.panelHeader.Controls.Add(this.lblFlexPositiva);
            this.panelHeader.Controls.Add(this.lblFlexPositivaValor);
            this.panelHeader.Controls.Add(this.lblFlexNegativa);
            this.panelHeader.Controls.Add(this.lblFlexNegativaValor);
            this.panelHeader.Controls.Add(this.lblMonteCarlo);
            this.panelHeader.Controls.Add(this.lblMonteCarloValor);
            this.panelHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelHeader.Location = new System.Drawing.Point(2, 2);
            this.panelHeader.Margin = new System.Windows.Forms.Padding(2);
            this.panelHeader.Name = "panelHeader";
            this.panelHeader.Size = new System.Drawing.Size(1248, 122);
            this.panelHeader.TabIndex = 0;


            this.lblProjeto.AutoSize = true;
            this.lblProjeto.Location = new System.Drawing.Point(16, 15);
            this.lblProjeto.Name = "lblProjeto";
            this.lblProjeto.Size = new System.Drawing.Size(43, 13);
            this.lblProjeto.TabIndex = 0;
            this.lblProjeto.Text = "Project:";


            this.lblProjetoValor.AutoSize = true;
            this.lblProjetoValor.Location = new System.Drawing.Point(90, 15);
            this.lblProjetoValor.Name = "lblProjetoValor";
            this.lblProjetoValor.Size = new System.Drawing.Size(10, 13);
            this.lblProjetoValor.TabIndex = 1;
            this.lblProjetoValor.Text = "-";


            this.lblAtividades.AutoSize = true;
            this.lblAtividades.Location = new System.Drawing.Point(16, 40);
            this.lblAtividades.Name = "lblAtividades";
            this.lblAtividades.Size = new System.Drawing.Size(52, 13);
            this.lblAtividades.TabIndex = 2;
            this.lblAtividades.Text = "Activities:";


            this.lblAtividadesValor.AutoSize = true;
            this.lblAtividadesValor.Location = new System.Drawing.Point(90, 40);
            this.lblAtividadesValor.Name = "lblAtividadesValor";
            this.lblAtividadesValor.Size = new System.Drawing.Size(10, 13);
            this.lblAtividadesValor.TabIndex = 3;
            this.lblAtividadesValor.Text = "-";


            this.lblRecursos.AutoSize = true;
            this.lblRecursos.Location = new System.Drawing.Point(16, 65);
            this.lblRecursos.Name = "lblRecursos";
            this.lblRecursos.Size = new System.Drawing.Size(61, 13);
            this.lblRecursos.TabIndex = 4;
            this.lblRecursos.Text = "Resources:";


            this.lblRecursosValor.AutoSize = true;
            this.lblRecursosValor.Location = new System.Drawing.Point(90, 65);
            this.lblRecursosValor.Name = "lblRecursosValor";
            this.lblRecursosValor.Size = new System.Drawing.Size(10, 13);
            this.lblRecursosValor.TabIndex = 5;
            this.lblRecursosValor.Text = "-";


            this.lblHeuristica.AutoSize = true;
            this.lblHeuristica.Location = new System.Drawing.Point(260, 15);
            this.lblHeuristica.Name = "lblHeuristica";
            this.lblHeuristica.Size = new System.Drawing.Size(51, 13);
            this.lblHeuristica.TabIndex = 6;
            this.lblHeuristica.Text = "Heuristic:";


            this.lblHeuristicaValor.AutoSize = true;
            this.lblHeuristicaValor.Location = new System.Drawing.Point(330, 15);
            this.lblHeuristicaValor.Name = "lblHeuristicaValor";
            this.lblHeuristicaValor.Size = new System.Drawing.Size(10, 13);
            this.lblHeuristicaValor.TabIndex = 7;
            this.lblHeuristicaValor.Text = "-";


            this.lblEsquema.AutoSize = true;
            this.lblEsquema.Location = new System.Drawing.Point(260, 40);
            this.lblEsquema.Name = "lblEsquema";
            this.lblEsquema.Size = new System.Drawing.Size(49, 13);
            this.lblEsquema.TabIndex = 8;
            this.lblEsquema.Text = "Scheme:";


            this.lblEsquemaValor.AutoSize = true;
            this.lblEsquemaValor.Location = new System.Drawing.Point(330, 40);
            this.lblEsquemaValor.Name = "lblEsquemaValor";
            this.lblEsquemaValor.Size = new System.Drawing.Size(10, 13);
            this.lblEsquemaValor.TabIndex = 9;
            this.lblEsquemaValor.Text = "-";


            this.lblDirecao.AutoSize = true;
            this.lblDirecao.Location = new System.Drawing.Point(260, 65);
            this.lblDirecao.Name = "lblDirecao";
            this.lblDirecao.Size = new System.Drawing.Size(52, 13);
            this.lblDirecao.TabIndex = 10;
            this.lblDirecao.Text = "Direction:";


            this.lblDirecaoValor.AutoSize = true;
            this.lblDirecaoValor.Location = new System.Drawing.Point(330, 65);
            this.lblDirecaoValor.Name = "lblDirecaoValor";
            this.lblDirecaoValor.Size = new System.Drawing.Size(10, 13);
            this.lblDirecaoValor.TabIndex = 11;
            this.lblDirecaoValor.Text = "-";


            this.lblFlexPositiva.AutoSize = true;
            this.lblFlexPositiva.Location = new System.Drawing.Point(520, 15);
            this.lblFlexPositiva.Name = "lblFlexPositiva";
            this.lblFlexPositiva.Size = new System.Drawing.Size(69, 13);
            this.lblFlexPositiva.TabIndex = 12;
            this.lblFlexPositiva.Text = "Positive Flex:";


            this.lblFlexPositivaValor.AutoSize = true;
            this.lblFlexPositivaValor.Location = new System.Drawing.Point(610, 15);
            this.lblFlexPositivaValor.Name = "lblFlexPositivaValor";
            this.lblFlexPositivaValor.Size = new System.Drawing.Size(10, 13);
            this.lblFlexPositivaValor.TabIndex = 13;
            this.lblFlexPositivaValor.Text = "-";


            this.lblFlexNegativa.AutoSize = true;
            this.lblFlexNegativa.Location = new System.Drawing.Point(520, 40);
            this.lblFlexNegativa.Name = "lblFlexNegativa";
            this.lblFlexNegativa.Size = new System.Drawing.Size(75, 13);
            this.lblFlexNegativa.TabIndex = 14;
            this.lblFlexNegativa.Text = "Negative Flex:";


            this.lblFlexNegativaValor.AutoSize = true;
            this.lblFlexNegativaValor.Location = new System.Drawing.Point(610, 40);
            this.lblFlexNegativaValor.Name = "lblFlexNegativaValor";
            this.lblFlexNegativaValor.Size = new System.Drawing.Size(10, 13);
            this.lblFlexNegativaValor.TabIndex = 15;
            this.lblFlexNegativaValor.Text = "-";


            this.lblMonteCarlo.AutoSize = true;
            this.lblMonteCarlo.Location = new System.Drawing.Point(520, 65);
            this.lblMonteCarlo.Name = "lblMonteCarlo";
            this.lblMonteCarlo.Size = new System.Drawing.Size(67, 13);
            this.lblMonteCarlo.TabIndex = 16;
            this.lblMonteCarlo.Text = "Monte Carlo:";


            this.lblMonteCarloValor.AutoSize = true;
            this.lblMonteCarloValor.Location = new System.Drawing.Point(610, 65);
            this.lblMonteCarloValor.Name = "lblMonteCarloValor";
            this.lblMonteCarloValor.Size = new System.Drawing.Size(10, 13);
            this.lblMonteCarloValor.TabIndex = 17;
            this.lblMonteCarloValor.Text = "-";


            this.tabPageResults.Controls.Add(this.panelResults);
            this.tabPageResults.Location = new System.Drawing.Point(4, 22);
            this.tabPageResults.Margin = new System.Windows.Forms.Padding(2);
            this.tabPageResults.Name = "tabPageResults";
            this.tabPageResults.Padding = new System.Windows.Forms.Padding(8);
            this.tabPageResults.Size = new System.Drawing.Size(1252, 712);
            this.tabPageResults.TabIndex = 1;
            this.tabPageResults.Text = "Results";
            this.tabPageResults.UseVisualStyleBackColor = true;


            this.panelResults.Controls.Add(this.gridResultados);
            this.panelResults.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelResults.Location = new System.Drawing.Point(8, 8);
            this.panelResults.Name = "panelResults";
            this.panelResults.Size = new System.Drawing.Size(1236, 696);
            this.panelResults.TabIndex = 0;


            this.gridResultados.AllowUserToAddRows = false;
            this.gridResultados.AllowUserToDeleteRows = false;
            this.gridResultados.AllowUserToOrderColumns = true;
            this.gridResultados.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridResultados.BackgroundColor = System.Drawing.SystemColors.Window;
            this.gridResultados.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridResultados.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridResultados.Location = new System.Drawing.Point(0, 0);
            this.gridResultados.Margin = new System.Windows.Forms.Padding(2);
            this.gridResultados.MultiSelect = false;
            this.gridResultados.Name = "gridResultados";
            this.gridResultados.ReadOnly = true;
            this.gridResultados.RowHeadersWidth = 51;
            this.gridResultados.RowTemplate.Height = 24;
            this.gridResultados.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridResultados.Size = new System.Drawing.Size(1236, 696);
            this.gridResultados.TabIndex = 0;


            this._tabPageFrm.Controls.Add(this._panelFrm);
            this._tabPageFrm.Location = new System.Drawing.Point(4, 22);
            this._tabPageFrm.Name = "_tabPageFrm";
            this._tabPageFrm.Padding = new System.Windows.Forms.Padding(3);
            this._tabPageFrm.Size = new System.Drawing.Size(1252, 712);
            this._tabPageFrm.TabIndex = 2;
            this._tabPageFrm.Text = "FRM";
            this._tabPageFrm.UseVisualStyleBackColor = true;


            this._panelFrm.Controls.Add(this._gridFrm);
            this._panelFrm.Controls.Add(this._lblFrmSummary);
            this._panelFrm.Dock = System.Windows.Forms.DockStyle.Fill;
            this._panelFrm.Location = new System.Drawing.Point(3, 3);
            this._panelFrm.Name = "_panelFrm";
            this._panelFrm.Padding = new System.Windows.Forms.Padding(8);
            this._panelFrm.Size = new System.Drawing.Size(1246, 706);
            this._panelFrm.TabIndex = 0;


            this._gridFrm.AllowUserToAddRows = false;
            this._gridFrm.AllowUserToDeleteRows = false;
            this._gridFrm.AllowUserToOrderColumns = true;
            this._gridFrm.AllowUserToResizeRows = false;
            this._gridFrm.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.DisplayedCells;
            this._gridFrm.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            this._gridFrm.BackgroundColor = System.Drawing.Color.White;
            this._gridFrm.Dock = System.Windows.Forms.DockStyle.Fill;
            this._gridFrm.Location = new System.Drawing.Point(8, 178);
            this._gridFrm.MultiSelect = false;
            this._gridFrm.Name = "_gridFrm";
            this._gridFrm.ReadOnly = true;
            this._gridFrm.RowTemplate.Height = 30;
            this._gridFrm.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this._gridFrm.Size = new System.Drawing.Size(1230, 520);
            this._gridFrm.TabIndex = 0;


            this._lblFrmSummary.BackColor = System.Drawing.Color.WhiteSmoke;
            this._lblFrmSummary.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this._lblFrmSummary.Dock = System.Windows.Forms.DockStyle.Top;
            this._lblFrmSummary.Font = new System.Drawing.Font("Segoe UI", 11F);
            this._lblFrmSummary.Location = new System.Drawing.Point(8, 8);
            this._lblFrmSummary.Multiline = true;
            this._lblFrmSummary.Name = "_lblFrmSummary";
            this._lblFrmSummary.ReadOnly = true;
            this._lblFrmSummary.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this._lblFrmSummary.Size = new System.Drawing.Size(1230, 170);
            this._lblFrmSummary.TabIndex = 1;
            this._lblFrmSummary.Text = "Select a run in Results to calculate FRM.";
            this._lblFrmSummary.WordWrap = false;


            this._tabRisk.Controls.Add(this._panelRisk);
            this._tabRisk.Location = new System.Drawing.Point(4, 22);
            this._tabRisk.Name = "_tabRisk";
            this._tabRisk.Padding = new System.Windows.Forms.Padding(3);
            this._tabRisk.Size = new System.Drawing.Size(1252, 712);
            this._tabRisk.TabIndex = 3;
            this._tabRisk.Text = "RISK";
            this._tabRisk.UseVisualStyleBackColor = true;


            this._panelRisk.Controls.Add(this._gridRisk);
            this._panelRisk.Controls.Add(this._lblRiskSummary);
            this._panelRisk.Dock = System.Windows.Forms.DockStyle.Fill;
            this._panelRisk.Location = new System.Drawing.Point(3, 3);
            this._panelRisk.Name = "_panelRisk";
            this._panelRisk.Padding = new System.Windows.Forms.Padding(8);
            this._panelRisk.Size = new System.Drawing.Size(1246, 706);
            this._panelRisk.TabIndex = 0;


            this._gridRisk.AllowUserToAddRows = false;
            this._gridRisk.AllowUserToDeleteRows = false;
            this._gridRisk.AllowUserToOrderColumns = true;
            this._gridRisk.AllowUserToResizeRows = false;
            this._gridRisk.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.DisplayedCells;
            this._gridRisk.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            this._gridRisk.BackgroundColor = System.Drawing.Color.White;
            this._gridRisk.Dock = System.Windows.Forms.DockStyle.Fill;
            this._gridRisk.Location = new System.Drawing.Point(8, 178);
            this._gridRisk.MultiSelect = false;
            this._gridRisk.Name = "_gridRisk";
            this._gridRisk.ReadOnly = true;
            this._gridRisk.RowTemplate.Height = 30;
            this._gridRisk.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this._gridRisk.Size = new System.Drawing.Size(1230, 520);
            this._gridRisk.TabIndex = 0;


            this._lblRiskSummary.BackColor = System.Drawing.Color.WhiteSmoke;
            this._lblRiskSummary.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this._lblRiskSummary.Dock = System.Windows.Forms.DockStyle.Top;
            this._lblRiskSummary.Font = new System.Drawing.Font("Segoe UI", 11F);
            this._lblRiskSummary.Location = new System.Drawing.Point(8, 8);
            this._lblRiskSummary.Name = "_lblRiskSummary";
            this._lblRiskSummary.Padding = new System.Windows.Forms.Padding(12, 10, 12, 10);
            this._lblRiskSummary.Size = new System.Drawing.Size(1230, 170);
            this._lblRiskSummary.TabIndex = 1;
            this._lblRiskSummary.Text = "Select a run in Results to calculate the risk analysis.";


            this._tabCrashing.Controls.Add(this._splitCrashing);
            this._tabCrashing.Location = new System.Drawing.Point(4, 22);
            this._tabCrashing.Name = "_tabCrashing";
            this._tabCrashing.Padding = new System.Windows.Forms.Padding(3);
            this._tabCrashing.Size = new System.Drawing.Size(1252, 712);
            this._tabCrashing.TabIndex = 4;
            this._tabCrashing.Text = "CRASHING";
            this._tabCrashing.UseVisualStyleBackColor = true;


            this._splitCrashing.Dock = System.Windows.Forms.DockStyle.Fill;
            this._splitCrashing.Location = new System.Drawing.Point(3, 3);
            this._splitCrashing.Name = "_splitCrashing";


            this._splitCrashing.Panel1.Controls.Add(this.panelCrashingLeft);


            this._splitCrashing.Panel2.Controls.Add(this._groupCrashSummary);
            this._splitCrashing.Size = new System.Drawing.Size(1246, 706);
            this._splitCrashing.SplitterDistance = 415;
            this._splitCrashing.TabIndex = 0;


            this._groupCrashSummary.Controls.Add(this._gridCrashingHistory);
            this._groupCrashSummary.Controls.Add(this._lblCrashClassification);
            this._groupCrashSummary.Controls.Add(this._lblCrashSummary);
            this._groupCrashSummary.Dock = System.Windows.Forms.DockStyle.Fill;
            this._groupCrashSummary.Location = new System.Drawing.Point(0, 0);
            this._groupCrashSummary.Name = "_groupCrashSummary";
            this._groupCrashSummary.Padding = new System.Windows.Forms.Padding(8);
            this._groupCrashSummary.Size = new System.Drawing.Size(827, 706);
            this._groupCrashSummary.TabIndex = 2;
            this._groupCrashSummary.TabStop = false;
            this._groupCrashSummary.Text = "Comparative Summary";


            this._gridCrashingHistory.AllowUserToAddRows = false;
            this._gridCrashingHistory.AllowUserToDeleteRows = false;
            this._gridCrashingHistory.AllowUserToResizeRows = false;
            this._gridCrashingHistory.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.DisplayedCells;
            this._gridCrashingHistory.BackgroundColor = System.Drawing.Color.White;
            this._gridCrashingHistory.Dock = System.Windows.Forms.DockStyle.Fill;
            this._gridCrashingHistory.Location = new System.Drawing.Point(8, 109);
            this._gridCrashingHistory.MultiSelect = false;
            this._gridCrashingHistory.Name = "_gridCrashingHistory";
            this._gridCrashingHistory.ReadOnly = true;
            this._gridCrashingHistory.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this._gridCrashingHistory.Size = new System.Drawing.Size(811, 589);
            this._gridCrashingHistory.TabIndex = 4;


            this._lblCrashClassification.BackColor = System.Drawing.Color.White;
            this._lblCrashClassification.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this._lblCrashClassification.Dock = System.Windows.Forms.DockStyle.Top;
            this._lblCrashClassification.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this._lblCrashClassification.Location = new System.Drawing.Point(8, 79);
            this._lblCrashClassification.Name = "_lblCrashClassification";
            this._lblCrashClassification.Padding = new System.Windows.Forms.Padding(10, 6, 10, 6);
            this._lblCrashClassification.Size = new System.Drawing.Size(811, 30);
            this._lblCrashClassification.TabIndex = 2;
            this._lblCrashClassification.Text = "Classification: not evaluated";


            this._lblCrashSummary.BackColor = System.Drawing.Color.WhiteSmoke;
            this._lblCrashSummary.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this._lblCrashSummary.Dock = System.Windows.Forms.DockStyle.Top;
            this._lblCrashSummary.Font = new System.Drawing.Font("Segoe UI", 10F);
            this._lblCrashSummary.Location = new System.Drawing.Point(8, 21);
            this._lblCrashSummary.Name = "_lblCrashSummary";
            this._lblCrashSummary.Padding = new System.Windows.Forms.Padding(10, 8, 10, 8);
            this._lblCrashSummary.Size = new System.Drawing.Size(811, 58);
            this._lblCrashSummary.TabIndex = 3;
            this._lblCrashSummary.Text = "Select a run in Results. The crashing scenario will be prepared automatically.";


            this._tabComparison.Controls.Add(this._splitComparison);
            this._tabComparison.Location = new System.Drawing.Point(4, 22);
            this._tabComparison.Name = "_tabComparison";
            this._tabComparison.Padding = new System.Windows.Forms.Padding(3);
            this._tabComparison.Size = new System.Drawing.Size(1252, 712);
            this._tabComparison.TabIndex = 6;
            this._tabComparison.Text = "Comparison";
            this._tabComparison.UseVisualStyleBackColor = true;


            this._splitComparison.Dock = System.Windows.Forms.DockStyle.Fill;
            this._splitComparison.Location = new System.Drawing.Point(3, 3);
            this._splitComparison.Name = "_splitComparison";
            this._splitComparison.Size = new System.Drawing.Size(1246, 706);
            this._splitComparison.SplitterDistance = 415;
            this._splitComparison.TabIndex = 0;


            this._statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._lblStatusMain,
            this._toolStripSep1,
            this._lblStatusRun,
            this._toolStripSep2,
            this._lblStatusStep,
            this._toolStripSep3,
            this._progressStatus});
            this._statusStrip.Location = new System.Drawing.Point(0, 738);
            this._statusStrip.Name = "_statusStrip";
            this._statusStrip.Size = new System.Drawing.Size(1260, 22);
            this._statusStrip.SizingGrip = false;
            this._statusStrip.TabIndex = 1;


            this._lblStatusMain.Name = "_lblStatusMain";
            this._lblStatusMain.Size = new System.Drawing.Size(1129, 17);
            this._lblStatusMain.Spring = true;
            this._lblStatusMain.Text = "Ready";
            this._lblStatusMain.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;


            this._toolStripSep1.Name = "_toolStripSep1";
            this._toolStripSep1.Size = new System.Drawing.Size(10, 17);
            this._toolStripSep1.Text = "|";


            this._lblStatusRun.Name = "_lblStatusRun";
            this._lblStatusRun.Size = new System.Drawing.Size(39, 17);
            this._lblStatusRun.Text = "Run: -";


            this._toolStripSep2.Name = "_toolStripSep2";
            this._toolStripSep2.Size = new System.Drawing.Size(10, 17);
            this._toolStripSep2.Text = "|";


            this._lblStatusStep.Name = "_lblStatusStep";
            this._lblStatusStep.Size = new System.Drawing.Size(47, 17);
            this._lblStatusStep.Text = "Stage: -";


            this._toolStripSep3.Name = "_toolStripSep3";
            this._toolStripSep3.Size = new System.Drawing.Size(10, 17);
            this._toolStripSep3.Text = "|";


            this._progressStatus.Name = "_progressStatus";
            this._progressStatus.Size = new System.Drawing.Size(140, 16);
            this._progressStatus.Visible = false;


            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1260, 760);
            this.Controls.Add(this.tabControlMain);
            this.Controls.Add(this._statusStrip);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.MinimumSize = new System.Drawing.Size(980, 620);
            this.Name = "FormBaseline";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Baseline";
            this.panelCrashingLeft.ResumeLayout(false);
            this._groupCrashScenario.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._gridCrashingScenario)).EndInit();
            this.panelCrashToolbar.ResumeLayout(false);
            this.tabControlMain.ResumeLayout(false);
            this.tabPageBaseline.ResumeLayout(false);
            this.panelBody.ResumeLayout(false);
            this.splitContainerMain.Panel1.ResumeLayout(false);
            this.splitContainerMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).EndInit();
            this.splitContainerMain.ResumeLayout(false);
            this.groupBoxAtividades.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridAtividades)).EndInit();
            this.groupBoxRecursos.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridRecursos)).EndInit();
            this.panelHeader.ResumeLayout(false);
            this.panelHeader.PerformLayout();
            this.tabPageResults.ResumeLayout(false);
            this.panelResults.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridResultados)).EndInit();
            this._tabPageFrm.ResumeLayout(false);
            this._panelFrm.ResumeLayout(false);
            this._panelFrm.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._gridFrm)).EndInit();
            this._tabRisk.ResumeLayout(false);
            this._panelRisk.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._gridRisk)).EndInit();
            this._tabCrashing.ResumeLayout(false);
            this._splitCrashing.Panel1.ResumeLayout(false);
            this._splitCrashing.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._splitCrashing)).EndInit();
            this._splitCrashing.ResumeLayout(false);
            this._groupCrashSummary.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._gridCrashingHistory)).EndInit();
            this._tabComparison.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._splitComparison)).EndInit();
            this._splitComparison.ResumeLayout(false);
            this._statusStrip.ResumeLayout(false);
            this._statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private Panel panelCrashingLeft;
        private GroupBox _groupCrashScenario;
        private DataGridView _gridCrashingScenario;
        private Panel panelCrashToolbar;
        private Button _btnCrashClear;
        private Button _btnCrashRunAll;
        private GroupBox _groupCrashSummary;
        private DataGridView _gridCrashingHistory;
        private Label _lblCrashClassification;
        private Label _lblCrashSummary;
    }
}
