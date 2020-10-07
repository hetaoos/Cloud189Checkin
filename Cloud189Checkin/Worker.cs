using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Cloud189Checkin
{
    public class Worker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IOptions<Config> _config;
        private readonly ILogger<Worker> _logger;

        /// <summary>
        /// API
        /// </summary>
        private static ConcurrentDictionary<string, CheckinApi> apis = new ConcurrentDictionary<string, CheckinApi>();

        public Worker(IServiceProvider serviceProvider, IOptions<Config> config, ILogger<Worker> logger)
        {
            _serviceProvider = serviceProvider;
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var cfg = _config.Value;

            if (cfg.Times?.Any() != true)
                cfg.Times = new TimeSpan[] { new TimeSpan(7, 10, 0), new TimeSpan(22, 30, 0) };

            var accounts = cfg.Accounts?.Where(o => o?.IsValid() == true).ToArray();
            if (accounts?.Any() != true)
            {
                _logger.LogWarning("没有有效的账号。");
                throw new Exception("没有有效的账号。");
            }

            foreach (var time in cfg.Times)
            {
                _logger.LogInformation($"add recurring job at: {time}");
                RecurringJob.AddOrUpdate(() => DoAsync(2), Hangfire.Cron.Daily(time.Hours % 24, time.Minutes), TimeZoneInfo.Local);
            }

            switch (cfg.RestartAction)
            {
                case 0:
                    break;

                case 1:
                    await DoAsync(1);
                    break;

                default:
                    await DoAsync(2);
                    break;
            }

            _logger.LogInformation("Worker staring at: {time}", DateTimeOffset.Now);
        }

        /// <summary>
        /// 尝试登录或者签到
        /// </summary>
        /// <param name="mode"></param>
        /// <returns></returns>
        public async Task DoAsync(int mode = 2)
        {
            var cfg = _config.Value;

            var accounts = cfg.Accounts?.Where(o => o?.IsValid() == true).ToArray();

            if (accounts?.Any() != true)
            {
                _logger.LogWarning("没有有效的账号。");
                return;
            }

            foreach (var account in accounts)
            {
                _logger.LogInformation($"account: {account.UserName}");
                var api = apis.GetOrAdd(account.UserName, (_) => _serviceProvider.GetService<CheckinApi>());
                api.SetAccount(account.UserName, account.Password);
                if (mode == 1)
                    await api.TryLoginAsync();
                else
                    await api.DoAsync();
            }
        }
    }
}