using FYP.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FYP.Pages
{
    public class AboutModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public string BrandName { get; set; }

        public AboutModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public void OnGet()
        {
            BrandName = _db.Settings.FirstOrDefault(s => s.Key == "Name")?.Value ?? "Fine O Dine";
        }
    }
}