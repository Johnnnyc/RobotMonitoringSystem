using System.Timers;
using RobotMonitoringSystem.Models;
using RobotMonitoringSystem.Data;
using HslCommunication.Profinet.Melsec;
using HslCommunication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.IO;

namespace RobotMonitoringSystem.Services
{
    /// <summary>
    /// 工作站配置类
    /// </summary>
    public class WorkstationConfig
    {
        /// <summary>
        /// 工作站名称
        /// </summary>
        public string WorkstationName { get; set; }
        
        /// <summary>
        /// IP地址
        /// </summary>
        public string IpAddress { get; set; }
        
        /// <summary>
        /// 端口号
        /// </summary>
        public int Port { get; set; }
        
        /// <summary>
        /// 机器人数量寄存器地址
        /// </summary>
        public string RobotCountRegister { get; set; }
        
        /// <summary>
        /// 机器人配置列表
        /// </summary>
        public List<RobotRegisterConfig> Robots { get; set; }
    }
    
    /// <summary>
    /// 机器人寄存器配置类
    /// </summary>
    public class RobotRegisterConfig
    {
        /// <summary>
        /// 机器人编号
        /// </summary>
        public int RobotNumber { get; set; }
        
        /// <summary>
        /// 焊道编号寄存器地址
        /// </summary>
        public string SeamNumberRegister { get; set; }
        
        /// <summary>
        /// 焊接电压寄存器地址
        /// </summary>
        public string VoltageRegister { get; set; }
        
        /// <summary>
        /// 焊接电流寄存器地址
        /// </summary>
        public string CurrentRegister { get; set; }
        
        /// <summary>
        /// 送丝速度寄存器地址
        /// </summary>
        public string SpeedRegister { get; set; }
    }
    
    /// <summary>
    /// 用于跟踪焊接速度的计数器
    /// </summary>
    public class SpeedCounter
    {
        public int ZeroCount { get; set; } = 0;
        public double LastSpeed { get; set; } = -1;
    }
    
    /// <summary>
    /// HSL服务，用于实时采集机器人状态数据
    /// </summary>
    public class HslService
    {
        private readonly System.Timers.Timer _timer;
        private readonly List<RobotStatus> _robotStatuses;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private readonly Dictionary<string, MelsecMcNet> _plcConnections;
        private readonly List<WorkstationConfig> _workstationConfigs;
        private readonly ILogger<HslService>? _logger;
        // 添加焊接速度计数器字典
        private readonly Dictionary<string, SpeedCounter> _speedCounters = new Dictionary<string, SpeedCounter>();
        private readonly string _configFilePath;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="configuration">配置信息</param>
        /// <param name="logger">日志记录器</param>
        public HslService(IConfiguration configuration, ILogger<HslService>? logger = null)
        {
            _logger = logger;
            _robotStatuses = new List<RobotStatus>();
            _plcConnections = new Dictionary<string, MelsecMcNet>();
            
            // 设置配置文件路径
            _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RobotConfig.csv");
            Console.WriteLine($"配置文件路径: {_configFilePath}");
            Console.WriteLine($"配置文件是否存在: {File.Exists(_configFilePath)}");
            CsvConfigHelper.SetConfigFilePath(_configFilePath);
            
            // 从CSV配置文件读取工作站配置
            _workstationConfigs = LoadWorkstationConfigs();
            Console.WriteLine($"加载的工作站配置数量: {_workstationConfigs.Count}");
            
            // 初始化机器人的初始状态
            InitializeRobotStatuses();
            Console.WriteLine($"初始化的机器人状态数量: {_robotStatuses.Count}");
            
            // 初始化定时器，将采集频率从200ms降低到500ms，减少线程阻塞
            _timer = new System.Timers.Timer(500);
            _timer.Elapsed += new System.Timers.ElapsedEventHandler(OnTimedEvent);
        }
        
        /// <summary>
        /// 从CSV配置文件加载工作站配置
        /// </summary>
        /// <returns>工作站配置列表</returns>
        private List<WorkstationConfig> LoadWorkstationConfigs()
        {
            var configs = new List<WorkstationConfig>();
            
            try
            {
                configs = CsvConfigHelper.ReadConfig();
                
                string message = $"成功从配置文件加载{configs.Count}个工作站配置";
                _logger?.LogInformation(message);
                Console.WriteLine(message);
            }
            catch (Exception ex)
            {
                string message = $"加载配置文件异常: {ex.Message}";
                _logger?.LogError(ex, message);
                Console.WriteLine(message);
            }
            
            return configs;
        }
        
