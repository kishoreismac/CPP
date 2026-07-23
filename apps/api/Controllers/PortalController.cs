using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
namespace Cpp.Api.Controllers;

[ApiController, Route("api")]
public class PortalController(AppDb db, IOrderRuleService rules, IFulfillmentGateway fulfillment) : ControllerBase
{
    static readonly CurrentUser DemoUser = new("u1", "RVERVE13", "Riley Verve", "Administrator", true, true);
    [HttpGet("current-user")] public object CurrentUser() => DemoUser;
    [HttpGet("accounts")] public async Task<object> Accounts() => await db.Accounts.OrderBy(x => x.Name).ToListAsync();
    [HttpGet("accounts/{id}")] public async Task<ActionResult<Account>> Account(string id) => await db.Accounts.FindAsync(id) is { } a ? a : NotFound();
    [HttpPost("accounts")] public async Task<IActionResult> CreateAccount(AccountRequest request) { var error = await ValidateAccount(request); if (error is not null) return BadRequest(new ProblemDetails { Title = "Account validation failed", Detail = error }); var idBase = new string(request.Name.ToLowerInvariant().Where(char.IsLetterOrDigit).Take(18).ToArray()); if (string.IsNullOrWhiteSpace(idBase)) idBase = "account"; var id = idBase; var suffix = 1; while (await db.Accounts.AnyAsync(x => x.Id == id)) id = $"{idBase}-{suffix++}"; var account = MapAccount(new Account { Id = id }, request); db.Accounts.Add(account); await db.SaveChangesAsync(); return CreatedAtAction(nameof(Account), new { id = account.Id }, account); }
    [HttpPut("accounts/{id}")] public async Task<IActionResult> UpdateAccount(string id, AccountRequest request) { var account = await db.Accounts.FindAsync(id); if (account is null) return NotFound(); var error = await ValidateAccount(request, id); if (error is not null) return BadRequest(new ProblemDetails { Title = "Account validation failed", Detail = error }); MapAccount(account, request); await db.SaveChangesAsync(); return Ok(account); }
    [HttpGet("accounts/{id}/deliver-to-locations")] public async Task<object> DeliverToLocations(string id) => await db.DeliverToLocations.Where(x => x.ShipToAccountId == id).OrderByDescending(x => x.IsDefault).ThenBy(x => x.Name).ToListAsync();
    [HttpGet("dashboard/messages")] public object Messages() => Enumerable.Range(1, 6).Select(i => new { id = i, title = $"Crop Protection update {i}", snippet = "Demonstration notice for ordering and delivery operations.", timestamp = DateTime.UtcNow.AddHours(-i) });
    [HttpGet("dashboard/knowledge")] public object Knowledge() => new[] { "CPP ordering quick guide", "Understanding availability status", "Freight option reference", "Order confirmation and PDF help", "Customer pickup checklist" }.Select((x, i) => new { id = i + 1, title = x });
    [HttpGet("products/search")] public async Task<object> Products([FromQuery] string? criterion = "Product Name", [FromQuery] string? q = "", [FromQuery] string? query = null, [FromQuery] string? shipToAccountId = null, [FromQuery] bool favorites = false) { q = Normalize(query ?? q ?? ""); var normalizedQuery = SearchToken(q); var all = await db.Products.ToListAsync(); string Field(Product p) => Criterion(criterion) switch { "itemNumber" => p.ItemNumber, "activeIngredient" => p.ActiveIngredients, "productCategory" => p.Category, "gtin" => p.Gtin, "packageSize" => p.PackageSize, "supplier" => p.Supplier, "productLine" => p.ProductLine, _ => p.Name }; return all.Where(p => (!favorites || p.Favorite) && (q.Length == 0 || Field(p).Contains(q, StringComparison.OrdinalIgnoreCase) || SearchToken(Field(p)).Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))).OrderBy(p => Rank(Field(p), q)).ThenBy(p => p.Name); }
    [HttpGet("products/suggestions")] public async Task<object> Suggestions([FromQuery] string? q = null, [FromQuery] string? query = null, [FromQuery] string criterion = "productName", [FromQuery] string? shipToAccountId = null, [FromQuery] int limit = 10) { var normalized = Normalize(query ?? q ?? ""); if (normalized.Length < 2) return new { suggestions = Array.Empty<object>(), products = Array.Empty<Product>(), totalCount = 0 }; var products = ((IEnumerable<Product>)await Products(criterion, normalized, null, shipToAccountId, false)).Take(limit).ToList(); var suggestions = Criterion(criterion) == "activeIngredient" ? products.SelectMany(p => p.ActiveIngredients.Split(',', StringSplitOptions.TrimEntries)).Distinct(StringComparer.OrdinalIgnoreCase).Where(v => v.Contains(normalized, StringComparison.OrdinalIgnoreCase)).Select(v => new { productId = (string?)null, value = v, displayText = v, secondaryText = "Active ingredient", matchType = MatchType(v, normalized) }).Take(limit) : products.Select(p => new { productId = (string?)p.Id, value = Criterion(criterion) == "itemNumber" ? p.ItemNumber : p.Name, displayText = Criterion(criterion) == "itemNumber" ? p.ItemNumber : p.Name, secondaryText = $"{p.Supplier} · {p.PackageSize}", matchType = MatchType(Criterion(criterion) == "itemNumber" ? p.ItemNumber : p.Name, normalized) }); return new { suggestions, products, totalCount = products.Count }; }
    [HttpGet("products/{id}")] public async Task<ActionResult<Product>> Product(string id) => await db.Products.FindAsync(id) is { } p ? p : NotFound();
    [HttpPut("products/{id}/favorite")] public async Task<IActionResult> Favorite(string id) { var p = await db.Products.FindAsync(id); if (p is null) return NotFound(); p.Favorite = !p.Favorite; await db.SaveChangesAsync(); return Ok(p); }
    [HttpGet("orders")] public async Task<object> Orders([FromQuery] string? status = null, [FromQuery] string? q = null) { var os = await db.Orders.OrderByDescending(x => x.SubmittedAt ?? x.UpdatedAt).ToListAsync(); var accts = await db.Accounts.ToDictionaryAsync(x => x.Id); var products = await db.Products.ToDictionaryAsync(x => x.Id); return os.Where(x => status == null || x.Status.ToString().Equals(status, StringComparison.OrdinalIgnoreCase)).Where(x => string.IsNullOrWhiteSpace(q) || x.Id.Contains(q, StringComparison.OrdinalIgnoreCase) || (x.WebOrderNumber?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) || (x.CustomerPo?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) || (accts.GetValueOrDefault(x.ShipToAccountId)?.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)).Select(x => new { x.Id, x.WebOrderNumber, status = x.Status.ToString(), x.CustomerPo, x.CreatedAt, x.UpdatedAt, x.SubmittedAt, account = accts.GetValueOrDefault(x.ShipToAccountId), x.Lines, products = x.Lines.Select(line => new { line.ProductId, name = products.GetValueOrDefault(line.ProductId)?.Name ?? "Unavailable product", line.Quantity, line.Uom, line.UnitPrice, total = line.UnitPrice * line.Quantity }), totalQuantity = x.Lines.Sum(line => line.Quantity), totalAmount = x.Lines.Sum(line => line.UnitPrice * line.Quantity), canEdit = CanEdit(x) }); }
    [HttpGet("orders/{id}")] public async Task<ActionResult<Order>> Order(string id) => await db.Orders.FindAsync(id) is { } o ? o : NotFound();
    [HttpPost("orders")] public async Task<object> Save(OrderRequest req) { var o = Map(new Order(), req); db.Orders.Add(o); db.AuditEvents.Add(new() { OrderId = o.Id, EventType = "DraftCreated", Detail = "Draft persisted" }); await db.SaveChangesAsync(); return o; }
    [HttpPut("orders/{id}")] public async Task<IActionResult> Update(string id, OrderRequest req) { var o = await db.Orders.FindAsync(id); if (o is null) return NotFound(); if (!CanEdit(o)) return Conflict(new ProblemDetails { Title = "This order can no longer be edited", Detail = "Submitted, submitting, and cancelled orders are locked by the order lifecycle rules." }); Map(o, req); db.AuditEvents.Add(new() { OrderId = id, EventType = "DraftUpdated", Detail = "Draft fields updated" }); await db.SaveChangesAsync(); return Ok(o); }
    [HttpDelete("orders/{id}")] public async Task<IActionResult> Delete(string id) { var o = await db.Orders.FindAsync(id); if (o is null) return NotFound(); if (o.Status != OrderState.Draft) return Conflict(); db.Remove(o); await db.SaveChangesAsync(); return NoContent(); }
    [HttpPost("orders/{id}/duplicate")] public async Task<IActionResult> Duplicate(string id) { var o = await db.Orders.FindAsync(id); if (o is null) return NotFound(); var copy = new Order { ShipToAccountId = o.ShipToAccountId, SoldToName = o.SoldToName, ContactEmail = o.ContactEmail, ShippingInstructions = o.ShippingInstructions, FreightOption = o.FreightOption, RequestedArrivalDate = DateTime.UtcNow.Date.AddDays(7), Lines = o.Lines }; db.Add(copy); await db.SaveChangesAsync(); return Ok(copy); }
    [HttpPost("orders/{id}/validate")] public async Task<IActionResult> Validate(string id) { var o = await db.Orders.FindAsync(id); if (o is null) return NotFound(); return Ok(await ValidateOrder(o)); }
    [HttpPost("orders/{id}/submit")] public async Task<IActionResult> Submit(string id, [FromHeader(Name = "Idempotency-Key")] string? key) { var o = await db.Orders.FindAsync(id); if (o is null) return NotFound(); if (o.Status == OrderState.Submitted) return Ok(new { o.WebOrderNumber, fulfillmentOrderNumbers = System.Text.Json.JsonSerializer.Deserialize<string[]>(o.FulfillmentOrdersJson) }); var validation = await ValidateOrder(o); if (validation.Any(x => x.Severity == "error")) return UnprocessableEntity(new { title = "Order validation failed", errors = validation }); try { o.Status = OrderState.Submitting; await db.SaveChangesAsync(); var r = await fulfillment.SubmitOrderAsync(o, HttpContext.RequestAborted); o.Status = OrderState.Submitted; o.WebOrderNumber = r.WebOrderNumber; o.SubmittedAt = DateTime.UtcNow; o.FulfillmentOrdersJson = System.Text.Json.JsonSerializer.Serialize(r.FulfillmentOrderNumbers); db.AuditEvents.Add(new() { OrderId = id, EventType = "SubmissionSucceeded", Detail = r.WebOrderNumber }); await db.SaveChangesAsync(); return Ok(r); } catch (Exception ex) { o.Status = OrderState.SubmissionFailed; db.AuditEvents.Add(new() { OrderId = id, EventType = "SubmissionFailed", Detail = ex.Message }); await db.SaveChangesAsync(); return StatusCode(503, new ProblemDetails { Title = "Fulfillment submission failed", Detail = ex.Message }); } }
    [HttpGet("orders/export")] public async Task<IActionResult> Export() { var rows = await db.Orders.ToListAsync(); var csv = "Order ID,Web Order Number,Status,Customer PO\n" + string.Join("\n", rows.Select(x => $"{x.Id},{x.WebOrderNumber},{x.Status},{x.CustomerPo}")); return File(Encoding.UTF8.GetBytes(csv), "text/csv", "cpp-orders.csv"); }
    [HttpGet("orders/{id}/confirmation.pdf")]
    public async Task<IActionResult> Pdf(string id)
    {
        var order = await db.Orders.FindAsync(id);
        if (order?.Status != OrderState.Submitted) return NotFound();
        var account = await db.Accounts.FindAsync(order.ShipToAccountId);
        var delivery = string.IsNullOrWhiteSpace(order.DeliverToAccountId)
          ? null
          : await db.DeliverToLocations.FindAsync(order.DeliverToAccountId);
        var productIds = order.Lines.Select(line => line.ProductId).Distinct().ToList();
        var products = await db.Products.Where(product => productIds.Contains(product.Id)).ToDictionaryAsync(product => product.Id);
        var bytes = OrderConfirmationPdf(order, account, delivery, products);
        db.AuditEvents.Add(new() { OrderId = id, EventType = "ConfirmationDownloaded", Detail = "PDF generated" });
        await db.SaveChangesAsync();
        return File(bytes, "application/pdf", $"CPP_Order_Confirmation_{order.WebOrderNumber}.pdf");
    }
    async Task<List<ValidationResult>> ValidateOrder(Order o) { var a = await db.Accounts.FindAsync(o.ShipToAccountId); var products = await db.Products.ToDictionaryAsync(x => x.Id); var result = rules.Validate(new(o.Id, o.ShipToAccountId, o.DeliverToAccountId, o.CustomerPo, o.ContactEmail, o.ShippingInstructions, o.CustomerPickup, o.FreightOption, o.RequestedArrivalDate, o.Lines, o.DeliverToAnotherLocation, o.AlternateDelivery), a, products); if (o.DeliverToAnotherLocation && (o.AlternateDelivery is null || string.IsNullOrWhiteSpace(o.AlternateDelivery.LocationName) || string.IsNullOrWhiteSpace(o.AlternateDelivery.AddressLine1) || string.IsNullOrWhiteSpace(o.AlternateDelivery.City) || string.IsNullOrWhiteSpace(o.AlternateDelivery.State) || string.IsNullOrWhiteSpace(o.AlternateDelivery.PostalCode))) result.Add(new("error", "ALTERNATE_DELIVERY", "Complete all required alternate delivery address fields.", "alternateDelivery")); return result; }
    static Order Map(Order o, OrderRequest r) { o.ShipToAccountId = r.ShipToAccountId; o.DeliverToAccountId = r.DeliverToAnotherLocation ? null : r.DeliverToAccountId; o.DeliverToAnotherLocation = r.DeliverToAnotherLocation; o.AlternateDelivery = r.DeliverToAnotherLocation ? r.AlternateDelivery : null; o.CustomerPo = r.CustomerPo; o.ContactEmail = r.ContactEmail; o.ShippingInstructions = r.ShippingInstructions; o.CustomerPickup = r.CustomerPickup; o.FreightOption = r.FreightOption; o.RequestedArrivalDate = r.RequestedArrivalDate; o.Lines = r.Lines; o.UpdatedAt = DateTime.UtcNow; o.Status = OrderState.Draft; return o; }
    static bool CanEdit(Order order) => order.Status is not (OrderState.Submitted or OrderState.Submitting or OrderState.Cancelled);
    async Task<string?> ValidateAccount(AccountRequest request, string? currentId = null) { if (string.IsNullOrWhiteSpace(request.AccountNumber) || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Address) || string.IsNullOrWhiteSpace(request.City) || string.IsNullOrWhiteSpace(request.State) || string.IsNullOrWhiteSpace(request.PostalCode) || string.IsNullOrWhiteSpace(request.SoldToName)) return "Account number, name, address, city, state, postal code, and Sold-To name are required."; if (!string.IsNullOrWhiteSpace(request.ContactEmail) && !request.ContactEmail.Contains('@')) return "Contact email must be valid."; if (await db.Accounts.AnyAsync(x => x.AccountNumber == request.AccountNumber.Trim() && x.Id != currentId)) return "Account number already exists."; return null; }
    static Account MapAccount(Account account, AccountRequest request) { account.AccountNumber = request.AccountNumber.Trim(); account.Name = request.Name.Trim(); account.Address = request.Address.Trim(); account.City = request.City.Trim(); account.State = request.State.Trim().ToUpperInvariant(); account.PostalCode = request.PostalCode.Trim(); account.SoldToName = request.SoldToName.Trim(); account.ContactEmail = request.ContactEmail.Trim(); account.ShippingInstructions = request.ShippingInstructions.Trim(); account.RequiresCustomerPo = request.RequiresCustomerPo; return account; }
    static string Normalize(string value) => string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    static string SearchToken(string value) => string.Concat(value.Where(char.IsLetterOrDigit));
    static string Criterion(string? value) => value?.Replace(" ", "").Replace("/", "").ToLowerInvariant() switch { "itemnumber" => "itemNumber", "activeingredient" => "activeIngredient", "productcategory" => "productCategory", "gtin" => "gtin", "packagesize" => "packageSize", "vendorsupplier" or "supplier" => "supplier", "productline" => "productLine", _ => "productName" };
    static int Rank(string value, string query) { if (value.Equals(query, StringComparison.OrdinalIgnoreCase)) return 0; if (value.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 1; if (value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(x => x.StartsWith(query, StringComparison.OrdinalIgnoreCase))) return 2; return 3; }
    static string MatchType(string value, string query) => Rank(value, query) switch { 0 => "exact", 1 => "startsWith", 2 => "wordStartsWith", _ => "contains" };
    static byte[] OrderConfirmationPdf(Order order, Account? account, DeliverToLocation? delivery, IReadOnlyDictionary<string, Product> products)
    {
        static string PdfText(string? value, int max = 70)
        {
            var text = string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
            if (text.Length > max) text = text[..(max - 3)] + "...";
            return text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        }

        static string Money(decimal value) => value.ToString("C2", CultureInfo.GetCultureInfo("en-US"));

        var content = new StringBuilder();
        void Text(float x, float y, string value, int size = 10, bool bold = false, string color = "0.12 0.20 0.23") =>
          content.AppendLine($"{color} rg BT /{(bold ? "F2" : "F1")} {size} Tf {x:0.##} {y:0.##} Td ({PdfText(value)}) Tj ET");
        void Line(float x1, float y1, float x2, float y2, string color = "0.82 0.86 0.86") =>
          content.AppendLine($"{color} RG {x1:0.##} {y1:0.##} m {x2:0.##} {y2:0.##} l S");

        content.AppendLine("0.05 0.20 0.25 rg 0 700 612 92 re f");
        Text(48, 750, "CPP ORDER CONFIRMATION", 21, true, "1 1 1");
        Text(48, 724, $"Web order {order.WebOrderNumber}", 12, false, "0.84 0.94 0.93");
        Text(430, 750, "CONFIRMED", 10, true, "0.98 0.78 0.12");

        Text(48, 670, "ORDER SUMMARY", 11, true);
        Line(48, 660, 564, 660);
        Text(48, 640, "Internal order", 8, false, "0.38 0.46 0.48");
        Text(48, 624, order.Id, 10, true);
        Text(200, 640, "Customer PO", 8, false, "0.38 0.46 0.48");
        Text(200, 624, order.CustomerPo ?? "Not provided", 10, true);
        Text(355, 640, "Requested arrival", 8, false, "0.38 0.46 0.48");
        Text(355, 624, order.RequestedArrivalDate.ToString("MMM d, yyyy"), 10, true);
        Text(480, 640, "Generated", 8, false, "0.38 0.46 0.48");
        Text(480, 624, DateTime.UtcNow.ToString("MMM d, yyyy"), 10, true);

        Text(48, 590, "SHIP-TO & DELIVERY", 11, true);
        Line(48, 580, 564, 580);
        Text(48, 560, account?.Name ?? order.ShipToAccountId, 11, true);
        Text(48, 544, $"Account #{account?.AccountNumber ?? order.ShipToAccountId}", 9);
        Text(48, 528, account?.Address ?? "Address unavailable", 9);
        Text(320, 560, delivery?.Name ?? "Delivery location not recorded", 11, true);
        Text(320, 544, delivery?.AddressLine1 ?? order.ShippingInstructions, 9);
        Text(320, 528, delivery is null ? order.FreightOption : $"{delivery.City}, {delivery.State} {delivery.PostalCode}", 9);

        Text(48, 492, "PRODUCTS", 11, true);
        content.AppendLine("0.93 0.96 0.96 rg 48 458 516 26 re f");
        Text(58, 468, "ITEM / PRODUCT", 8, true);
        Text(355, 468, "QTY", 8, true);
        Text(405, 468, "UNIT PRICE", 8, true);
        Text(500, 468, "TOTAL", 8, true);

        var y = 438f;
        foreach (var line in order.Lines.Take(8))
        {
            var product = products.GetValueOrDefault(line.ProductId);
            Text(58, y, product?.Name ?? "Unavailable product", 9, true);
            Text(58, y - 14, $"{product?.ItemNumber ?? line.ProductId} | {line.Uom}", 8, false, "0.38 0.46 0.48");
            Text(355, y - 5, line.Quantity.ToString(CultureInfo.InvariantCulture), 9);
            Text(405, y - 5, Money(line.UnitPrice), 9);
            Text(500, y - 5, Money(line.UnitPrice * line.Quantity), 9, true);
            Line(48, y - 22, 564, y - 22);
            y -= 42;
        }

        if (order.Lines.Count > 8) Text(58, y, $"Plus {order.Lines.Count - 8} additional line(s)", 8);
        var total = order.Lines.Sum(line => line.UnitPrice * line.Quantity);
        var totalY = Math.Max(92, y - 12);
        Text(405, totalY, "ORDER TOTAL", 10, true);
        Text(500, totalY, Money(total), 12, true, "0.04 0.45 0.43");
        Text(48, 54, "Thank you for your order.", 9, true);
        Text(48, 38, "Keep this confirmation for your records. Contact customer service with questions.", 8, false, "0.38 0.46 0.48");
        Text(515, 38, "Page 1 of 1", 8, false, "0.38 0.46 0.48");

        var stream = content.ToString();
        var objects = new[]
        {
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            "<</Type/Page/Parent 2 0 R/MediaBox[0 0 612 792]/Resources<</Font<</F1 4 0 R/F2 5 0 R>>>>/Contents 6 0 R>>",
            "<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>",
            "<</Type/Font/Subtype/Type1/BaseFont/Helvetica-Bold>>",
            $"<</Length {Encoding.ASCII.GetByteCount(stream)}>>stream\n{stream}endstream"
        };
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }
        var xrefOffset = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) pdf.Append($"{offset:0000000000} 00000 n \n");
        pdf.Append($"trailer<</Root 1 0 R/Size {objects.Length + 1}>>\nstartxref\n{xrefOffset}\n%%EOF");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
