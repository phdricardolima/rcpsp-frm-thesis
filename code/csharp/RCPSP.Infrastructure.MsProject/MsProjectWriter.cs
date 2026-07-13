using System;
using System.Globalization;
using System.Linq;
using Microsoft.Office.Interop.MSProject;
using RCPSP.Application;
using RCPSP.Contracts;

namespace RCPSP.Infrastructure.MsProject
{
    public sealed class MsProjectWriter : IProjectScheduleWriter
    {
        public void WriteSchedule(object activeProject, ExecutionSummary summary)
        {
            if (summary == null)
                throw new ArgumentNullException(nameof(summary));

            Project project = ResolveProject(activeProject);
            if (project == null)
                throw new InvalidOperationException("There is no active Microsoft Project project.");

            if (summary.Baseline == null)
                return;

            ApplyBaseline(project, summary.Baseline);
            RecalculateProject(activeProject, project);
        }

        private static Project ResolveProject(object activeProject)
        {
            if (activeProject == null)
                return null;

            var directProject = activeProject as Project;
            if (directProject != null)
                return directProject;

            var app = activeProject as Microsoft.Office.Interop.MSProject.Application;
            if (app != null)
                return app.ActiveProject;

            return null;
        }

        private static void ApplyBaseline(Project project, BaselineResultDto result)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            DateTime baseStart = ResolveProjectStart(project);

            foreach (Task task in project.Tasks)
            {
                if (task == null || task.ID <= 0)
                    continue;

                int startOffset;
                int finishOffset;

                if (!result.StartTimesByActivity.TryGetValue(task.ID, out startOffset))
                    continue;

                if (!result.FinishTimesByActivity.TryGetValue(task.ID, out finishOffset))
                    continue;

                DateTime startDate = baseStart.AddDays(startOffset);
                DateTime finishDate = baseStart.AddDays(finishOffset);

                try
                {
                    task.Manual = false;
                }
                catch
                {
                }

                task.Start = startDate.ToString(CultureInfo.CurrentCulture);
                task.Finish = finishDate.ToString(CultureInfo.CurrentCulture);
            }
        }

        private static DateTime ResolveProjectStart(Project project)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            DateTime? projectStart = null;

            try
            {
                projectStart = MsProjectFieldConverters.SafeDate(project.ProjectStart);
            }
            catch
            {
            }

            if (projectStart.HasValue)
                return projectStart.Value;

            try
            {
                var firstTaskStart = project.Tasks
                    .Cast<Task>()
                    .Where(t => t != null && t.ID > 0)
                    .Select(t => MsProjectFieldConverters.SafeDate(t.Start))
                    .Where(d => d.HasValue)
                    .Select(d => d.Value)
                    .OrderBy(d => d)
                    .FirstOrDefault();

                if (firstTaskStart != default(DateTime))
                    return firstTaskStart;
            }
            catch
            {
            }

            return DateTime.Today;
        }

        private static void RecalculateProject(object activeProject, Project project)
        {
            try
            {
                var app = activeProject as Microsoft.Office.Interop.MSProject.Application;
                if (app != null)
                {
                    app.CalculateProject();
                    return;
                }
            }
            catch
            {
            }

            try
            {
                if (project != null && project.Application != null)
                    project.Application.CalculateProject();
            }
            catch
            {
            }
        }
    }
}
