using System;

namespace FYP.ViewModels.Reports
{
    public class AveragePartySizeVM
    {
        public DateTime? Date { get; set; }
        public double AveragePartySize { get; set; }
        public int Count { get; set; }
    }
}
