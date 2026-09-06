using Microsoft.AspNetCore.Identity;

namespace TicketFlow.Domain.Entities;

public class User : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public bool IsActive { get; set; } = true;
    public virtual ICollection<Booking> Bookings { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
