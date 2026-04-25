namespace SincoPresupuesto.Domain.Presupuestos.Commands;

/// <summary>
/// Comando de dominio: aprobar el presupuesto, congelando su baseline y bloqueando
/// modificaciones estructurales posteriores. Spec: slices/05-aprobar-presupuesto/spec.md §2.
/// </summary>
/// <param name="AprobadoPor">Usuario que aprueba. Vacío/whitespace/null → "sistema" al emitir.</param>
public sealed record AprobarPresupuesto(
    string AprobadoPor = "sistema");
