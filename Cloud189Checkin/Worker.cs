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

            var times = cfg.Times.Select(o => new TimeSpan(o.Hours % 24, o.Minutes, 0)).Distinct().OrderBy(o => o).ToList();

            (TimeSpan next, TimeSpan sleep) GetNext()
            {
                var now = DateTime.Now.TimeOfDay;
                now = new TimeSpan(now.Hours, now.Minutes, 0);

                if (times.Where(o => o > now).Any())
                {
                    var next = times.Where(o => o > now).First();
                    return (next, next - now);
                }
                else
                {
                    var next = times.First();
                    return (next, times.First() + new TimeSpan(24, 0, 0) - now);
                }
            }
            while (!stoppingToken.IsCancellationRequested)
            {
                var sp = GetNext();
                _logger.LogInformation($"the next time it will be executed at {sp.next}.");
                await Task.Delay(sp.sleep, stoppingToken);
                if (!stoppingToken.IsCancellationRequested)
                    await DoAsync(2);
            }
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