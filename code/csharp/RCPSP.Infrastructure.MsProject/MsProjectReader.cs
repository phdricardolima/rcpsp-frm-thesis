using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Office.Interop.MSProject;
using RCPSP.Application;
using RCPSP.Contracts;

namespace RCPSP.Infrastructure.MsProject
{
    public sealed class MsProjectReader : IProjectDataReader
    {
        public ProjectDataDto ReadActiveProject(object activeProject)
        {
            Project project = ResolveProject(activeProject);
            if (project == null)
                throw new InvalidOperationException("There is no active Microsoft Project project.");

            var dto = new ProjectDataDto
            {
                ProjectName = project.Name ?? string.Empty,
                StatusDate = SafeReadStatusDate(project),
                DecisionDate = SafeReadStatusDate(project),
                HoursPerDay = SafeReadHoursPerDay(project)
            };

            var resources = ReadResources(project);
            dto.Resources.AddRange(resources.Values.OrderBy(r => r.Id));

            var activities = ReadActivities(project, resources, dto.HoursPerDay);
            dto.Activities.AddRange(activities.OrderBy(a => a.Id));

            BuildSuccessors(dto);

            return dto;
        }

        private static Project ResolveProject(object activeProject)
        {
            if (activeProject == null)
                return null;

            if (activeProject is Project directProject)
                return directProject;

            if (activeProject is Microsoft.Office.Interop.MSProject.Application app)
                return app.ActiveProject;

            return null;
        }

        private static DateTime? SafeReadStatusDate(Project project)
        {
            try
            {
                return MsProjectFieldConverters.SafeDate(project.StatusDate);
            }
            catch
            {
                return null;
            }
        }

        private static double SafeReadHoursPerDay(Project project)
        {
            try
            {
                double value = project.HoursPerDay;
                return value > 0 ? value : 8.0;
            }
            catch
            {
                return 8.0;
            }
        }

        private static Dictionary<int, ResourceDto> ReadResources(Project project)
        {
            var result = new Dictionary<int, ResourceDto>();

            foreach (Resource resource in project.Resources)
            {
                if (resource == null || resource.ID <= 0)
                    continue;

                var dto = new ResourceDto
                {
                    Id = resource.ID,
                    Name = resource.Name ?? string.Empty,
                    Capacity = SafeReadMaxUnits(resource)
                };

                if (!result.ContainsKey(dto.Id))
                    result.Add(dto.Id, dto);
            }

            return result;
        }

        private static double SafeReadMaxUnits(Resource resource)
        {
            try
            {
                double units = resource.MaxUnits;
                return units > 0 ? units : 1.0;
            }
            catch
            {
                return 1.0;
            }
        }

        private static List<ActivityDto> ReadActivities(
            Project project,
            Dictionary<int, ResourceDto> resourceMap,
            double hoursPerDay)
        {
            var result = new List<ActivityDto>();
            double minutesPerDay = hoursPerDay > 0 ? hoursPerDay * 60.0 : 480.0;

            foreach (Task task in project.Tasks)
            {
                if (task == null || task.ID <= 0)
                    continue;

                var activity = new ActivityDto
                {
                    Id = task.ID,
                    OriginalId = task.ID,
                    Name = task.Name ?? string.Empty,
                    DurationDays = ReadDurationDays(task, minutesPerDay),
                    RemainingDurationDays = ReadRemainingDurationDays(task, minutesPerDay),
                    IsSummary = SafeReadBool(() => task.Summary),
                    IsMilestone = SafeReadBool(() => task.Milestone),
                    PlannedStart = SafeReadDate(() => task.Start),
                    PlannedFinish = SafeReadDate(() => task.Finish),
                    ActualStart = SafeReadDate(() => task.ActualStart),
                    ActualFinish = SafeReadDate(() => task.ActualFinish),
                    ExecutionState = ReadExecutionState(task)
                };

                activity.PredecessorIds.AddRange(ReadPredecessors(task));
                activity.Assignments.AddRange(ReadAssignments(task, resourceMap));
                activity.IsDummy = InferIsDummy(activity);

                result.Add(activity);
            }

            return result;
        }

        private static int ReadDurationDays(Task task, double minutesPerDay)
        {
            try
            {
                return MsProjectFieldConverters.SafeDurationDays(task.Duration, minutesPerDay);
            }
            catch
            {
                return 0;
            }
        }

        private static int? ReadRemainingDurationDays(Task task, double minutesPerDay)
        {
            try
            {
                int value = MsProjectFieldConverters.SafeDurationDays(task.RemainingDuration, minutesPerDay);
                return value;
            }
            catch
            {
                return null;
            }
        }

        private static string ReadExecutionState(Task task)
        {
            try
            {
                if (SafeReadBool(() => task.Summary))
                    return "NotStarted";

                double percentComplete = task.PercentComplete;

                if (percentComplete >= 100.0)
                    return "Completed";

                if (percentComplete > 0.0)
                    return "InProgress";
            }
            catch
            {
            }

            return "NotStarted";
        }

        private static IEnumerable<int> ReadPredecessors(Task task)
        {
            var result = new List<int>();

            try
            {
                foreach (TaskDependency dependency in task.TaskDependencies)
                {
                    if (dependency == null || dependency.From == null || dependency.To == null)
                        continue;

                    if (dependency.To.ID == task.ID && !result.Contains(dependency.From.ID))
                        result.Add(dependency.From.ID);
                }
            }
            catch
            {
            }

            return result;
        }

        private static IEnumerable<ResourceAssignmentDto> ReadAssignments(
            Task task,
            Dictionary<int, ResourceDto> resourceMap)
        {
            var result = new List<ResourceAssignmentDto>();

            try
            {
                foreach (Assignment assignment in task.Assignments)
                {
                    if (assignment == null || assignment.Resource == null)
                        continue;

                    Resource resource = assignment.Resource;
                    if (resource == null || resource.ID <= 0)
                        continue;

                    if (!resourceMap.ContainsKey(resource.ID))
                        continue;

                    result.Add(new ResourceAssignmentDto
                    {
                        ResourceId = resource.ID,
                        ResourceName = resource.Name ?? string.Empty,
                        Units = SafeReadAssignmentUnits(assignment)
                    });
                }
            }
            catch
            {
            }

            return result;
        }

        private static double SafeReadAssignmentUnits(Assignment assignment)
        {
            try
            {
                double units = assignment.Units;
                return units > 0 ? units : 0.0;
            }
            catch
            {
                return 0.0;
            }
        }

        private static void BuildSuccessors(ProjectDataDto dto)
        {
            var byId = dto.Activities.ToDictionary(a => a.Id);

            foreach (var activity in dto.Activities)
            {
                foreach (int predecessorId in activity.PredecessorIds)
                {
                    if (!byId.TryGetValue(predecessorId, out ActivityDto predecessor))
                        continue;

                    if (!predecessor.SuccessorIds.Contains(activity.Id))
                        predecessor.SuccessorIds.Add(activity.Id);
                }
            }
        }

        private static bool InferIsDummy(ActivityDto activity)
        {
            if (activity.IsSummary)
                return false;

            return activity.IsMilestone &&
                   activity.DurationDays == 0 &&
                   activity.Assignments.Count == 0;
        }

        private static DateTime? SafeReadDate(Func<object> getter)
        {
            try
            {
                return MsProjectFieldConverters.SafeDate(getter());
            }
            catch
            {
                return null;
            }
        }

        private static bool SafeReadBool(Func<bool> getter)
        {
            try
            {
                return getter();
            }
            catch
            {
                return false;
            }
        }
    }
}
