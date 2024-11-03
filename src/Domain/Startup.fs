module Domain.Startup

open Domain.Core
open Domain.Repos
open Domain.Workflows
open Microsoft.Extensions.DependencyInjection
open otsom.fs.Extensions.DependencyInjection

let addDomain (services: IServiceCollection) =
  services
    .BuildSingleton<Preset.Get, PresetRepo.Load>(Preset.get)
    .AddSingleton<Preset.Validate>(Preset.validate)
