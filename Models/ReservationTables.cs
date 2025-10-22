using System.ComponentModel.DataAnnotations;

namespace FYP.Models
{
    public class ReservationTables  
    {
        [Key]
        public int ReservationTablesID { get; set; }
        [Required]
        public int ReservationID { get; set; }
        public Reservation Reservation { get; set; }
        [Required]
        public int TableID { get; set; }
        public Table Table { get; set; }

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
