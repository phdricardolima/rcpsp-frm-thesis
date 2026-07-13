using RCPSP.Scheduling.Model;
using System.Collections.Generic;

namespace RCPSP.Scheduling.Heuristics
{
    public sealed class HeuristicMTS : IHeuristicPriorityRule
    {
        public string Name => "MTS";

        public List<SchedulingActivity> OrderActivities(SchedulingProjectData project)
        {
            var activities = project.GetNonSummaryActivities();
            var memo = new Dictionary<int, int>(activities.Count);
            var visiting = new HashSet<int>();

            for (int i = 0; i < activities.Count; i++)
                CountTotalSuccessors(project, activities[i].Id, memo, visiting);

            var ordered = new List<SchedulingActivity>(activities);
            ordered.Sort((a, b) =>
            {
                int countA = memo[a.Id];
                int countB = memo[b.Id];
                int c = countB.CompareTo(countA);
                if (c != 0) return c;
                return a.Id.CompareTo(b.Id);
            });
            return ordered;
        }

        private static int CountTotalSuccessors(
            SchedulingProjectData project,
            int activityId,
            Dictionary<int, int> memo,
            HashSet<int> visiting)
        {
            int cached;
            if (memo.TryGetValue(activityId, out cached))
                return cached;

            if (!visiting.Add(activityId))
                return 0;

            var activity = project.GetActivity(activityId);
            if (activity == null || activity.SuccessorIds == null || activity.SuccessorIds.Count == 0)
            {
                visiting.Remove(activityId);
                memo[activityId] = 0;
                return 0;
            }

            var visited = new HashSet<int>();
            CollectSuccessors(project, activity.SuccessorIds, visited);

            visiting.Remove(activityId);
            memo[activityId] = visited.Count;
            return visited.Count;
        }

        private static void CollectSuccessors(
            SchedulingProjectData project,
            List<int> successorIds,
            HashSet<int> visited)
        {
            for (int i = 0; i < successorIds.Count; i++)
            {
                int successorId = successorIds[i];
                if (!visited.Add(successorId))
                    continue;

                var successor = project.GetActivity(successorId);
                if (successor != null && successor.SuccessorIds != null && successor.SuccessorIds.Count > 0)
                    CollectSuccessors(project, successor.SuccessorIds, visited);
            }
        }
    }
}