        /// <summary>
        /// 初始化机器人状态
        /// </summary>
        private void InitializeRobotStatuses()
        {
            foreach (var config in _workstationConfigs)
            {
                foreach (var robotConfig in config.Robots)
                {
                    _robotStatuses.Add(new RobotStatus
                    {
                        DeviceId = $"{config.WorkstationName}_Robot{robotConfig.RobotNumber}",
                        RobotNumber = robotConfig.RobotNumber,
                        Name = $"{config.WorkstationName}机器人{robotConfig.RobotNumber}",
                        WeldingVoltage = 0,
                        WeldingCurrent = 0,
                        WeldingSpeed = 0,
                        ControlStatus = 3, // 初始状态为待机
                        RunFeedback = 0,   // 初始状态为正常
                        CollectionTime = DateTime.Now,
                        SeamNumber = 0, // 焊道编号将从PLC读取
                        SeamName = "",
                        DeviceNumber = 0,
                        DeviceName = config.WorkstationName
                    });
                    
                    // 为每个机器人初始化速度计数器
                    string key = $"{config.IpAddress}_{config.Port}_Robot{robotConfig.RobotNumber}";
                    _speedCounters[key] = new SpeedCounter();
                }
            }
        }
        
        /// <summary>
        /// 初始化PLC连接对象
        /// </summary>
        private void InitializePlcConnections()
        {
            foreach (var config in _workstationConfigs)
            {
                try
                {
                    string plcKey = $"{config.IpAddress}_{config.Port}";
                    if (_plcConnections.ContainsKey(plcKey))
                    {
                        continue; // 已存在连接，跳过
                    }
                    
                    MelsecMcNet plc = new MelsecMcNet(config.IpAddress, config.Port);
                    
                    // 连接PLC
                    OperateResult connectResult = plc.ConnectServer();
                    if (connectResult.IsSuccess)
                    {
                        _plcConnections[plcKey] = plc;
                        string message = $"成功连接到工作站{config.WorkstationName}的PLC: {config.IpAddress}:{config.Port}";
                        _logger?.LogInformation(message);
                        Console.WriteLine(message);
                    }
                    else
                    {
                        string message = $"连接工作站{config.WorkstationName}的PLC失败: {connectResult.Message}";
                        _logger?.LogError(message);
                        Console.WriteLine(message);
                    }
                }
                catch (Exception ex)
                {
                    string message = $"初始化工作站{config.WorkstationName}的PLC连接异常: {ex.Message}";
                    _logger?.LogError(ex, message);
                    Console.WriteLine(message);
                }
            }
        }
        
        /// <summary>
        /// 连接到特定工作站的PLC
        /// </summary>
        /// <param name="config">工作站配置</param>
        /// <returns>连接是否成功</returns>
        private bool ConnectToPlc(WorkstationConfig config)
        {
            try
            {
                string plcKey = $"{config.IpAddress}_{config.Port}";
                
                // 创建或获取PLC连接对象
                MelsecMcNet plc;
                if (_plcConnections.ContainsKey(plcKey))
                {
                    plc = _plcConnections[plcKey];
                    // 先关闭现有连接
                    plc.ConnectClose();
                }
                else
                {
                    plc = new MelsecMcNet(config.IpAddress, config.Port);
                }
                
                // 连接PLC
                OperateResult connectResult = plc.ConnectServer();
                if (connectResult.IsSuccess)
                {
                    _plcConnections[plcKey] = plc;
                    string message = $"成功连接到工作站{config.WorkstationName}的PLC: {config.IpAddress}:{config.Port}";
                    _logger?.LogInformation(message);
                    Console.WriteLine(message);
                    return true;
                }
                else
                {
                    string message = $"连接工作站{config.WorkstationName}的PLC失败: {connectResult.Message}";
                    _logger?.LogError(message);
                    Console.WriteLine(message);
                    return false;
                }
            }
            catch (Exception ex)
            {
                string message = $"连接工作站{config.WorkstationName}的PLC异常: {ex.Message}";
                _logger?.LogError(ex, message);
                Console.WriteLine(message);
                return false;
            }
        }
        
