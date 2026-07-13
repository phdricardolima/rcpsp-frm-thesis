using System;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using RCPSP.Scheduling.Model;

namespace RCPSP.Scheduling.Cpm
{
    public sealed class PertCpmCalculator : ICpmCalculator
    {
        public void Compute(SchedulingProjectData project)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            var activities = project.GetNonSummaryActivities();
            if (activities.Count == 0)
                return;

            int n = activities.Count;

            for (int x = 0; x < n; x++)
            {
                var act = activities[x];

                if (act.PredecessorIds == null || act.PredecessorIds.Count == 0)
                {
                    act.ES = 0;
                }
                else
                {
                    int maxEF = 0;

                    foreach (int pred in act.PredecessorIds)
                    {
                        var predAct = project.GetActivity(pred);
                        if (predAct != null && predAct.EF > maxEF)
                            maxEF = predAct.EF;
                    }

                    act.ES = maxEF;
                }

                act.EF = act.ES + act.Duration;
            }

            int makespan = activities.Max(a => a.EF);

            for (int x = n - 1; x >= 0; x--)
            {
                var act = activities[x];

                if (act.SuccessorIds == null || act.SuccessorIds.Count == 0)
                {
                    act.LF = makespan;
                }
                else
                {
                    int minLS = int.MaxValue;

                    foreach (int suc in act.SuccessorIds)
                    {
                        var sucAct = project.GetActivity(suc);
                        if (sucAct != null && sucAct.LS < minLS)
                            minLS = sucAct.LS;
                    }

                    act.LF = (minLS != int.MaxValue) ? minLS : makespan;
                }

                act.LS = act.LF - act.Duration;
                act.Slack = act.LS - act.ES;
            }

        }

    }
}
