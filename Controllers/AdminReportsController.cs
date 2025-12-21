using FYP.Services.Interfaces;
using FYP.ViewModels.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using FYP.Services.Pdf;

namespace FYP.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminReportsController : Controller
    {
        private readonly IReportService _reportService;
        private readonly ILogger<AdminReportsController> _logger;

        public AdminReportsController(IReportService reportService, ILogger<AdminReportsController> logger)
        {
            _reportService = reportService;
            _logger = logger;
        }

        // GET: /AdminReports
        public async Task<IActionResult> Index(DateTime? from, DateTime? to)
        {
            var model = await _reportService.GetFullReportAsync(from, to);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> PeakTimesJson(DateTime? from, DateTime? to)
        {
            var data = await _reportService.GetPeakTimesAsync(from, to);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> CapacityAvailabilityJson(DateTime? from, DateTime? to)
        {
            var data = await _reportService.GetCapacityAvailabilityAsync(from, to);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> PeakTablesJson(DateTime? from, DateTime? to)
        {
            var data = await _reportService.GetPeakTablesAsync(from, to);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> AveragePartySizeJson(DateTime? from, DateTime? to)
        {
            var data = await _reportService.GetAveragePartySizeAsync(from, to);
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> GuestVsCustomerJson(DateTime? from, DateTime? to)
        {
            var data = await _reportService.GetGuestVsCustomerAsync(from, to);
            return Json(data);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        [Consumes("application/json")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> GeneratePdf([FromBody] ReportPdfRequestVM vm, [FromServices] ReportPdfGenerator generator)
        {
            // Build data VM
            var data = new ReportPdfDataVM
            {
                Title = vm.Title ?? "Admin Reports",
                SubTitle = vm.SubTitle ?? string.Empty,
                GeneratedAt = DateTime.UtcNow,
                GeneratedBy = vm.GeneratedBy ?? (User?.Identity?.Name ?? "system"),
                FromDate = vm.FromDate,
                ToDate = vm.ToDate
            };

            // Copy charts
            foreach (var kv in vm.Charts)
            {
                data.Charts[kv.Key] = kv.Value;
            }

            // Add KPI snapshots
            data.KPIs["Total Reservations"] = vm.Charts.ContainsKey("TotalReservations") ? "See chart" : "-";

            var bytes = generator.GeneratePdf(data);
            // Save PDF to wwwroot/reports with timestamp
            try
            {
                var webRoot = HttpContext.RequestServices.GetService(typeof(Microsoft.AspNetCore.Hosting.IWebHostEnvironment)) as Microsoft.AspNetCore.Hosting.IWebHostEnvironment;
                if (webRoot != null)
                {
                    var reportsDir = System.IO.Path.Combine(webRoot.WebRootPath, "reports");
                    if (!System.IO.Directory.Exists(reportsDir)) System.IO.Directory.CreateDirectory(reportsDir);
                    var fileName = $"AdminReports_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
                    var filePath = System.IO.Path.Combine(reportsDir, fileName);
                    await System.IO.File.WriteAllBytesAsync(filePath, bytes);
                }
            }
            catch { /* don't block response on save errors */ }

            return File(bytes, "application/pdf", "AdminReports.pdf");
        }
    }
}
