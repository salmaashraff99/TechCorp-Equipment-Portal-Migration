using Equipment_Service.Infrastructure;
using Equipment_Service.Infrastructure.Data;
using Equipment_Service.Repositories.Implementations;
using Equipment_Service.Repositories.Interfaces;
using Equipment_Service.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Equipment Service API", Version = "v1" });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IEquipmentRequestRepository, EquipmentRequestRepository>();
builder.Services.AddScoped<IWorkFlowRepository, WorkFlowRepository>();
builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<EquipmentRequest_Service>();
builder.Services.AddScoped<WorkFlow_Service>();

builder.Services.AddHttpClient("NotificationService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:NotificationService"]
        ?? "http://localhost:5102");
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();
app.Run();
