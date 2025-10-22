using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FYP.Models
{
    public class Settings
    {
        [Key]
        public int SettingsID { get; set; }

        [Required, StringLength(100)]
        public string Key { get; set; }

        [Required, StringLength(500)]
        public string Value { get; set; }

        [Required, StringLength(450)]
        public string CreatedBy { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(450)]
        public string? UpdatedBy { get; set; }

        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Restaurant>? Restaurants { get; set; }
    }
}