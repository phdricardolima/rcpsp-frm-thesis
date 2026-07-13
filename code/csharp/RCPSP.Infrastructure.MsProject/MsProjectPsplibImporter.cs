using RCPSP.Application;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MSProject = Microsoft.Office.Interop.MSProject;

namespace RCPSP.Infrastructure.MsProject
{
    public sealed class MsProjectPsplibImporter : IProjectImporter
    {
        public void ImportPsplibRcp(string filePath, object applicationInstance)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path was not provided.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException(".rcp file not found.", filePath);

            MSProject.Application app = applicationInstance as MSProject.Application;
            if (app == null)
                throw new InvalidOperationException("The Microsoft Project application is not available.");


            EnsureTargetProject(app);

            MSProject.Project currentProject = app.ActiveProject;
            if (currentProject == null)
                throw new InvalidOperationException("Could not get the active project.");

            string projectName = Path.GetFileNameWithoutExtension(filePath);

            currentProject.Title = projectName;
            currentProject.Name = projectName;
            currentProject.DefaultTaskType = MSProject.PjTaskFixedType.pjFixedUnits;
            currentProject.DisplayProjectSummaryTask = true;
            currentProject.DefaultEarnedValueMethod =
                MSProject.PjEarnedValueMethod.pjPhysicalPercentComplete;
            currentProject.Activate();

            var calendarService = new ProjectCalendarService();
            calendarService.EnsureContinuousCalendar(currentProject, app);

            ImportFileContent(filePath, currentProject);

            if (currentProject.Tasks != null &&
                currentProject.Tasks.Count > 0 &&
                currentProject.Tasks[1] != null)
            {
                try
                {
                    DateTime startDate = (DateTime)currentProject.Tasks[1].Start;
                    currentProject.StatusDate = startDate;
                }
                catch
                {

                }
            }
        }


        private static void EnsureTargetProject(MSProject.Application app)
        {
            if (app.ActiveProject == null)
            {
                app.FileNew(false, "", false, false);
                return;
            }

            try
            {
                if (app.ActiveProject.Tasks != null && app.ActiveProject.Tasks.Count != 0)
                {
                    app.AppMaximize();
                    app.FileNew(false, "", false, false);
                }
            }
            catch
            {
                app.FileNew(false, "", false, false);
            }
        }

        private static void ImportFileContent(string filePath, MSProject.Project project)
        {
            var successorsByActivity = new List<string>();

            int lineIndex = 0;
            int realActivities = 0;
            int resourceCount = 0;
            int currentActivityNumber = 0;

            using (var reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    List<string> tokens = Tokenize(line);

                    if (tokens.Count == 0)
                    {
                        lineIndex++;
                        continue;
                    }

                    if (lineIndex == 0)
                    {
                        if (tokens.Count < 2)
                            throw new InvalidOperationException(
                                "Invalid first line in the .rcp file.");

                        realActivities = int.Parse(tokens[0]) - 2;
                        resourceCount = int.Parse(tokens[1]);

                        if (realActivities <= 0 || resourceCount <= 0)
                            throw new InvalidOperationException(
                                "Invalid number of activities or resources in the .rcp file.");
                    }
                    else if (lineIndex == 1)
                    {
                        if (tokens.Count < resourceCount)
                            throw new InvalidOperationException(
                                "Invalid resource capacities line in the .rcp file.");

                        CreateResources(project, tokens, resourceCount);
                    }
                    else if (lineIndex >= 3 && lineIndex <= realActivities + 2)
                    {
                        currentActivityNumber++;
                        CreateTaskFromLine(
                            project,
                            tokens,
                            resourceCount,
                            realActivities,
                            currentActivityNumber,
                            successorsByActivity);
                    }

                    lineIndex++;
                }
            }

            ApplySuccessors(project, successorsByActivity);
        }

        private static List<string> Tokenize(string line)
        {
            string[] rawTokens = Regex.Split(line.Trim(), @"\D+");
            var tokens = new List<string>();

            foreach (string token in rawTokens)
            {
                if (!string.IsNullOrWhiteSpace(token))
                    tokens.Add(token);
            }

            return tokens;
        }

        private static void CreateResources(
            MSProject.Project project,
            List<string> tokens,
            int resourceCount)
        {
            for (int i = 0; i < resourceCount; i++)
            {
                int capacity = int.Parse(tokens[i]);

                project.Resources.Add("R" + (i + 1));

                if (project.Resources[i + 1] != null)
                    project.Resources[i + 1].MaxUnits = capacity;
            }
        }

        private static void CreateTaskFromLine(
            MSProject.Project project,
            List<string> tokens,
            int resourceCount,
            int realActivities,
            int currentActivityNumber,
            List<string> successorsByActivity)
        {
            MSProject.Task task = project.Tasks.Add("Act " + currentActivityNumber);
            task.Manual = false;

            string resourceNames = string.Empty;
            string successors = string.Empty;

            for (int index = 0; index < tokens.Count; index++)
            {
                if (index == 0)
                {
                    task.Duration = tokens[index];
                }
                else if (index > 0 && index <= resourceCount)
                {
                    int demand = int.Parse(tokens[index]);
                    if (demand == 0)
                        continue;

                    if (project.Application.ShowAssignmentUnitsAs ==
                        MSProject.PjAssignmentUnits.pjDecimalAssignmentUnits)
                    {
                        resourceNames += "R" + index + "[" + demand + "];";
                    }
                    else
                    {
                        resourceNames += "R" + index + "[" + (demand * 100.0) + "%];";
                    }
                }
                else if (index >= resourceCount + 2)
                {
                    int successorId = int.Parse(tokens[index]) - 1;

                    if (successorId <= realActivities)
                    {
                        successors = string.IsNullOrWhiteSpace(successors)
                            ? successorId.ToString()
                            : successors + ";" + successorId;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(resourceNames))
                task.ResourceNames = resourceNames;

            successorsByActivity.Add(successors);
        }

        private static void ApplySuccessors(
            MSProject.Project project,
            List<string> successorsByActivity)
        {
            for (int i = 1; i <= successorsByActivity.Count; i++)
            {
                if (project.Tasks[i] == null)
                    continue;

                string successors = successorsByActivity[i - 1];
                if (string.IsNullOrWhiteSpace(successors))
                    continue;

                project.Tasks[i].Successors = successors;
            }
        }
    }
}
