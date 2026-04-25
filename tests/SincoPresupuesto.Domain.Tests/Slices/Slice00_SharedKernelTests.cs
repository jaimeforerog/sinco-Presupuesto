using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using SincoPresupuesto.Domain.Presupuestos;
using SincoPresupuesto.Domain.SharedKernel;
using Xunit;

namespace SincoPresupuesto.Domain.Tests.Slices;

/// <summary>
/// Slice 00 — SharedKernel (retroactivo).
/// Spec: slices/00-shared-kernel/spec.md §6 (22 escenarios, firmada Q1=Q2=Q3=Q4=(a)).
///
/// Cada escenario de §6 se marcó como **(pinning — ya pasa hoy)** o
/// **(rojo — requiere green)**. Tras green §12 los tests rojos originales ya pasan:
///  - §6.4/§6.6: <see cref="MonedasDistintasException"/> hereda de <see cref="DominioException"/> (Q1).
///  - §6.12: <c>Dinero.En(destino, factor &lt;= 0)</c> lanza <see cref="FactorDeConversionInvalidoException"/> con <c>FactorIntentado</c> (Q2).
///  - §6.17: <c>Moneda.CantidadCodigosIso4217Soportados &gt;= 150</c>.
///  - §6.22: <see cref="MonedasDistintasException"/> asignable a <see cref="DominioException"/> (Q1).
///
/// Nota metodológica (retroactividad, METHODOLOGY §7.3): los tests pinning pasan
/// de inmediato sobre el código actual — excepción documentada a "red siempre falla
/// primero". La fase green para los pinnings es un no-op; solo opera sobre los rojos.
/// </summary>
public class Slice00_SharedKernelTests
{
    // ══════════════════════════════════════════════════════════════════
    // §6.1 – §6.13 — Dinero
    // ══════════════════════════════════════════════════════════════════

    // §6.1 Dinero — suma misma moneda (pinning).
    [Fact]
    public void Dinero_suma_misma_moneda_devuelve_resultado_con_la_misma_moneda()
    {
        // Given
        var a = new Dinero(100m, Moneda.COP);
        var b = new Dinero(50m, Moneda.COP);

        // When
        var suma = a + b;

        // Then
        suma.Should().Be(new Dinero(150m, Moneda.COP));
    }

    // §6.2 Dinero — suma entre monedas distintas (pinning).
    [Fact]
    public void Dinero_suma_entre_monedas_distintas_lanza_MonedasDistintasException()
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

    // §6.3 Dinero — resta misma moneda, incluyendo resultado negativo (pinning).
    [Theory]
    [InlineData(100, 30, 70, false)]
    [InlineData(30, 100, -70, true)]
    public void Dinero_resta_misma_moneda_devuelve_diferencia(
        decimal valorA, decimal valorB, decimal valorEsperado, bool esNegativo)
    {
        // Given
        var a = new Dinero(valorA, Moneda.COP);
        var b = new Dinero(valorB, Moneda.COP);

        // When
        var resultado = a - b;

        // Then
        resultado.Should().Be(new Dinero(valorEsperado, Moneda.COP));
        resultado.EsNegativo.Should().Be(esNegativo);
    }

    // §6.4 Dinero — resta entre monedas distintas (rojo — Q1 aceptada).
    [Fact]
    public void Dinero_resta_entre_monedas_distintas_lanza_MonedasDistintasException_que_es_DominioException()
    {
        // Given
        var cop = new Dinero(100m, Moneda.COP);
        var usd = new Dinero(10m, Moneda.USD);

        // When
        var act = () => { var _ = cop - usd; };

        // Then
        var ex = act.Should().Throw<MonedasDistintasException>()
                    .Which;
        ex.Izquierda.Should().Be(Moneda.COP);
        ex.Derecha.Should().Be(Moneda.USD);

        // Q1 (aceptada): tras green, MonedasDistintasException hereda de DominioException.
        ex.Should().BeAssignableTo<DominioException>(
            "Q1 aceptada: MonedasDistintasException debe heredar de DominioException tras green (spec §12).");
    }

