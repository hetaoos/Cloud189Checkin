# Cloud189Checkin
天翼云盘自动签到服务

### 配置

编辑 `appsettings.json` 文件内的以下配置
```json
{
  "Config": {
    "Times": [ "07:10:00", "22:30:00" ],
    "Accounts": [
      {
        "Enable": true,
        "UserName": "13800138000",
        "Password": "p@ssw0rd"
      }
    ]
  }
}
```

字段说明：
- `Times` 执行签到时间列表，`"07:10:00"` 表示在 7点10分执行一次，秒部分无效，但不能省略。
- `Accounts` 账号列表，