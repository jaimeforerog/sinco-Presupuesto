namespace SincoPresupuesto.Domain.SharedKernel;

/// <summary>
/// Helpers de validación reutilizables para precondiciones de campos del dominio.
/// </summary>
public static class Requerir
{
    /// <summary>
    /// Lanza <see cref="CampoRequeridoException"/> si <paramref name="valor"/> es nulo, vacío o whitespace.
    /// Devuelve el valor sin modificar (útil para chain, p.ej. <c>Requerir.Campo(x, "X").Trim()</c>).
    /// </summary>
    public static string Campo(string? valor, string nombreCampo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new CampoRequeridoException(nombreCampo);
        }
        return valor;
    }
}
