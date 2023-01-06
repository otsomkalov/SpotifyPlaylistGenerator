open System
open Database
open Microsoft.AspNetCore.Builder
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Options
open Shared.Settings
let configureDbContext (provider: IServiceProvider) (builder: DbContextOptionsBuilder) =
  let settings =
    provider
      .GetRequiredService<IOptions<DatabaseSettings>>()
      .Value

  builder.UseNpgsql(settings.ConnectionString) |> ignore

  ()

let builder = WebApplication.CreateBuilder()

builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection(DatabaseSettings.SectionName))

builder.Services.AddDbContext<AppDbContext>(configureDbContext) |> ignore


let app = builder.Build()

