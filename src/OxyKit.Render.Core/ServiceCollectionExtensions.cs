using Microsoft.Extensions.DependencyInjection;
using OxyKit.Render.Core.Inputs;
using OxyKit.Render.Core.Outputs;

namespace OxyKit.Render.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers rendering emitters and <see cref="DocumentRenderService"/>.</summary>
    public static IServiceCollection AddRenderCore(this IServiceCollection services)
    {
        services.AddSingleton(EngineIdentity.Create());
        services.AddSingleton<TemplateStore>();
        services.AddSingleton<HtmlToPdfRenderer>();
        services.AddSingleton<TableToPdfRenderer>();
        services.AddSingleton<TableToHtmlRenderer>();
        services.AddSingleton<TableToExcelRenderer>();
        services.AddSingleton<TableToCsvRenderer>();
        services.AddSingleton<DocumentRenderService>();
        return services;
    }
}
