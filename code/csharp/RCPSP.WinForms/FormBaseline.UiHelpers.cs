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
        private static DataGridView CreateReadOnlyGrid()
        {
            var grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.AllowUserToResizeColumns = true;
            grid.ReadOnly = true;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.RowHeadersVisible = false;
            grid.BackgroundColor = Color.White;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            grid.ScrollBars = ScrollBars.Both;
            return grid;
        }


        private void ApplyUserGridPreferencesToAllGrids()
        {
            ApplyUserGridPreferencesRecursive(this);
        }

        private void ApplyUserGridPreferencesRecursive(Control parent)
        {
            if (parent == null)
                return;

            var grid = parent as DataGridView;
            if (grid != null)
            {
                ApplyUserGridPreferences(grid);
                return;
            }

            foreach (Control child in parent.Controls)
                ApplyUserGridPreferencesRecursive(child);
        }

        private static void ApplyUserGridPreferences(DataGridView grid)
        {
            if (grid == null)
                return;

            grid.AllowUserToResizeColumns = true;
            grid.ScrollBars = ScrollBars.Both;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (column == null)
                    continue;

                if (column.AutoSizeMode != DataGridViewAutoSizeColumnMode.None)
                    column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;

                if (column.Width < 60)
                    column.Width = 60;
            }
        }

        private void UpdateCrashFilterButtons()
        {
            int total = _allCrashScenarios != null ? _allCrashScenarios.Count : 0;
            int robust = _allCrashScenarios != null ? _allCrashScenarios.Count(x => string.Equals(x.StructuralStatus, "ROBUST", StringComparison.OrdinalIgnoreCase)) : 0;
            int feasible = _allCrashScenarios != null ? _allCrashScenarios.Count(x => string.Equals(x.StructuralStatus, "FEASIBLE", StringComparison.OrdinalIgnoreCase)) : 0;
            int fragile = _allCrashScenarios != null ? _allCrashScenarios.Count(x => string.Equals(x.StructuralStatus, "FRAGILE", StringComparison.OrdinalIgnoreCase)) : 0;
            int inviable = _allCrashScenarios != null ? _allCrashScenarios.Count(x => string.Equals(x.StructuralStatus, "INVIABLE", StringComparison.OrdinalIgnoreCase)) : 0;

            if (_btnCrashFilterAll != null) _btnCrashFilterAll.Text = "All\n" + total;
            if (_btnCrashFilterRobust != null) _btnCrashFilterRobust.Text = "Robust\n" + robust;
            if (_btnCrashFilterFeasible != null) _btnCrashFilterFeasible.Text = "Feasible\n" + feasible;
            if (_btnCrashFilterFragile != null) _btnCrashFilterFragile.Text = "Fragile\n" + fragile;
            if (_btnCrashFilterInviable != null) _btnCrashFilterInviable.Text = "Inviable\n" + inviable;

            UpdateCrashFilterButton(_btnCrashFilterAll, "ALL");
            UpdateCrashFilterButton(_btnCrashFilterRobust, "ROBUST");
            UpdateCrashFilterButton(_btnCrashFilterFeasible, "FEASIBLE");
            UpdateCrashFilterButton(_btnCrashFilterFragile, "FRAGILE");
            UpdateCrashFilterButton(_btnCrashFilterInviable, "INVIABLE");
        }

        private void UpdateCrashFilterButton(Button button, string status)
        {
            if (button == null)
                return;

            bool active = string.Equals(_crashFilterStatus, status, StringComparison.OrdinalIgnoreCase);
            button.FlatAppearance.BorderSize = active ? 2 : 1;
            button.Font = new Font(button.Font, active ? FontStyle.Bold : FontStyle.Regular);
        }

        private static Color GetStructuralStatusColor(string status)
        {
            switch ((status ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "ROBUST": return Color.FromArgb(210, 242, 210);
                case "FEASIBLE": return Color.FromArgb(255, 243, 176);
                case "FRAGILE": return Color.FromArgb(255, 214, 153);
                default: return Color.FromArgb(255, 199, 206);
            }
        }

        private static string ClassifyStructuralStatus(bool isStructurallyRobust, double sif, double robustness)
        {
            if (isStructurallyRobust && sif > 0.0 && robustness >= 0.20)
                return "ROBUST";
            if (isStructurallyRobust)
                return "FEASIBLE";
            if (sif > 0.0 || robustness > 0.05)
                return "FRAGILE";
            return "INVIABLE";
        }

        private static void AddTextColumn(DataGridView grid, string name, string headerText, int width)
        {
            var column = new DataGridViewTextBoxColumn();
            column.Name = name;
            column.HeaderText = headerText;
            column.Width = width;
            column.ReadOnly = true;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.Columns.Add(column);
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        private static string JoinInts(IEnumerable<int> values)
        {
            if (values == null)
                return string.Empty;

            return string.Join(", ", values);
        }

        private static string JoinAssignments(IEnumerable<ResourceAssignmentDto> assignments)
        {
            if (assignments == null)
                return string.Empty;

            return string.Join(
                "; ",
                assignments.Select(a => string.Format("{0} ({1})", Safe(a.ResourceName), a.Units.ToString("0.##"))));
        }


        private void RunOnUiThread(Action action)
        {
            if (action == null)
                return;

            if (IsDisposed || Disposing)
                return;

            if (InvokeRequired)
            {
                Invoke(action);
                return;
            }

            action();
        }

        private Task RunOnUiThreadAsync(Action action)
        {
            if (action == null)
                return Task.CompletedTask;

            if (IsDisposed || Disposing)
                return Task.CompletedTask;

            if (InvokeRequired)
            {
                var tcs = new TaskCompletionSource<bool>();

                BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (!IsDisposed && !Disposing)
                            action();

                        tcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }));

                return tcs.Task;
            }

            action();
            return Task.CompletedTask;
        }


    }
}
