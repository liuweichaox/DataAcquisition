using System.Text;
using HslCommunication.Profinet.Melsec;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DataAcquisition.Simulator;

internal class Program
{
    // 寄存器地址常量
    private const string D100_HEARTBEAT = "D100";
    private const string D6000_TEMPERATURE = "D6000";
    private const string D6001_PRESSURE = "D6001";
    private const string D6002_CURRENT = "D6002";
    private const string D6003_VOLTAGE = "D6003";
    private const string D6004_LIGHT_BARRIER = "D6004";
    private const string D6005_SERVO_SPEED = "D6005";
    private const string D6006_DEVICE_FLAG = "D6006";

    private static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        PrintHeader();

        // 加载配置
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true)
            .AddCommandLine(args)
            .Build();

        var port = configuration.GetValue("Port", 502);

        // 创建日志
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        var logger = loggerFactory.CreateLogger<Simulator>();

        // 监听 Ctrl+C 退出
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true; // 阻止进程立即退出
            cts.Cancel();    // 触发取消
            logger.LogInformation("收到退出信号，正在关闭...");
        };

        // 创建并启动模拟器
        using var simulator = new Simulator(port, logger);
        simulator.Start();
        PrintInfo(port);

        try
        {
            // 保持应用运行，实时数据输出已在 UpdateSimulatedData 中进行
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("应用已退出");
        }
        finally
        {
            simulator.Stop();
            logger.LogInformation("模拟器已关闭");
        }
    }

    private static void PrintHeader()
    {
        Console.WriteLine("=".PadRight(60, '='));
        Console.WriteLine("Plc 模拟器 - DataAcquisition Simulator");
        Console.WriteLine("=".PadRight(60, '='));
        Console.WriteLine();
    }

    private static void PrintInfo(int port)
    {
        Console.WriteLine($"Plc 模拟器运行在 0.0.0.0:{port}");
        Console.WriteLine($"提示: 使用 'telnet 127.0.0.1 {port}' 测试连接");
        Console.WriteLine($"提示: 使用 'netstat -ano | findstr :{port}' 检查端口监听状态");
        Console.WriteLine();
        Console.WriteLine("支持的寄存器范围: D0-D9999");
        Console.WriteLine("测试寄存器（自动更新）:");
        Console.WriteLine("  D100  - 心跳寄存器");
        Console.WriteLine("  传感器数据（批量读取起始地址 D6000）:");
        Console.WriteLine("    D6000 (索引0) - 温度 (200-300, 单位0.1°C)");
        Console.WriteLine("    D6001 (索引2) - 压力 (100-200, 单位0.1MPa)");
        Console.WriteLine("    D6002 (索引4) - 电流 (0-500, 单位0.1A)");
        Console.WriteLine("    D6003 (索引6) - 电压 (3800-4200, 单位0.1V)");
        Console.WriteLine("    D6004 (索引8) - 光栅位置 (0-1000, 单位mm)");
        Console.WriteLine("    D6005 (索引10) - 伺服速度 (0-3000, 单位rpm)");
        Console.WriteLine("    D6006 (索引12) - 设备的生产状态 (0=休息 1=生产中)");
    }
}