using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NSubstitute;
using WayniBcra.Domain;

namespace WayniBcra.ApiTests;

/// <summary>
/// Tests for the query endpoints. Validate routing, status codes, and that the JSON response uses the
/// Spanish field names from the challenge statement (the [JsonPropertyName] contract).
/// </summary>
public sealed class QueryEndpointsTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public QueryEndpointsTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetDebtorByCuit_WhenExists_ReturnsSpanishJsonFields()
    {
        _factory.DebtorRepository
            .GetByIdentificationNumberAsync("20004256337", Arg.Any<CancellationToken>())
            .Returns(new Debtor("20004256337", "05", 807.0m));

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/deudores/20004256337");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // The contract exposes the Spanish names from the statement.
        Assert.Equal("20004256337", root.GetProperty("nro_identificacion").GetString());
        Assert.Equal("05", root.GetProperty("situacion_maxima").GetString());
        Assert.Equal(807.0m, root.GetProperty("suma_total_prestamos").GetDecimal());
    }

    [Fact]
    public async Task GetDebtorByCuit_WhenNotFound_Returns404()
    {
        _factory.DebtorRepository
            .GetByIdentificationNumberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Debtor?)null);

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/deudores/00000000000");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetEntityByCode_WhenExists_ReturnsSpanishJsonFields()
    {
        _factory.FinancialEntityRepository
            .GetByEntityCodeAsync("00007", Arg.Any<CancellationToken>())
            .Returns(new FinancialEntity("00007", 700382.0m));

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/entidades/00007");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("00007", root.GetProperty("codigo_entidad").GetString());
        Assert.Equal(700382.0m, root.GetProperty("suma_total_prestamos").GetDecimal());
    }

    [Fact]
    public async Task GetTopDebtors_ReturnsListWithSpanishFields()
    {
        _factory.DebtorRepository
            .GetTopByTotalFinancingAsync(3, Arg.Any<CancellationToken>())
            .Returns(new List<Debtor>
            {
                new("20000000001", "05", 900.0m),
                new("20000000002", "03", 500.0m),
                new("20000000003", "01", 100.0m)
            });

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/deudores/top/3");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var items = doc.RootElement.EnumerateArray().ToList();

        Assert.Equal(3, items.Count);
        Assert.Equal("20000000001", items[0].GetProperty("nro_identificacion").GetString());
        Assert.Equal("05", items[0].GetProperty("situacion_maxima").GetString());
        Assert.Equal(900.0m, items[0].GetProperty("suma_total_prestamos").GetDecimal());
    }

    [Fact]
    public async Task GetTopDebtors_WithInvalidN_ReturnsProblemDetails400()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/deudores/top/0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // The error is returned as RFC 7807 ProblemDetails, not a bare string.
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("detail").GetString()));
    }
}
