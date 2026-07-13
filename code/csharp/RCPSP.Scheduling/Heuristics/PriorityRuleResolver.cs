using RCPSP.Scheduling.Cpm;
using RCPSP.Scheduling.Model;
using System;
using System.Collections.Generic;

namespace RCPSP.Scheduling.Heuristics
{
    public sealed class PriorityRuleResolver
    {
        private readonly ICpmCalculator _cpmCalculator;

        public PriorityRuleResolver(ICpmCalculator cpmCalculator)
        {
            _cpmCalculator = cpmCalculator ?? throw new ArgumentNullException(nameof(cpmCalculator));
        }

        public List<SchedulingActivity> Resolve(SchedulingProjectData project, string heuristicName)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            _cpmCalculator.Compute(project);

            IHeuristicPriorityRule rule = HeuristicFactory.Create(heuristicName);
            return rule.OrderActivities(project);
        }
    }
}
