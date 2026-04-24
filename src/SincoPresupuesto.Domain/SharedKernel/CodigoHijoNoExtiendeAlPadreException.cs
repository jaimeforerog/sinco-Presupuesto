namespace SincoPresupuesto.Domain.SharedKernel;

/// <summary>
/// INV-F — El código del hijo no extiende al del padre con exactamente un segmento <c>\.\d{2}</c>.
/// </summary>
public sealed class CodigoHijoNoExtiendeAlPadreException : DominioException
{
    public string CodigoPadre { get; }
    public string CodigoHijo { get; }

    public CodigoHijoNoExtiendeAlPadreException(string codigoPadre, string codigoHijo)
        : base($"El código hijo '{codigoHijo}' no extiende al padre '{codigoPadre}' con exactamente un segmento.")
    {
        CodigoPadre = codigoPadre;
        CodigoHijo = codigoHijo;
    }
}
