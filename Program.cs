using Microsoft.EntityFrameworkCore;
using CMDB.Data;
using CMDB.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<CmdbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=cmdb.db"));

// NVD sync — typed HttpClient with a generous timeout for large paginated syncs
builder.Services.AddHttpClient<NvdSyncService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(10);
    client.DefaultRequestHeaders.Add("User-Agent", "CMDB/1.0");
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CmdbContext>();

    // OO Refactor migration: if the Discriminator column is missing (old schema),
    // drop and recreate the database to apply the new TPH schema.
    var needsReset = false;
    try
    {
        db.Database.ExecuteSqlRaw("SELECT Discriminator FROM Assets LIMIT 1");
    }
    catch
    {
        // Column doesn't exist — old single-table schema
        needsReset = true;
    }

    if (needsReset)
    {
        db.Database.EnsureDeleted();
    }

    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
