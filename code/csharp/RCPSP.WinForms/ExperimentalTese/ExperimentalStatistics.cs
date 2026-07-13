// Thesis traceability: Appendix A, Algorithm A.9 (weighted and confirmatory statistics).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RCPSP.WinForms.ExperimentalTese
{
    internal static class ExperimentalStatistics
    {
        public static double Pearson(IList<double> x, IList<double> y)
        {
            if (x == null || y == null || x.Count != y.Count || x.Count < 2)
                return 0.0;

            double mx = x.Average();
            double my = y.Average();
            double num = 0.0;
            double dx2 = 0.0;
            double dy2 = 0.0;

            for (int i = 0; i < x.Count; i++)
            {
                double dx = x[i] - mx;
                double dy = y[i] - my;
                num += dx * dy;
                dx2 += dx * dx;
                dy2 += dy * dy;
            }

            double den = Math.Sqrt(dx2 * dy2);
            return den <= 0.0 ? 0.0 : num / den;
        }

        public static double Spearman(IList<double> x, IList<double> y)
        {
            if (x == null || y == null || x.Count != y.Count || x.Count < 2)
                return 0.0;
            return Pearson(Ranks(x), Ranks(y));
        }

        public static List<double> Ranks(IList<double> values)
        {
            var indexed = new List<Tuple<double, int>>();
            for (int i = 0; i < values.Count; i++)
                indexed.Add(Tuple.Create(values[i], i));

            indexed.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            double[] ranks = new double[values.Count];
            int pos = 0;
            while (pos < indexed.Count)
            {
                int end = pos;
                while (end + 1 < indexed.Count && Math.Abs(indexed[end + 1].Item1 - indexed[pos].Item1) < 1e-9)
                    end++;

                double rank = (pos + 1 + end + 1) / 2.0;
                for (int i = pos; i <= end; i++)
                    ranks[indexed[i].Item2] = rank;
                pos = end + 1;
            }

            return ranks.ToList();
        }


        public static double WeightedMean(IList<double> values, IList<double> weights)
        {
            if (values == null || weights == null || values.Count != weights.Count || values.Count == 0)
                return 0.0;
            double sw = 0.0, sx = 0.0;
            for (int i = 0; i < values.Count; i++)
            {
                if (double.IsNaN(values[i]) || double.IsInfinity(values[i]) || weights[i] <= 0.0)
                    continue;
                sw += weights[i];
                sx += weights[i] * values[i];
            }
            return sw <= 0.0 ? 0.0 : sx / sw;
        }

        public static double WeightedPearson(IList<double> x, IList<double> y, IList<double> weights)
        {
            if (x == null || y == null || weights == null || x.Count != y.Count || x.Count != weights.Count || x.Count < 2)
                return 0.0;
            double mx = WeightedMean(x, weights);
            double my = WeightedMean(y, weights);
            double num = 0.0, dx2 = 0.0, dy2 = 0.0;
            for (int i = 0; i < x.Count; i++)
            {
                double w = weights[i];
                if (w <= 0.0 || double.IsNaN(x[i]) || double.IsNaN(y[i]))
                    continue;
                double dx = x[i] - mx;
                double dy = y[i] - my;
                num += w * dx * dy;
                dx2 += w * dx * dx;
                dy2 += w * dy * dy;
            }
            double den = Math.Sqrt(dx2 * dy2);
            return den <= 0.0 ? 0.0 : num / den;
        }

        public static double WeightedSpearman(IList<double> x, IList<double> y, IList<double> weights)
        {
            if (x == null || y == null || weights == null || x.Count != y.Count || x.Count != weights.Count || x.Count < 2)
                return 0.0;
            return WeightedPearson(Ranks(x), Ranks(y), weights);
        }

        public static double Mean(IEnumerable<double> values)
        {
            var list = values == null ? new List<double>() : values.ToList();
            return list.Count == 0 ? 0.0 : list.Average();
        }

        public static double StdDev(IEnumerable<double> values)
        {
            var list = values == null ? new List<double>() : values.ToList();
            if (list.Count < 2)
                return 0.0;
            double avg = list.Average();
            double sum = list.Sum(v => (v - avg) * (v - avg));
            return Math.Sqrt(sum / (list.Count - 1));
        }

        public static double StudentTCritical975(int degreesOfFreedom)
        {
            if (degreesOfFreedom <= 0)
                return double.NaN;


            double[] values =
            {
                0.0,
                12.7062047364, 4.30265272975, 3.18244630528, 2.7764451052,
                2.57058183564, 2.44691185114, 2.36462425101, 2.30600413503, 2.2621571628,
                2.22813885196, 2.20098516008, 2.17881282967, 2.16036865646, 2.14478668792,
                2.13144954556, 2.11990529922, 2.10981557783, 2.10092204024, 2.09302405441,
                2.08596344727, 2.07961384473, 2.0738730679, 2.06865761042, 2.06389856163,
                2.05953855275, 2.05552943864, 2.05183051648, 2.0484071418, 2.04522964213,
                2.0422724563, 2.0395134464, 2.03693334346, 2.03451529745, 2.03224450932,
                2.03010792825, 2.02809400098, 2.02619246303, 2.02439416391, 2.02269092004,
                2.02107539031, 2.01954097044, 2.01808170282, 2.01669219923, 2.01536757444,
                2.01410338888, 2.01289559892, 2.01174051373, 2.01063475762, 2.00957523449
            };

            if (degreesOfFreedom < values.Length)
                return values[degreesOfFreedom];
            return 1.95996398454;
        }

        public static string InterpretCorrelation(double value)
        {
            double abs = Math.Abs(value);
            string strength = abs >= 0.70 ? "forte" : abs >= 0.40 ? "moderada" : abs >= 0.20 ? "fraca" : "muito fraca";
            string direction = value < 0 ? "negativa" : value > 0 ? "positiva" : "nula";
            return strength + " " + direction;
        }

        public static string Format(double value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }


        public static WilcoxonResult WilcoxonSignedRankTest(IList<double> differences)
        {
            var result = new WilcoxonResult();
            if (differences == null || differences.Count == 0)
            {
                result.Note = "No data.";
                return result;
            }


            var nonZero = differences.Where(d => Math.Abs(d) > 1e-12).ToList();
            result.N = nonZero.Count;
            result.NZeros = differences.Count - nonZero.Count;

            if (result.N < 2)
            {
                result.Note = "Insufficient non-zero differences (n=" + result.N + "); test not executed.";
                return result;
            }


            var indexed = nonZero
                .Select((d, i) => new { Diff = d, AbsDiff = Math.Abs(d), Index = i })
                .OrderBy(x => x.AbsDiff)
                .ToList();

            double[] ranks = new double[result.N];
            int pos = 0;
            while (pos < indexed.Count)
            {
                int end = pos;
                while (end + 1 < indexed.Count
                       && Math.Abs(indexed[end + 1].AbsDiff - indexed[pos].AbsDiff) < 1e-9)
                    end++;

                double avgRank = (pos + 1 + end + 1) / 2.0;
                for (int i = pos; i <= end; i++)
                    ranks[indexed[i].Index] = avgRank;
                pos = end + 1;
            }


            double wPlus = 0.0;
            double wMinus = 0.0;
            for (int i = 0; i < result.N; i++)
            {
                if (nonZero[i] > 0)
                    wPlus += ranks[i];
                else
                    wMinus += ranks[i];
            }

            result.WPlus = wPlus;
            result.WMinus = wMinus;


            int n = result.N;
            double eW = n * (n + 1) / 4.0;
            double varW = n * (n + 1) * (2 * n + 1) / 24.0;
            double sdW = Math.Sqrt(varW);


            result.Z = sdW > 1e-9 ? (wPlus - eW - 0.5) / sdW : 0.0;
            result.PValueOneTailed = 1.0 - StandardNormalCdf(result.Z);
            result.EffectSizeR = result.Z / Math.Sqrt(n);

            result.Significant = result.PValueOneTailed < 0.05;
            result.Note = n < 10
                ? "n=" + n + "; normal approximation is unreliable for n<10. Interpret with caution."
                : "n=" + n + "; normal approximation applied.";

            return result;
        }


        private static double StandardNormalCdf(double z)
        {
            if (z < -8.0) return 0.0;
            if (z > 8.0) return 1.0;

            double a1 = 0.319381530, a2 = -0.356563782, a3 = 1.781477937;
            double a4 = -1.821255978, a5 = 1.330274429;
            double t = 1.0 / (1.0 + 0.2316419 * Math.Abs(z));
            double poly = t * (a1 + t * (a2 + t * (a3 + t * (a4 + t * a5))));
            double pdf = Math.Exp(-0.5 * z * z) / Math.Sqrt(2.0 * Math.PI);
            double cdf = 1.0 - pdf * poly;
            return z >= 0 ? cdf : 1.0 - cdf;
        }
    }
}
