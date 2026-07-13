using RCPSP.Contracts;
using RCPSP.Scheduling.Model;
using System;
using System.Collections.Generic;

namespace RCPSP.Infrastructure.Cpu
{
    public static class ProjectDataMapper
    {
        public static SchedulingProjectData Map(ProjectDataDto source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var result = new SchedulingProjectData
            {
                ProjectName = source.ProjectName ?? string.Empty
            };

            var resources = source.Resources ?? new List<ResourceDto>();
            result.Resources = new List<SchedulingResource>(resources.Count);
            for (int i = 0; i < resources.Count; i++)
            {
                var resource = resources[i];
                if (resource == null)
                    continue;

                result.Resources.Add(new SchedulingResource
                {
                    Id = resource.Id,
                    Name = resource.Name ?? ("R" + resource.Id),
                    Capacity = ConvertLegacyInt(resource.Capacity)
                });
            }

            var activities = source.Activities ?? new List<ActivityDto>();
            result.Activities = new List<SchedulingActivity>(activities.Count);
            int inputOrder = 0;

            for (int i = 0; i < activities.Count; i++)
            {
                var activity = activities[i];
                if (activity == null)
                    continue;

                inputOrder++;

                var mapped = new SchedulingActivity
                {
                    Id = activity.Id,
                    OriginalId = activity.OriginalId,
                    InputOrder = inputOrder,
                    Name = activity.Name ?? ("Act " + activity.Id),
                    Duration = Math.Max(0, ConvertLegacyInt(activity.DurationDays)),
                    IsSummary = activity.IsSummary,
                    IsDummy = activity.IsDummy
                };

                if (activity.PredecessorIds != null && activity.PredecessorIds.Count > 0)
                    mapped.PredecessorIds = new List<int>(activity.PredecessorIds);

                if (activity.SuccessorIds != null && activity.SuccessorIds.Count > 0)
                    mapped.SuccessorIds = new List<int>(activity.SuccessorIds);

                if (activity.Assignments != null)
                {
                    for (int j = 0; j < activity.Assignments.Count; j++)
                    {
                        var assignment = activity.Assignments[j];
                        if (assignment == null || assignment.ResourceId <= 0)
                            continue;

                        int demand = ConvertLegacyInt(assignment.Units);
                        if (demand <= 0)
                            continue;

                        int existing;
                        if (mapped.ResourceDemandByResourceId.TryGetValue(assignment.ResourceId, out existing))
                            mapped.ResourceDemandByResourceId[assignment.ResourceId] = existing + demand;
                        else
                            mapped.ResourceDemandByResourceId[assignment.ResourceId] = demand;
                    }
                }

                result.Activities.Add(mapped);
            }

            result.BuildCaches();
            return result;
        }

        private static int ConvertLegacyInt(double value)
        {
            return (int)value;
        }
    }
}