    // §6.5 Dinero — operadores de comparación happy (pinning).
    [Fact]
    public void Dinero_operadores_de_comparacion_con_misma_moneda_devuelven_resultado_esperado()
    {
        // Given
        var a = new Dinero(100m, Moneda.COP);
        var b = new Dinero(50m, Moneda.COP);
        var c = new Dinero(100m, Moneda.COP); // igual a `a`

        // When / Then — operador >
        (a > b).Should().BeTrue();
        (b > a).Should().BeFalse();
        (a > c).Should().BeFalse();

        // When / Then — operador <
        (b < a).Should().BeTrue();
        (a < b).Should().BeFalse();
        (a < c).Should().BeFalse();

        // When / Then — operador >=
        (a >= c).Should().BeTrue();
        (a >= b).Should().BeTrue();
        (b >= a).Should().BeFalse();

        // When / Then — operador <=
        (a <= c).Should().BeTrue();
        (b <= a).Should().BeTrue();
        (a <= b).Should().BeFalse();
    }

    // §6.6 Dinero — comparación entre monedas distintas lanza (rojo — Q1 aceptada).
    // Se parametriza sobre los cuatro operadores. `operador` es una función que ejecuta
    // la comparación y fuerza la evaluación para que la excepción se propague.
    public static IEnumerable<object[]> OperadoresDeComparacionEntreMonedas() =>
        new object[][]
        {
            new object[] { "<",  (Action<Dinero, Dinero>)((x, y) => { var _ = x < y; }) },
            new object[] { ">",  (Action<Dinero, Dinero>)((x, y) => { var _ = x > y; }) },
            new object[] { "<=", (Action<Dinero, Dinero>)((x, y) => { var _ = x <= y; }) },
            new object[] { ">=", (Action<Dinero, Dinero>)((x, y) => { var _ = x >= y; }) },
        };

    [Theory]
    [MemberData(nameof(OperadoresDeComparacionEntreMonedas))]
    public void Dinero_operadores_de_comparacion_entre_monedas_distintas_lanzan_MonedasDistintasException_que_es_DominioException(
        string nombreOperador, Action<Dinero, Dinero> operador)
    {
        // Given
        var cop = new Dinero(100m, Moneda.COP);
        var usd = new Dinero(10m, Moneda.USD);
        _ = nombreOperador; // sólo etiqueta visual en el runner xUnit.

        // When
        var act = () => operador(cop, usd);

        // Then
        var ex = act.Should().Throw<MonedasDistintasException>()
                    .Which;
        ex.Izquierda.Should().Be(Moneda.COP);
        ex.Derecha.Should().Be(Moneda.USD);

        // Q1 (aceptada): tras green, MonedasDistintasException hereda de DominioException.
        ex.Should().BeAssignableTo<DominioException>(
            "Q1 aceptada: MonedasDistintasException debe heredar de DominioException tras green (spec §12).");
    }

    // §6.7 Dinero — multiplicación por factor, ambos lados (pinning).
    [Theory]
    [InlineData(2, 200, false, true, false)]     // positivo
    [InlineData(0, 0, true, false, false)]       // cero
    [InlineData(-1, -100, false, false, true)]   // negativo (contra-asiento)
    public void Dinero_multiplicacion_por_factor_en_ambos_lados_es_conmutativa(
        decimal factor, decimal valorEsperado, bool esCero, bool esPositivo, bool esNegativo)
    {
        // Given
        var a = new Dinero(100m, Moneda.COP);
        var esperado = new Dinero(valorEsperado, Moneda.COP);

        // When
        var porDerecha = a * factor;
        var porIzquierda = factor * a;

        // Then
        porDerecha.Should().Be(esperado);
        porIzquierda.Should().Be(esperado);
        porDerecha.EsCero.Should().Be(esCero);
        porDerecha.EsPositivo.Should().Be(esPositivo);
        porDerecha.EsNegativo.Should().Be(esNegativo);
    }

