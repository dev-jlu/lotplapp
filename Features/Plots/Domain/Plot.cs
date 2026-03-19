using System.ComponentModel.DataAnnotations;

namespace Lotplapp.Features.Plots.Domain;

public class Plot
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>FK → AspNetUsers.Id (string GUID from ASP.NET Core Identity)</summary>
    [Required]
    public string OwnerId { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    public string Currency { get; set; } = "MXN";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
