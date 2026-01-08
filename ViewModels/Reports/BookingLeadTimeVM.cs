namespace FYP.ViewModels.Reports
{
    public class BookingLeadTimeVM
    {
        public string TimeframeLabel { get; set; } = string.Empty; // e.g., "Same Day", "1-3 Days", "1-2 Weeks", "2+ Weeks"
        public int Count { get; set; }
        public double Percentage { get; set; }
    }
}
