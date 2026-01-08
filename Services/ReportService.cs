using FYP.Data;
using FYP.Services.Interfaces;
using FYP.ViewModels.Reports;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FYP.Services
{
    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _context;

        public ReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<PartySizeDistributionVM>> GetPartySizeDistributionAsync(DateTime? from = null, DateTime? to = null)
        {
            var q = _context.Reservations.AsQueryable();
            if (from.HasValue) q = q.Where(r => r.ReservedFor >= from.Value.Date);
            if (to.HasValue) q = q.Where(r => r.ReservedFor < to.Value.Date.AddDays(1));

            var data = await q.GroupBy(r => r.PartySize)
                .Select(g => new PartySizeDistributionVM { PartySize = g.Key, Count = g.Count() })
                .OrderBy(x => x.PartySize)
                .ToListAsync();

            return data;
        }

        public async Task<List<WeekdayHeatmapVM>> GetWeekdayHeatmapAsync(DateTime? from = null, DateTime? to = null)
        {
            var q = _context.Reservations.AsQueryable();
            if (from.HasValue) q = q.Where(r => r.ReservedFor >= from.Value.Date);
            if (to.HasValue) q = q.Where(r => r.ReservedFor < to.Value.Date.AddDays(1));

            // Materialize minimal columns to client and perform grouping in memory
            var list = await q.Select(r => new { Date = r.ReservedFor, r.ReservationTime }).ToListAsync();

            var grouped = list
                .GroupBy(x => new { Weekday = x.Date.DayOfWeek, Hour = x.ReservationTime.Hours })
                .Select(g => new WeekdayHeatmapVM { Weekday = (int)g.Key.Weekday, Hour = g.Key.Hour, Count = g.Count() })
                .ToList();

            return grouped;
        }

        public async Task<List<PeakTimeReportVM>> GetPeakTimesAsync(DateTime? from = null, DateTime? to = null)
        {
            var q = _context.Reservations.AsQueryable();
            if (from.HasValue) q = q.Where(r => r.ReservedFor >= from.Value.Date);
            if (to.HasValue) q = q.Where(r => r.ReservedFor < to.Value.Date.AddDays(1));

            var data = await q
                .GroupBy(r => r.ReservationTime.Hours)
                .Select(g => new PeakTimeReportVM { Hour = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            return data;
        }

        public async Task<List<CapacityAvailabilityVM>> GetCapacityAvailabilityAsync(DateTime? from = null, DateTime? to = null)
        {
            var tablesTotal = await _context.Tables.SumAsync(t => t.Capacity);

            var q = _context.Reservations.AsQueryable();
            if (from.HasValue) q = q.Where(r => r.ReservedFor >= from.Value.Date);
            if (to.HasValue) q = q.Where(r => r.ReservedFor < to.Value.Date.AddDays(1));

            // Use DateDiffDay grouping key so EF can translate to SQL
            var anchor = new DateTime(2000, 1, 1);
            var grouped = await q
                .GroupBy(r => EF.Functions.DateDiffDay(anchor, r.ReservedFor))
                .Select(g => new
                {
                    DayIndex = g.Key,
                    ReservedSeats = g.Sum(r => r.PartySize)
                })
                .OrderBy(x => x.DayIndex)
                .ToListAsync();

            var data = grouped.Select(x => new CapacityAvailabilityVM
            {
                Date = anchor.AddDays(x.DayIndex),
                ReservedSeats = x.ReservedSeats,
                TotalCapacity = tablesTotal
            }).ToList();

            return data;
        }

        public async Task<List<PeakTableVM>> GetPeakTablesAsync(DateTime? from = null, DateTime? to = null)
        {

            var q = _context.ReservationTables
                .Include(rt => rt.Table)
                .Include(rt => rt.Reservation)
                .AsQueryable();

            if (from.HasValue) q = q.Where(rt => rt.Reservation.ReservedFor >= from.Value.Date);
            if (to.HasValue) q = q.Where(rt => rt.Reservation.ReservedFor < to.Value.Date.AddDays(1));

            var data = await q
                .GroupBy(rt => rt.Table.TableNumber)
                .Select(g => new PeakTableVM { TableNumber = g.Key, TimesUsed = g.Count() })
                .OrderByDescending(x => x.TimesUsed)
                .ToListAsync();

            return data;
        }

        public async Task<AveragePartySizeVM> GetAveragePartySizeAsync(DateTime? from = null, DateTime? to = null)
        {
            var q = _context.Reservations.AsQueryable();
            if (from.HasValue) q = q.Where(r => r.ReservedFor >= from.Value.Date);
            if (to.HasValue) q = q.Where(r => r.ReservedFor < to.Value.Date.AddDays(1));

            var overallCount = await q.CountAsync();
            var overallAvg = overallCount == 0 ? 0 : await q.AverageAsync(r => (double)r.PartySize);

            return new AveragePartySizeVM
            {
                Date = null,
                AveragePartySize = Math.Round(overallAvg, 2),
                Count = overallCount
            };
        }

        public async Task<List<GuestVsCustomerVM>> GetGuestVsCustomerAsync(DateTime? from = null, DateTime? to = null)
        {
            var q = _context.Reservations
                .Include(r => r.Customer)
                .AsQueryable();

            if (from.HasValue) q = q.Where(r => r.ReservedFor >= from.Value.Date);
            if (to.HasValue) q = q.Where(r => r.ReservedFor < to.Value.Date.AddDays(1));

            var data = await q
                .Where(r => r.CustomerID != null)
                .GroupBy(r => new { r.Customer.CustomerID, Name = (r.Customer.FirstName + " " + r.Customer.LastName) })
                .Select(g => new GuestVsCustomerVM
                {
                    CustomerName = g.Key.Name,
                    TotalReservations = g.Count(),
                    TotalGuests = g.Sum(r => r.PartySize)
                })
                .OrderByDescending(x => x.TotalReservations)
                .ToListAsync();

            return data;
        }

        // NEW: Reservation Time Distribution (when reservations are FOR - breakfast/lunch/dinner)
        public async Task<List<ReservationTimeDistributionVM>> GetReservationTimeDistributionAsync(DateTime? from = null, DateTime? to = null)
        {
            var q = _context.Reservations.AsQueryable();
            if (from.HasValue) q = q.Where(r => r.ReservedFor >= from.Value.Date);
            if (to.HasValue) q = q.Where(r => r.ReservedFor < to.Value.Date.AddDays(1));

            var reservations = await q.Select(r => new { r.ReservationTime, r.PartySize }).ToListAsync();

            var distribution = new List<ReservationTimeDistributionVM>
            {
                new ReservationTimeDistributionVM { TimeLabel = "Breakfast (6-10 AM)", Count = 0, TotalGuests = 0 },
                new ReservationTimeDistributionVM { TimeLabel = "Lunch (11 AM-3 PM)", Count = 0, TotalGuests = 0 },
                new ReservationTimeDistributionVM { TimeLabel = "Dinner (4-10 PM)", Count = 0, TotalGuests = 0 },
                new ReservationTimeDistributionVM { TimeLabel = "Late Night (After 10 PM)", Count = 0, TotalGuests = 0 }
            };

            foreach (var r in reservations)
            {
                var hour = r.ReservationTime.Hours;
                if (hour >= 6 && hour < 11)
                {
                    distribution[0].Count++;
                    distribution[0].TotalGuests += r.PartySize;
                }
                else if (hour >= 11 && hour < 16)
                {
                    distribution[1].Count++;
                    distribution[1].TotalGuests += r.PartySize;
                }
                else if (hour >= 16 && hour < 22)
                {
                    distribution[2].Count++;
                    distribution[2].TotalGuests += r.PartySize;
                }
                else
                {
                    distribution[3].Count++;
                    distribution[3].TotalGuests += r.PartySize;
                }
            }

            return distribution.Where(d => d.Count > 0).ToList();
        }

        // NEW: Booking Lead Time (how far in advance are reservations made)
        public async Task<List<BookingLeadTimeVM>> GetBookingLeadTimeAsync(DateTime? from = null, DateTime? to = null)
        {
            var q = _context.Reservations.AsQueryable();
            if (from.HasValue) q = q.Where(r => r.ReservedFor >= from.Value.Date);
            if (to.HasValue) q = q.Where(r => r.ReservedFor < to.Value.Date.AddDays(1));

            var reservations = await q.Select(r => new { r.CreatedAt, r.ReservedFor }).ToListAsync();
            var total = reservations.Count;
            if (total == 0) return new List<BookingLeadTimeVM>();

            var leadTimes = new List<BookingLeadTimeVM>
            {
                new BookingLeadTimeVM { TimeframeLabel = "Same Day", Count = 0 },
                new BookingLeadTimeVM { TimeframeLabel = "1-3 Days Ahead", Count = 0 },
                new BookingLeadTimeVM { TimeframeLabel = "4-7 Days Ahead", Count = 0 },
                new BookingLeadTimeVM { TimeframeLabel = "1-2 Weeks Ahead", Count = 0 },
                new BookingLeadTimeVM { TimeframeLabel = "2+ Weeks Ahead", Count = 0 }
            };

            foreach (var r in reservations)
            {
                var days = (r.ReservedFor.Date - r.CreatedAt.Date).TotalDays;
                if (days < 1) leadTimes[0].Count++;
                else if (days <= 3) leadTimes[1].Count++;
                else if (days <= 7) leadTimes[2].Count++;
                else if (days <= 14) leadTimes[3].Count++;
                else leadTimes[4].Count++;
            }

            leadTimes.ForEach(lt => lt.Percentage = Math.Round((double)lt.Count / total * 100, 1));
            return leadTimes.Where(lt => lt.Count > 0).ToList();
        }

        // NEW: Status Distribution
        public async Task<List<ReservationStatusDistributionVM>> GetStatusDistributionAsync(DateTime? from = null, DateTime? to = null)
        {
            var q = _context.Reservations.Include(r => r.ReservationStatus).AsQueryable();
            if (from.HasValue) q = q.Where(r => r.ReservedFor >= from.Value.Date);
            if (to.HasValue) q = q.Where(r => r.ReservedFor < to.Value.Date.AddDays(1));

            var total = await q.CountAsync();
            if (total == 0) return new List<ReservationStatusDistributionVM>();

            var distribution = await q
                .GroupBy(r => r.ReservationStatus.StatusName)
                .Select(g => new ReservationStatusDistributionVM
                {
                    StatusName = g.Key,
                    Count = g.Count(),
                    Percentage = Math.Round((double)g.Count() / total * 100, 1)
                })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            return distribution;
        }

        // NEW: Peak Days
        public async Task<List<PeakDayVM>> GetPeakDaysAsync(DateTime? from = null, DateTime? to = null, int topCount = 10)
        {
            var q = _context.Reservations.AsQueryable();
            if (from.HasValue) q = q.Where(r => r.ReservedFor >= from.Value.Date);
            if (to.HasValue) q = q.Where(r => r.ReservedFor < to.Value.Date.AddDays(1));

            var reservations = await q.Select(r => new { r.ReservedFor, r.PartySize }).ToListAsync();

            var peakDays = reservations
                .GroupBy(r => r.ReservedFor.Date)
                .Select(g => new PeakDayVM
                {
                    Date = g.Key,
                    DayOfWeek = g.Key.DayOfWeek.ToString(),
                    ReservationCount = g.Count(),
                    TotalGuests = g.Sum(x => x.PartySize)
                })
                .OrderByDescending(x => x.ReservationCount)
                .Take(topCount)
                .ToList();

            return peakDays;
        }

        public async Task<FullReportVM> GetFullReportAsync(DateTime? from = null, DateTime? to = null)
        {
            var full = new FullReportVM
            {
                PeakTimes = await GetPeakTimesAsync(from, to),
                CapacityAvailability = await GetCapacityAvailabilityAsync(from, to),
                PeakTables = await GetPeakTablesAsync(from, to),
                AveragePartySize = await GetAveragePartySizeAsync(from, to),
                PartySizeDistribution = await GetPartySizeDistributionAsync(from, to),
                WeekdayHeatmap = await GetWeekdayHeatmapAsync(from, to),
                
                // NEW Metrics
                ReservationTimeDistribution = await GetReservationTimeDistributionAsync(from, to),
                BookingLeadTime = await GetBookingLeadTimeAsync(from, to),
                StatusDistribution = await GetStatusDistributionAsync(from, to),
                PeakDays = await GetPeakDaysAsync(from, to, 7) // Top 7 days
            };

            full.FromDate = from;
            full.ToDate = to;

            // Improved KPIs
            var rQuery = _context.Reservations.Include(r => r.ReservationStatus).AsQueryable();
            if (from.HasValue) rQuery = rQuery.Where(r => r.ReservedFor >= from.Value.Date);
            if (to.HasValue) rQuery = rQuery.Where(r => r.ReservedFor < to.Value.Date.AddDays(1));

            var allReservations = await rQuery.ToListAsync();
            var total = allReservations.Count;

            full.TotalReservations = total;
            full.TotalGuests = allReservations.Sum(r => r.PartySize);
            full.ConfirmedReservations = allReservations.Count(r => r.ReservationStatus.StatusName == "Confirmed");
            full.CancelledReservations = allReservations.Count(r => r.ReservationStatus.StatusName == "Cancelled");
            full.CancellationRate = total > 0 ? Math.Round((double)full.CancelledReservations / total * 100, 1) : 0;
            
            // Booking lead time average
            if (allReservations.Any())
            {
                var leadTimes = allReservations.Select(r => (r.ReservedFor.Date - r.CreatedAt.Date).TotalDays);
                full.AverageLeadTimeDays = Math.Round(leadTimes.Average(), 1);
            }

            full.TopTable = full.PeakTables.FirstOrDefault();
            full.BusiestDay = full.PeakDays.FirstOrDefault();
            
            // Table turnovers
            full.TotalTableTurnovers = await _context.ReservationTables
                .Where(rt => from.HasValue ? rt.Reservation.ReservedFor >= from.Value.Date : true)
                .Where(rt => to.HasValue ? rt.Reservation.ReservedFor < to.Value.Date.AddDays(1) : true)
                .CountAsync();

            // Average guests per day
            if (from.HasValue && to.HasValue)
            {
                var days = (to.Value.Date - from.Value.Date).Days + 1;
                full.AverageGuestsPerDay = days > 0 ? Math.Round((double)full.TotalGuests / days, 1) : 0;
            }

            // Walk-in vs Pre-booked
            full.WalkInCount = allReservations.Count(r => r.ReservationType == false);
            full.PreBookedCount = allReservations.Count(r => r.ReservationType == true);

            // No-show rate (seated / confirmed ratio)
            var seatedCount = allReservations.Count(r => r.ReservationStatus.StatusName == "Seated" || r.ReservationStatus.StatusName == "Completed");
            full.NoShowRate = full.ConfirmedReservations > 0 ? Math.Round((1.0 - ((double)seatedCount / full.ConfirmedReservations)) * 100, 1) : 0;

            return full;
        }
    }
}
