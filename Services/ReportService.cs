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

        public async Task<FullReportVM> GetFullReportAsync(DateTime? from = null, DateTime? to = null)
        {
            var full = new FullReportVM
            {
                PeakTimes = await GetPeakTimesAsync(from, to),
                CapacityAvailability = await GetCapacityAvailabilityAsync(from, to),
                PeakTables = await GetPeakTablesAsync(from, to),
                AveragePartySize = await GetAveragePartySizeAsync(from, to),
                GuestVsCustomer = await GetGuestVsCustomerAsync(from, to)
            };

            full.FromDate = from;
            full.ToDate = to;

            // KPIs
            var rQuery = _context.Reservations.AsQueryable();
            if (from.HasValue) rQuery = rQuery.Where(r => r.ReservedFor >= from.Value.Date);
            if (to.HasValue) rQuery = rQuery.Where(r => r.ReservedFor < to.Value.Date.AddDays(1));

            full.TotalReservations = await rQuery.CountAsync();
            full.TotalGuests = await rQuery.SumAsync(r => r.PartySize);
            full.TotalCustomers = await rQuery.Where(r => r.CustomerID != null).Select(r => r.CustomerID).Distinct().CountAsync();
            full.TopTable = full.PeakTables.FirstOrDefault();
            full.PartySizeDistribution = await GetPartySizeDistributionAsync(from, to);
            full.WeekdayHeatmap = await GetWeekdayHeatmapAsync(from, to);

            return full;
        }
    }
}
