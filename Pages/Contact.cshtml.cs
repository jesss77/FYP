using FYP.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace FYP.Pages
{
    public class ContactModel : PageModel
    {
        private readonly IEmailService _emailService;

        public ContactModel(IEmailService emailService)
        {
            _emailService = emailService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            public string Name { get; set; }

            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [StringLength(2000)]
            public string Message { get; set; }
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var subject = $"Contact form: {Input.Name}";
            var body = $@"<h3>Contact message</h3>
                          <p><strong>Name:</strong> {System.Net.WebUtility.HtmlEncode(Input.Name)}</p>
                          <p><strong>Email:</strong> {System.Net.WebUtility.HtmlEncode(Input.Email)}</p>
                          <p><strong>Message:</strong><br/>{System.Net.WebUtility.HtmlEncode(Input.Message)}</p>";

            // Send to site admin
            var adminEmail = "admin@localhost"; // replace or fetch from settings
            await _emailService.SendEmailAsync(adminEmail, subject, body);

            TempData["SuccessMessage"] = "Your message has been sent. We'll get back to you shortly.";
            return RedirectToPage("/Contact");
        }
    }
}