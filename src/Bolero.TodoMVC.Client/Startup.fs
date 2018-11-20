namespace Bolero.TodoMVC.Client

open Microsoft.AspNetCore.Blazor.Builder
open Microsoft.AspNetCore.Blazor.Hosting
open Microsoft.Extensions.DependencyInjection

type Startup() =

    member __.ConfigureServices(services: IServiceCollection) =
        ()

    member __.Configure(app: IBlazorApplicationBuilder) =
        app.AddComponent<Main.TodoList.Component>(".todoapp")

module Program =

    [<EntryPoint>]
    let Main args =
        BlazorWebAssemblyHost.CreateDefaultBuilder()
            .UseBlazorStartup<Startup>()
            .Build()
            .Run()
        0
