using System.ComponentModel.DataAnnotations;

namespace TicketFlow.Contracts.Seats;

public class CreateSeatRequest
{
        [Required]
        [MaxLength(10)]
        public string Row { get; set; } = string.Empty;

        [Range(1, int.MaxValue)]
        public int? Number { get; set; }

        [Range(1, double.MaxValue)]
        public decimal? Price { get; set; }
}
