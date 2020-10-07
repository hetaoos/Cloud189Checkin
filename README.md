# Cloud189Checkin
天翼云盘自动签到服务

## 配置说明

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

## Docker 部署

### 创建目录
```
mkdir Cloud189Checkin/Cookies -p && cd Cloud189Checkin
```

### 创建 `appsettings.json` 配置文件

```json
{
  "Config": {
    "Times": [ "07:10:00", "22:30:00" ],
    "Accounts": [
      {
        "UserName": "189xxxx",
        "Password": "p@ssw0rd"
      }
    ]
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Warning",
      "Hangfire": "Warning"
    }
  }
}
```

### 创建 `docker-compose.yaml` 配置文件
```yaml
version: '3.7'

services:
  cloud189checkin:
    image: hetaoos/cloud189checkin:latest
    container_name: cloud189checkin
    restart: always
    network_mode: bridge
    volumes:
      - ./Cookies:/app/Cookies
      - ./appsettings.json:/app/appsettings.json
```

### 启动
>docker-compose up -d