using System.ComponentModel.DataAnnotations;

namespace QuotesApi.Models.Dtos;

public sealed class AddItemRequest
{
    [Required]
    public int QuoteId { get; init; }
}
