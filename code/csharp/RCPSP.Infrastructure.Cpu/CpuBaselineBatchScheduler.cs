// Thesis traceability: Appendix A, Algorithm A.2 (candidate baseline generation).
using RCPSP.Application;
using RCPSP.Contracts;
using System;
using System.Collections.Generic;

namespace RCPSP.Infrastructure.Cpu
{
    public sealed class CpuBaselineBatchScheduler : IBaselineBatchScheduler
    {
        private static readonly string[] SupportedHeuristics =
        {
            "SPT",
            "LPT",
            "EST",
            "EFT",
            "LST",
            "LFT",
            "MSLK",
            "MIS",
            "MTS",
            "GRWC"
        };

        private static readonly (string Scheme, string Direction)[] SupportedCombinations =
        {
            ("SERIAL", "FORWARD"),
            ("SERIAL", "BACKWARD"),
            ("PARALLEL", "FORWARD"),
            ("PARALLEL", "BACKWARD")
        };

        private readonly IBaselineScheduler _baselineScheduler;
        private readonly CpuExactBaselineScheduler _exactBaselineScheduler;

        public CpuBaselineBatchScheduler(IBaselineScheduler baselineScheduler)
        {
            _baselineScheduler = baselineScheduler ?? throw new ArgumentNullException(nameof(baselineScheduler));
            _exactBaselineScheduler = new CpuExactBaselineScheduler();
        }

        public List<BaselineRunSummaryDto> Run(ProjectDataDto project, SchedulingOptionsDto options)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            string heuristic = Normalize(options.Heuristic);

            if (IsModifiedDhBranchAndBound(heuristic))
                return new List<BaselineRunSummaryDto> { ExecuteModifiedDhExactRun(project, options) };

            if (heuristic == "ALL")
                return RunAll(project, options);

            return new List<BaselineRunSummaryDto>
            {
                ExecuteHeuristicRun(project, options, heuristic, Normalize(options.Scheme), Normalize(options.Direction))
            };
        }

        private List<BaselineRunSummaryDto> RunAll(ProjectDataDto project, SchedulingOptionsDto options)
        {


            var runs = new List<BaselineRunSummaryDto>(
                SupportedHeuristics.Length * SupportedCombinations.Length + 1);

            for (int h = 0; h < SupportedHeuristics.Length; h++)
            {
                string heuristic = SupportedHeuristics[h];

                for (int c = 0; c < SupportedCombinations.Length; c++)
                {
                    var combo = SupportedCombinations[c];
                    runs.Add(ExecuteHeuristicRun(project, options, heuristic, combo.Scheme, combo.Direction));
                }
            }


            runs.Add(CreateExactPendingRun("Modified DH B&B", "DH_BB"));

            return runs;
        }

        private BaselineRunSummaryDto ExecuteHeuristicRun(
            ProjectDataDto project,
            SchedulingOptionsDto baseOptions,
            string heuristic,
            string scheme,
            string direction)
        {
            try
            {
                var runOptions = CloneOptions(baseOptions);
                runOptions.Heuristic = heuristic;
                runOptions.Scheme = scheme;
                runOptions.Direction = direction;
                runOptions.UseExactEngine = false;
                runOptions.Engine = "HEURISTIC";
                runOptions.RunLabel = heuristic + " | " + scheme + " | " + direction;

                var baseline = _baselineScheduler.Run(project, runOptions);

                return new BaselineRunSummaryDto
                {
                    RunType = "Heuristic",
                    Heuristic = heuristic,
                    Scheme = scheme,
                    Direction = direction,
                    IsExact = false,
                    ExactMode = string.Empty,
                    EngineKey = "HEURISTIC",
                    MethodClassification = BaselineMethodClassifier.Heuristic,
                    Success = true,
                    Status = "Success",
                    ErrorMessage = string.Empty,
                    Makespan = baseline != null ? baseline.Makespan : 0,
                    ScheduledActivities = baseline != null && baseline.Activities != null
                        ? baseline.Activities.Count
                        : 0,
                    PriorityListText = JoinInts(baseline != null ? baseline.PriorityList : null),
                    ScheduledOrderText = JoinInts(baseline != null ? baseline.ScheduledOrder : null),
                    BaselineResult = baseline ?? new BaselineResultDto()
                };
            }
            catch (Exception ex)
            {
                return new BaselineRunSummaryDto
                {
                    RunType = "Heuristic",
                    Heuristic = heuristic,
                    Scheme = scheme,
                    Direction = direction,
                    IsExact = false,
                    ExactMode = string.Empty,
                    EngineKey = "HEURISTIC",
                    MethodClassification = BaselineMethodClassifier.Heuristic,
                    Success = false,
                    Status = "Error",
                    ErrorMessage = ex.Message,
                    Makespan = 0,
                    ScheduledActivities = 0,
                    PriorityListText = string.Empty,
                    ScheduledOrderText = string.Empty,
                    BaselineResult = new BaselineResultDto
                    {
                        RunLabel = heuristic + " | " + scheme + " | " + direction
                    }
                };
            }
        }

