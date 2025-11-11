using System.ComponentModel.DataAnnotations;

namespace FYP.Models
{
    public class Reservation
    {
        [Key]
        public int ReservationID { get; set; }
        [Required]
        public int CustomerID { get; set; }
        public Customer Customer { get; set; }
        public int RestaurantID { get; set; }
        public Restaurant Restaurant { get; set; }
        [Required]
        public DateTime ReservationDate { get; set; }
        [Required]
        public TimeSpan ReservationTime { get; set; }
        [Required, Range(60, int.MaxValue)]
        public int Duration { get; set; } // Duration in minutes
        public string? Notes { get; set; }

        // Guest flow fields
        public bool IsGuest { get; set; } = false;
        [StringLength(200)]
        public string? GuestName { get; set; }
        [EmailAddress, StringLength(255)]
        public string? GuestEmail { get; set; }
        [Phone, StringLength(20)]
        public string? GuestPhone { get; set; }

        [Required]
        public int ReservationStatusID { get; set; }
        public ReservationStatus ReservationStatus { get; set; }
        [Required]
        public bool ReservationType { get; set; } // True for Pre-booked , False for Walk-in
        [Required, Range(1, int.MaxValue)]
        public int PartySize { get; set; }
        public ICollection<ReservationTables> ReservationTables { get; set; }
        public ICollection<ReservationLog> ReservationLogs { get; set; }
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
