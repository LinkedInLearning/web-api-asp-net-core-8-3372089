using Asp.Versioning;
using HPlusSport.API;
using HPlusSport.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddApiVersioning(options =>
{
    options.ReportApiVersions = true;
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new QueryStringApiVersionReader("api-version"),
        new HeaderApiVersionReader("X-API-Version"),
        new MediaTypeApiVersionReader("ver"));
})
.AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
builder.Services.AddSwaggerGen(
    options =>
    {
        options.OperationFilter<SwaggerDefaultValues>();
    });

builder.Services.AddDbContext<ShopContext>(options =>
{
    options.UseInMemoryDatabase("Shop");
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        var descriptions = app.DescribeApiVersions();

        foreach (var description in app.DescribeApiVersions())
        {
            options.SwaggerEndpoint(
                $"/swagger/{description.GroupName}/swagger.json",
                description.GroupName.ToUpperInvariant());
        }
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .HasApiVersion(new ApiVersion(2, 0))
    .ReportApiVersions()
    .Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
    await db.Database.EnsureCreatedAsync();
}

app.MapGet("/products", async (ShopContext _context) =>
{
    return await _context.Products.ToListAsync();
})
    .WithApiVersionSet(apiVersionSet)
    .MapToApiVersion(new ApiVersion(1, 0));

app.MapGet("/products", async (ShopContext _context) =>
{
    return await _context.Products.Where(p => p.IsAvailable == true).ToListAsync();
})
    .WithApiVersionSet(apiVersionSet)
    .MapToApiVersion(new ApiVersion(2, 0));


app.MapGet("/products/{id:int}", async (int id, ShopContext _context) =>
{
    var product = await _context.Products.FindAsync(id);

    if (product == null)
    {
        return Results.NotFound();
    }

    return Results.Ok(product);
}).WithName("GetProduct");

app.MapPost("/products", async (Product product, ShopContext _context) =>
{
    _context.Products.Add(product);
    await _context.SaveChangesAsync();

    return Results.CreatedAtRoute(
        "GetProduct",
        new { id = product.Id },
        product);
});

app.MapPut("/products/{id:int}", async (int id, [FromBody] Product product, ShopContext _context) =>
{
    _context.Entry(product).State = EntityState.Modified;

    try
    {
        await _context.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        if (!_context.Products.Any(p => p.Id == id))
        {
            return Results.NotFound();
        }
        else
        {
            throw;
        }
    }

    return Results.NoContent();
});

app.MapDelete("/products/{id:int}", async (int id, ShopContext _context) =>
{
    var product = await _context.Products.FindAsync(id);

    if (product == null)
    {
        return Results.NotFound();
    }

    _context.Products.Remove(product);
    await _context.SaveChangesAsync();

    return Results.Ok(product);
});

app.Run();
