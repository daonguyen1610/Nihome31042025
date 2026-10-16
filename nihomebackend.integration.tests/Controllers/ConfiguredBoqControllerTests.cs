using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public class ConfiguredBoqControllerTests(NihomeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private static BoqEntryField Field(string key, string binding, bool number = false) => new()
    {
        Key = key,
        Binding = binding,
        NameVi = key,
        Name = key,
        NameZh = key,
        NameJa = key,
        Required = binding != "None",
        Type = number ? "Number" : "Text",
        MaxLength = binding == "Code" ? 38 : binding == "Unit" ? 30 : 300,
        DecimalPlaces = binding == "UnitPrice" ? 2 : 4
    };
    private static BoqConfiguration Config() => new()
    {
        Sheets = [new() { Key = "work", NameVi = "Xây dựng", Name = "Construction", NameZh = "施工", NameJa = "工事", Fields =
            [Field("code", "Code"), Field("name", "Name"), Field("unit", "Unit"), Field("quantity", "Quantity", true), Field("price", "UnitPrice", true), Field("length", "None", true)] },
            new() { Key = "reference", NameVi = "Tham khảo", Name = "Reference", NameZh = "参考", NameJa = "参考", IsPriced = false,
                Fields = [Field("code", "Code"), Field("name", "Name"), Field("spec", "None")] }]
    };

    private async Task<(int Catalog, JsonElement Revision, string Hash)> CreateAsync()
    {
        var created = await Client.PostAsJsonAsync("/api/material-rate-catalogs", new
        { code = "BOQ-" + Guid.NewGuid().ToString("N"), name = "BOQ nhà xưởng Nicon", catalogType = "Boq", currency = "VND", isActive = true });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var catalog = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = catalog.GetProperty("id").GetInt32();
        var configured = await Client.PutAsJsonAsync($"/api/material-rate-catalogs/{id}/boq-configuration", new
        { expectedUpdatedAt = catalog.GetProperty("updatedAt").GetString(), configuration = Config() });
        configured.StatusCode.Should().Be(HttpStatusCode.OK, await configured.Content.ReadAsStringAsync());
        var body = await configured.Content.ReadFromJsonAsync<JsonElement>();
        var revision = await Client.PostAsJsonAsync($"/api/material-rate-catalogs/{id}/revisions", new { effectiveFrom = "2044-01-01" });
        revision.StatusCode.Should().Be(HttpStatusCode.Created);
        return (id, await revision.Content.ReadFromJsonAsync<JsonElement>(), body.GetProperty("boqConfigurationHash").GetString()!);
    }
    private static MultipartFormDataContent Upload(byte[] bytes)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", "Nicon-BOQ.xlsx");
        return form;
    }
    private async Task<byte[]> FilledAsync(int catalog, Action<XLWorkbook>? change = null)
    {
        var bytes = await Client.GetByteArrayAsync($"/api/material-rate-catalogs/{catalog}/configured-boq-template?language=en");
        using var book = new XLWorkbook(new MemoryStream(bytes));
        var work = book.Worksheet("work");
        work.Cell(3, 1).Value = "001"; work.Cell(3, 2).Value = "Concrete foundation"; work.Cell(3, 3).Value = "m3";
        work.Cell(3, 4).Value = 2.5; work.Cell(3, 5).Value = 1000.25; work.Cell(3, 6).Value = 3.125;
        var reference = book.Worksheet("reference");
        reference.Cell(3, 1).Value = "001"; reference.Cell(3, 2).Value = "Approved material reference"; reference.Cell(3, 3).Value = "Concrete C30";
        change?.Invoke(book);
        using var output = new MemoryStream(); book.SaveAs(output); return output.ToArray();
    }

    [Fact]
    public async Task DownloadImportManualEditAndApproval_KeepCategoryFieldsAndRejectStaleOrMismatchedWrites()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "ADMIN"));
        var (catalog, revision, hash) = await CreateAsync();
        var id = revision.GetProperty("id").GetInt32();
        var path = $"/api/material-rate-catalogs/{catalog}/revisions/{id}";
        var bytes = await FilledAsync(catalog);
        using var previewForm = Upload(bytes);
        var preview = await Client.PostAsync($"/api/material-rate-catalogs/{catalog}/configured-boq-preview", previewForm);
        preview.StatusCode.Should().Be(HttpStatusCode.OK, await preview.Content.ReadAsStringAsync());
        var parsed = await preview.Content.ReadFromJsonAsync<JsonElement>();
        parsed.GetProperty("rows").GetArrayLength().Should().Be(2);
        using var importForm = Upload(bytes);
        importForm.Add(new StringContent(parsed.GetProperty("sourceHash").GetString()!), "sourceHash");
        importForm.Add(new StringContent(revision.GetProperty("updatedAt").GetString()!), "expectedUpdatedAt");
        var imported = await Client.PostAsync(path + "/configured-boq-import", importForm);
        imported.StatusCode.Should().Be(HttpStatusCode.OK, await imported.Content.ReadAsStringAsync());
        var current = await imported.Content.ReadFromJsonAsync<JsonElement>();
        current.GetProperty("boqRows")[0].GetProperty("values").GetProperty("length").GetString().Should().Be("3.125");
        current.GetProperty("lines").GetArrayLength().Should().Be(1, "reference sheets must not become quote prices");
        current.GetProperty("lines")[0].GetProperty("materialCode").GetString().Should().Be("work.001");
        decimal.Parse(current.GetProperty("totalAmount").GetString()!, System.Globalization.CultureInfo.InvariantCulture).Should().Be(2500.625m);
        var rows = current.GetProperty("boqRows").Deserialize<List<BoqEntryRow>>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        rows[0].Values["price"] = "2000";
        var saved = await Client.PutAsJsonAsync(path + "/configured-boq-rows", new { configurationHash = hash, expectedUpdatedAt = current.GetProperty("updatedAt").GetString(), rows });
        saved.StatusCode.Should().Be(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
        var latest = await saved.Content.ReadFromJsonAsync<JsonElement>();
        (await Client.PutAsJsonAsync(path + "/configured-boq-rows", new { configurationHash = hash, expectedUpdatedAt = current.GetProperty("updatedAt").GetString(), rows })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        rows[0].Values["wrong_category_field"] = "must reject";
        (await Client.PutAsJsonAsync(path + "/configured-boq-rows", new { configurationHash = hash, expectedUpdatedAt = latest.GetProperty("updatedAt").GetString(), rows })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        rows[0].Values.Remove("wrong_category_field");
        (await Client.PutAsJsonAsync(path + "/configured-boq-rows", new { configurationHash = "different", expectedUpdatedAt = latest.GetProperty("updatedAt").GetString(), rows })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.PostAsJsonAsync(path + "/lines", new { itemCode = "X", itemName = "Legacy bypass", unit = "m2", quantity = 1, unitPrice = 1 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var category = await Client.GetFromJsonAsync<JsonElement>($"/api/material-rate-catalogs/{catalog}");
        (await Client.PutAsJsonAsync($"/api/material-rate-catalogs/{catalog}/boq-configuration", new { expectedUpdatedAt = category.GetProperty("updatedAt").GetString(), configuration = Config() })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var approved = await Client.PostAsJsonAsync(path + "/approve", new { note = "Approved configured Nicon BOQ" });
        approved.StatusCode.Should().Be(HttpStatusCode.OK);
        var locked = await approved.Content.ReadFromJsonAsync<JsonElement>();
        (await Client.PutAsJsonAsync(path + "/configured-boq-rows", new { configurationHash = hash, expectedUpdatedAt = locked.GetProperty("updatedAt").GetString(), rows })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var unchanged = await Client.GetFromJsonAsync<JsonElement>(path);
        unchanged.GetProperty("boqRows")[0].GetProperty("values").GetProperty("price").GetString().Should().Be("2000");
    }

    [Theory]
    [InlineData("header")]
    [InlineData("label")]
    [InlineData("sheet")]
    [InlineData("empty")]
    [InlineData("text-number")]
    [InlineData("formula")]
    [InlineData("duplicate")]
    [InlineData("missing")]
    public async Task InvalidWorkbook_IsRejectedWithoutChangingRevision(string error)
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "ADMIN"));
        var (catalog, revision, _) = await CreateAsync();
        var bytes = await FilledAsync(catalog, book =>
        {
            var tab = book.Worksheet("work");
            if (error == "header") tab.Cell(1, 6).Value = "different";
            if (error == "label") tab.Cell(2, 6).Value = "Incorrect visible field label";
            if (error == "sheet") tab.Name = "different";
            if (error == "empty") { tab.Row(3).Clear(); book.Worksheet("reference").Row(3).Clear(); }
            if (error == "text-number") tab.Cell(3, 4).Value = "1,5";
            if (error == "formula") tab.Cell(3, 4).FormulaA1 = "1+1";
            if (error == "duplicate") tab.Row(3).CopyTo(tab.Row(4));
            if (error == "missing") tab.Cell(3, 3).Clear();
        });
        using var content = Upload(bytes);
        var result = await Client.PostAsync($"/api/material-rate-catalogs/{catalog}/configured-boq-preview", content);
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var unchanged = await Client.GetFromJsonAsync<JsonElement>($"/api/material-rate-catalogs/{catalog}/revisions/{revision.GetProperty("id").GetInt32()}");
        unchanged.GetProperty("boqRows").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task DifferentCategoryFileAndUnauthorizedActor_AreRejected()
    {
        (await Client.GetAsync("/api/material-rate-catalogs/1/configured-boq-template")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "ADMIN"));
        var first = await CreateAsync(); var second = await CreateAsync();
        var bytes = await FilledAsync(first.Catalog);
        using var content = Upload(bytes);
        (await Client.PostAsync($"/api/material-rate-catalogs/{second.Catalog}/configured-boq-preview", content)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN"));
        (await Client.PutAsJsonAsync($"/api/material-rate-catalogs/{first.Catalog}/boq-configuration", new { expectedUpdatedAt = DateTime.UtcNow, configuration = Config() })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
