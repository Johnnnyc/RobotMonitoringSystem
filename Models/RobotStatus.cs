namespace RobotMonitoringSystem.Models
{
    /// <summary>
    /// 机器人状态数据模型
    /// </summary>
    public class RobotStatus
    {
        /// <summary>
        /// 主键ID
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// 设备编号
        /// </summary>
        public string DeviceId { get; set; }
        
        /// <summary>
        /// 机器人编号
        /// </summary>
        public int RobotNumber { get; set; }
        
        /// <summary>
        /// 机器人名称
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// 焊接电压
        /// </summary>
        public double WeldingVoltage { get; set; }
        
        /// <summary>
        /// 焊接电流
        /// </summary>
        public double WeldingCurrent { get; set; }
        
        /// <summary>
        /// 焊接速度
        /// </summary>
        public double WeldingSpeed { get; set; }
        
        /// <summary>
        /// 控制状态（0: 停止, 1: 运行, 2: 故障, 3: 待机）
        /// </summary>
        public int ControlStatus { get; set; }
        
        /// <summary>
        /// 运行反馈（0: 正常, 1: 异常）
        /// </summary>
        public int RunFeedback { get; set; }
        
        /// <summary>
        /// 采集时间
        /// </summary>
        public DateTime CollectionTime { get; set; }
        
        /// <summary>
        /// 焊道编号
        /// </summary>
        public int SeamNumber { get; set; }
        
        /// <summary>
        /// 焊道名称
        /// </summary>
        public string SeamName { get; set; }
        
        /// <summary>
        /// 设备编号
        /// </summary>
        public int DeviceNumber { get; set; }
        
        /// <summary>
        /// 设备名称
        /// </summary>
        public string DeviceName { get; set; }
    }
}