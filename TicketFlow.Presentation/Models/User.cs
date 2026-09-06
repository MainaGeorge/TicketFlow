using Microsoft.AspNetCore.Identity;
using TicketFlow.Presentation.DTOs;

namespace TicketFlow.Presentation.Models;

public class User : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public bool IsActive { get; set; } = true;
    public virtual ICollection<Booking> Bookings { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
