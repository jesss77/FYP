namespace FYP.ViewModels.Reports
{
    public class ReservationTimeDistributionVM
    {
        public string TimeLabel { get; set; } = string.Empty; // e.g., "Breakfast (6-10)", "Lunch (11-15)", "Dinner (16-22)"
        public int Count { get; set; }
        public int TotalGuests { get; set; }
    }
}
