using System.ComponentModel.DataAnnotations;

namespace TicketFlow.Contracts.DTOs;

public class CreateSeatRequest
{
        [Required]
        [MaxLength(10)]
        public string Row { get; set; } = string.Empty;

        [Range(1, int.MaxValue)]
        public int? Number { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? Price { get; set; }
}
