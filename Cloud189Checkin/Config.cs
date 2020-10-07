using System;

namespace Cloud189Checkin
{
    /// <summary>
    /// 配置
    /// </summary>
    public class Config
    {
        /// <summary>
        /// 每日签到时间
        /// </summary>
        public TimeSpan[] Times { get; set; }

        /// <summary>
        /// 账号列表
        /// </summary>
        public Account[] Accounts { get; set; }

        /// <summary>
        /// 启动后执行的操作：默认为2，0：不执行操作；1，尝试登录；2，尝试签到
        /// </summary>
        public int? RestartAction { get; set; } = 2;
    }

    /// <summary>
    /// 账号
    /// </summary>
    public class Account
    {
        /// <summary>
        /// 是否启用，默认为启用
        /// </summary>
        public bool? Enable { get; set; } = true;

        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 密码
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// 是否有效
        /// </summary>
        /// <returns></returns>
        public bool IsValid()
            => !(Enable == false || string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Password));

        public override string ToString()
            => UserName;
    }
}