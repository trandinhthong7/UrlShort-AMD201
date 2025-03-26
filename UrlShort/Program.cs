using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using UrlShort.Models;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

// Register API documentation (Swagger)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Enable in-memory caching for improved performance
builder.Services.AddMemoryCache();

// HTTP Client Factory for making external HTTP requests
builder.Services.AddHttpClient();

// Configure SQLite Database Connection
var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApiDbContext>(options => options.UseSqlite(connStr));


var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Validate if URL is accessible
async Task<bool> IsValidUrl(string url, IHttpClientFactory httpClientFactory)
{
    try
    {
        using var httpClient = httpClientFactory.CreateClient();
        var response = await httpClient.GetAsync(url);
        return response.IsSuccessStatusCode;
    }
    catch
    {
        return false;
    }
}

// API Endpoint: Shorten a URL
app.MapPost("/shorturl", async (UrlDto url, ApiDbContext db, HttpContext ctx, IMemoryCache cache, IHttpClientFactory httpClientFactory) =>
{
    // Validate URL format
    if (string.IsNullOrWhiteSpace(url.Url) || !Uri.TryCreate(url.Url, UriKind.Absolute, out _))
        return Results.BadRequest("Invalid URL format.");

    // Check if URL is accessible
    if (!await IsValidUrl(url.Url, httpClientFactory))
        return Results.BadRequest("The provided URL is not accessible.");

    // Check cache first
    if (cache.TryGetValue(url.Url, out string? cachedShortUrl))
    {
        return Results.Ok(new UrlResponseDto { Url = cachedShortUrl });
    }

    // Check if URL already exists in database
    var existingEntry = await db.Urls.FirstOrDefaultAsync(u => u.Url == url.Url);
    if (existingEntry != null)
    {
        var existingShortUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}/{existingEntry.ShortUrl}";

        // Store in cache for 10 minutes
        cache.Set(url.Url, existingShortUrl, TimeSpan.FromMinutes(10));

        return Results.Ok(new UrlResponseDto { Url = existingShortUrl });
    }

    // Generate a new short URL
    string GenerateShortUrl(int length = 8)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyz";
        return new string(Enumerable.Range(0, length)
            .Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)]).ToArray());
    }

    string randomStr;
    do
    {
        randomStr = GenerateShortUrl();
    } while (await db.Urls.AnyAsync(u => u.ShortUrl == randomStr));

    var sUrl = new UrlManagement { Url = url.Url, ShortUrl = randomStr };

    // Save to database
    db.Urls.Add(sUrl);
    await db.SaveChangesAsync();

    var result = $"{ctx.Request.Scheme}://{ctx.Request.Host}/{sUrl.ShortUrl}";

    // Store in cache
    cache.Set(url.Url, result, TimeSpan.FromMinutes(10));

    return Results.Ok(new UrlResponseDto { Url = result });
});



// API Endpoint: Retrieve all stored URLs
app.MapGet("/urls", async (ApiDbContext db, HttpContext ctx, IMemoryCache cache) =>
{
    if (!cache.TryGetValue("allUrls", out List<Dictionary<string, string>>? urls))
    {
        var dbUrls = await db.Urls.ToListAsync();
        urls = dbUrls.Select(u => new Dictionary<string, string>
        {
            { "Url", u.Url },
            { "ShortUrl", $"{ctx.Request.Scheme}://{ctx.Request.Host}/{u.ShortUrl}" }
        }).ToList();

        // Cache result for 5 minutes
        cache.Set("allUrls", urls, TimeSpan.FromMinutes(5));
    }

    return Results.Ok(urls ?? new List<Dictionary<string, string>>());
});


// API Endpoint: Delete a short URL
app.MapDelete("/delete/{shortUrl}", async (string shortUrl, ApiDbContext db, IMemoryCache cache) =>
{
    var urlEntry = await db.Urls.FirstOrDefaultAsync(u => u.ShortUrl == shortUrl);
    if (urlEntry == null)
        return Results.NotFound("Short URL not found.");

    db.Urls.Remove(urlEntry);
    await db.SaveChangesAsync();

    // Remove from cache
    cache.Remove(urlEntry.Url);
    cache.Remove("allUrls");

    return Results.Ok("Deleted successfully.");
});

// API Endpoint: Redirect short URL to original URL
app.MapFallback(async (ApiDbContext db, HttpContext ctx) =>
{
    var path = ctx.Request.Path.ToUriComponent().Trim('/');
    var urlMatch = await db.Urls.FirstOrDefaultAsync(x => x.ShortUrl == path);
    if (urlMatch == null)
        return Results.NotFound("Short URL not found.");

    return Results.Redirect(urlMatch.Url);
});

// Enable static files for potential frontend integration
app.UseDefaultFiles();
app.UseStaticFiles();

app.Run();

// Database Context
class ApiDbContext : DbContext
{
    public virtual DbSet<UrlManagement> Urls { get; set; }

    public ApiDbContext(DbContextOptions<ApiDbContext> options) : base(options) { }
}