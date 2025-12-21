using System;

namespace FYP.ViewModels.Reports
{
    public class CapacityAvailabilityVM
    {
        public DateTime Date { get; set; }
        public int TotalCapacity { get; set; }
        public int ReservedSeats { get; set; }
        public int Availability => TotalCapacity - ReservedSeats;
    }
}
