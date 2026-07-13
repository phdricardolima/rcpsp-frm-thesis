using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace RCPSP.WinForms
{
    public partial class FormBaseline : Form
    {
        private void InitializeExportContextMenus()
        {
            RegisterExportContextMenusRecursive(this);
        }

        private void RegisterExportContextMenusRecursive(Control parent)
        {
            if (parent == null)
                return;

            var grid = parent as DataGridView;
            if (grid != null)
                AttachGridExportContextMenu(grid);

            var chart = parent as Chart;
            if (chart != null)
                AttachChartExportContextMenu(chart);

            foreach (Control child in parent.Controls)
                RegisterExportContextMenusRecursive(child);
        }

        private void AttachGridExportContextMenu(DataGridView grid)
        {
            if (grid == null)
                return;

            const string itemName = "miSaveGridAsXlsx";
            var menu = grid.ContextMenuStrip;
            if (menu == null)
            {
                menu = new ContextMenuStrip();
                grid.ContextMenuStrip = menu;
            }

            foreach (ToolStripItem item in menu.Items)
            {
                if (string.Equals(item.Name, itemName, StringComparison.Ordinal))
                    return;
            }

            if (menu.Items.Count > 0)
                menu.Items.Add(new ToolStripSeparator());

            var exportItem = new ToolStripMenuItem("Save grid as XLSX...");
            exportItem.Name = itemName;
            exportItem.Tag = grid;
            exportItem.Click += SaveGridAsXlsx_Click;
            menu.Items.Add(exportItem);
        }

        private void AttachChartExportContextMenu(Chart chart)
        {
            if (chart == null)
                return;

            const string itemName = "miSaveChartAsPngAndXlsx";
            var menu = chart.ContextMenuStrip;
            if (menu == null)
            {
                menu = new ContextMenuStrip();
                chart.ContextMenuStrip = menu;
            }

            foreach (ToolStripItem item in menu.Items)
            {
                if (string.Equals(item.Name, itemName, StringComparison.Ordinal))
                    return;
            }

            if (menu.Items.Count > 0)
                menu.Items.Add(new ToolStripSeparator());

            var exportItem = new ToolStripMenuItem("Save chart as PNG + XLSX...");
            exportItem.Name = itemName;
            exportItem.Tag = chart;
            exportItem.Click += SaveChartAsPngAndXlsx_Click;
            menu.Items.Add(exportItem);
        }

        private void SaveGridAsXlsx_Click(object sender, EventArgs e)
        {
            var item = sender as ToolStripItem;
            var grid = item != null ? item.Tag as DataGridView : null;
            if (grid == null)
                return;

            try
            {
                string path = PromptForXlsxPath(BuildSuggestedFileName(grid, "grid"));
                if (string.IsNullOrWhiteSpace(path))
                    return;

                var sheet = XlsxExportHelper.BuildSheetFromGrid(GetControlLabel(grid), grid);
                XlsxExportHelper.SaveWorkbook(path, new List<XlsxExportHelper.SheetData> { sheet });
                MessageBox.Show(this, "XLSX file saved successfully.", "RCPSP-FRM", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not save the grid as XLSX.\r\n\r\n" + ex.Message, "RCPSP-FRM", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveChartAsPngAndXlsx_Click(object sender, EventArgs e)
        {
            var item = sender as ToolStripItem;
            var chart = item != null ? item.Tag as Chart : null;
            if (chart == null)
                return;

            try
            {
                string xlsxPath = PromptForXlsxPath(BuildSuggestedFileName(chart, "grafico"));
                if (string.IsNullOrWhiteSpace(xlsxPath))
                    return;

                string pngPath = Path.ChangeExtension(xlsxPath, ".png");

                SaveChartImageAsPng(chart, pngPath);

                var sheets = XlsxExportHelper.BuildSheetsFromChart(GetControlLabel(chart), chart);
                XlsxExportHelper.SaveWorkbook(xlsxPath, sheets);

                MessageBox.Show(this,
                    "Export completed successfully.\r\n\r\n" +
                    "Chart image (PNG):\r\n" + pngPath + "\r\n\r\n" +
                    "Chart data (XLSX):\r\n" + xlsxPath,
                    "RCPSP-FRM",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Could not save the chart as PNG + XLSX.\r\n\r\n" + ex.Message,
                    "RCPSP-FRM",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static void SaveChartImageAsPng(Chart chart, string pngPath)
        {
            if (chart == null)
                throw new ArgumentNullException("chart");
            if (string.IsNullOrWhiteSpace(pngPath))
                throw new ArgumentException("Invalid PNG path.", "pngPath");

            string directory = Path.GetDirectoryName(pngPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            if (File.Exists(pngPath))
                File.Delete(pngPath);

            chart.Update();
            chart.Invalidate();
            chart.SaveImage(pngPath, ChartImageFormat.Png);
        }

        private string PromptForXlsxPath(string suggestedName)
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "Save as XLSX";
                dialog.Filter = "Excel Workbook (*.xlsx)|*.xlsx";
                dialog.DefaultExt = "xlsx";
                dialog.AddExtension = true;
                dialog.FileName = suggestedName;
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
            }
        }

        private string BuildSuggestedFileName(Control control, string fallback)
        {
            if (control == _gridFrm)
                return BuildFrmSuggestedFileName();

            string label = GetControlLabel(control);
            if (string.IsNullOrWhiteSpace(label))
                label = fallback;

            return MakeSafeFileName(label) + ".xlsx";
        }

        private string BuildFrmSuggestedFileName()
        {
            string runType = GetSelectedResultsRunType();
            if (string.IsNullOrWhiteSpace(runType))
                runType = "Selected";

            return MakeSafeFileName("FRM-" + runType) + ".xlsx";
        }

        private string GetSelectedResultsRunType()
        {
            if (gridResultados == null)
                return string.Empty;

            int rowIndex = GetCurrentResultsGridRowIndex();
            if (rowIndex < 0 || rowIndex >= gridResultados.Rows.Count)
                return string.Empty;

            var row = gridResultados.Rows[rowIndex];
            if (row == null)
                return string.Empty;

            var view = row.DataBoundItem as ResultsRunView;
            if (view != null)
                return Safe(view.RunType);

            if (gridResultados.Columns.Contains("RunType"))
            {
                object value = row.Cells["RunType"].Value;
                return value != null ? Safe(value.ToString()) : string.Empty;
            }

            if (gridResultados.Columns.Contains("run_type"))
            {
                object value = row.Cells["run_type"].Value;
                return value != null ? Safe(value.ToString()) : string.Empty;
            }

            return string.Empty;
        }

        private static string GetControlLabel(Control control)
        {
            if (control == null)
                return "Export";

            string text = control.Text;
            if (!string.IsNullOrWhiteSpace(text))
                return text;

            Control current = control.Parent;
            while (current != null)
            {
                if (!string.IsNullOrWhiteSpace(current.Text))
                    return current.Text;
                current = current.Parent;
            }

            return string.IsNullOrWhiteSpace(control.Name) ? "Export" : control.Name;
        }

        private static string MakeSafeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Export";

            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            foreach (char ch in value)
            {
                if (invalid.Contains(ch))
                    builder.Append('_');
                else
                    builder.Append(ch);
            }

            string result = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(result) ? "Export" : result;
        }
    }

    internal static class XlsxExportHelper
    {
        internal sealed class SheetData
        {
            public string Name;
            public List<object[]> Rows = new List<object[]>();
        }

        public static SheetData BuildSheetFromGrid(string sheetName, DataGridView grid)
        {
            var sheet = new SheetData();
            sheet.Name = sheetName;

            var visibleColumns = new List<DataGridViewColumn>();
            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (column != null && column.Visible)
                    visibleColumns.Add(column);
            }

            var header = new object[visibleColumns.Count];
            for (int i = 0; i < visibleColumns.Count; i++)
                header[i] = visibleColumns[i].HeaderText;
            sheet.Rows.Add(header);

            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row == null || row.IsNewRow)
                    continue;

                var values = new object[visibleColumns.Count];
                for (int i = 0; i < visibleColumns.Count; i++)
                {
                    var cell = row.Cells[visibleColumns[i].Index];
                    values[i] = cell != null ? cell.FormattedValue : null;
                }
                sheet.Rows.Add(values);
            }

            return sheet;
        }

        public static List<SheetData> BuildSheetsFromChart(string baseName, Chart chart)
        {
            var sheets = new List<SheetData>();

            var meta = new SheetData();
            meta.Name = baseName + "_Info";
            meta.Rows.Add(new object[] { "Chart", baseName });
            meta.Rows.Add(new object[] { "SeriesCount", chart.Series.Count });
            if (chart.Titles != null && chart.Titles.Count > 0)
                meta.Rows.Add(new object[] { "Title", chart.Titles[0].Text });
            sheets.Add(meta);

            int fallbackSeriesIndex = 1;
            foreach (Series series in chart.Series)
            {
                if (series == null)
                    continue;

                var sheet = new SheetData();
                sheet.Name = string.IsNullOrWhiteSpace(series.Name) ? "Series_" + fallbackSeriesIndex.ToString(CultureInfo.InvariantCulture) : series.Name;

                int yCount = 0;
                foreach (DataPoint point in series.Points)
                {
                    if (point != null && point.YValues != null)
                        yCount = Math.Max(yCount, point.YValues.Length);
                }
                yCount = Math.Max(1, yCount);

                var header = new List<object>();
                header.Add("PointIndex");
                header.Add("AxisLabel");
                header.Add("XValue");
                for (int i = 0; i < yCount; i++)
                    header.Add("YValue" + (i + 1).ToString(CultureInfo.InvariantCulture));
                header.Add("Label");
                header.Add("LegendText");
                header.Add("ToolTip");
                sheet.Rows.Add(header.ToArray());

                int pointIndex = 1;
                foreach (DataPoint point in series.Points)
                {
                    if (point == null)
                        continue;

                    var row = new List<object>();
                    row.Add(pointIndex);
                    row.Add(point.AxisLabel);
                    row.Add(point.XValue);
                    for (int i = 0; i < yCount; i++)
                    {
                        double value = point.YValues != null && i < point.YValues.Length ? point.YValues[i] : 0d;
                        row.Add(value);
                    }
                    row.Add(point.Label);
                    row.Add(point.LegendText);
                    row.Add(point.ToolTip);
                    sheet.Rows.Add(row.ToArray());
                    pointIndex++;
                }

                sheets.Add(sheet);
                fallbackSeriesIndex++;
            }

            return sheets;
        }

        public static void SaveWorkbook(string path, List<SheetData> sheets)
        {
            if (sheets == null || sheets.Count == 0)
                throw new InvalidOperationException("There is no data to export.");

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            if (File.Exists(path))
                File.Delete(path);

            var normalizedSheets = NormalizeSheets(sheets);

            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteEntry(archive, "[Content_Types].xml", BuildContentTypesXml(normalizedSheets.Count));
                WriteEntry(archive, "_rels/.rels", BuildRootRelsXml());
                WriteEntry(archive, "docProps/core.xml", BuildCoreXml());
                WriteEntry(archive, "docProps/app.xml", BuildAppXml(normalizedSheets));
                WriteEntry(archive, "xl/workbook.xml", BuildWorkbookXml(normalizedSheets));
                WriteEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelsXml(normalizedSheets.Count));
                WriteEntry(archive, "xl/styles.xml", BuildStylesXml());

                for (int i = 0; i < normalizedSheets.Count; i++)
                {
                    WriteEntry(archive, string.Format(CultureInfo.InvariantCulture, "xl/worksheets/sheet{0}.xml", i + 1), BuildWorksheetXml(normalizedSheets[i]));
                }
            }
        }

        private static List<SheetData> NormalizeSheets(List<SheetData> sheets)
        {
            var result = new List<SheetData>(sheets.Count);
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var sheet in sheets)
            {
                if (sheet == null)
                    continue;

                var normalized = new SheetData();
                normalized.Rows = sheet.Rows ?? new List<object[]>();
                normalized.Name = MakeUniqueSheetName(SanitizeSheetName(sheet.Name), usedNames);
                result.Add(normalized);
            }

            if (result.Count == 0)
                throw new InvalidOperationException("There are no worksheets to export.");

            return result;
        }

        private static string SanitizeSheetName(string name)
        {
            string value = string.IsNullOrWhiteSpace(name) ? "Sheet" : name.Trim();
            char[] invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
            var builder = new StringBuilder(value.Length);
            foreach (char ch in value)
            {
                builder.Append(invalid.Contains(ch) ? '_' : ch);
            }
            string result = builder.ToString();
            if (result.Length > 31)
                result = result.Substring(0, 31);
            if (string.IsNullOrWhiteSpace(result))
                result = "Sheet";
            return result;
        }

        private static string MakeUniqueSheetName(string baseName, HashSet<string> usedNames)
        {
            string candidate = baseName;
            int counter = 2;
            while (usedNames.Contains(candidate))
            {
                string suffix = "_" + counter.ToString(CultureInfo.InvariantCulture);
                int maxBaseLength = 31 - suffix.Length;
                string trimmed = baseName.Length > maxBaseLength ? baseName.Substring(0, maxBaseLength) : baseName;
                candidate = trimmed + suffix;
                counter++;
            }
            usedNames.Add(candidate);
            return candidate;
        }

        private static void WriteEntry(ZipArchive archive, string path, string content)
        {
            var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
            using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
                writer.Write(content);
        }

        private static string BuildContentTypesXml(int sheetCount)
        {
            var builder = new StringBuilder();
            builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            builder.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
            builder.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
            builder.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
            builder.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
            builder.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
            builder.Append("<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>");
            builder.Append("<Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/>");
            for (int i = 1; i <= sheetCount; i++)
                builder.AppendFormat(CultureInfo.InvariantCulture, "<Override PartName=\"/xl/worksheets/sheet{0}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>", i);
            builder.Append("</Types>");
            return builder.ToString();
        }

        private static string BuildRootRelsXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                   "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                   "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                   "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/>" +
                   "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties\" Target=\"docProps/app.xml\"/>" +
                   "</Relationships>";
        }

        private static string BuildCoreXml()
        {
            string now = DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture) + "Z";
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                   "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:dcmitype=\"http://purl.org/dc/dcmitype/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
                   "<dc:creator>RCPSP-FRM</dc:creator><cp:lastModifiedBy>RCPSP-FRM</cp:lastModifiedBy>" +
                   "<dcterms:created xsi:type=\"dcterms:W3CDTF\">" + now + "</dcterms:created>" +
                   "<dcterms:modified xsi:type=\"dcterms:W3CDTF\">" + now + "</dcterms:modified>" +
                   "</cp:coreProperties>";
        }

        private static string BuildAppXml(List<SheetData> sheets)
        {
            var builder = new StringBuilder();
            builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            builder.Append("<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\" xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\">");
            builder.Append("<Application>RCPSP-FRM</Application><DocSecurity>0</DocSecurity><ScaleCrop>false</ScaleCrop><HeadingPairs><vt:vector size=\"2\" baseType=\"variant\"><vt:variant><vt:lpstr>Worksheets</vt:lpstr></vt:variant><vt:variant><vt:i4>");
            builder.Append(sheets.Count.ToString(CultureInfo.InvariantCulture));
            builder.Append("</vt:i4></vt:variant></vt:vector></HeadingPairs><TitlesOfParts><vt:vector size=\"");
            builder.Append(sheets.Count.ToString(CultureInfo.InvariantCulture));
            builder.Append("\" baseType=\"lpstr\">");
            foreach (var sheet in sheets)
                builder.Append("<vt:lpstr>" + XmlEscape(sheet.Name) + "</vt:lpstr>");
            builder.Append("</vt:vector></TitlesOfParts></Properties>");
            return builder.ToString();
        }

        private static string BuildWorkbookXml(List<SheetData> sheets)
        {
            var builder = new StringBuilder();
            builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            builder.Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");
            for (int i = 0; i < sheets.Count; i++)
            {
                builder.AppendFormat(CultureInfo.InvariantCulture, "<sheet name=\"{0}\" sheetId=\"{1}\" r:id=\"rId{1}\"/>", XmlEscape(sheets[i].Name), i + 1);
            }
            builder.Append("</sheets></workbook>");
            return builder.ToString();
        }

        private static string BuildWorkbookRelsXml(int sheetCount)
        {
            var builder = new StringBuilder();
            builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            builder.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
            for (int i = 1; i <= sheetCount; i++)
            {
                builder.AppendFormat(CultureInfo.InvariantCulture, "<Relationship Id=\"rId{0}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{0}.xml\"/>", i);
            }
            builder.AppendFormat(CultureInfo.InvariantCulture, "<Relationship Id=\"rId{0}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>", sheetCount + 1);
            builder.Append("</Relationships>");
            return builder.ToString();
        }

        private static string BuildStylesXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                   "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                   "<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
                   "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>" +
                   "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
                   "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                   "<cellXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/></cellXfs>" +
                   "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
                   "</styleSheet>";
        }

        private static string BuildWorksheetXml(SheetData sheet)
        {
            var builder = new StringBuilder();
            builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            builder.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");

            int rowIndex = 1;
            foreach (var row in sheet.Rows)
            {
                builder.AppendFormat(CultureInfo.InvariantCulture, "<row r=\"{0}\">", rowIndex);
                if (row != null)
                {
                    for (int colIndex = 0; colIndex < row.Length; colIndex++)
                    {
                        string cellRef = GetCellReference(colIndex + 1, rowIndex);
                        builder.Append(BuildCellXml(cellRef, row[colIndex]));
                    }
                }
                builder.Append("</row>");
                rowIndex++;
            }

            builder.Append("</sheetData></worksheet>");
            return builder.ToString();
        }

        private static string BuildCellXml(string cellReference, object value)
        {
            if (value == null)
                return string.Format(CultureInfo.InvariantCulture, "<c r=\"{0}\" />", cellReference);

            var boolValue = value as bool?;
            if (boolValue.HasValue)
            {
                return string.Format(CultureInfo.InvariantCulture, "<c r=\"{0}\" t=\"b\"><v>{1}</v></c>", cellReference, boolValue.Value ? 1 : 0);
            }

            if (IsNumeric(value))
            {
                string number = Convert.ToString(value, CultureInfo.InvariantCulture);
                return string.Format(CultureInfo.InvariantCulture, "<c r=\"{0}\"><v>{1}</v></c>", cellReference, number);
            }

            string text = Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;
            return string.Format(CultureInfo.InvariantCulture, "<c r=\"{0}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{1}</t></is></c>", cellReference, XmlEscape(text));
        }

        private static bool IsNumeric(object value)
        {
            return value is byte || value is sbyte || value is short || value is ushort || value is int || value is uint || value is long || value is ulong || value is float || value is double || value is decimal;
        }

        private static string GetCellReference(int columnIndex, int rowIndex)
        {
            return GetColumnName(columnIndex) + rowIndex.ToString(CultureInfo.InvariantCulture);
        }

        private static string GetColumnName(int columnIndex)
        {
            var builder = new StringBuilder();
            int index = columnIndex;
            while (index > 0)
            {
                int remainder = (index - 1) % 26;
                builder.Insert(0, (char)('A' + remainder));
                index = (index - 1) / 26;
            }
            return builder.ToString();
        }

        private static string XmlEscape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("&", "&amp;")
                        .Replace("<", "&lt;")
                        .Replace(">", "&gt;")
                        .Replace("\"", "&quot;")
                        .Replace("'", "&apos;");
        }
    }
}
