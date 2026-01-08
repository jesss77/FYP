using FYP.Data;
using FYP.Models;
using FYP.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FYP.Controllers
{
    [Authorize(Roles = "manager")]
    public class ManagerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IReportService _reportService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly FYP.Services.TableAllocationService _allocationService;

        public ManagerController(ApplicationDbContext context, IReportService reportService, UserManager<ApplicationUser> userManager, FYP.Services.TableAllocationService allocationService)
        {
            _context = context;
            _reportService = reportService;
            _userManager = userManager;
            _allocationService = allocationService;
        }
        public IActionResult Index()
        {
            return View("Index");
        }
        public IActionResult Tables()
        {
            return RedirectToAction("Tables", "Tables");
        }

        public IActionResult CreateTable()
        {
            return RedirectToAction("CreateTable", "Tables");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateTable(FYP.Models.Table model)
        {
            return RedirectToAction("CreateTable", "Tables");
        }

        public async Task<IActionResult> EditTable(int id)
        {
            var table = await _context.Tables.FindAsync(id);
            if (table == null) return NotFound();
            return View("EditTable", table);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTable(int id, FYP.Models.Table model)
        {
            if (id != model.TableID) return BadRequest();

            ModelState.Remove("CreatedBy");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("UpdatedBy");
            ModelState.Remove("UpdatedAt");
            ModelState.Remove("Restaurant");
            ModelState.Remove("RestaurantID");

            if (!ModelState.IsValid)
                return View("EditTable", model);

            var table = await _context.Tables.FindAsync(id);
            if (table == null) return NotFound();

                if (await _context.Tables.AnyAsync(t => t.TableNumber == model.TableNumber && t.RestaurantID == table.RestaurantID && t.TableID != id))
                {
                    ModelState.AddModelError("TableNumber", "A table with this number already exists.");
                    return View("EditTable", model);
                }

            table.TableNumber = model.TableNumber;
            table.Capacity = model.Capacity;
            table.IsJoinable = model.IsJoinable;
            table.IsAvailable = model.IsAvailable;
            table.UpdatedBy = User.Identity?.Name ?? "manager";
            table.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Table {model.TableNumber} updated successfully!";
            return RedirectToAction(nameof(Tables));
        }

        public async Task<IActionResult> DeleteTable(int id)
        {
            var table = await _context.Tables.FindAsync(id);
            if (table == null) return NotFound();

            _context.Tables.Remove(table);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Table {table.TableNumber} deleted successfully!";
            return RedirectToAction(nameof(Tables));
        }

        public async Task<IActionResult> ToggleTableAvailability(int id)
        {
            var table = await _context.Tables.FindAsync(id);
            if (table == null) return NotFound();

            table.IsAvailable = !table.IsAvailable;
            table.UpdatedBy = User.Identity?.Name ?? "manager";
            table.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Table {table.TableNumber} availability updated.";
            return RedirectToAction(nameof(Tables));
        }

        public IActionResult TableJoins()
        {
            return RedirectToAction("TableJoins", "Tables");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateTableJoin(int primaryTableId, int joinedTableId)
        {
            return RedirectToAction("TableJoins", "Tables");
        }

        public IActionResult DeleteTableJoin(int id)
        {
            return RedirectToAction("TableJoins", "Tables");
        }

        // Create walk-in reservation (manager) - redirects to Reservations controller
        public IActionResult CreateWalkIn()
        {
            return RedirectToAction("CreateWalkIn", "Reservations");
        }

        // Reservations - redirects to Reservations controller
        public IActionResult Reservations()
        {
            return RedirectToAction("Index", "Reservations");
        }

    }
}
