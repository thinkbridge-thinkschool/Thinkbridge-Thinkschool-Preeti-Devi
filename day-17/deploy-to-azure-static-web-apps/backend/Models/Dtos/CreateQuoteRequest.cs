using System.ComponentModel.DataAnnotations;

namespace QuotesApi.Models.Dtos;

public sealed class CreateQuoteRequest
{
    [Required]
    [StringLength(100)]
    public string Author { get; init; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Text { get; init; } = string.Empty;
}