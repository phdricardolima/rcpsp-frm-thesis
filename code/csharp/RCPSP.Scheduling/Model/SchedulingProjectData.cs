using System.Collections.Generic;

namespace RCPSP.Scheduling.Model
{
    public sealed class SchedulingProjectData
    {
        public string ProjectName { get; set; } = string.Empty;

        public List<SchedulingActivity> Activities { get; set; } = new List<SchedulingActivity>();
        public List<SchedulingResource> Resources { get; set; } = new List<SchedulingResource>();

        private Dictionary<int, SchedulingActivity> _activityById;
        private Dictionary<int, SchedulingResource> _resourceById;
        private List<SchedulingActivity> _nonSummaryActivities;

        public void BuildCaches()
        {
            int activityCount = Activities != null ? Activities.Count : 0;
            int resourceCount = Resources != null ? Resources.Count : 0;

            _activityById = new Dictionary<int, SchedulingActivity>(activityCount);
            _resourceById = new Dictionary<int, SchedulingResource>(resourceCount);
            _nonSummaryActivities = new List<SchedulingActivity>(activityCount);

            if (Activities != null)
            {
                for (int i = 0; i < Activities.Count; i++)
                {
                    SchedulingActivity activity = Activities[i];
                    if (activity == null)
                        continue;

                    _activityById[activity.Id] = activity;
                    if (!activity.IsSummary)
                        _nonSummaryActivities.Add(activity);
                }
            }

            if (_nonSummaryActivities.Count > 1)
            {
                _nonSummaryActivities.Sort((a, b) =>
                {
                    int cmp = a.InputOrder.CompareTo(b.InputOrder);
                    return cmp != 0 ? cmp : a.Id.CompareTo(b.Id);
                });
            }

            if (Resources != null)
            {
                for (int i = 0; i < Resources.Count; i++)
                {
                    SchedulingResource resource = Resources[i];
                    if (resource == null)
                        continue;

                    _resourceById[resource.Id] = resource;
                }
            }
        }

        public SchedulingActivity GetActivity(int activityId)
        {
            if (_activityById == null)
                BuildCaches();

            SchedulingActivity activity;
            return _activityById.TryGetValue(activityId, out activity) ? activity : null;
        }

        public SchedulingResource GetResource(int resourceId)
        {
            if (_resourceById == null)
                BuildCaches();

            SchedulingResource resource;
            return _resourceById.TryGetValue(resourceId, out resource) ? resource : null;
        }

        public List<SchedulingActivity> GetNonSummaryActivities()
        {
            if (_nonSummaryActivities == null)
                BuildCaches();

            return _nonSummaryActivities;
        }

        public int GetProjectMakespanFromCpm()
        {
            var activities = GetNonSummaryActivities();
            if (activities == null || activities.Count == 0)
                return 0;

            int max = 0;
            for (int i = 0; i < activities.Count; i++)
            {
                var act = activities[i];
                if (act != null && act.EF > max)
                    max = act.EF;
            }

            return max;
        }

        public int GetHorizonEstimate()
        {
            var activities = GetNonSummaryActivities();
            if (activities == null || activities.Count == 0)
                return 0;

            int sum = 0;
            for (int i = 0; i < activities.Count; i++)
            {
                var act = activities[i];
                if (act == null)
                    continue;

                sum += act.Duration < 0 ? 0 : act.Duration;
            }

            return sum + 10;
        }

        public SchedulingProjectData Clone()
        {
            int activityCount = Activities != null ? Activities.Count : 0;
            int resourceCount = Resources != null ? Resources.Count : 0;

            var clone = new SchedulingProjectData
            {
                ProjectName = ProjectName,
                Activities = new List<SchedulingActivity>(activityCount),
                Resources = new List<SchedulingResource>(resourceCount)
            };

            if (Activities != null)
            {
                for (int i = 0; i < Activities.Count; i++)
                {
                    var activity = Activities[i];
                    if (activity != null)
                        clone.Activities.Add(activity.Clone());
                }
            }

            if (Resources != null)
            {
                for (int i = 0; i < Resources.Count; i++)
                {
                    var resource = Resources[i];
                    if (resource != null)
                        clone.Resources.Add(resource.Clone());
                }
            }

            clone.BuildCaches();
            return clone;
        }
    }
}
