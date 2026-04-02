using Microsoft.AspNetCore.Mvc;
using RobotMonitoringSystem.Services;
using RobotMonitoringSystem.Models;

namespace RobotMonitoringSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RobotController : ControllerBase
    {
        private readonly HslService _hslService;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="hslService">HSL服务</param>
        public RobotController(HslService hslService)
        {
            _hslService = hslService;
        }
        
        /// <summary>
        /// 获取所有机器人状态
        /// </summary>
        /// <returns>机器人状态列表</returns>
        [HttpGet]
        public ActionResult<IEnumerable<RobotStatus>> GetAll()
        {
            var robotStatuses = _hslService.GetAllRobotStatuses();
            return Ok(robotStatuses);
        }
        
        /// <summary>
        /// 根据PLC IP和端口获取机器人状态
        /// </summary>
        /// <param name="plcIp">PLC IP地址</param>
        /// <param name="plcPort">PLC端口号</param>
        /// <returns>机器人状态列表</returns>
        [HttpGet("by-plc")]
        public ActionResult<IEnumerable<RobotStatus>> GetByPlc([FromQuery] string plcIp, [FromQuery] int plcPort)
        {
            var robotStatuses = _hslService.GetRobotStatusByPlc(plcIp, plcPort);
            return Ok(robotStatuses);
        }
    }
}