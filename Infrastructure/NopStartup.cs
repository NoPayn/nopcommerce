using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Payments.NoPayn.Services;

namespace Nop.Plugin.Payments.NoPayn.Infrastructure;

public class NopStartup : INopStartup
{
    public int Order => 1;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient();
        services.AddScoped<NoPaynApiClient>();
        services.AddScoped<NoPaynLogger>();
    }

    public void Configure(IApplicationBuilder application)
    {
    }
}
