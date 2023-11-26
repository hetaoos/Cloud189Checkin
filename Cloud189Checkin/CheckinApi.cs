using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

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

        /// <summary>
        /// 加密公钥
        /// </summary>
        private static string rsa_public_key = "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCZLyV4gHNDUGJMZoOcYauxmNEsKrc0TlLeBEVVIIQNzG4WqjimceOj5R9ETwDeeSN3yejAKLGHgx83lyy2wBjvnbfm/nLObyWwQD/09CmpZdxoFYCH6rdDjRpwZOZ2nXSZpgkZXoOBkfNXNxnN74aXtho2dqBynTw3NFTWyQl8BQIDAQAB";

        private static string app_conf_url = "https://open.e.189.cn/api/logbox/oauth2/appConf.do";
        private static string redirect_url = "https://cloud.189.cn/api/portal/loginUrl.action?redirectURL=https://cloud.189.cn/web/redirect.html?returnURL=/main.action";
        private static string login_url = "https://open.e.189.cn/api/logbox/oauth2/loginSubmit.do";

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
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
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
            if (d != null && d["ret"]?.GetValue<int>() == 1)
            {
                _logger.LogInformation("currently logged in.");
                return true;
            }

            _logger.LogInformation("start logging in.");
            try
            {
                var resp = await client.GetAsync(redirect_url);
                var nameValuePairs = HttpUtility.ParseQueryString(resp.RequestMessage.RequestUri.Query);

                var param = new NameValueCollection(nameValuePairs);
                param.Add("rsaKey", rsa_public_key);

                var urlEncodedContent = new FormUrlEncodedContent(new Dictionary<string, string>()
                {
                    ["version"] = "2.0",
                    ["appKey"] = "cloud",
                });
                urlEncodedContent.Headers.TryAddWithoutValidation("Referer", "https://open.e.189.cn/");
                urlEncodedContent.Headers.TryAddWithoutValidation("lt", param["lt"]);
                urlEncodedContent.Headers.TryAddWithoutValidation("REQID", param["reqId"]);

                resp = await client.PostAsync(app_conf_url, urlEncodedContent);
                var json = await resp.Content.ReadAsStringAsync();
                var obj = JsonNode.Parse(json)!;
                string returnUrl = obj["data"]["returnUrl"].GetValue<string>();
                string paramId = obj["data"]["paramId"].GetValue<string>();

                client.DefaultRequestHeaders.TryAddWithoutValidation("lt", param["lt"]);
                using var rsa = RSA.Create();
                rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(rsa_public_key), out var _);

                string Encrypt(string _s)
                    => BitConverter.ToString(rsa.Encrypt(Encoding.UTF8.GetBytes(_s), RSAEncryptionPadding.Pkcs1)).Replace("-", "").ToLower();

                var data = new Dictionary<string, string>
                {
                    ["appKey"] = "cloud",
                    ["accountType"] = "01",
                    ["userName"] = $"{{NRP}}{Encrypt(_username)}",
                    ["password"] = $"{{NRP}}{Encrypt(_password)}",
                    ["validateCode"] = "",
                    ["captchaToken"] = "",
                    ["returnUrl"] = returnUrl,
                    ["mailSuffix"] = "@189.cn",
                    ["paramId"] = paramId,
                };

                var c = new FormUrlEncodedContent(data);
                var s = await c.ReadAsStringAsync();
                resp = await client.PostAsync(login_url, c);
                json = await resp.Content.ReadAsStringAsync();
                obj = JsonNode.Parse(json)!;
                var result = obj["result"].GetValue<int>();
                var msg = obj["msg"].GetValue<string>();

                if (result != 0)
                {
                    _logger.LogError($"login failed: {msg}");
                    return false;
                }
                _logger.LogInformation(msg);

                url = obj["toUrl"].GetValue<string>();

                var html = await client.GetStringAsync(url);

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
            _logger.LogInformation($"sign time: {d["signTime"]?.GetValue<DateTime?>():yyyy-MM-dd HH:mm:ss}, netdiskBonus: {d["netdiskBonus"]}M.");

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
                var errorCode = d["errorCode"]?.GetValue<string>();
                if (errorCode == "User_Not_Chance")
                    _logger.LogInformation("already draw.");
                else if (errorCode != null)
                    _logger.LogWarning($"draw failed: {errorCode}");
                else
                    _logger.LogInformation($"draw successful: {d["description"]}");
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
        private async Task<JsonNode> GetData(string url)
        {
            if (client == null)
                return null;

            var resp = await client.GetAsync(url);
            if (resp.IsSuccessStatusCode == false)
                return null;
            var json = await resp.Content.ReadAsStringAsync();
            try
            {
                var node = JsonNode.Parse(json);
                return node;
            }
            catch (Exception ex)
            {
                _logger.LogError($"get data failed: {ex.Message}");
            }

            return null;
        }

        private string GetCookieFileName()
            => $"Cookies/{_username}.v2.cookies";

        private CookieContainer TryLoadCookies()
        {
            var fn = GetCookieFileName();
            if (File.Exists(fn) == false)
                return null;

            try
            {
                var cc = new CookieContainer();
                using var fs = File.OpenRead(fn);
                var cookies = JsonSerializer.Deserialize(fs, MyJsonSerializerContext.Default.ListCookie);
                foreach (var cookie in cookies)
                    cc.Add(cookie);

                return cc;
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

                var cookies = httpClientHandler.CookieContainer.GetAllCookies().ToList();
                var bytes = JsonSerializer.SerializeToUtf8Bytes(cookies, MyJsonSerializerContext.Default.ListCookie);
                File.WriteAllBytes(fn, bytes);
            }
            catch { }
        }
    }

    [JsonSerializable(typeof(List<Cookie>))]
    [JsonSerializable(typeof(Cookie))]
    [JsonSerializable(typeof(Config))]
    [JsonSerializable(typeof(Account))]
    internal partial class MyJsonSerializerContext : JsonSerializerContext
    {
    }
}