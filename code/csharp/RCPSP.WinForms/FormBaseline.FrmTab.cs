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
        private void PopulateFrm(ExecutionSummary summary)
        {
            var frm = summary != null && summary.Frm != null
                ? summary.Frm
                : new FrmResultDto();

            BuildFrmGridColumns(frm);
            FillFrmGrid(frm);
            _lblFrmSummary.Text = BuildFrmSummaryText(frm);
        }

        private void BuildFrmGridColumns(FrmResultDto frm)
        {
            _gridFrm.DataSource = null;
            _gridFrm.Rows.Clear();
            _gridFrm.Columns.Clear();

            _gridFrm.AutoGenerateColumns = false;
            _gridFrm.AllowUserToAddRows = false;
            _gridFrm.AllowUserToDeleteRows = false;
            _gridFrm.AllowUserToOrderColumns = false;
            _gridFrm.ReadOnly = true;
            _gridFrm.RowHeadersVisible = true;
            _gridFrm.RowHeadersWidth = 32;
            _gridFrm.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _gridFrm.MultiSelect = false;
            _gridFrm.EnableHeadersVisualStyles = false;
            _gridFrm.BorderStyle = BorderStyle.FixedSingle;
            _gridFrm.CellBorderStyle = DataGridViewCellBorderStyle.Single;
            _gridFrm.GridColor = Color.FromArgb(168, 168, 168);
            _gridFrm.BackgroundColor = Color.White;
            _gridFrm.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            _gridFrm.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            _gridFrm.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
            _gridFrm.ColumnHeadersHeight = 42;
            _gridFrm.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _gridFrm.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 120, 215);
            _gridFrm.DefaultCellStyle.SelectionForeColor = Color.White;
            _gridFrm.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _gridFrm.ColumnHeadersDefaultCellStyle.BackColor = Color.White;
            _gridFrm.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;
            _gridFrm.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            _gridFrm.RowHeadersDefaultCellStyle.BackColor = Color.White;
            _gridFrm.RowHeadersDefaultCellStyle.ForeColor = Color.Black;
            _gridFrm.RowHeadersDefaultCellStyle.SelectionBackColor = Color.White;
            _gridFrm.RowHeadersDefaultCellStyle.SelectionForeColor = Color.Black;
            _gridFrm.RowsDefaultCellStyle.BackColor = Color.White;
            _gridFrm.AlternatingRowsDefaultCellStyle.BackColor = Color.White;

            AddTextColumn(_gridFrm, "colActivity", "Activity", 95);
            AddTextColumn(_gridFrm, "colStart", "Si", 60);
            AddTextColumn(_gridFrm, "colDurationNominal", "DiNom", 70);
            AddTextColumn(_gridFrm, "colFinish", "Fi", 60);
            AddTextColumn(_gridFrm, "colSlackI", "SLACK", 70);
            AddTextColumn(_gridFrm, "colDMax", "DiMax", 70);
            AddTextColumn(_gridFrm, "colDMin", "DiMin", 70);
            AddTextColumn(_gridFrm, "colDSMax", "DiSMax", 70);
            AddTextColumn(_gridFrm, "colDSMin", "DiSMin", 70);
            AddTextColumn(_gridFrm, "colDNewBefore", "DiBefore", 75);
            AddTextColumn(_gridFrm, "colDNew", "DiNew", 70);
            AddTextColumn(_gridFrm, "colBalanceDecision", "Balance decision", 210);

            foreach (var resource in GetFrmResourceList(frm))
            {
                string scoreName = "score_" + resource.Id;
                string balanceName = "balance_" + resource.Id;

                AddTextColumn(_gridFrm, scoreName, "SCORE" + resource.Id, 75);
                AddTextColumn(_gridFrm, balanceName, "BALANCE" + resource.Id, 85);
            }

            if (_gridFrm.Columns.Contains("colActivity"))
                _gridFrm.Columns["colActivity"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        }

        private void FillFrmGrid(FrmResultDto frm)
        {
            _gridFrm.Rows.Clear();

            AddFrmSifRow(frm);
            AddFrmBalance0Row(frm);

            var activityMap = (frm.Activities ?? new List<FrmActivityResultDto>())
                .ToDictionary(a => a.ActivityId, a => a);

            var sequence = frm.Sequence ?? new List<int>();
            var orderedActivities = new List<FrmActivityResultDto>();

            if (sequence.Count > 0)
            {
                foreach (int activityId in sequence)
                {
                    FrmActivityResultDto activity;
                    if (activityMap.TryGetValue(activityId, out activity))
                        orderedActivities.Add(activity);
                }

                foreach (var activity in frm.Activities ?? new List<FrmActivityResultDto>())
                {
                    if (!sequence.Contains(activity.ActivityId))
                        orderedActivities.Add(activity);
                }
            }
            else
            {
                orderedActivities = (frm.Activities ?? new List<FrmActivityResultDto>())
                    .OrderBy(a => a.Start)
                    .ThenBy(a => a.ActivityId)
                    .ToList();
            }

            foreach (var a in orderedActivities)
            {
                int rowIndex = _gridFrm.Rows.Add();
                DataGridViewRow row = _gridFrm.Rows[rowIndex];

                row.Cells["colActivity"].Value = "Act " + a.ActivityId;
                row.Cells["colStart"].Value = a.Start;
                row.Cells["colDurationNominal"].Value = a.DurationNominal;
                row.Cells["colFinish"].Value = a.Finish;
                row.Cells["colSlackI"].Value = a.SlackI;
                row.Cells["colDMax"].Value = a.DMax;
                row.Cells["colDMin"].Value = a.DMin;
                row.Cells["colDSMax"].Value = a.DSMax;
                row.Cells["colDSMin"].Value = a.DSMin;
                row.Cells["colDNewBefore"].Value = a.StructuralDurationBeforeBalance;
                row.Cells["colDNew"].Value = a.DNew;
                row.Cells["colBalanceDecision"].Value = a.DurationDecision;

                bool hasPositiveScore = false;
                bool hasNegativeScore = false;

                foreach (var resource in GetFrmResourceList(frm))
                {
                    int score = 0;
                    int balance = 0;

                    int rawScore = 0;
                    int appliedScore = 0;
                    if (a.ScoreBrutoByResourceId != null && a.ScoreBrutoByResourceId.ContainsKey(resource.Id))
                        rawScore = a.ScoreBrutoByResourceId[resource.Id];
                    if (a.ScoreIkByResourceId != null && a.ScoreIkByResourceId.ContainsKey(resource.Id))
                        appliedScore = a.ScoreIkByResourceId[resource.Id];


                    score = rawScore > 0 ? rawScore : appliedScore;

                    if (a.BalanceByResourceId != null && a.BalanceByResourceId.ContainsKey(resource.Id))
                        balance = a.BalanceByResourceId[resource.Id];

                    string scoreColumn = "score_" + resource.Id;
                    string balanceColumn = "balance_" + resource.Id;

                    if (_gridFrm.Columns.Contains(scoreColumn))
                        row.Cells[scoreColumn].Value = score;

                    if (_gridFrm.Columns.Contains(balanceColumn))
                        row.Cells[balanceColumn].Value = balance;

                    int requested = a.BalanceRequestedByResourceId != null && a.BalanceRequestedByResourceId.ContainsKey(resource.Id)
                        ? a.BalanceRequestedByResourceId[resource.Id]
                        : 0;
                    if (requested > 0)
                        hasNegativeScore = true;
                    else if (score > 0)
                        hasPositiveScore = true;
                }

                ApplyFrmRowStyle(row, hasPositiveScore, hasNegativeScore);
            }

            AddFrmSummaryRow(frm);
        }

        private void AddFrmBalance0Row(FrmResultDto frm)
        {
            if (frm == null)
                return;

            int rowIndex = _gridFrm.Rows.Add();
            DataGridViewRow row = _gridFrm.Rows[rowIndex];

            row.Cells["colActivity"].Value = "Balance 0";
            row.Cells["colStart"].Value = string.Empty;
            row.Cells["colDurationNominal"].Value = string.Empty;
            row.Cells["colFinish"].Value = string.Empty;
            row.Cells["colSlackI"].Value = string.Empty;
            row.Cells["colDMax"].Value = string.Empty;
            row.Cells["colDMin"].Value = string.Empty;
            row.Cells["colDSMax"].Value = string.Empty;
            row.Cells["colDSMin"].Value = string.Empty;
            row.Cells["colDNewBefore"].Value = string.Empty;
            row.Cells["colDNew"].Value = string.Empty;
            row.Cells["colBalanceDecision"].Value = string.Empty;

            foreach (var resource in GetFrmResourceList(frm))
            {
                string scoreColumn = "score_" + resource.Id;
                string balanceColumn = "balance_" + resource.Id;

                int balance0 = 0;
                if (frm.Balance0ByResourceId != null && frm.Balance0ByResourceId.ContainsKey(resource.Id))
                    balance0 = frm.Balance0ByResourceId[resource.Id];

                if (_gridFrm.Columns.Contains(scoreColumn))
                    row.Cells[scoreColumn].Value = string.Empty;

                if (_gridFrm.Columns.Contains(balanceColumn))
                    row.Cells[balanceColumn].Value = balance0;
            }

            row.DefaultCellStyle.BackColor = Color.White;
            row.DefaultCellStyle.ForeColor = Color.Black;
        }

        private void AddFrmSifRow(FrmResultDto frm)
        {
            if (frm == null)
                return;

            int rowIndex = _gridFrm.Rows.Add();
            DataGridViewRow row = _gridFrm.Rows[rowIndex];

            row.Cells["colActivity"].Value = "SIF";
            row.Cells["colStart"].Value = string.Empty;
            row.Cells["colDurationNominal"].Value = string.Empty;
            row.Cells["colFinish"].Value = string.Empty;
            row.Cells["colSlackI"].Value = frm.SifGlobal.ToString("0.###");
            row.Cells["colDMax"].Value = string.Empty;
            row.Cells["colDMin"].Value = string.Empty;
            row.Cells["colDSMax"].Value = string.Empty;
            row.Cells["colDSMin"].Value = string.Empty;
            row.Cells["colDNewBefore"].Value = string.Empty;
            row.Cells["colDNew"].Value = string.Empty;
            row.Cells["colBalanceDecision"].Value = string.Empty;

            foreach (var resource in GetFrmResourceList(frm))
            {
                string scoreColumn = "score_" + resource.Id;
                string balanceColumn = "balance_" + resource.Id;

                double sif = frm.SifByResourceId != null && frm.SifByResourceId.ContainsKey(resource.Id)
                    ? frm.SifByResourceId[resource.Id]
                    : 0.0;

                if (_gridFrm.Columns.Contains(scoreColumn))
                    row.Cells[scoreColumn].Value = string.Empty;

                if (_gridFrm.Columns.Contains(balanceColumn))
                    row.Cells[balanceColumn].Value = sif.ToString("0.###");
            }

            row.DefaultCellStyle.BackColor = Color.White;
            row.DefaultCellStyle.ForeColor = Color.Black;
        }

        private void ApplyFrmRowStyle(
    DataGridViewRow row,
    bool hasPositiveScore,
    bool hasNegativeScore)
        {
            Color neutralColor = Color.White;
            Color warningColor = Color.FromArgb(242, 220, 194);
            Color positiveColor = Color.FromArgb(193, 240, 200);

            Color rowColor = neutralColor;


            if (hasNegativeScore)
                rowColor = warningColor;
            else if (hasPositiveScore)
                rowColor = positiveColor;

            foreach (DataGridViewCell cell in row.Cells)
            {
                cell.Style.BackColor = rowColor;
                cell.Style.ForeColor = Color.Black;
            }


            if (_gridFrm.Columns.Contains("colActivity"))
            {
                row.Cells["colActivity"].Style.BackColor = Color.White;
                row.Cells["colActivity"].Style.ForeColor = Color.Black;
            }
        }

        private void AddFrmSummaryRow(FrmResultDto frm)
        {
            if (frm == null)
                return;

            int rowIndex = _gridFrm.Rows.Add();
            DataGridViewRow row = _gridFrm.Rows[rowIndex];

            row.Cells["colActivity"].Value = string.Empty;
            row.Cells["colStart"].Value = string.Empty;
            row.Cells["colDurationNominal"].Value = string.Empty;
            row.Cells["colFinish"].Value = string.Empty;
            row.Cells["colSlackI"].Value = string.Empty;
            row.Cells["colDMax"].Value = string.Empty;
            row.Cells["colDMin"].Value = string.Empty;
            row.Cells["colDSMax"].Value = string.Empty;
            row.Cells["colDSMin"].Value = string.Empty;
            row.Cells["colDNewBefore"].Value = string.Empty;
            row.Cells["colDNew"].Value = string.Empty;
            row.Cells["colBalanceDecision"].Value = string.Empty;

            foreach (var resource in GetFrmResourceList(frm))
            {
                string scoreColumn = "score_" + resource.Id;
                string balanceColumn = "balance_" + resource.Id;

                int scoreSum = 0;
                if (frm.Activities != null)
                {
                    scoreSum = frm.Activities.Sum(a =>
                    {
                        int raw = a.ScoreBrutoByResourceId != null && a.ScoreBrutoByResourceId.ContainsKey(resource.Id)
                            ? a.ScoreBrutoByResourceId[resource.Id]
                            : 0;
                        int applied = a.ScoreIkByResourceId != null && a.ScoreIkByResourceId.ContainsKey(resource.Id)
                            ? a.ScoreIkByResourceId[resource.Id]
                            : 0;
                        return raw > 0 ? raw : applied;
                    });
                }

                int finalBalance = 0;
                if (frm.Activities != null && frm.Activities.Count > 0)
                {
                    var last = frm.Activities
                        .OrderBy(a => a.Start)
                        .ThenBy(a => a.ActivityId)
                        .LastOrDefault();

                    if (last != null &&
                        last.BalanceByResourceId != null &&
                        last.BalanceByResourceId.ContainsKey(resource.Id))
                    {
                        finalBalance = last.BalanceByResourceId[resource.Id];
                    }
                }

                if (_gridFrm.Columns.Contains(scoreColumn))
                    row.Cells[scoreColumn].Value = "∑ Score_" + resource.Id;

                if (_gridFrm.Columns.Contains(balanceColumn))
                    row.Cells[balanceColumn].Value = finalBalance;
            }

            row.DefaultCellStyle.BackColor = Color.White;
            row.DefaultCellStyle.ForeColor = Color.Black;
        }

        private List<ResourceDto> GetFrmResourceList(FrmResultDto frm)
        {
            var resources = (_request != null && _request.Project != null && _request.Project.Resources != null)
                ? _request.Project.Resources.OrderBy(r => r.Id).ToList()
                : new List<ResourceDto>();

            if (resources.Count > 0)
                return resources;

            var ids = new HashSet<int>();

            foreach (var kv in frm.Balance0ByResourceId ?? new Dictionary<int, int>())
                ids.Add(kv.Key);

            foreach (var activity in frm.Activities ?? new List<FrmActivityResultDto>())
            {
                foreach (var kv in activity.ScoreBrutoByResourceId ?? new Dictionary<int, int>())
                    ids.Add(kv.Key);

                foreach (var kv in activity.BalanceByResourceId ?? new Dictionary<int, int>())
                    ids.Add(kv.Key);
            }

            foreach (var diagnostic in frm.ResourceDiagnostics ?? new List<FrmResourceDiagnosticDto>())
                ids.Add(diagnostic.ResourceId);

            return ids
                .OrderBy(id => id)
                .Select(id => new ResourceDto
                {
                    Id = id,
                    Name = "R" + id,
                    Capacity = 0
                })
                .ToList();
        }

        private string BuildFrmSummaryText(FrmResultDto frm)
        {
            var lines = new List<string>();

            lines.Add(
                "Run: " +
                Safe(frm.Heuristic) + " | " +
                Safe(frm.Scheme) + " | " +
                Safe(frm.Direction));

            lines.Add(
                "Makespan: " + frm.Makespan +
                " | Flex+: " + frm.FlexPositivePercent.ToString("0.##") + "%" +
                " | Flex-: " + frm.FlexNegativePercent.ToString("0.##") + "%");

            lines.Add(
                "Structural robustness: " +
                (frm.IsStructurallyRobust ? "ROBUST" : "NOT ROBUST"));

            lines.Add("SIF global: " + frm.SifGlobal.ToString("0.###"));

            var diagnostics = frm.ResourceDiagnostics ?? new List<FrmResourceDiagnosticDto>();
            if (diagnostics.Count > 0)
            {
                lines.Add("Resources:");

                foreach (var d in diagnostics.OrderBy(x => x.ResourceId))
                {
                    lines.Add(
                        "R" + d.ResourceId +
                        " [" + Safe(d.ResourceName) + "]" +
                        " | SIF=" + d.Sif.ToString("0.###") +
                        " | B0=" + d.Balance0 +
                        " | Final=" + d.BalanceFinal +
                        " | IR=" + d.RobustnessIndex.ToString("0.###") +
                        " | " + Safe(d.Classification));
                }
            }

            if (!string.IsNullOrWhiteSpace(frm.SummaryText))
            {
                lines.Add("");
                lines.Add(Safe(frm.SummaryText));
            }

            return string.Join(Environment.NewLine, lines);
        }


    }
}
