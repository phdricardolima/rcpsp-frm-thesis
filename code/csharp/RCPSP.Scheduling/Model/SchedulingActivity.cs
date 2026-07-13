using System.Collections.Generic;

namespace RCPSP.Scheduling.Model
{
    public sealed class SchedulingActivity
    {
        public int Id { get; set; }
        public int OriginalId { get; set; }


        public int InputOrder { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Duration { get; set; }
        public bool IsDummy { get; set; }
        public bool IsSummary { get; set; }

        public List<int> PredecessorIds { get; set; } = new List<int>();
        public List<int> SuccessorIds { get; set; } = new List<int>();

        public Dictionary<int, int> ResourceDemandByResourceId { get; set; } = new Dictionary<int, int>();

        public int ES { get; set; }
        public int EF { get; set; }
        public int LS { get; set; }
        public int LF { get; set; }
        public int Slack { get; set; }

        public int ScheduledStart { get; set; }
        public int ScheduledFinish { get; set; }

        public SchedulingActivity Clone()
        {
            return new SchedulingActivity
            {
                Id = Id,
                OriginalId = OriginalId,
                InputOrder = InputOrder,
                Name = Name,
                Duration = Duration,
                IsDummy = IsDummy,
                IsSummary = IsSummary,
                PredecessorIds = new List<int>(PredecessorIds),
                SuccessorIds = new List<int>(SuccessorIds),
                ResourceDemandByResourceId = new Dictionary<int, int>(ResourceDemandByResourceId),
                ES = ES,
                EF = EF,
                LS = LS,
                LF = LF,
                Slack = Slack,
                ScheduledStart = ScheduledStart,
                ScheduledFinish = ScheduledFinish
            };
        }
    }
}