        /// <summary>
        /// 启动HSL服务
        /// </summary>
        public void Start()
        {
            _timer.Start();
            string message = "HSL服务已启动，开始采集机器人状态数据...";
            _logger?.LogInformation(message);
            Console.WriteLine(message);
            
            // 异步初始化PLC连接，不会阻塞HTTP服务的启动
            Task.Run(() => InitializePlcConnections());
        }
        
        /// <summary>
        /// 停止HSL服务
        /// </summary>
        public void Stop()
        {
            _timer.Stop();
            
            // 关闭所有PLC连接
            foreach (var plcEntry in _plcConnections)
            {
                try
                {
                    plcEntry.Value.ConnectClose();
                    string message = $"已关闭PLC连接: {plcEntry.Key}";
                    _logger?.LogInformation(message);
                    Console.WriteLine(message);
                }
                catch (Exception ex)
                {
                    string message = $"关闭PLC连接{plcEntry.Key}异常: {ex.Message}";
                    _logger?.LogError(ex, message);
                    Console.WriteLine(message);
                }
            }
            
            _plcConnections.Clear();
            string stopMessage = "HSL服务已停止，停止采集机器人状态数据...";
            _logger?.LogInformation(stopMessage);
            Console.WriteLine(stopMessage);
        }
        
        /// <summary>
        /// 定时事件处理函数，每500ms执行一次
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void OnTimedEvent(object? sender, ElapsedEventArgs e)
        {
            // 将PLC数据采集操作放在一个单独的线程中执行，这样就不会阻塞HTTP请求的处理
            Task.Run(() =>
            {
                int successCount = 0;
                
                // 遍历所有工作站配置
                foreach (var config in _workstationConfigs)
                {
                    try
                    {
                        string plcKey = $"{config.IpAddress}_{config.Port}";

                        // 检查PLC连接是否存在
                        MelsecMcNet plc = null;
                        bool plcConnected = false;

                        // 使用读锁检查PLC连接是否存在
                        _lock.EnterReadLock();
                        try
                        {
                            plcConnected = _plcConnections.ContainsKey(plcKey);
                            if (plcConnected)
                            {
                                plc = _plcConnections[plcKey];
                            }
                        }
                        finally
                        {
                            _lock.ExitReadLock();
                        }

                        // 如果PLC连接不存在，尝试连接
                        if (!plcConnected)
                        {
                            string message = $"工作站{config.WorkstationName}的PLC连接不存在，尝试连接...";
                            _logger?.LogWarning(message);
                            Console.WriteLine(message);

                            if (!ConnectToPlc(config))
                            {
                                message = $"连接工作站{config.WorkstationName}的PLC失败，跳过本次采集";
                                _logger?.LogError(message);
                                Console.WriteLine(message);
                                continue;
                            }

                            // 获取刚连接的PLC
                            _lock.EnterReadLock();
                            try
                            {
                                if (_plcConnections.ContainsKey(plcKey))
                                {
                                    plc = _plcConnections[plcKey];
                                    plcConnected = true;
                                }
                            }
                            finally
                            {
                                _lock.ExitReadLock();
                            }

                            if (!plcConnected)
                            {
                                string connMessage = $"获取工作站{config.WorkstationName}的PLC连接失败，跳过本次采集";
                                _logger?.LogError(connMessage);
                                Console.WriteLine(connMessage);
                                continue;
                            }
                        }

                        // 读取机器人数量
                        int robotCount = 1; // 默认1个机器人
                        OperateResult<ushort> robotCountResult = plc.ReadUInt16(config.RobotCountRegister);
                        if (robotCountResult.IsSuccess)
                        {
                            robotCount = robotCountResult.Content;
                            // 限制最多2个机器人
                            if (robotCount > 2)
                            {
                                robotCount = 2;
                            }
                            else if (robotCount < 1)
                            {
                                robotCount = 1;
                            }
                        }
                        else
                        {
                            _logger?.LogWarning($"读取工作站{config.WorkstationName}机器人数量失败: {robotCountResult.Message}");
                        }

                        // 根据读取到的机器人数量进行采集
                        for (int i = 0; i < robotCount; i++)
                        {
                            var robotConfig = config.Robots[i];
                            try
                            {
                                // 读取机器人状态数据
                                ushort seamNumber = 0;
                                ushort voltage = 0;
                                ushort current = 0;
                                ushort speed = 0;

                                // 执行耗时的PLC通信操作，不占用锁
                                // 读取焊道编号
                                OperateResult<ushort> seamResult = plc.ReadUInt16(robotConfig.SeamNumberRegister);
                                if (seamResult.IsSuccess)
                                {
                                    seamNumber = seamResult.Content;
                                }
                                else
                                {
                                    _logger?.LogWarning($"读取工作站{config.WorkstationName}机器人{robotConfig.RobotNumber}焊道编号失败: {seamResult.Message}");
                                }

                                // 读取焊接电压
                                OperateResult<ushort> voltageResult = plc.ReadUInt16(robotConfig.VoltageRegister);
                                if (voltageResult.IsSuccess)
                                {
                                    voltage = voltageResult.Content;
                                }
                                else
                                {
                                    _logger?.LogWarning($"读取工作站{config.WorkstationName}机器人{robotConfig.RobotNumber}焊接电压失败: {voltageResult.Message}");
                                }

                                // 读取焊接电流
                                OperateResult<ushort> currentResult = plc.ReadUInt16(robotConfig.CurrentRegister);
                                if (currentResult.IsSuccess)
                                {
                                    current = currentResult.Content;
                                }
                                else
                                {
                                    _logger?.LogWarning($"读取工作站{config.WorkstationName}机器人{robotConfig.RobotNumber}焊接电流失败: {currentResult.Message}");
                                }

                                // 读取送丝速度
                                OperateResult<ushort> speedResult = plc.ReadUInt16(robotConfig.SpeedRegister);
                                if (speedResult.IsSuccess)
                                {
                                    speed = speedResult.Content;
                                }
                                else
                                {
                                    _logger?.LogWarning($"读取工作站{config.WorkstationName}机器人{robotConfig.RobotNumber}送丝速度失败: {speedResult.Message}");
                                }

                                // 只在更新机器人状态时获取写锁
                                _lock.EnterWriteLock();
                                try
                                {
                                    // 获取速度计数器
                                    string speedCounterKey = $"{config.IpAddress}_{config.Port}_Robot{robotConfig.RobotNumber}";
                                    if (!_speedCounters.ContainsKey(speedCounterKey))
                                    {
                                        _speedCounters[speedCounterKey] = new SpeedCounter();
                                    }
                                    var speedCounter = _speedCounters[speedCounterKey];

                                    // 检查焊接速度是否为0
                                    if (speed == 0)
                                    {
                                        // 如果上次速度不是0或为空，重置计数
                                        if (speedCounter.LastSpeed != 0)
                                        {
                                            speedCounter.ZeroCount = 1;
                                        }
                                        else
                                        {
                                            speedCounter.ZeroCount++;
                                        }
                                    }
                                    else
                                    {
                                        // 如果速度不为0，重置计数
                                        speedCounter.ZeroCount = 0;
                                    }

                                    // 更新最后的速度值
                                    speedCounter.LastSpeed = speed;

                                    // 更新机器人状态
                                    var robotStatus = _robotStatuses.FirstOrDefault(r =>
                                        r.Name == $"{config.WorkstationName}机器人{robotConfig.RobotNumber}");
                                    if (robotStatus != null)
                                    {
                                        // 如果焊接速度连续3次为0，则将电压/10，电流也为0
                                        if (speedCounter.ZeroCount >= 3)
                                        {
                                            robotStatus.WeldingVoltage = 0;
                                            robotStatus.WeldingCurrent = 0;                 // 电流设为0
                                            robotStatus.WeldingSpeed = 0;                   // 速度设为0
                                        }
                                        else
                                        {
                                            robotStatus.WeldingVoltage = voltage / 10.0;  // 直接赋值ushort到double
                                            robotStatus.WeldingCurrent = current;  // 直接赋值ushort到double
                                            robotStatus.WeldingSpeed = speed / 10.0;      // 直接赋值ushort到double
                                        }

                                        robotStatus.SeamNumber = seamNumber;
                                        robotStatus.CollectionTime = DateTime.Now;
                                    }

                                    successCount++;
                                }
                                finally
                                {
                                    _lock.ExitWriteLock();
                                }
                            }
                            catch (Exception ex)
                            {
                                string message = $"采集工作站{config.WorkstationName}机器人{robotConfig.RobotNumber}状态数据异常: {ex.Message}";
                                _logger?.LogError(ex, message);
                                Console.WriteLine(message);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        string message = $"采集工作站{config.WorkstationName}状态数据异常: {ex.Message}";
                        _logger?.LogError(ex, message);
                        Console.WriteLine(message);

                        // 连接异常，移除当前连接，下次采集时重新连接
                        string plcKey = $"{config.IpAddress}_{config.Port}";
                        _lock.EnterWriteLock();
                        try
                        {
                            if (_plcConnections.ContainsKey(plcKey))
                            {
                                try
                                {
                                    _plcConnections[plcKey].ConnectClose();
                                }
                                catch { }
                                _plcConnections.Remove(plcKey);
                            }
                        }
                        finally
                        {
                            _lock.ExitWriteLock();
                        }
                    }
                }
                
                string successMessage = $"本次采集成功{successCount}个机器人状态数据";
                _logger?.LogInformation(successMessage);
                // 仅在调试模式下输出到控制台
                if (_logger == null || _logger.IsEnabled(LogLevel.Debug))
                {
                    Console.WriteLine(successMessage);
                }
            });
        }
        
        /// <summary>
        /// 根据PLC IP地址和端口获取机器人状态
        /// </summary>
        /// <param name="plcIp">PLC IP地址</param>
        /// <param name="plcPort">PLC端口号</param>
        /// <returns>机器人状态列表（最多返回两个机器人的状态）</returns>
        public List<RobotStatus> GetRobotStatusByPlc(string plcIp, int plcPort)
        {
            // 使用读锁来保护读取操作
            _lock.EnterReadLock();
            try
            {
                // 查找使用指定PLC的工作站配置
                var workstationConfig = _workstationConfigs.FirstOrDefault(config => 
                    config.IpAddress == plcIp && config.Port == plcPort);
                
                if (workstationConfig == null)
                {
                    return new List<RobotStatus>();
                }
                
                // 收集该工作站的机器人状态
                List<RobotStatus> result = new List<RobotStatus>();
                foreach (var robotConfig in workstationConfig.Robots.Take(2)) // 最多返回两个机器人
                {
                    var status = _robotStatuses.FirstOrDefault(r => 
                        r.Name == $"{workstationConfig.WorkstationName}机器人{robotConfig.RobotNumber}");
                    
                    if (status != null)
                    {
                        // 为每个机器人状态创建一个新的对象，避免直接引用
                        result.Add(new RobotStatus
                        {
                            DeviceId = status.DeviceId,
                            RobotNumber = status.RobotNumber,
                            Name = status.Name,
                            WeldingVoltage = status.WeldingVoltage,
                            WeldingCurrent = status.WeldingCurrent,
                            WeldingSpeed = status.WeldingSpeed,
                            ControlStatus = status.ControlStatus,
                            RunFeedback = status.RunFeedback,
                            CollectionTime = status.CollectionTime,
                            SeamNumber = status.SeamNumber,
                            SeamName = status.SeamName,
                            DeviceNumber = status.DeviceNumber,
                            DeviceName = status.DeviceName
                        });
                    }
                }
                
                return result;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        
        /// <summary>
        /// 获取所有机器人状态
        /// </summary>
        /// <returns>所有机器人状态列表</returns>
        public List<RobotStatus> GetAllRobotStatuses()
        {
            // 使用读锁来保护读取操作
            _lock.EnterReadLock();
            try
            {
                // 为每个机器人状态创建一个新的对象，避免直接引用
                var result = new List<RobotStatus>();
                foreach (var status in _robotStatuses)
                {
                    result.Add(new RobotStatus
                    {
                        DeviceId = status.DeviceId,
                        RobotNumber = status.RobotNumber,
                        Name = status.Name,
                        WeldingVoltage = status.WeldingVoltage,
                        WeldingCurrent = status.WeldingCurrent,
                        WeldingSpeed = status.WeldingSpeed,
                        ControlStatus = status.ControlStatus,
                        RunFeedback = status.RunFeedback,
                        CollectionTime = status.CollectionTime,
                        SeamNumber = status.SeamNumber,
                        SeamName = status.SeamName,
                        DeviceNumber = status.DeviceNumber,
                        DeviceName = status.DeviceName
                    });
                }
                
                return result;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        
        /// <summary>
        /// 重新加载配置文件
        /// </summary>
        public void ReloadConfig()
        {
            _lock.EnterWriteLock();
            try
            {
                var newConfigs = CsvConfigHelper.ReloadConfig();
                
                // 清空旧的配置和状态
                _workstationConfigs.Clear();
                _robotStatuses.Clear();
                _speedCounters.Clear();
                
                // 添加新的配置
                foreach (var config in newConfigs)
                {
                    _workstationConfigs.Add(config);
                }
                
                // 初始化新的机器人状态
                InitializeRobotStatuses();
                
                // 重新连接PLC
                InitializePlcConnections();
                
                string message = "配置文件已重新加载";
                _logger?.LogInformation(message);
                Console.WriteLine(message);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
    }
    
    /// <summary>
    /// CSV配置读取辅助类
    /// </summary>
    public class CsvConfigHelper
    {
        private static string _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RobotConfig.csv");
        
        /// <summary>
        /// 设置配置文件路径
        /// </summary>
        /// <param name="filePath">配置文件路径</param>
        public static void SetConfigFilePath(string filePath)
        {
            _configFilePath = filePath;
        }
        
        /// <summary>
        /// 读取配置文件
        /// </summary>
        /// <returns>工作站配置列表</returns>
        public static List<WorkstationConfig> ReadConfig()
        {
            var configs = new List<WorkstationConfig>();
            
            if (!File.Exists(_configFilePath))
            {
                Console.WriteLine($"配置文件不存在: {_configFilePath}");
                return configs;
            }
            
            try
            {
                var lines = File.ReadAllLines(_configFilePath);
                if (lines.Length < 2) // 至少需要标题行和一行数据
                {
                    Console.WriteLine("配置文件数据不足");
                    return configs;
                }
                
                // 解析标题行
                var headers = lines[0].Split(',');
                var headerIndex = new Dictionary<string, int>();
                for (int i = 0; i < headers.Length; i++)
                {
                    headerIndex[headers[i]] = i;
                }
                
                // 解析数据行
                for (int i = 1; i < lines.Length; i++)
                {
                    var values = lines[i].Split(',');
                    if (values.Length < headers.Length)
                    {
                        Console.WriteLine($"第{i + 1}行数据格式错误");
                        continue;
                    }
                    
                    var config = new WorkstationConfig
                    {
                        WorkstationName = values[headerIndex["工作站名称"]],
                        IpAddress = values[headerIndex["IP地址"]],
                        Port = int.Parse(values[headerIndex["port端口"]]),
                        RobotCountRegister = values[headerIndex["机器人数量寄存器"]],
                        Robots = new List<RobotRegisterConfig>()
                    };
                    
                    // 始终添加1号机器人配置（作为默认配置）
                    var robot1 = new RobotRegisterConfig
                    {
                        RobotNumber = 1,
                        SeamNumberRegister = values[headerIndex["1号机器人焊道编号"]],
                        VoltageRegister = values[headerIndex["1号机器人焊接电压"]],
                        CurrentRegister = values[headerIndex["1号机器人焊接电流"]],
                        SpeedRegister = values[headerIndex["1号机器人送丝速度"]]
                    };
                    config.Robots.Add(robot1);
                    
                    // 始终添加2号机器人配置（作为备用配置）
                    var robot2 = new RobotRegisterConfig
                    {
                        RobotNumber = 2,
                        SeamNumberRegister = values[headerIndex["2号机器人焊道编号"]],
                        VoltageRegister = values[headerIndex["2号机器人焊接电压"]],
                        CurrentRegister = values[headerIndex["2号机器人焊接电流"]],
                        SpeedRegister = values[headerIndex["2号机器人送丝速度"]]
                    };
                    config.Robots.Add(robot2);
                    
                    configs.Add(config);
                }
                
                string message = $"成功从配置文件加载{configs.Count}个工作站配置";
                Console.WriteLine(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取配置文件异常: {ex.Message}");
            }
            
            return configs;
        }
        
        /// <summary>
        /// 重新加载配置文件
        /// </summary>
        /// <returns>工作站配置列表</returns>
        public static List<WorkstationConfig> ReloadConfig()
        {
            return ReadConfig();
        }
    }
}
