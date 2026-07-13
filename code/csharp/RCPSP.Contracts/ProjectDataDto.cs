using System;
using System.Collections.Generic;

namespace RCPSP.Contracts
{
    public sealed class ProjectDataDto
    {
        public string ProjectName { get; set; } = string.Empty;
        public DateTime? StatusDate { get; set; }
        public DateTime? DecisionDate { get; set; }
        public double HoursPerDay { get; set; } = 8.0;

        public List<ActivityDto> Activities { get; set; } = new List<ActivityDto>();
        public List<ResourceDto> Resources { get; set; } = new List<ResourceDto>();
    }

    public sealed class ActivityDto
    {
        public int Id { get; set; }
        public int OriginalId { get; set; }
        public string Name { get; set; } = string.Empty;

        public int DurationDays { get; set; }
        public int? RemainingDurationDays { get; set; }

        public bool IsSummary { get; set; }
        public bool IsMilestone { get; set; }
        public bool IsDummy { get; set; }

        public string ExecutionState { get; set; } = "NotStarted";

        public DateTime? PlannedStart { get; set; }
        public DateTime? PlannedFinish { get; set; }
        public DateTime? ActualStart { get; set; }
        public DateTime? ActualFinish { get; set; }

        public List<int> PredecessorIds { get; set; } = new List<int>();
        public List<int> SuccessorIds { get; set; } = new List<int>();

        public List<ResourceAssignmentDto> Assignments { get; set; } = new List<ResourceAssignmentDto>();
    }

    public sealed class ResourceDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public double Capacity { get; set; }
    }

    public sealed class ResourceAssignmentDto
    {
        public int ResourceId { get; set; }
        public string ResourceName { get; set; } = string.Empty;
        public double Units { get; set; }
    }
}
