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
                "completed"
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

            return View();
        }
    }
}

