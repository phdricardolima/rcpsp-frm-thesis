namespace RCPSP.Scheduling.Model
{
    public sealed class SchedulingResource
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Capacity { get; set; }

        public SchedulingResource Clone()
        {
            return new SchedulingResource
            {
                Id = Id,
                Name = Name,
                Capacity = Capacity
            };
        }
    }
}
