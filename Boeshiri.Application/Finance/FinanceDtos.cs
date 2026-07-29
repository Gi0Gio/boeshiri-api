using System.ComponentModel.DataAnnotations;
using Boeshiri.Domain.Enums;

namespace Boeshiri.Application.Finance;

public record CreateMovementRequest
{
    [Required]
    public required DateTime Date { get; init; }

    [Required, MaxLength(200)]
    public required string Concept { get; init; }

    [Required]
    public required MovementType Type { get; init; }

    [Range(0.01, 100000000)]
    public decimal Amount { get; init; }
}

public record UpdateMovementRequest
{
    [Required]
    public required DateTime Date { get; init; }

    [Required, MaxLength(200)]
    public required string Concept { get; init; }

    [Required]
    public required MovementType Type { get; init; }

    [Range(0.01, 100000000)]
    public decimal Amount { get; init; }
}

public record MovementDto(Guid Id, DateTime Date, string Concept, MovementType Type, decimal Amount);

/// <summary>Balance general del colectivo (RF-ADM-08).</summary>
public record FinanceSummaryDto(
    decimal Balance,
    decimal TotalIncome,
    decimal TotalExpense,
    IReadOnlyList<MovementDto> Movements);
