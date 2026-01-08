using FYP.ViewModels.Reports;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FYP.Services.Interfaces
{
    public interface IReportService
    {
        Task<FullReportVM> GetFullReportAsync(DateTime? from = null, DateTime? to = null);
        Task<List<PeakTimeReportVM>> GetPeakTimesAsync(DateTime? from = null, DateTime? to = null);
        Task<List<CapacityAvailabilityVM>> GetCapacityAvailabilityAsync(DateTime? from = null, DateTime? to = null);
        Task<List<PeakTableVM>> GetPeakTablesAsync(DateTime? from = null, DateTime? to = null);
        Task<AveragePartySizeVM> GetAveragePartySizeAsync(DateTime? from = null, DateTime? to = null);
        
        // Keep for backward compatibility (but won't display in main reports)
        Task<List<GuestVsCustomerVM>> GetGuestVsCustomerAsync(DateTime? from = null, DateTime? to = null);
        
        // NEW: More meaningful metrics
        Task<List<ReservationTimeDistributionVM>> GetReservationTimeDistributionAsync(DateTime? from = null, DateTime? to = null);
        Task<List<BookingLeadTimeVM>> GetBookingLeadTimeAsync(DateTime? from = null, DateTime? to = null);
        Task<List<ReservationStatusDistributionVM>> GetStatusDistributionAsync(DateTime? from = null, DateTime? to = null);
        Task<List<PeakDayVM>> GetPeakDaysAsync(DateTime? from = null, DateTime? to = null, int topCount = 10);
    }
}
