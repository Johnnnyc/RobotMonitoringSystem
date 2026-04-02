using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.StaticFiles;
using RobotMonitoringSystem.Data;
using RobotMonitoringSystem.Services;

var builder = WebApplication.CreateBuilder(args);

// 移除了有问题的URL配置，让应用程序使用默认配置或appsettings.json中的Kestrel配置

// 添加服务
//builder.Services.AddDbContext<RobotDbContext>(options =>
//    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 注册HslService，使用单例模式，并注入IConfiguration和ILogger
builder.Services.AddSingleton<HslService>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var logger = provider.GetRequiredService<ILogger<HslService>>();
    return new HslService(configuration, logger);
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader());
});

var app = builder.Build();

// 配置中间件
// 移除HTTPS重定向，避免证书问题
// app.UseHttpsRedirection();

// 配置静态文件服务（必须在路由之前）
app.UseDefaultFiles(); // 允许访问默认文件（如 index.html）

// 配置静态文件服务，添加 GLB 文件的 MIME 类型支持
var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".glb"] = "model/gltf-binary";
provider.Mappings[".gltf"] = "model/gltf+json";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});

// 简化Swagger配置，放在前面以便不需要授权即可访问
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Robot Monitoring API V1");
    // 移除了根路径设置，使用默认的/swagger路径，这样静态文件服务就可以正常工作了
});

// CORS配置
app.UseCors("AllowAll");

// 授权配置
app.UseAuthorization();

// 添加一个简单的健康检查端点，放在API路由前面
app.MapGet("/health", () => "API is running");

// 配置API路由
app.MapControllers();

// 启动HSL服务
var hslService = app.Services.GetRequiredService<HslService>();
hslService.Start();

app.Run();