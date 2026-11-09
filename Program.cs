using Microsoft.EntityFrameworkCore; 
using FinancialController.Data; 
using project_API.Middleware;
using project_API.Services;

var builder = WebApplication.CreateBuilder(args); 

// 0. Configura Logs Estruturados em formato JSON legível por máquina com suporte a Escopos (CorrelationId)
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});

// Registra o coletor de métricas em memória para o endpoint /metrics (modelo Pull)
builder.Services.AddSingleton<MetricsCollector>();

// 1. Configura o CORS de maneira correta para o React 
builder.Services.AddCors(options =>     
    options.AddPolicy("ReactAppPolicy", policy =>     
    {         
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")               
              .AllowAnyHeader()               
              .AllowAnyMethod()
              .WithExposedHeaders(CorrelationIdMiddleware.CorrelationIdHeaderName);     
    })); // <- FECHAMENTO CORRIGIDO AQUI

// 2. Configura o Banco de Dados PostgreSQL com resiliência para a nuvem 
builder.Services.AddDbContext<ApplicationDbContext>(options =>     
{
    // Desabilita explicitamente o log de parâmetros SQL sensíveis (CPF, senhas, etc.) para conformidade com a LGPD
    options.EnableSensitiveDataLogging(false);
    options.UseNpgsql(         
        builder.Configuration.GetConnectionString("DefaultConnection"),         
        npgsqlOptions => {             
            // Permite que o EF tente reconectar se o Neon demorar para acordar             
            npgsqlOptions.EnableRetryOnFailure(                 
                maxRetryCount: 5,                  
                maxRetryDelay: TimeSpan.FromSeconds(30),                  
                errorCodesToAdd: null             
            );             
            // Define o tempo limite do comando para 60 segundos (evita o timeout)             
            npgsqlOptions.CommandTimeout(60);          
        }     
    );
}); 

builder.Services.AddControllers(); 
builder.Services.AddEndpointsApiExplorer(); 
builder.Services.AddSwaggerGen(); 

var app = builder.Build(); 

// Gera e propaga o CorrelationId no exato momento em que a requisição entra no sistema
app.UseMiddleware<CorrelationIdMiddleware>();

// Configure the HTTP request pipeline. 
if (app.Environment.IsDevelopment())
{ // <- CHAVE ADICIONADA AQUI
    app.UseSwagger();     
    app.UseSwaggerUI(); 
} // <- CHAVE ADICIONADA AQUI

// 3. Ativar o CORS no pipeline 
app.UseCors("ReactAppPolicy"); 
app.UseHttpsRedirection(); 
app.UseAuthorization(); 
app.MapControllers(); 
app.Run();