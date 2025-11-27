using Complex_for_analyzing_hash_functions.Data;
using Complex_for_analyzing_hash_functions.Services;
using Microsoft.EntityFrameworkCore;
using Complex_for_analyzing_hash_functions.Interfaces;
using The_complex_of_testing_hash_functions.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IHashFunction, KeccakHash>();
builder.Services.AddScoped<StatisticsService>();
builder.Services.AddSingleton<INistTestingService, NistTestingService>();
builder.Services.AddSingleton<IDiehardTestingService, DiehardTestingService>();


// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
