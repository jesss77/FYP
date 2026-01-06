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

        [Required]
        public int RestaurantID { get; set; }
        public Restaurant Restaurant { get; set; }
        // When the reservation was made (timestamp)
        [Required]
        public DateTime ReservedAt { get; set; }

        // When the reservation is for (the actual date/time the guest will arrive)
        // Map this property to the existing database column `ReservationDate` so the
        // database schema remains unchanged (no new column).
        [System.ComponentModel.DataAnnotations.Schema.Column("ReservationDate")]
        [Required]
        public DateTime ReservedFor { get; set; }

        [Required]
        public TimeSpan ReservationTime { get; set; }

        [Required, Range(45, int.MaxValue)]
        public int Duration { get; set; } // in minutes

        public string? Notes { get; set; }

        [Required]
        public int ReservationStatusID { get; set; }
        public ReservationStatus ReservationStatus { get; set; }

        [Required]
        public bool ReservationType { get; set; } // True for Pre-booked, False for Walk-in

        [Required, Range(1, int.MaxValue)]
        public int PartySize { get; set; }

        // Navigation properties
        public ICollection<ReservationTables> ReservationTables { get; set; } = new List<ReservationTables>();
        public ICollection<ReservationLog> ReservationLogs { get; set; } = new List<ReservationLog>();

        // Computed convenience property used across views/services. Maps to the date portion of ReservedFor.
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public DateTime ReservationDate
        {
            get => ReservedFor.Date;
            set
            {
                // Preserve the existing ReservationTime if set, otherwise default to midnight
                var time = ReservationTime;
                ReservedFor = value.Date + time;
            }
        }

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