    // §6.8 Dinero.Cero — neutro aditivo (pinning).
    [Fact]
    public void Dinero_Cero_devuelve_neutro_aditivo_con_la_moneda_indicada()
    {
        // Given / When
        var cero = Dinero.Cero(Moneda.USD);

        // Then
        cero.Should().Be(new Dinero(0m, Moneda.USD));
        cero.EsCero.Should().BeTrue();
        cero.EsPositivo.Should().BeFalse();
        cero.EsNegativo.Should().BeFalse();
    }

    // §6.9 Helpers EsCero/EsPositivo/EsNegativo (pinning).
    [Theory]
    [InlineData(0, true, false, false)]
    [InlineData(0.0001, false, true, false)]
    [InlineData(-0.0001, false, false, true)]
    public void Dinero_helpers_EsCero_EsPositivo_EsNegativo_reflejan_el_signo_del_valor(
        decimal valor, bool esCero, bool esPositivo, bool esNegativo)
    {
        // Given / When
        var d = new Dinero(valor, Moneda.COP);

        // Then
        d.EsCero.Should().Be(esCero);
        d.EsPositivo.Should().Be(esPositivo);
        d.EsNegativo.Should().Be(esNegativo);
    }

    // §6.10 Dinero.En — misma moneda es idempotente (pinning).
    [Fact]
    public void Dinero_En_misma_moneda_ignora_el_factor_y_devuelve_el_mismo_valor()
    {
        // Given
        var a = new Dinero(100m, Moneda.COP);

        // When
        var resultado = a.En(Moneda.COP, 999m); // factor absurdo — debe ignorarse.

        // Then
        resultado.Should().Be(a);
    }

    // §6.11 Dinero.En — otra moneda con factor > 0 (pinning).
    [Fact]
    public void Dinero_En_otra_moneda_con_factor_positivo_aplica_el_factor()
    {
        // Given
        var usd = new Dinero(10m, Moneda.USD);

        // When
        var enCop = usd.En(Moneda.COP, 4200m);

        // Then
        enCop.Should().Be(new Dinero(42_000m, Moneda.COP));
    }

    // §6.12 Dinero.En — factor inválido (Q2 aceptada — green aplicó §12).
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Dinero_En_otra_moneda_con_factor_no_positivo_lanza_FactorDeConversionInvalidoException(decimal factor)
    {
        // Given
        var usd = new Dinero(10m, Moneda.USD);

        // When
        var act = () => usd.En(Moneda.COP, factor);

        // Then
        act.Should().Throw<FactorDeConversionInvalidoException>()
           .Which.FactorIntentado.Should().Be(factor);
        act.Should().Throw<FactorDeConversionInvalidoException>()
           .Which.Should().BeAssignableTo<DominioException>();
    }

