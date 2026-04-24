namespace SincoPresupuesto.Application.Presupuestos;

public sealed class RubroReadModel
{
    public Guid RubroId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public Guid? PadreId { get; set; }
    public int Nivel { get; set; }
}
