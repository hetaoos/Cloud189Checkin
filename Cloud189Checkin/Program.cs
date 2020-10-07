using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cloud189Checkin
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHangfire(configuration =>
                    configuration.UseSimpleAssemblyNameTypeSerializer()
                    .UseRecommendedSerializerSettings()
                    .UseMemoryStorage());

                    services.AddHangfireServer();

                    services.Configure<Config>(hostContext.Configuration.GetSection("Config"));
                    services.AddTransient<CheckinApi>();
                    services.AddHostedService<Worker>();
                });
    }
}