    // §6.13 Dinero.ToString — formato "{Valor:0.####} {Codigo}" (pinning).
    // El format `0.####` depende de CultureInfo.CurrentCulture. Se fija la cultura
    // invariante para que los asertos sean deterministas (followup §13 #1).
    [Theory]
    [InlineData(100, "COP", "100 COP")]
    [InlineData(100.5, "COP", "100.5 COP")]
    [InlineData(100.1234, "USD", "100.1234 USD")]
    [InlineData(100.12345, "USD", "100.1235 USD")] // redondea a 4 decimales
    [InlineData(-42, "EUR", "-42 EUR")]
    [InlineData(0, "COP", "0 COP")]
    public void Dinero_ToString_formatea_valor_y_codigo_de_moneda(
        decimal valor, string codigoMoneda, string esperado)
    {
        // Given — cultura invariante.
        var culturaPrevia = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var dinero = new Dinero(valor, new Moneda(codigoMoneda));

            // When
            var texto = dinero.ToString();

            // Then
            texto.Should().Be(esperado);
        }
        finally
        {
            CultureInfo.CurrentCulture = culturaPrevia;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // §6.14 – §6.19 — Moneda
    // ══════════════════════════════════════════════════════════════════

    // §6.14 Moneda — normalización trim + upper (pinning).
    [Theory]
    [InlineData("USD", "USD")]
    [InlineData("usd", "USD")]
    [InlineData("  cop  ", "COP")]
    [InlineData(" eur ", "EUR")]
    public void Moneda_normaliza_codigo_con_trim_y_upperinvariant(string entrada, string esperado)
    {
        // Given / When
        var moneda = new Moneda(entrada);

        // Then
        moneda.Codigo.Should().Be(esperado);
    }

    // §6.15 Moneda — rechaza códigos mal formados (pinning).
    [Theory]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("US1")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Moneda_rechaza_codigos_mal_formados_con_CodigoMonedaInvalidoException(string? entrada)
    {
        // Given / When
        var act = () => new Moneda(entrada!);

        // Then
        act.Should().Throw<CodigoMonedaInvalidoException>();
    }

    // §6.16 Moneda — rechaza códigos bien formados pero no ISO 4217 (pinning).
    [Theory]
    [InlineData("XYZ")]
    [InlineData("ABC")]
    [InlineData("AAA")]
    public void Moneda_rechaza_codigos_tres_letras_no_ISO_4217(string entrada)
    {
        // Given / When
        var act = () => new Moneda(entrada);

        // Then
        act.Should().Throw<CodigoMonedaInvalidoException>()
           .Which.CodigoIntentado.Should().Be(entrada);
    }

    // §6.17 Moneda — cobertura ISO 4217 sampling + cardinalidad
    // (sampling: pinning; cardinalidad: rojo — requiere API nueva en green §12).
    [Theory]
    [InlineData("COP")]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("MXN")]
    [InlineData("CLP")]
    [InlineData("ARS")]
    [InlineData("PEN")]
    [InlineData("BRL")]
    [InlineData("GBP")]
    [InlineData("JPY")]
    [InlineData("CAD")]
    [InlineData("CHF")]
    [InlineData("AUD")]
    [InlineData("ZAR")]
    [InlineData("INR")]
    [InlineData("CNY")]
    [InlineData("KRW")]
    public void Moneda_acepta_codigos_del_sample_ISO_4217(string codigo)
    {
        // Given / When
        var moneda = new Moneda(codigo);

        // Then
        moneda.Codigo.Should().Be(codigo);
    }

    [Fact]
    public void Moneda_CantidadCodigosIso4217Soportados_es_al_menos_150()
    {
        Moneda.CantidadCodigosIso4217Soportados.Should().BeGreaterThanOrEqualTo(150);
    }

    // §6.18 Moneda — igualdad por valor (pinning).
    [Fact]
    public void Moneda_igualdad_por_valor_normalizado_y_mismo_hashcode()
    {
        // Given
        var a = new Moneda("usd");
        var b = new Moneda("USD");
        var c = Moneda.USD;

        // When / Then
        (a == b).Should().BeTrue();
        (a == c).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
        a.GetHashCode().Should().Be(c.GetHashCode());
    }

    // §6.19 Moneda — ToString y conversión implícita a string (pinning).
    [Fact]
    public void Moneda_ToString_y_conversion_implicita_a_string_devuelven_el_Codigo()
    {
        // Given
        var m = Moneda.COP;

        // When / Then
        m.ToString().Should().Be("COP");

        string s = m; // conversión implícita
        s.Should().Be("COP");

        var interpolado = $"{m}";
        interpolado.Should().Be("COP");
    }

