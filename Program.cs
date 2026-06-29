using BusEnVivo.Services;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Detrás del proxy/IIS de somee, respeta el esquema real (X-Forwarded-Proto)
// para que la app sepa cuándo la petición vino por HTTPS y redirija bien.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// HttpClient tipado para consumir la API de la STM en tiempo real.
builder.Services.AddHttpClient<IStmService, StmService>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(30);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("BusEnVivo/1.0");
});

// Caché en memoria para terminales (TTL largo) y resultados de horarios (TTL corto),
// y así no golpear urubus en cada request del cliente.
builder.Services.AddMemoryCache();

// HttpClient tipado para consumir los horarios interurbanos de urubus.com.uy.
// User-Agent identificable (uso personal/educativo) y timeout amplio (la página pesa ~2 MB).
builder.Services.AddHttpClient<IUrubusService, UrubusService>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(40);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("MiBondi/1.0 (proyecto educativo)");
});

var app = builder.Build();

// Aplica el esquema reenviado por el proxy antes de cualquier decisión de redirección.
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Redirige HTTP -> HTTPS (con el certificado que agregaste en somee).
app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
