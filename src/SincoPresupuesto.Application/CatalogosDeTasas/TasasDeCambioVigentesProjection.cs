using Marten.Events.Aggregation;
using SincoPresupuesto.Domain.CatalogosDeTasas.Events;

namespace SincoPresupuesto.Application.CatalogosDeTasas;

/// <summary>
/// Proyección single-stream que mantiene <see cref="TasasDeCambioVigentes"/> con la última
/// tasa por par <c>(MonedaDesde, MonedaHacia)</c> (last-write-wins, INV-CT-1 spec §5).
/// Inline para que `AprobarPresupuestoHandler` (followup #29) pueda consultarla en la misma
/// transacción cuando se habilite la integración multimoneda real.
/// </summary>
public sealed class TasasDeCambioVigentesProjection : SingleStreamProjection<TasasDeCambioVigentes>
{
    public TasasDeCambioVigentes Create(TasaDeCambioRegistrada e) => new()
    {
        Id = CatalogoDeTasasStreamId.Value,
        Tasas = new List<TasaVigente>
        {
            ToVigente(e),
        },
    };

    public void Apply(TasaDeCambioRegistrada e, TasasDeCambioVigentes model)
    {
        var existente = model.Tasas.FirstOrDefault(t =>
            t.MonedaDesde == e.MonedaDesde.Codigo &&
            t.MonedaHacia == e.MonedaHacia.Codigo);

        if (existente is not null)
        {
            existente.Tasa = e.Tasa;
            existente.Fecha = e.Fecha;
            existente.Fuente = e.Fuente;
            existente.RegistradaEn = e.RegistradaEn;
            existente.RegistradaPor = e.RegistradaPor;
        }
        else
        {
            model.Tasas.Add(ToVigente(e));
        }
    }

    private static TasaVigente ToVigente(TasaDeCambioRegistrada e) => new()
    {
        MonedaDesde = e.MonedaDesde.Codigo,
        MonedaHacia = e.MonedaHacia.Codigo,
        Tasa = e.Tasa,
        Fecha = e.Fecha,
        Fuente = e.Fuente,
        RegistradaEn = e.RegistradaEn,
        RegistradaPor = e.RegistradaPor,
    };
}
