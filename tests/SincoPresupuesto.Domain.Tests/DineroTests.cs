using FluentAssertions;
using SincoPresupuesto.Domain.SharedKernel;
using Xunit;

namespace SincoPresupuesto.Domain.Tests;

/// <summary>
/// Tests del value object <see cref="Dinero"/> y <see cref="Moneda"/>.
/// Pertenecen al SharedKernel (no a un slice específico). Seed pre-metodología
/// — un slice retroactivo "00-shared-kernel" está en FOLLOWUPS.md.
/// </summary>
public class DineroTests
{
    [Fact]
    public void Suma_de_misma_moneda_funciona()
    {
        // Given
        var a = new Dinero(100m, Moneda.COP);
        var b = new Dinero(50m, Moneda.COP);

        // When
        var suma = a + b;

        // Then
        suma.Should().Be(new Dinero(150m, Moneda.COP));
    }

    [Fact]
    public void Suma_entre_monedas_distintas_lanza_MonedasDistintasException()
    {
        // Given
        var cop = new Dinero(100m, Moneda.COP);
        var usd = new Dinero(10m, Moneda.USD);

        // When
        var act = () => { var _ = cop + usd; };

        // Then
        act.Should().Throw<MonedasDistintasException>()
           .Which.Should().Match<MonedasDistintasException>(ex =>
               ex.Izquierda == Moneda.COP && ex.Derecha == Moneda.USD);
    }

    [Fact]
    public void En_aplica_factor_a_la_moneda_destino()
    {
        // Given
        var usd = new Dinero(10m, Moneda.USD);

        // When
        var enCop = usd.En(Moneda.COP, 4200m);

        // Then
        enCop.Should().Be(new Dinero(42_000m, Moneda.COP));
    }

    [Fact]
    public void En_a_la_misma_moneda_no_aplica_factor()
    {
        // Given
        var cop = new Dinero(100m, Moneda.COP);

        // When
        var resultado = cop.En(Moneda.COP, 999m);

        // Then
        resultado.Should().Be(cop);
    }

    [Theory]
    [InlineData("USD", "USD")]
    [InlineData("usd", "USD")]
    [InlineData("  cop ", "COP")]
    public void Moneda_normaliza_codigo(string entrada, string esperado)
    {
        // Given: código recibido.

        // When
        var moneda = new Moneda(entrada);

        // Then
        moneda.Codigo.Should().Be(esperado);
    }

    [Theory]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("US1")]
    [InlineData("")]
    [InlineData("XYZ")]
    public void Moneda_rechaza_codigo_invalido(string entrada)
    {
        // Given: código inválido.

        // When
        var act = () => new Moneda(entrada);

        // Then
        act.Should().Throw<CodigoMonedaInvalidoException>();
    }
}