    // ══════════════════════════════════════════════════════════════════
    // §6.20 — Requerir
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void Requerir_Campo_happy_y_fallo_cubre_null_vacio_y_whitespace()
    {
        // Given / When / Then — happy path
        Requerir.Campo("acme", "TenantId").Should().Be("acme");

        // Sin trim: el helper solo valida presencia, no normaliza.
        Requerir.Campo("  acme  ", "TenantId").Should().Be("  acme  ");

        // Fallos: null, vacío, whitespace.
        var actNull = () => Requerir.Campo(null, "TenantId");
        actNull.Should().Throw<CampoRequeridoException>()
               .Which.NombreCampo.Should().Be("TenantId");

        var actVacio = () => Requerir.Campo("", "Nombre");
        actVacio.Should().Throw<CampoRequeridoException>()
                .Which.NombreCampo.Should().Be("Nombre");

        var actWhitespace = () => Requerir.Campo("   ", "Codigo");
        actWhitespace.Should().Throw<CampoRequeridoException>()
                     .Which.NombreCampo.Should().Be("Codigo");
    }

    // ══════════════════════════════════════════════════════════════════
    // §6.21 – §6.22 — Jerarquía de excepciones
    // ══════════════════════════════════════════════════════════════════

    // §6.21 Toda excepción del kernel hereda de DominioException y preserva
    // sus propiedades estructuradas (pinning).
    [Fact]
    public void DominioException_contrato_de_jerarquia_y_propiedades_en_todas_las_excepciones_del_kernel()
    {
        // Given — fixtures locales.
        var guid = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var inicio = new DateOnly(2026, 12, 31);
        var fin = new DateOnly(2026, 1, 1);

        // When — instanciar cada excepción concreta del kernel.
        var campoRequerido = new CampoRequeridoException("TenantId");
        var periodoInvalido = new PeriodoInvalidoException(inicio, fin);
        var profundidadFueraDeRango = new ProfundidadMaximaFueraDeRangoException(0, 1, 15);
        var codigoMonedaInvalido = new CodigoMonedaInvalidoException("XYZ");
        var tenantYaConfigurado = new TenantYaConfiguradoException("acme", Moneda.COP);
        var presupuestoNoEsBorrador = new PresupuestoNoEsBorradorException(EstadoPresupuesto.Aprobado);
        var codigoRubroInvalido = new CodigoRubroInvalidoException("1.x");
        var codigoRubroDuplicado = new CodigoRubroDuplicadoException("1.01");
        var codigoHijoNoExtiende = new CodigoHijoNoExtiendeAlPadreException("1.01", "1.01.99.99");
        var rubroPadreNoExiste = new RubroPadreNoExisteException(guid);
        var profundidadExcedida = new ProfundidadExcedidaException(10, 11);
        var presupuestoNoEncontrado = new PresupuestoNoEncontradoException(guid);

        // Then — asignabilidad a DominioException.
        campoRequerido.Should().BeAssignableTo<DominioException>();
        periodoInvalido.Should().BeAssignableTo<DominioException>();
        profundidadFueraDeRango.Should().BeAssignableTo<DominioException>();
        codigoMonedaInvalido.Should().BeAssignableTo<DominioException>();
        tenantYaConfigurado.Should().BeAssignableTo<DominioException>();
        presupuestoNoEsBorrador.Should().BeAssignableTo<DominioException>();
        codigoRubroInvalido.Should().BeAssignableTo<DominioException>();
        codigoRubroDuplicado.Should().BeAssignableTo<DominioException>();
        codigoHijoNoExtiende.Should().BeAssignableTo<DominioException>();
        rubroPadreNoExiste.Should().BeAssignableTo<DominioException>();
        profundidadExcedida.Should().BeAssignableTo<DominioException>();
        presupuestoNoEncontrado.Should().BeAssignableTo<DominioException>();

        // Then — propiedades estructuradas preservadas.
        campoRequerido.NombreCampo.Should().Be("TenantId");

        periodoInvalido.PeriodoInicio.Should().Be(inicio);
        periodoInvalido.PeriodoFin.Should().Be(fin);

        profundidadFueraDeRango.Valor.Should().Be(0);
        profundidadFueraDeRango.MinimoInclusivo.Should().Be(1);
        profundidadFueraDeRango.MaximoInclusivo.Should().Be(15);

        codigoMonedaInvalido.CodigoIntentado.Should().Be("XYZ");

        tenantYaConfigurado.TenantId.Should().Be("acme");
        tenantYaConfigurado.MonedaLocalActual.Should().Be(Moneda.COP);

        presupuestoNoEsBorrador.EstadoActual.Should().Be(EstadoPresupuesto.Aprobado);

        codigoRubroInvalido.CodigoIntentado.Should().Be("1.x");
        codigoRubroDuplicado.CodigoIntentado.Should().Be("1.01");

        codigoHijoNoExtiende.CodigoPadre.Should().Be("1.01");
        codigoHijoNoExtiende.CodigoHijo.Should().Be("1.01.99.99");

        rubroPadreNoExiste.RubroPadreId.Should().Be(guid);

        profundidadExcedida.ProfundidadMaxima.Should().Be(10);
        profundidadExcedida.NivelIntentado.Should().Be(11);

        presupuestoNoEncontrado.PresupuestoId.Should().Be(guid);
    }

