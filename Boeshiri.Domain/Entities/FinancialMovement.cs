using Boeshiri.Domain.Enums;

namespace Boeshiri.Domain.Entities;

/// <summary>
/// Movimiento del balance del colectivo (§10.4). El balance se deriva de la suma
/// de ingresos menos egresos. Solo el Tesorero puede crearlos/editarlos (RF-ADM-08).
/// </summary>
public class FinancialMovement
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Fecha contable del movimiento.</summary>
    public DateTime Date { get; set; }

    public required string Concept { get; set; }
    public MovementType Type { get; set; }

    /// <summary>Importe, siempre positivo; el signo lo da <see cref="Type"/>.</summary>
    public decimal Amount { get; set; }

    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
