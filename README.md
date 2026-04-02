# 机器人数字孪生状态监测系统

## 项目简介

本项目是一个基于C# Web API和Three.js的机器人数字孪生状态监测系统，主要功能包括：

1. 后端通过HSL实时采集30台机器人的状态数据
2. 采集数据包括焊接数据、控制状态数据、运行反馈数据
3. 数据采集周期为200ms
4. 数据存储到SQL数据库
5. 前端使用Three.js搭建数字孪生状态监测网页
6. 网页展示纵向两列设备，每列8个，每个里面包含1-2个机器人
7. 鼠标悬停在机器人上时，显示其各项实时数据

## 项目结构

```
RobotMonitoringSystem/
├── Controllers/           # API控制器
│   └── RobotController.cs # 机器人状态API
├── Data/                  # 数据访问层
│   └── RobotDbContext.cs  # 数据库上下文
├── Models/                # 数据模型
│   └── RobotStatus.cs     # 机器人状态模型
├── Services/              # 业务逻辑层
│   └── HslService.cs      # HSL服务，用于实时采集数据
├── wwwroot/               # 静态文件
│   └── index.html         # 前端页面，使用Three.js
├── appsettings.json       # 应用配置
├── Program.cs             # 应用入口
└── RobotMonitoringSystem.csproj # 项目文件
```

## 技术栈

- 后端：C# .NET 8.0, ASP.NET Core Web API
- 前端：HTML5, JavaScript, Three.js
- 数据库：SQL Server
- 实时通信：HSL（模拟）

## 安装和运行

### 1. 安装.NET SDK

确保你的系统已经安装了.NET 8.0 SDK。你可以从[Microsoft官网](https://dotnet.microsoft.com/download)下载安装。

### 2. 克隆或下载项目

将项目文件下载到本地。

### 3. 运行后端服务

在项目根目录下，打开命令行终端，执行以下命令：

```bash
dotnet run
```

后端服务将启动，默认监听端口为5001（HTTPS）和5000（HTTP）。

### 4. 访问前端页面

打开浏览器，访问以下URL：

```
http://localhost:5000/index.html
```

或者

```
https://localhost:5001/index.html
```

### 5. 访问API文档

后端提供了Swagger API文档，你可以通过以下URL访问：

```
http://localhost:5000/swagger
```

或者

```
https://localhost:5001/swagger
```

## API接口

### 获取所有机器人状态

```
GET /api/robot
```

返回所有机器人的实时状态数据。

### 获取特定机器人状态

```
GET /api/robot/{id}
```

返回指定ID的机器人实时状态数据。

## 前端功能

1. **3D可视化**：使用Three.js创建机器人的3D模型
2. **实时数据更新**：每200ms从后端获取一次数据
3. **状态指示器**：机器人头部的状态灯根据控制状态显示不同颜色
   - 绿色：运行
   - 橙色：待机
   - 灰色：停止
   - 红色：故障
4. **鼠标悬停**：鼠标悬停在机器人上时，显示其详细数据
5. **统计信息**：右侧面板显示机器人状态统计
6. **控制面板**：左侧面板提供开始/停止模拟、重置视角等功能

## 数据模型

### RobotStatus

| 字段名 | 类型 | 描述 |
|--------|------|------|
| Id | int | 主键ID |
| DeviceId | string | 设备编号 |
| RobotNumber | int | 机器人编号 |
| WeldingVoltage | double | 焊接电压 |
| WeldingCurrent | double | 焊接电流 |
| WeldingSpeed | double | 焊接速度 |
| ControlStatus | int | 控制状态（0: 停止, 1: 运行, 2: 故障, 3: 待机） |
| RunFeedback | int | 运行反馈（0: 正常, 1: 异常） |
| CollectionTime | DateTime | 采集时间 |

## 注意事项

1. 本项目中的HSL服务是模拟实现，实际使用时需要替换为真实的HSL通信代码
2. 数据库连接字符串配置在appsettings.json文件中，实际使用时需要根据你的数据库配置进行修改
3. 前端页面中的API请求地址需要根据实际后端服务地址进行修改
4. 项目使用了CORS中间件，允许所有来源的请求，实际生产环境中需要根据需求进行配置

## 扩展建议

1. 添加历史数据查询功能
2. 添加异常告警功能
3. 添加数据分析和报表功能
4. 添加用户认证和授权
5. 优化前端性能，支持更多机器人的实时显示
6. 添加机器人控制功能

## 许可证

MIT License
