namespace FYP.ViewModels.Reports
{
    public class WeekdayHeatmapVM
    {
        public int Weekday { get; set; } // 0=Sunday..6=Saturday
        public int Hour { get; set; }
        public int Count { get; set; }
    }
}
