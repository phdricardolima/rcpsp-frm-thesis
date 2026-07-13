// Thesis traceability: Appendix A, Algorithm A.10 and Appendix C (experimental file persistence).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace RCPSP.WinForms.ExperimentalTese
{
    internal static class ExperimentalCsv
    {
        public static void WriteRows(string filePath, string[] headers, IEnumerable<string[]> rows)
        {
            EnsureDirectory(filePath);
            using (var writer = new StreamWriter(filePath, false, new UTF8Encoding(true)))
            {
                writer.WriteLine(ToLine(headers));
                if (rows == null)
                    return;

                foreach (var row in rows)
                    writer.WriteLine(ToLine(row));
            }
        }

        public static void AppendRow(string filePath, string[] headers, string[] row)
        {
            EnsureDirectory(filePath);
            bool exists = File.Exists(filePath);
            using (var writer = new StreamWriter(filePath, true, new UTF8Encoding(true)))
            {
                if (!exists)
                    writer.WriteLine(ToLine(headers));
                writer.WriteLine(ToLine(row));
            }
        }

        public static string S(object value)
        {
            if (value == null)
                return string.Empty;

            IFormattable formattable = value as IFormattable;
            if (formattable != null)
                return formattable.ToString(null, CultureInfo.InvariantCulture);

            return value.ToString() ?? string.Empty;
        }

        public static string JoinInts(IEnumerable<int> values)
        {
            if (values == null)
                return string.Empty;
            var parts = new List<string>();
            foreach (int value in values)
                parts.Add(value.ToString(CultureInfo.InvariantCulture));
            return string.Join(";", parts.ToArray());
        }

        private static string ToLine(string[] values)
        {
            if (values == null || values.Length == 0)
                return string.Empty;

            var escaped = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                escaped[i] = Escape(values[i]);
            return string.Join(",", escaped);
        }

        private static string Escape(string value)
        {
            if (value == null)
                value = string.Empty;

            bool mustQuote = value.IndexOf(',') >= 0 || value.IndexOf('"') >= 0 || value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0;
            value = value.Replace("\"", "\"\"");
            return mustQuote ? "\"" + value + "\"" : value;
        }

        private static void EnsureDirectory(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
