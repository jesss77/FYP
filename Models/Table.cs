using FYP.Models;
using System.ComponentModel.DataAnnotations;

public class Table
{
    [Key]
    public int TableID { get; set; }

    [Required]
    public int TableNumber { get; set; }

    [Required]
    public int Capacity { get; set; }

    public bool IsJoinable { get; set; }
    public bool IsAvailable { get; set; }
    public int RestaurantID { get; set; }
    public Restaurant Restaurant { get; set; }
    public ICollection<TablesJoin> JoinedTables { get; set; } = new List<TablesJoin>();
    public ICollection<ReservationTables> ReservationTables { get; set; } = new List<ReservationTables>();

    [Required, StringLength(450)]
    public string CreatedBy { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(450)]
    public string? UpdatedBy { get; set; }

    [Required]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
