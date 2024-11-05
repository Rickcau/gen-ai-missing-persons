using Blazored.LocalStorage;
using MudBlazor.Services;
using ui_frontend.Components;
using ui_frontend.Service;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// Configure HttpClient for the server-side Blazor app
// builder.Services.AddHttpClient("ChatAPI", client =>
// {
//     // for local API http://localhost:7129/api/ 
//     // for azure deployment https://someapi.azurewebsites.net/Chat
//     // Below is an example of the local api-missing-persons API running on port 7049
//     // https://localhost:7049/Chat 
//     // Example Request Body
//     // {
//     //     "sessionId": "123456",
//     //     "prompt": "how many people went missing in April"
//     // }
//     client.BaseAddress = new Uri("http://localhost:7129/api/");
// });
builder.Services.AddHttpClient("ChatAPI", client =>
{
    // for local API http://localhost:7049/Chat/
    // for azure deployment https://someapi.azurewebsites.net/api/travel-data
    client.BaseAddress = new Uri("https://localhost:7049/");
    client.DefaultRequestHeaders.Add("api-key", "1234");
});

builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<SyncService>();
builder.Services.AddScoped<ProgressService>();
builder.Services.AddScoped<UserSessionService>();

builder.Services.AddBlazoredLocalStorage();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
