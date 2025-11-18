using System.ComponentModel.DataAnnotations;

namespace FYP.Models
{
    public class Reservation
    {
        [Key]
        public int ReservationID { get; set; }

        // Foreign key - can be either Customer or Guest
        public int? CustomerID { get; set; }
        public Customer? Customer { get; set; }

        public int? GuestID { get; set; }
        public Guest? Guest { get; set; }

        // Flag to determine which type
        [Required]
        public bool IsGuest { get; set; } = false;

        [Required]
        public int RestaurantID { get; set; }
        public Restaurant Restaurant { get; set; }

        [Required]
        public DateTime ReservationDate { get; set; }

        [Required]
        public TimeSpan ReservationTime { get; set; }

        [Required, Range(45, int.MaxValue)]
        public int Duration { get; set; } // Duration in minutes

        public string? Notes { get; set; }

        [Required]
        public int ReservationStatusID { get; set; }
        public ReservationStatus ReservationStatus { get; set; }

        [Required]
        public bool ReservationType { get; set; } // True for Pre-booked, False for Walk-in

        [Required, Range(1, int.MaxValue)]
        public int PartySize { get; set; }

        // Navigation properties
        public ICollection<ReservationTables> ReservationTables { get; set; }
        public ICollection<ReservationLog> ReservationLogs { get; set; }

        // Audit fields
        [Required, StringLength(450)]
        public string CreatedBy { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        [StringLength(450)]
        public string UpdatedBy { get; set; }

        [Required]
        public DateTime UpdatedAt { get; set; }
    }
}
