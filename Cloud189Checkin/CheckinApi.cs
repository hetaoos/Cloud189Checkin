using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Cloud189Checkin
{
    /// <summary>
    /// 签到接口
    /// </summary>
    public class CheckinApi
    {
        private readonly ILogger<CheckinApi> _logger;

        private HttpClient client;
        private HttpClientHandler httpClientHandler;
        private string _username;
        private string _password;

        public CheckinApi(ILogger<CheckinApi> logger)
        {
            _logger = logger;
        }

        private void CreateHttpClient()
        {
            if (client != null)
                client.Dispose();

            if (httpClientHandler != null)
                httpClientHandler.Dispose();

            httpClientHandler = new HttpClientHandler()
            {
                CookieContainer = TryLoadCookies() ?? new CookieContainer(),
                AutomaticDecompression = DecompressionMethods.All,
                UseCookies = true,
            };
            client = new HttpClient(httpClientHandler);
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Linux; Android 5.1.1; SM-G930K Build/NRD90M; wv) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/74.0.3729.136 Mobile Safari/537.36 Ecloud/8.6.3 Android/22 clientId/355325117317828 clientModel/SM-G930K imsi/460071114317824 clientChannelId/qq proVersion/1.0.6");
            client.DefaultRequestHeaders.Referrer = new Uri("https://m.cloud.189.cn/zhuanti/2016/sign/index.jsp?albumBackupOpened=1");
            client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
        }

        public CheckinApi SetAccount(string username, string password)
        {
            _username = username;
            _password = password;
            CreateHttpClient();
            return this;
        }

        public async Task<bool> TryLoginAsync()
        {
            var url = "https://cloud.189.cn/v2/getUserLevelInfo.action";

            var d = await GetData(url);
            if (d != null && d.ret == 1)
            {
                _logger.LogInformation("currently logged in.");
                return true;
            }

            _logger.LogInformation("start logging in.");
            try
            {
                url = "https://cloud.189.cn/udb/udb_login.jsp?pageId=1&redirectURL=/main.action";
                var html = await client.GetStringAsync(url);

                var captchaToken = GetFieldValue(html, "captchaToken");
                var lt = GetJsVariableValue(html, "lt");
                var returnUrl = GetJsVariableValue(html, "returnUrl");
                var paramId = GetJsVariableValue(html, "paramId");
                var j_rsaKey = GetFieldValue(html, "j_rsaKey");

                client.DefaultRequestHeaders.TryAddWithoutValidation("lt", lt);
                using var rsa = RSA.Create();
                rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(j_rsaKey), out var _);

                string Encrypt(string _s)
                    => BitConverter.ToString(rsa.Encrypt(Encoding.UTF8.GetBytes(_s), RSAEncryptionPadding.Pkcs1)).Replace("-", "").ToLower();

                var data = new Dictionary<string, string>
                {
                    ["appKey"] = "cloud",
                    ["accountType"] = "01",
                    ["userName"] = $"{{RSA}}{Encrypt(_username)}",
                    ["password"] = $"{{RSA}}{Encrypt(_password)}",
                    ["validateCode"] = "",
                    ["captchaToken"] = captchaToken,
                    ["returnUrl"] = returnUrl,
                    ["mailSuffix"] = "@189.cn",
                    ["paramId"] = paramId,
                };

                url = "https://open.e.189.cn/api/logbox/oauth2/loginSubmit.do";
                var c = new FormUrlEncodedContent(data);
                var s = await c.ReadAsStringAsync();
                var resp = await client.PostAsync(url, c);
                var json = await resp.Content.ReadAsStringAsync();
                var r = JsonConvert.DeserializeObject<dynamic>(json);

                string msg = r.msg;
                if (r.result != 0)
                {
                    _logger.LogError($"login failed: {msg}");
                    return false;
                }
                _logger.LogInformation(msg);

                url = r.toUrl;

                html = await client.GetStringAsync(url);

                SaveCookies();

                _logger.LogInformation("login successful.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"login failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DoAsync()
        {
            if (await TryLoginAsync() == false)
                return false;

            var rand = DateTime.Now.Ticks % 1000000;
            var url = $"https://m.cloud.189.cn/mkt/userSign.action?rand={rand}&clientType=TELEANDROID&version=8.6.3&model=SM-G930K";
            var d = await GetData(url);
            if (d == null)
            {
                _logger.LogWarning("check in failed.");
                return false;
            }
            _logger.LogInformation($"sign time: {d.signTime}, netdiskBonus: {d.netdiskBonus}M.");

            url = "https://m.cloud.189.cn/v2/drawPrizeMarketDetails.action?taskId=TASK_SIGNIN&activityId=ACT_SIGNIN";
            await DoCheckin(url);
            url = "https://m.cloud.189.cn/v2/drawPrizeMarketDetails.action?taskId=TASK_SIGNIN_PHOTOS&activityId=ACT_SIGNIN";
            await DoCheckin(url);
            async Task DoCheckin(string _url)
            {
                d = await GetData(_url);
                if (d == null)
                {
                    _logger.LogWarning("draw failed.");
                    return;
                }
                if (d.errorCode == "User_Not_Chance")
                    _logger.LogInformation("already draw.");
                else if (d.errorCode != null)
                    _logger.LogWarning($"draw failed: {d.errorCode}");
                else
                    _logger.LogInformation($"draw successful: {d.description}");
            }

            //重新保存下
            SaveCookies();

            return true;
        }

        /// <summary>
        /// 获取Js环境变量
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private string GetJsVariableValue(string html, string name)
        {
            //var reqId = "0057c94596e646bf";
            var reg = new Regex(@$" {name}.*=.*['""](?<value>.*)['""]", RegexOptions.Compiled);
            var m = reg.Match(html);
            if (m.Success)
                return m.Groups["value"].Value;
            return string.Empty;
        }

        /// <summary>
        /// 获取Html Field Value
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private string GetFieldValue(string html, string name)
        {
            //<input type='hidden' name='captchaToken' value='0846f99596be243cb833188bc81baf76kfxwyxvh'>

            var reg = new Regex(@$"{name}.+value.*=.*['""](?<value>.*)['""]", RegexOptions.Compiled);
            var m = reg.Match(html);
            if (m.Success)
                return m.Groups["value"].Value;
            return string.Empty;
        }

        /// <summary>
        /// 获取数据
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private async Task<dynamic> GetData(string url)
        {
            if (client == null)
                return null;

            var resp = await client.GetAsync(url);
            if (resp.IsSuccessStatusCode == false)
                return null;
            var json = await resp.Content.ReadAsStringAsync();
            try
            {
                return JsonConvert.DeserializeObject<dynamic>(json);
            }
            catch (Exception ex)
            {
                _logger.LogError($"get data failed: {ex.Message}");
            }

            return null;
        }

        private string GetCookieFileName()
            => $"Cookies/{_username}.cookies";

        private CookieContainer TryLoadCookies()
        {
            var fn = GetCookieFileName();
            if (File.Exists(fn) == false)
                return null;

            try
            {
                using var fs = File.OpenRead(fn);
                var formatter = new BinaryFormatter();
                return (CookieContainer)formatter.Deserialize(fs);
            }
            catch { }

            return null;
        }

        private void SaveCookies()
        {
            var fn = GetCookieFileName();
            var dir = Path.GetDirectoryName(fn);
            try
            {
                if (Directory.Exists(dir) == false)
                    Directory.CreateDirectory(dir);
                using var fs = File.OpenWrite(fn);
                var formatter = new BinaryFormatter();
                formatter.Serialize(fs, httpClientHandler.CookieContainer);
                fs.Flush();
                fs.Close();
            }
            catch { }
        }
    }
}