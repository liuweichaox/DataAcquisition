using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DataAcquisition.Simulator;

internal class Program
{
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
            // 启动实时显示（支持取消）
            await PrintRealTimeDataAsync(simulator, logger, cts.Token);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("实时数据显示已取消");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "实时数据显示出错");
        }
        finally
        {
            simulator.Stop();
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
        Console.WriteLine("    D6006 (索引12) - 设备的生产状态 (状态为0表示设备在休息，为1表示设备再生产中)");
    }
    
    private static async Task PrintRealTimeDataAsync(
        Simulator simulator, 
        ILogger logger, 
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1000, cancellationToken); // 支持取消的延迟

            var heartbeat = simulator.GetRegister("D100") ?? 0;
            var temp = simulator.GetRegister("D6000") ?? 0;
            var pressure = simulator.GetRegister("D6001") ?? 0;
            var current = simulator.GetRegister("D6002") ?? 0;
            var voltage = simulator.GetRegister("D6003") ?? 0;
            var lightBarrierPos = simulator.GetRegister("D6004") ?? 0;
            var servoSpeed = simulator.GetRegister("D6005") ?? 0;
            var deviceFlag = simulator.GetRegister("D6006") ?? 0;
            var timestamp = DateTime.Now.ToString("HH:mm:ss");

            Console.WriteLine(
                $"[{timestamp}] 心跳={heartbeat} | 温度={temp,4} | 压力={pressure,4} | 电流={current,3} | 电压={voltage,4} | 光栅={lightBarrierPos,4} | 伺服={servoSpeed,4} | 生产状态={deviceFlag}");
        }
    }
}