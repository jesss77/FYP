using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FYP.Models
{
    public class ReservationLog
    {
        [Key]
        public int LogID { get; set; }

        [Required]
        public int ReservationID { get; set; }

        [Required]
        public int ActionTypeID { get; set; }

        public string? OldValue { get; set; }

        [Required, StringLength(450)]
        public string CreatedBy { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(450)]
        public string? UpdatedBy { get; set; }

        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public Reservation Reservation { get; set; }
        public ActionType ActionType { get; set; }
    }
}