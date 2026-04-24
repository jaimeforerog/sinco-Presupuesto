namespace SincoPresupuesto.Domain.Presupuestos.Commands;

/// <summary>
/// Comando de dominio: agregar un rubro al árbol del presupuesto.
/// <para>
/// El <paramref name="Codigo"/> llega validado desde fuera (el caller puede autogenerarlo o
/// recibir override del usuario); el dominio verifica formato (INV-10), unicidad (INV-11),
/// relación con el padre (INV-F) y profundidad (INV-8).
/// </para>
/// <para>
/// <paramref name="RubroPadreId"/> null ⇒ rubro raíz del árbol del presupuesto.
/// </para>
/// </summary>
/// <param name="Codigo">Código jerárquico del rubro (p.ej. "01" o "01.01").</param>
/// <param name="Nombre">Nombre legible del rubro.</param>
/// <param name="RubroPadreId">Id del rubro padre (opcional). Null para rubros raíz.</param>
public sealed record AgregarRubro(
    string Codigo,
    string Nombre,
    Guid? RubroPadreId = null);
