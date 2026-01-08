using System.Collections.Generic;

namespace FYP.ViewModels.Reports
{
    public class FullReportVM
    {
        // Existing useful metrics
        public List<PeakTimeReportVM> PeakTimes { get; set; } = new List<PeakTimeReportVM>();
        public List<CapacityAvailabilityVM> CapacityAvailability { get; set; } = new List<CapacityAvailabilityVM>();
        public List<PeakTableVM> PeakTables { get; set; } = new List<PeakTableVM>();
        public AveragePartySizeVM AveragePartySize { get; set; } = new AveragePartySizeVM();
        public List<PartySizeDistributionVM> PartySizeDistribution { get; set; } = new List<PartySizeDistributionVM>();
        public List<WeekdayHeatmapVM> WeekdayHeatmap { get; set; } = new List<WeekdayHeatmapVM>();
        
        // NEW: More meaningful metrics
        public List<ReservationTimeDistributionVM> ReservationTimeDistribution { get; set; } = new List<ReservationTimeDistributionVM>();
        public List<BookingLeadTimeVM> BookingLeadTime { get; set; } = new List<BookingLeadTimeVM>();
        public List<ReservationStatusDistributionVM> StatusDistribution { get; set; } = new List<ReservationStatusDistributionVM>();
        public List<PeakDayVM> PeakDays { get; set; } = new List<PeakDayVM>();
        
        // Optional filter dates
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        
        // UPDATED KPIs - More meaningful
        public int TotalReservations { get; set; }
        public int TotalGuests { get; set; }
        public int ConfirmedReservations { get; set; }
        public int CancelledReservations { get; set; }
        public double CancellationRate { get; set; }
        public double AverageLeadTimeDays { get; set; }
        public PeakTableVM? TopTable { get; set; }
        public PeakDayVM? BusiestDay { get; set; }
        public int TotalTableTurnovers { get; set; }
        public double AverageGuestsPerDay { get; set; }
        public int WalkInCount { get; set; }
        public int PreBookedCount { get; set; }
        public double NoShowRate { get; set; }
    }
}
