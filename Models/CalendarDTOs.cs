namespace FYP.Models
{
    public class CalendarResource
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Order { get; set; }
        public int Capacity { get; set; }
    }

    public class CalendarEvent
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public int ResourceId { get; set; }
        public string? ClassName { get; set; }
        public int PartySize { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsWalkIn { get; set; }
    }
}