        private BaselineRunSummaryDto CreateExactPendingRun(string label, string engineKey)
        {
            return new BaselineRunSummaryDto
            {
                RunType = "Exact B&B",
                Heuristic = label,
                Scheme = "EXACT",
                Direction = "EXACT",
                IsExact = true,
                ExactMode = label,
                EngineKey = engineKey,
                MethodClassification = BaselineMethodClassifier.ExactPending,
                Success = false,
                Status = "Running",
                ErrorMessage = label + " is running; wait until it finishes before selecting this row.",
                Makespan = 0,
                ScheduledActivities = 0,
                PriorityListText = "(exact)",
                ScheduledOrderText = string.Empty,
                BranchAndBoundTimeLimitSeconds = null,
                BranchAndBoundTimeLimitReached = null,
                BranchAndBoundOptimalityProven = null,
                BranchAndBoundNodesVisited = null,
                BranchAndBoundSlackSum = null,
                BranchAndBoundTrace = string.Empty,
                BaselineResult = new BaselineResultDto
                {
                    RunLabel = label
                }
            };
        }

        private BaselineRunSummaryDto ExecuteModifiedDhExactRun(ProjectDataDto project, SchedulingOptionsDto baseOptions)
        {
            string label = "Modified DH B&B";
            string engineKey = "DH_BB";

            try
            {
                var runOptions = CloneOptions(baseOptions);
                runOptions.Heuristic = label;
                runOptions.Scheme = "EXACT";
                runOptions.Direction = "EXACT";
                runOptions.UseExactEngine = true;
                runOptions.Engine = engineKey;
                runOptions.RunLabel = label;

                var exact = _exactBaselineScheduler.RunModifiedDhBranchAndBoundDetailed(project, runOptions);
                var baseline = exact != null ? exact.Baseline : null;

                var result = new BaselineRunSummaryDto
                {
                    RunType = "Exact B&B",
                    Heuristic = label,
                    Scheme = "EXACT",
                    Direction = "EXACT",
                    IsExact = true,
                    ExactMode = label,
                    EngineKey = engineKey,
                    Success = exact != null && exact.Success,
                    Status = exact != null && !string.IsNullOrWhiteSpace(exact.Status) ? exact.Status : "Success",
                    ErrorMessage = exact != null ? exact.Message : string.Empty,
                    Makespan = baseline != null ? baseline.Makespan : 0,
                    ScheduledActivities = baseline != null && baseline.Activities != null
                        ? baseline.Activities.Count
                        : 0,
                    PriorityListText = "(exact)",
                    ScheduledOrderText = JoinInts(baseline != null ? baseline.ScheduledOrder : null),
                    BranchAndBoundTimeLimitSeconds = exact != null ? (int?)exact.TimeLimitSeconds : null,
                    BranchAndBoundTimeLimitReached = exact != null ? (bool?)exact.TimeLimitReached : null,
                    BranchAndBoundOptimalityProven = exact != null ? (bool?)exact.OptimalityProven : null,
                    BranchAndBoundNodesVisited = exact != null ? (long?)exact.NodesVisited : null,
                    BranchAndBoundSlackSum = exact != null ? (double?)exact.SlackSum : null,
                    BranchAndBoundTrace = exact != null ? exact.Trace : string.Empty,
                    BaselineResult = baseline ?? new BaselineResultDto { RunLabel = label }
                };

                result.MethodClassification = BaselineMethodClassifier.Classify(
                    result,
                    result.BaselineResult != null && result.BaselineResult.Activities != null && result.BaselineResult.Activities.Count > 0);
                return result;
            }
            catch (Exception ex)
            {
                return new BaselineRunSummaryDto
                {
                    RunType = "Exact B&B",
                    Heuristic = label,
                    Scheme = "EXACT",
                    Direction = "EXACT",
                    IsExact = true,
                    ExactMode = label,
                    EngineKey = engineKey,
                    MethodClassification = BaselineMethodClassifier.ExactFailed,
                    Success = false,
                    Status = "Error",
                    ErrorMessage = ex.Message,
                    Makespan = 0,
                    ScheduledActivities = 0,
                    PriorityListText = "(exact)",
                    ScheduledOrderText = string.Empty,
                    BaselineResult = new BaselineResultDto
                    {
                        RunLabel = label
                    }
                };
            }
        }

        private static SchedulingOptionsDto CloneOptions(SchedulingOptionsDto source)
        {
            if (source == null)
                return new SchedulingOptionsDto();

            return new SchedulingOptionsDto
            {
                Heuristic = source.Heuristic,
                Scheme = source.Scheme,
                Direction = source.Direction,
                UseExactEngine = source.UseExactEngine,
                Engine = source.Engine,
                RunLabel = source.RunLabel,


                BranchAndBoundTimeLimitSeconds = source.BranchAndBoundTimeLimitSeconds
            };
        }

        private static string JoinInts(IEnumerable<int> values)
        {
            if (values == null)
                return string.Empty;

            var parts = new List<string>();
            foreach (int value in values)
                parts.Add(value.ToString());

            return parts.Count == 0 ? string.Empty : string.Join(", ", parts);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToUpperInvariant();
        }

        private static bool IsModifiedDhBranchAndBound(string value)
        {
            string normalized = Normalize(value);
            return normalized == "MODIFIED DH B&B"
                   || normalized == "B&B"
                   || normalized == "DHBB"
                   || normalized == "DH_BB"
                   || normalized == "ENHANCEFLEXIBILITY"
                   || normalized == "ENHANCE_FLEXIBILITY";
        }
    }
}
