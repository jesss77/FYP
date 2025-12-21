using System.Collections.Generic;

namespace FYP.ViewModels.Reports
{
    public class ReportPdfDataVM
    {
        // Parsed images as byte arrays could be useful but QuestPDF can accept base64 directly.
        public Dictionary<string, string> Charts { get; set; } = new Dictionary<string, string>();

        public string Title { get; set; } = "Admin Reports";
        public string SubTitle { get; set; } = string.Empty;

        // Any additional metadata
        public Dictionary<string, string> KPIs { get; set; } = new Dictionary<string, string>();
        public System.DateTime GeneratedAt { get; set; } = System.DateTime.UtcNow;
        public string? GeneratedBy { get; set; }
        public string? FromDate { get; set; }
        public string? ToDate { get; set; }
    }
}
