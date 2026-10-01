namespace TicketFlow.Domain.Entities;

public class Booking : Entity
{
    private Booking() 
    {
    }

    public int Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public User User { get; private set; } = null!;
    public int SeatId { get; private set; }
    public Seat Seat { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public string PaymentReference { get; private set; } = string.Empty;

    public static Booking Create(string userid, int seatId, DateTime createdAt)
    {
        var booking = new Booking
        {
            UserId = userid,
            SeatId = seatId,
            CreatedAt = createdAt
        };

        return booking;
    }
}