    // §6.22 MonedasDistintasException hereda de DominioException (rojo — Q1 aceptada).
    [Fact]
    public void MonedasDistintasException_preserva_propiedades_y_es_DominioException()
    {
        // Given / When
        var ex = new MonedasDistintasException(Moneda.COP, Moneda.USD);

        // Then — propiedades estructuradas.
        ex.Izquierda.Should().Be(Moneda.COP);
        ex.Derecha.Should().Be(Moneda.USD);

        // Q1 (aceptada): tras green, hereda de DominioException (hoy hereda de InvalidOperationException).
        ex.Should().BeAssignableTo<DominioException>(
            "Q1 aceptada: MonedasDistintasException debe migrar su base a DominioException (spec §10 Q1 + §12).");
    }

    // ═══════════════════════════════════════════════════════════════════
    // §6.23 Serialización JSON (System.Text.Json) — round-trip de VOs.
    // Bug descubierto por el visor de eventos (slice _obs-visor-eventos):
    // Moneda no se deserializaba por STJ (Codigo quedaba null) porque
    // el struct tiene constructor parametrizado + propiedad get-only.
    // Fix: [JsonConstructor] en Moneda(string codigo). Followup #23.
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Moneda_round_trip_STJ_preserva_Codigo()
    {
        // Given
        var original = Moneda.COP;

        // When
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<Moneda>(json);

        // Then
        deserialized.Codigo.Should().Be("COP");
        deserialized.Should().Be(original);
    }

    [Fact]
    public void Moneda_se_deserializa_desde_JSON_con_PascalCase_via_PropertyNameCaseInsensitive()
    {
        // Given: JSON con PascalCase, como lo persiste Marten. Marten configura STJ
        // con PropertyNameCaseInsensitive = true por default — replicamos esa opción
        // para que el test refleje las condiciones reales de rehidratación.
        const string jsonPascalCase = """{"Codigo":"USD"}""";
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // When
        var deserialized = JsonSerializer.Deserialize<Moneda>(jsonPascalCase, options);

        // Then — con [JsonConstructor] en Moneda + case-insensitive, se reconstruye OK.
        deserialized.Codigo.Should().Be("USD");
    }

    [Fact]
    public void Dinero_round_trip_STJ_preserva_Valor_y_Moneda()
    {
        // Given
        var original = new Dinero(1_500_000m, Moneda.COP);

        // When
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<Dinero>(json);

        // Then
        deserialized.Valor.Should().Be(1_500_000m);
        deserialized.Moneda.Codigo.Should().Be("COP");
    }
}
