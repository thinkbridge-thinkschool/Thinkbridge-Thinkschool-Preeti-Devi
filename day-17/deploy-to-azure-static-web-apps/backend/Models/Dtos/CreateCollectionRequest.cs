using System.ComponentModel.DataAnnotations;

namespace QuotesApi.Models.Dtos;

public sealed class CreateCollectionRequest
{
    [Required]
    [StringLength(80, MinimumLength = 3)]
    public string Name { get; init; } = string.Empty;

    [Required]
    public string OwnerId { get; init; } = string.Empty;
}
