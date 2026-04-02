using Microsoft.EntityFrameworkCore;
using RobotMonitoringSystem.Models;

namespace RobotMonitoringSystem.Data
{
    /// <summary>
    /// 机器人数据库上下文
    /// </summary>
    public class RobotDbContext : DbContext
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="options">数据库上下文选项</param>
        public RobotDbContext(DbContextOptions<RobotDbContext> options) : base(options)
        {}
        
        /// <summary>
        /// 机器人状态数据表
        /// </summary>
        public DbSet<RobotStatus> RobotStatuses { get; set; }
    }
}