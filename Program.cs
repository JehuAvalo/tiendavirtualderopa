using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Net.Mail;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<StoreDb>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("Store")));
builder.Services.AddScoped<IPasswordHasher<Customer>, PasswordHasher<Customer>>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options => {
 options.Cookie.Name = "BabyGirlie.Session";
 options.Cookie.HttpOnly = true;
 options.Cookie.SameSite = SameSiteMode.Strict;
 options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
 options.ExpireTimeSpan = TimeSpan.FromDays(14);
 options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
 options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options => { options.RejectionStatusCode = 429; options.AddPolicy("orders", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })); });
builder.Services.AddRateLimiter(options => options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 8, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
var app = builder.Build();
app.UseExceptionHandler(handler => handler.Run(async context => { context.Response.StatusCode = 503; await context.Response.WriteAsJsonAsync(new { error = "No pudimos completar la solicitud. Inténtalo nuevamente en unos momentos." }); }));
app.UseRateLimiter();
app.UseDefaultFiles(); app.UseStaticFiles();
app.UseAuthentication(); app.UseAuthorization();
app.Use(async (context, next) => {
 if (HttpMethods.IsPost(context.Request.Method) && context.Request.Path.StartsWithSegments("/api")) {
  var origin = context.Request.Headers.Origin.ToString();
  var expected = $"{context.Request.Scheme}://{context.Request.Host}";
  if ((!string.IsNullOrWhiteSpace(origin) && !string.Equals(origin, expected, StringComparison.OrdinalIgnoreCase)) || context.Request.Headers["Sec-Fetch-Site"] == "cross-site") { context.Response.StatusCode = 403; return; }
 }
 await next(context);
});
app.MapPost("/api/account/register", async (RegisterInput input, StoreDb db, IPasswordHasher<Customer> hasher, HttpContext context) => {
 var email = input.Email?.Trim() ?? "";
 if (email.Length is < 5 or > 256 || !MailAddress.TryCreate(email, out var address) || address.Address != email || input.Password is null || input.Password.Length is < 8 or > 128) return Results.BadRequest(new { error = "Escribe un correo válido y una contraseña de al menos 8 caracteres." });
 var normalized = email.ToUpperInvariant();
 if (await db.Customers.AnyAsync(c => c.NormalizedEmail == normalized)) return Results.Conflict(new { error = "Este correo ya tiene una cuenta." });
 var customer = new Customer { Email = email, NormalizedEmail = normalized, CreatedAt = DateTime.UtcNow };
 customer.PasswordHash = hasher.HashPassword(customer, input.Password);
 db.Customers.Add(customer);
 try { await db.SaveChangesAsync(); } catch (DbUpdateException) { return Results.Conflict(new { error = "Este correo ya tiene una cuenta." }); }
 await SignInCustomer(context, customer);
 return Results.Ok(new { customer.Id, customer.Email });
}).RequireRateLimiting("auth");
app.MapPost("/api/account/login", async (LoginInput input, StoreDb db, IPasswordHasher<Customer> hasher, HttpContext context) => {
 var normalized = input.Email?.Trim().ToUpperInvariant() ?? "";
 var customer = await db.Customers.FirstOrDefaultAsync(c => c.NormalizedEmail == normalized);
 if (customer is null || input.Password is null || hasher.VerifyHashedPassword(customer, customer.PasswordHash, input.Password) == PasswordVerificationResult.Failed) return Results.BadRequest(new { error = "Correo o contraseña incorrectos." });
 await SignInCustomer(context, customer);
 return Results.Ok(new { customer.Id, customer.Email });
}).RequireRateLimiting("auth");
app.MapPost("/api/account/logout", async (HttpContext context) => { await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); return Results.Ok(new { ok = true }); }).RequireAuthorization();
app.MapGet("/api/account/me", async (HttpContext context, StoreDb db) => {
 var id = CustomerId(context.User); var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
 return customer is null ? Results.Unauthorized() : Results.Ok(new { customer.Id, customer.Email });
}).RequireAuthorization();
app.MapGet("/api/account/orders", async (HttpContext context, StoreDb db) => { var customerId = CustomerId(context.User); return await db.Orders.AsNoTracking().Where(o => o.CustomerId == customerId).OrderByDescending(o => o.CreatedAt).Select(o => new { o.Number, o.CreatedAt, o.Status, o.Total, items = o.Items.Select(i => new { i.ProductName, i.Size, i.Quantity, i.UnitPrice }) }).ToListAsync(); }).RequireAuthorization();
app.MapGet("/api/products", async (StoreDb db) => await db.Products.AsNoTracking().Where(p => p.Active).Select(p => new { p.Id, p.Name, p.Price, p.Category, p.Detail, sizes = p.Sizes.Split(',', StringSplitOptions.None), p.Image }).ToListAsync());
app.MapPost("/api/orders", async (OrderInput input, StoreDb db, HttpContext context) => {
 if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 80 || string.IsNullOrWhiteSpace(input.Phone) || input.Phone.Length > 25 || !input.Phone.Any(char.IsDigit) || string.IsNullOrWhiteSpace(input.City) || input.City.Length > 100 || input.Notes?.Length > 400 || input.Items is null || input.Items.Count is < 1 or > 30 || input.RequestId == Guid.Empty) return Results.BadRequest(new { error = "Revisa tus datos y los artículos del pedido." });
 var previous = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.RequestId == input.RequestId);
 if (previous != null) return previous.CustomerId == CustomerId(context.User) ? Results.Ok(new { number = previous.Number, total = previous.Total, status = previous.Status }) : Results.Conflict(new { error = "No se puede reutilizar esa solicitud." });
 var ids = input.Items.Select(i => i.Id).Distinct().ToArray();
 var products = await db.Products.Where(p => ids.Contains(p.Id) && p.Active).ToDictionaryAsync(p => p.Id);
 var order = new StoreOrder { RequestId = input.RequestId, CustomerId = CustomerId(context.User), Number = "BG-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(), Name = input.Name.Trim(), Phone = input.Phone.Trim(), City = input.City.Trim(), Notes = input.Notes?.Trim() ?? "", Status = "Pendiente de confirmación", CreatedAt = DateTime.UtcNow };
 foreach(var item in input.Items) {
  if (item.Quantity is < 1 or > 20 || !products.TryGetValue(item.Id, out var p) || !p.Sizes.Split(',').Contains(item.Size)) return Results.BadRequest(new { error = "Una prenda o talla ya no está disponible." });
  order.Items.Add(new OrderItem { ProductId = p.Id, ProductName = p.Name, Size = item.Size, Quantity = item.Quantity, UnitPrice = p.Price });
 }
 order.Total = order.Items.Sum(i => i.UnitPrice * i.Quantity);
 db.Orders.Add(order);
 try { await db.SaveChangesAsync(); } catch(DbUpdateException) { db.ChangeTracker.Clear(); var duplicate = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.RequestId == input.RequestId); if(duplicate == null) throw; return duplicate.CustomerId == CustomerId(context.User) ? Results.Ok(new { number = duplicate.Number, total = duplicate.Total, status = duplicate.Status }) : Results.Conflict(new { error = "No se puede reutilizar esa solicitud." }); }
 return Results.Ok(new { number = order.Number, total = order.Total, status = order.Status });
}).RequireAuthorization().RequireRateLimiting("orders");
if (args.Contains("--init-db")) {
 using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<StoreDb>();
 await db.Database.EnsureCreatedAsync();
 await db.Database.ExecuteSqlRawAsync("""
 IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL
 BEGIN
 CREATE TABLE dbo.Customers (Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, Email nvarchar(256) NOT NULL, NormalizedEmail nvarchar(256) NOT NULL, PasswordHash nvarchar(512) NOT NULL, CreatedAt datetime2 NOT NULL);
 CREATE UNIQUE INDEX IX_Customers_NormalizedEmail ON dbo.Customers(NormalizedEmail);
 END;
 IF COL_LENGTH('dbo.Orders', 'CustomerId') IS NULL
 BEGIN
 ALTER TABLE dbo.Orders ADD CustomerId int NULL;
 CREATE INDEX IX_Orders_CustomerId ON dbo.Orders(CustomerId);
 ALTER TABLE dbo.Orders ADD CONSTRAINT FK_Orders_Customers_CustomerId FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id);
 END;
 """);
 var seedText = await File.ReadAllTextAsync(Path.Combine(app.Environment.ContentRootPath,"catalog-seed.json"));
 var seed = System.Text.Json.JsonSerializer.Deserialize<List<SeedProduct>>(seedText, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
 foreach(var p in seed) { var existing=await db.Products.FindAsync(p.Id); if(existing is null) db.Products.Add(new Product { Id=p.Id,Name=p.Name,Price=p.Price,Category=p.Category,Detail=p.Detail,Image=p.Image,Sizes=string.Join(',',p.Sizes) }); else { existing.Name=p.Name;existing.Price=p.Price;existing.Category=p.Category;existing.Detail=p.Detail;existing.Image=p.Image;existing.Sizes=string.Join(',',p.Sizes); } } await db.SaveChangesAsync();
 Console.WriteLine("Base de datos BabyGirlieDemo preparada."); return;
}
app.Run();
static int CustomerId(ClaimsPrincipal principal) => int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
static Task SignInCustomer(HttpContext context, Customer customer) => context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, customer.Id.ToString()), new Claim(ClaimTypes.Email, customer.Email)], CookieAuthenticationDefaults.AuthenticationScheme)));
public record RegisterInput(string? Email, string? Password);
public record LoginInput(string? Email, string? Password);
public record OrderInput(Guid RequestId, string Name, string Phone, string City, string? Notes, List<OrderLineInput>? Items);
public record OrderLineInput(string Id, string Size, int Quantity);
public class Product { public string Id {get;set;}=""; public string Name {get;set;}=""; public decimal Price {get;set;} public string Category {get;set;}=""; public string Detail {get;set;}=""; public string Image {get;set;}=""; public string Sizes {get;set;}="2,4,6,8"; public bool Active {get;set;}=true; }
public class Customer { public int Id {get;set;} public string Email {get;set;}=""; public string NormalizedEmail {get;set;}=""; public string PasswordHash {get;set;}=""; public DateTime CreatedAt {get;set;} }
public class StoreOrder { public int Id {get;set;} public Guid RequestId {get;set;} public int? CustomerId {get;set;} public string Number {get;set;}=""; public string Name {get;set;}=""; public string Phone {get;set;}=""; public string City {get;set;}=""; public string Notes {get;set;}=""; public decimal Total {get;set;} public string Status {get;set;}=""; public DateTime CreatedAt {get;set;} public List<OrderItem> Items {get;set;}=[]; }
public class OrderItem { public int Id {get;set;} public int StoreOrderId {get;set;} public string ProductId {get;set;}=""; public string ProductName {get;set;}=""; public string Size {get;set;}=""; public int Quantity {get;set;} public decimal UnitPrice {get;set;} }
public class StoreDb(DbContextOptions<StoreDb> options):DbContext(options) {
 public DbSet<Product> Products=>Set<Product>(); public DbSet<StoreOrder> Orders=>Set<StoreOrder>(); public DbSet<OrderItem> OrderItems=>Set<OrderItem>(); public DbSet<Customer> Customers=>Set<Customer>();
 protected override void OnModelCreating(ModelBuilder b) { b.Entity<Product>().Property(p=>p.Price).HasPrecision(12,2);b.Entity<Product>().Property(p=>p.Id).HasMaxLength(50); b.Entity<StoreOrder>().Property(o=>o.Total).HasPrecision(12,2); b.Entity<StoreOrder>().HasIndex(o=>o.RequestId).IsUnique();b.Entity<StoreOrder>().HasIndex(o=>o.Number).IsUnique();b.Entity<StoreOrder>().HasOne<Customer>().WithMany().HasForeignKey(o=>o.CustomerId).OnDelete(DeleteBehavior.Restrict);b.Entity<Customer>().HasIndex(c=>c.NormalizedEmail).IsUnique();b.Entity<Customer>().Property(c=>c.Email).HasMaxLength(256);b.Entity<Customer>().Property(c=>c.NormalizedEmail).HasMaxLength(256);b.Entity<Customer>().Property(c=>c.PasswordHash).HasMaxLength(512);b.Entity<OrderItem>().Property(i=>i.UnitPrice).HasPrecision(12,2);b.Entity<OrderItem>().HasOne<Product>().WithMany().HasForeignKey(i=>i.ProductId).OnDelete(DeleteBehavior.Restrict); }
}

public record SeedProduct(string Id,string Name,decimal Price,string Category,string Detail,string[] Sizes,string Image);
