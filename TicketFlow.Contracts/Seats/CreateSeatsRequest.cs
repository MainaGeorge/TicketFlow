using System.ComponentModel.DataAnnotations;

namespace TicketFlow.Contracts.Seats;

public sealed class CreateSeatsRequest
{
    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<CreateSeatRequest> Seats { get; init; } = [];
}

public sealed class CreateSeatRequest
{
    [Required]
    [MaxLength(10)]
    public string Row { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int? Number { get; init; }

    [Range(1, double.MaxValue)]
    public decimal? Price { get; init; }
}
