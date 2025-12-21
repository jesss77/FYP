using System.Collections.Generic;

namespace FYP.ViewModels.Reports
{
    public class ReportPdfRequestVM
    {
        // key: chart identifier, value: base64 image data (data:image/png;base64,...)
        public Dictionary<string, string> Charts { get; set; } = new Dictionary<string, string>();

        public string? Title { get; set; }
        public string? SubTitle { get; set; }
        public string? FromDate { get; set; }
        public string? ToDate { get; set; }
        public string? GeneratedBy { get; set; }
    }
}
