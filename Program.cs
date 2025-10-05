using project_celestial_shield.Components;
using MudBlazor.Services;
using project_celestial_shield.Services;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// Configure OpenAI service HttpClient. Add appsettings keys: OpenAI:BaseUrl and OpenAI:ApiKey
var openAiBase = builder.Configuration["OpenAI:BaseUrl"];
var openAiKey = builder.Configuration["OpenAI:ApiKey"];
if (!string.IsNullOrEmpty(openAiBase) && !string.IsNullOrEmpty(openAiKey))
{
    builder.Services.AddHttpClient<OpenAiService>(client =>
    {
        client.BaseAddress = new Uri(openAiBase);
        // If the base URL is api.openai.com, the expected header is Authorization: Bearer <key>
        // For Azure OpenAI use the api-key header and a resource URL; the user can supply either.
        if (openAiBase.Contains("api.openai.com", StringComparison.OrdinalIgnoreCase))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openAiKey);
        }
        else
        {
            client.DefaultRequestHeaders.Add("api-key", openAiKey);
        }
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
