using RCPSP.Contracts;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace RCPSP.Standalone
{
    internal sealed class RcpProjectDataImporter
    {
        public ProjectDataDto Import(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException(".rcp file path was not provided.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException(".rcp file not found.", filePath);

            var lines = File.ReadAllLines(filePath);
            if (lines.Length < 4)
                throw new InvalidOperationException("Invalid .rcp file: insufficient number of lines.");

            List<int> header = Tokenize(lines[0]);
            if (header.Count < 2)
                throw new InvalidOperationException("Invalid first line in the .rcp file. Expected: number of activities and number of resources.");

            int totalActivitiesWithDummies = header[0];
            int realActivities = totalActivitiesWithDummies - 2;
            int resourceCount = header[1];

            if (realActivities <= 0)
                throw new InvalidOperationException("Invalid number of real activities in the .rcp file.");
            if (resourceCount <= 0)
                throw new InvalidOperationException("Invalid number of resources in the .rcp file.");

            List<int> capacities = Tokenize(lines[1]);
            if (capacities.Count < resourceCount)
                throw new InvalidOperationException("Invalid resource capacities line in the .rcp file.");

            var project = new ProjectDataDto
            {
                ProjectName = Path.GetFileNameWithoutExtension(filePath),
                HoursPerDay = 8.0
            };

            for (int r = 0; r < resourceCount; r++)
            {
                project.Resources.Add(new ResourceDto
                {
                    Id = r + 1,
                    Name = "R" + (r + 1).ToString(CultureInfo.InvariantCulture),
                    Capacity = capacities[r]
                });
            }

            var successorsByActivity = new Dictionary<int, List<int>>();
            int currentActivityNumber = 0;

            for (int lineIndex = 3; lineIndex < lines.Length && currentActivityNumber < realActivities; lineIndex++)
            {
                List<int> tokens = Tokenize(lines[lineIndex]);
                if (tokens.Count == 0)
                    continue;

                currentActivityNumber++;
                if (tokens.Count < 1 + resourceCount)
                    throw new InvalidOperationException("Activity line " + currentActivityNumber + " is invalid in the .rcp file.");

                int duration = tokens[0];
                var activity = new ActivityDto
                {
                    Id = currentActivityNumber,
                    OriginalId = currentActivityNumber,
                    Name = "Act " + currentActivityNumber.ToString(CultureInfo.InvariantCulture),
                    DurationDays = duration,
                    ExecutionState = "NotStarted",
                    IsSummary = false,
                    IsDummy = false,
                    IsMilestone = duration == 0
                };

                for (int r = 0; r < resourceCount; r++)
                {
                    int demand = tokens[1 + r];
                    if (demand <= 0)
                        continue;

                    activity.Assignments.Add(new ResourceAssignmentDto
                    {
                        ResourceId = r + 1,
                        ResourceName = "R" + (r + 1).ToString(CultureInfo.InvariantCulture),
                        Units = demand
                    });
                }

                var successors = new List<int>();
                for (int index = resourceCount + 2; index < tokens.Count; index++)
                {
                    int successorId = tokens[index] - 1;
                    if (successorId >= 1 && successorId <= realActivities && successorId != currentActivityNumber)
                    {
                        if (!successors.Contains(successorId))
                            successors.Add(successorId);
                    }
                }

                successorsByActivity[currentActivityNumber] = successors;
                activity.SuccessorIds.AddRange(successors);
                project.Activities.Add(activity);
            }

            if (project.Activities.Count != realActivities)
                throw new InvalidOperationException("The number of activities read does not match the .rcp header.");

            var byId = project.Activities.ToDictionary(a => a.Id, a => a);
            foreach (var pair in successorsByActivity)
            {
                foreach (int succ in pair.Value)
                {
                    ActivityDto successor;
                    if (byId.TryGetValue(succ, out successor) && !successor.PredecessorIds.Contains(pair.Key))
                        successor.PredecessorIds.Add(pair.Key);
                }
            }

            return project;
        }

        private static List<int> Tokenize(string line)
        {
            var values = new List<int>();
            if (string.IsNullOrWhiteSpace(line))
                return values;

            string[] rawTokens = Regex.Split(line.Trim(), @"\D+");
            foreach (string token in rawTokens)
            {
                int value;
                if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                    values.Add(value);
            }

            return values;
        }
    }
}
