using System.Collections.Generic;

namespace FYP.ViewModels.Reports
{
    public class FullReportVM
    {
        public List<PeakTimeReportVM> PeakTimes { get; set; } = new List<PeakTimeReportVM>();
        public List<CapacityAvailabilityVM> CapacityAvailability { get; set; } = new List<CapacityAvailabilityVM>();
        public List<PeakTableVM> PeakTables { get; set; } = new List<PeakTableVM>();
        public AveragePartySizeVM AveragePartySize { get; set; } = new AveragePartySizeVM();
        public List<GuestVsCustomerVM> GuestVsCustomer { get; set; } = new List<GuestVsCustomerVM>();
        public List<PartySizeDistributionVM> PartySizeDistribution { get; set; } = new List<PartySizeDistributionVM>();
        public List<WeekdayHeatmapVM> WeekdayHeatmap { get; set; } = new List<WeekdayHeatmapVM>();
        // Optional filter dates
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        // KPIs
        public int TotalReservations { get; set; }
        public int TotalGuests { get; set; }
        public int TotalCustomers { get; set; }
        public PeakTableVM? TopTable { get; set; }
    }
}
