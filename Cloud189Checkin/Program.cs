using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Cloud189Checkin
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //×¢²á±àÂë
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddTransient<CheckinApi>();
            builder.Services.AddHostedService<Worker>();
            IConfigurationSection section = builder.Configuration.GetSection("Config");
            builder.Services.Configure<Config>(section);
            var host = builder.Build();
            host.Run();
        }
    }
}