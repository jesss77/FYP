using FYP.Data;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FYP.Controllers
{
    [Authorize(Roles = "admin,manager")]
    public class TablesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TablesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Tables()
        {
            var tables = await _context.Tables
                .Include(t => t.Restaurant)
                .OrderBy(t => t.TableNumber)
                .ToListAsync();

            return View("Tables", tables);
        }

        public IActionResult Index()
        {
            return View("Index");
        }

        public async Task<IActionResult> CreateTable()
        {
            return View("CreateTable");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTable(FYP.Models.Table model)
        {
            try
            {
                ModelState.Remove("CreatedBy");
                ModelState.Remove("CreatedAt");
                ModelState.Remove("UpdatedBy");
                ModelState.Remove("UpdatedAt");
                ModelState.Remove("Restaurant");
                ModelState.Remove("RestaurantID");

                if (!ModelState.IsValid)
                {
                    return View("CreateTable", model);
                }

                var restaurant = await _context.Restaurants.FirstOrDefaultAsync();
                if (restaurant == null)
                {
                    var settings = await _context.Settings.FirstOrDefaultAsync();
                    if (settings == null)
                    {
                        settings = new FYP.Models.Settings
                        {
                            Key = "Name",
                            Value = "Fine O Dine",
                            CreatedBy = User.Identity?.Name ?? "manager",
                            UpdatedBy = User.Identity?.Name ?? "manager",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _context.Settings.Add(settings);
                        await _context.SaveChangesAsync();
                    }

                    restaurant = new FYP.Models.Restaurant
                    {
                        SettingsID = settings.SettingsID,
                        CreatedBy = User.Identity?.Name ?? "manager",
                        UpdatedBy = User.Identity?.Name ?? "manager",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Restaurants.Add(restaurant);
                    await _context.SaveChangesAsync();
                }

                if (await _context.Tables.AnyAsync(t => t.TableNumber == model.TableNumber && t.RestaurantID == restaurant.RestaurantID))
                {
                    ModelState.AddModelError("TableNumber", "A table with this number already exists.");
                    return View("CreateTable", model);
                }

                model.RestaurantID = restaurant.RestaurantID;
                model.Restaurant = restaurant;
                model.CreatedBy = User.Identity?.Name ?? "manager";
                model.UpdatedBy = User.Identity?.Name ?? "manager";
                model.CreatedAt = DateTime.UtcNow;
                model.UpdatedAt = DateTime.UtcNow;

                _context.Tables.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Table {model.TableNumber} created successfully!";
                return RedirectToAction(nameof(Tables));
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View("CreateTable", model);
            }
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

        public async Task<IActionResult> TableJoins()
        {
            var joins = await _context.TablesJoins
                .Include(tj => tj.PrimaryTable)
                .Include(tj => tj.JoinedTable)
                .OrderBy(tj => tj.TablesJoinID)
                .ToListAsync();

            ViewBag.Tables = await _context.Tables.OrderBy(t => t.TableNumber).ToListAsync();
            return View("TableJoins", joins);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTableJoin(int primaryTableId, int joinedTableId)
        {
            if (joinedTableId < 1)
            {
                TempData["Error"] = "You must join at least 2 tables.";
                return RedirectToAction(nameof(TableJoins));
            }

            var primary = await _context.Tables.FindAsync(primaryTableId);
            if (primary == null)
            {
                TempData["Error"] = "Primary table not found.";
                return RedirectToAction(nameof(TableJoins));
            }

            var other = await _context.Tables.FindAsync(joinedTableId);
            if (other == null)
            {
                TempData["Error"] = "Joined table not found.";
                return RedirectToAction(nameof(TableJoins));
            }

            if (!primary.IsJoinable || !other.IsJoinable)
            {
                TempData["Error"] = "Both tables must be joinable.";
                return RedirectToAction(nameof(TableJoins));
            }

            if (primary.TableID == other.TableID)
            {
                TempData["Error"] = "Cannot join a table to itself.";
                return RedirectToAction(nameof(TableJoins));
            }

            var exists = await _context.TablesJoins.AnyAsync(tj => (tj.PrimaryTableID == primary.TableID && tj.JoinedTableID == other.TableID) || (tj.PrimaryTableID == other.TableID && tj.JoinedTableID == primary.TableID));
            if (exists)
            {
                TempData["Info"] = "Tables already joined.";
                return RedirectToAction(nameof(TableJoins));
            }

            _context.TablesJoins.Add(new TablesJoin
            {
                PrimaryTableID = primary.TableID,
                JoinedTableID = other.TableID,
                TotalCapacity = primary.Capacity + other.Capacity,
                CreatedBy = User.Identity?.Name ?? "manager",
                UpdatedBy = User.Identity?.Name ?? "manager",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "Tables joined successfully!";
            return RedirectToAction(nameof(TableJoins));
        }

        public async Task<IActionResult> DeleteTableJoin(int id)
        {
            var join = await _context.TablesJoins.FindAsync(id);
            if (join == null) return NotFound();

            _context.TablesJoins.Remove(join);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Table join removed.";
            return RedirectToAction(nameof(TableJoins));
        }
    }
}
