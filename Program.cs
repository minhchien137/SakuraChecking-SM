using Microsoft.EntityFrameworkCore;
using ScanCheckSakura.Data;
using ScanCheckSakura.Services.FGServices;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ICBCPService, CBCPService>();
builder.Services.AddScoped<ICBCPLogService, CBCPLogService>();

var app = builder.Build();


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UsePathBase("/ScanCheck");

app.Use((context, next) =>
{
    context.Request.PathBase = "/ScanCheck";
    return next();
});

// app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=FGCheck}/{action=CBCP}/{id?}")
    .WithStaticAssets();


app.Run();
