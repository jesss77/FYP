using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FYP.Data;
using FYP.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FYP.Controllers
{
    [Authorize(Roles = "employee,manager,admin")]
    public class ReservationCalendarController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReservationCalendarController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(DateTime? date)
        {
            var selectedDate = date ?? DateTime.UtcNow.Date;

            // Get business hours from Settings
            var businessDayStart = new TimeSpan(9, 0, 0);
            var businessDayEnd = new TimeSpan(22, 0, 0);

            var openingHoursSetting = await _context.Settings
                .FirstOrDefaultAsync(s => s.Key == "Opening Hours");

            if (openingHoursSetting != null)
            {
                // Parse "10:00 - 22:00" format
                var parts = openingHoursSetting.Value.Split('-', StringSplitOptions.TrimEntries);
                if (parts.Length == 2)
                {
                    if (TimeSpan.TryParse(parts[0], out var start))
                        businessDayStart = start;
                    if (TimeSpan.TryParse(parts[1], out var end))
                        businessDayEnd = end;
                }
            }

            // Get all tables as resources, sorted numerically by TableNumber
            var tables = await _context.Tables
                .Where(t => t.IsAvailable)
                .OrderBy(t => t.TableNumber)
                .ToListAsync();

            var resources = tables.Select(t => new CalendarResource
            {
                Id = t.TableID,
                Title = $"Table {t.TableNumber}",
                Order = t.TableNumber,
                Capacity = t.Capacity
            }).ToList();

            // Get reservations for selected date
            var reservations = await _context.Reservations
                .Include(r => r.Customer)
                .Include(r => r.Guest)
                .Include(r => r.ReservationStatus)
                .Include(r => r.ReservationTables)
                    .ThenInclude(rt => rt.Table)
                // Compare by ReservedFor range to avoid using the unmapped ReservationDate property
                .Where(r => r.ReservedFor >= selectedDate && r.ReservedFor < selectedDate.AddDays(1))
                .ToListAsync();

            // Create events from reservations
            var events = new List<CalendarEvent>();
            var allowedStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "confirmed",
                "seated",
                "completed",
                "cancelled"
            };

            foreach (var r in reservations)
            {
                var customerName = r.Guest != null
                    ? $"{r.Guest.FirstName} {r.Guest.LastName}"
                    : r.Customer != null
                        ? $"{r.Customer.FirstName} {r.Customer.LastName}"
                        : "Unknown";

                var statusName = r.ReservationStatus.StatusName ?? string.Empty;

                if (!allowedStatuses.Contains(statusName))
                    continue;

                var statusClass = statusName.ToLower() switch
                {
                    "confirmed" => "event-confirmed",
                    "pending" => "event-pending",
                    "seated" => "event-seated",
                    "completed" => "event-completed",
                    "cancelled" => "event-cancelled",
                    _ => "event-default"
                };

                // Create an event for each table in the reservation
                foreach (var rt in r.ReservationTables)
                {
                    events.Add(new CalendarEvent
                    {
                        Id = r.ReservationID,
                        Title = customerName,
                        Start = selectedDate.Add(r.ReservationTime),
                        End = selectedDate.Add(r.ReservationTime).AddMinutes(r.Duration),
                        ResourceId = rt.TableID,
                        ClassName = statusClass,
                        PartySize = r.PartySize,
                        Status = statusName,
                        IsWalkIn = !r.ReservationType
                    });
                }
            }

            // Serialize for JavaScript with camelCase property names (required by FullCalendar)
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            ViewData["ResourcesJson"] = JsonSerializer.Serialize(resources, jsonOptions);
            ViewData["EventsJson"] = JsonSerializer.Serialize(events, jsonOptions);
            // Format as HH:mm:ss for FullCalendar (needs leading zeros for hours)
            ViewData["BusinessDayStart"] = $"{businessDayStart.Hours:D2}:{businessDayStart.Minutes:D2}:00";
            ViewData["BusinessDayEnd"] = $"{businessDayEnd.Hours:D2}:{businessDayEnd.Minutes:D2}:00";
            ViewData["SelectedDate"] = selectedDate;

            // Get all tables for dropdown (for changing assigned tables)
            var allTables = await _context.Tables
                .Where(t => t.IsAvailable)
                .OrderBy(t => t.TableNumber)
                .ToListAsync();
            
            // Get joined tables
            var joinedTables = await _context.TablesJoins
                .Include(tj => tj.PrimaryTable)
                .Include(tj => tj.JoinedTable)
                .ToListAsync();
            
            ViewData["AllTables"] = allTables;
            ViewData["JoinedTables"] = joinedTables;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableTables(DateTime date, string time, int duration, int currentReservationId)
        {
            try
            {
                // Parse time
                if (!TimeSpan.TryParse(time, out var reservationTime))
                {
                    return BadRequest(new { success = false, message = "Invalid time format" });
                }

                // Build time window for the reservation
                var startUtc = new DateTime(date.Year, date.Month, date.Day, reservationTime.Hours, reservationTime.Minutes, 0, DateTimeKind.Utc);
                var endUtc = startUtc.AddMinutes(duration);

                // Get all available tables
                var allTables = await _context.Tables
                    .Where(t => t.IsAvailable)
                    .OrderBy(t => t.TableNumber)
                    .ToListAsync();

                // Get all reservations for the same date (excluding current reservation)
                var existingReservations = await _context.Reservations
                    .Include(r => r.ReservationTables)
                    .Where(r => r.ReservedFor.Date == date.Date && r.ReservationID != currentReservationId)
                    .Select(r => new
                    {
                        r.ReservationID,
                        r.ReservedFor,
                        r.ReservationTime,
                        r.Duration,
                        TableIds = r.ReservationTables.Select(rt => rt.TableID).ToList()
                    })
                    .ToListAsync();

                // Find available tables (no time overlap)
                var availableTables = new List<object>();
                var availableTableIds = new HashSet<int>();

                foreach (var table in allTables)
                {
                    bool hasOverlap = false;

                    foreach (var existing in existingReservations)
                    {
                        if (existing.TableIds.Contains(table.TableID))
                        {
                            var existingStart = new DateTime(
                                existing.ReservedFor.Year,
                                existing.ReservedFor.Month,
                                existing.ReservedFor.Day,
                                existing.ReservationTime.Hours,
                                existing.ReservationTime.Minutes, 0, DateTimeKind.Utc);
                            var existingEnd = existingStart.AddMinutes(existing.Duration);

                            // Check for time overlap
                            if (startUtc < existingEnd && existingStart < endUtc)
                            {
                                hasOverlap = true;
                                break;
                            }
                        }
                    }

                    if (!hasOverlap)
                    {
                        availableTables.Add(new
                        {
                            id = table.TableID,
                            number = table.TableNumber,
                            capacity = table.Capacity,
                            isJoinable = table.IsJoinable
                        });
                        availableTableIds.Add(table.TableID);
                    }
                }

                // Get available joined table pairs
                var joinedTables = await _context.TablesJoins
                    .Include(tj => tj.PrimaryTable)
                    .Include(tj => tj.JoinedTable)
                    .ToListAsync();

                var availableJoinedTables = joinedTables
                    .Where(tj => availableTableIds.Contains(tj.PrimaryTableID) && availableTableIds.Contains(tj.JoinedTableID))
                    .Select(tj => new
                    {
                        primaryId = tj.PrimaryTableID,
                        joinedId = tj.JoinedTableID,
                        primaryNumber = tj.PrimaryTable.TableNumber,
                        joinedNumber = tj.JoinedTable.TableNumber,
                        totalCapacity = tj.TotalCapacity
                    })
                    .ToList();

                return Json(new
                {
                    success = true,
                    singleTables = availableTables,
                    joinedTables = availableJoinedTables
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}

