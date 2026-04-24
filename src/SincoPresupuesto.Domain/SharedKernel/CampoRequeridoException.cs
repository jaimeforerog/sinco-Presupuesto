namespace SincoPresupuesto.Domain.SharedKernel;

/// <summary>
/// Un campo obligatorio del comando llegó nulo o vacío.
/// </summary>
public sealed class CampoRequeridoException : DominioException
{
    public string NombreCampo { get; }

    public CampoRequeridoException(string nombreCampo)
        : base($"El campo '{nombreCampo}' es obligatorio.")
    {
        NombreCampo = nombreCampo;
    }
}
