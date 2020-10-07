using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text;

namespace Cloud189Checkin
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //×¢²á±àÂë
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure<Config>(hostContext.Configuration.GetSection("Config"));
                    services.AddTransient<CheckinApi>();
                    services.AddHostedService<Worker>();
                });
    }
}