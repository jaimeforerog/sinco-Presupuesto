using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SincoPresupuesto.Domain.SharedKernel;

namespace SincoPresupuesto.Api.ExceptionHandlers;

/// <summary>
/// Mapea subclases de <see cref="DominioException"/> a respuestas HTTP con <see cref="ProblemDetails"/>.
/// Criterios (ver specs §9 de slices 01–03):
/// - 400: datos mal formados o inválidos (campos requeridos, formato, rango).
/// - 404: recursos inexistentes (presupuesto no cargable desde el stream).
/// - 409: conflicto lógico-estado (unicidad, estado incompatible, referencia interna inexistente).
/// </summary>
public sealed class DomainExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DominioException dom)
        {
            return false;
        }

        var (status, title) = Mapear(dom);

        var problem = new ProblemDetails
        {
            Type = $"https://sinco-presupuesto/errors/{dom.GetType().Name}",
            Title = title,
            Status = status,
            Detail = dom.Message,
            Instance = httpContext.Request.Path,
        };
        problem.Extensions["errorCode"] = dom.GetType().Name;

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private static (int Status, string Title) Mapear(DominioException e) => e switch
    {
        // 404 — recurso no existe
        PresupuestoNoEncontradoException => (StatusCodes.Status404NotFound, "Presupuesto no encontrado"),

        // 409 — conflicto lógico-estado
        PresupuestoNoEsBorradorException => (StatusCodes.Status409Conflict, "Estado del presupuesto no permite la operación"),
        CodigoRubroDuplicadoException => (StatusCodes.Status409Conflict, "Código de rubro duplicado"),
        RubroPadreNoExisteException => (StatusCodes.Status409Conflict, "Rubro padre no existe"),
        RubroNoExisteException => (StatusCodes.Status409Conflict, "Rubro destino no existe"),
        RubroEsAgrupadorException => (StatusCodes.Status409Conflict, "Rubro destino es agrupador"),
        TenantYaConfiguradoException => (StatusCodes.Status409Conflict, "Tenant ya configurado"),

        // 400 — datos mal formados o inválidos
        CampoRequeridoException => (StatusCodes.Status400BadRequest, "Campo requerido"),
        PeriodoInvalidoException => (StatusCodes.Status400BadRequest, "Periodo inválido"),
        ProfundidadMaximaFueraDeRangoException => (StatusCodes.Status400BadRequest, "Profundidad máxima fuera de rango"),
        CodigoMonedaInvalidoException => (StatusCodes.Status400BadRequest, "Código de moneda inválido"),
        CodigoRubroInvalidoException => (StatusCodes.Status400BadRequest, "Código de rubro inválido"),
        CodigoHijoNoExtiendeAlPadreException => (StatusCodes.Status400BadRequest, "Código de hijo no extiende al padre"),
        ProfundidadExcedidaException => (StatusCodes.Status400BadRequest, "Profundidad excedida"),
        MonedasDistintasException => (StatusCodes.Status400BadRequest, "Operación entre monedas distintas"),
        FactorDeConversionInvalidoException => (StatusCodes.Status400BadRequest, "Factor de conversión inválido"),
        MontoNegativoException => (StatusCodes.Status400BadRequest, "Monto negativo"),

        _ => (StatusCodes.Status400BadRequest, "Violación de invariante de dominio"),
    };
}
