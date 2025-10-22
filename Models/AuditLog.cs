using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FYP.Models
{
    public class AuditLog
    {
        [Key]
        public int AuditID { get; set; }

        [Required, StringLength(100)]
        public string EntityName { get; set; }

        [Required]
        public int EntityID { get; set; }

        [Required, StringLength(50)]
        public string ActionType { get; set; }

        public string? OldValue { get; set; }
        public string? NewValue { get; set; }

        [Required, StringLength(450)]
        public string CreatedBy { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(450)]
        public string? UpdatedBy { get; set; }

        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}