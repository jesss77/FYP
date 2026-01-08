namespace FYP.ViewModels.Reports
{
    public class PeakDayVM
    {
        public DateTime Date { get; set; }
        public string DayOfWeek { get; set; } = string.Empty;
        public int ReservationCount { get; set; }
        public int TotalGuests { get; set; }
    }
}
