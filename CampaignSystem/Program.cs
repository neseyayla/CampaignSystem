using CampaignSystem.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
// OpenAPI 3.0 zorlanıyor çünkü Swagger UI, .NET 10'un varsayılanı olan 3.1'deki
// dizi tipli (union) parametre şemalarını (ör. int route parametreleri için
// "type": ["integer","string"]) düzgün işleyemiyor ve "Required field is not
// provided" hatası veriyor.
builder.Services.AddOpenApi(options =>
{
    options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0;
});

// The connection string is supplied by User Secrets in development and by an
// environment variable in every other environment; appsettings.json only holds an
// empty placeholder so the required key is discoverable.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is not configured. In development, set it with: " +
        "dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"<value>\" --project CampaignSystem");
}

builder.Services.AddDbContext<CampaignDbContext>(options =>
    options.UseSqlServer(connectionString));



builder.Services.AddCors(options =>

{

    options.AddPolicy("AllowSwagger", policy =>

    {

        policy.AllowAnyOrigin()

              .AllowAnyMethod()

              .AllowAnyHeader();

    });

});



var app = builder.Build();



app.UseRouting();



app.UseCors("AllowSwagger"); // <-- Buraya ekleyin



app.UseAuthorization();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "CampaignSystem API v1");
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
