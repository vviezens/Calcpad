using Calcpad.TemplateServer.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;


var builder = WebApplication.CreateBuilder(args);

// Services registrieren
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<WeatherForecastService>();

// TemplateService & Controller registrieren
builder.Services.AddControllers();
builder.Services.AddSingleton<TemplateService>();

// HttpClient für API-Anfragen registrieren
builder.Services.AddHttpClient();


builder.Services.AddScoped(sp =>
{
    var navigationManager = sp.GetRequiredService<NavigationManager>();
    return new HttpClient { BaseAddress = new Uri(navigationManager.BaseUri) };
});







var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.UseStaticFiles();
app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");



app.Run();


// Am Ende von Program.cs hinzufügen
//await Calcpad.TemplateServer.Tests.TemplateLikesTest.RunTests();
