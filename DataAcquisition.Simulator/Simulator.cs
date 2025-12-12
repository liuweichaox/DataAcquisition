using HslCommunication.Profinet.Melsec;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace DataAcquisition.Simulator;

/// <summary>
/// Mitsubishi A1E 模拟器，直接使用 HslCommunication 服务器
/// </summary>
public class Simulator : IDisposable
{
    private readonly MelsecA1EServer _server;
    private readonly Timer? _dataUpdateTimer;
    private readonly ILogger<Simulator>? _logger;
    private bool _isRunning;
    private int _heartbeatCounter;
    private DateTime _simulatorStartTime = DateTime.Now;

    public Simulator(int port, ILogger<Simulator>? logger = null)
    {
        _logger = logger;
        _server = new MelsecA1EServer
        {
            Port = port
        };

        // 初始化一些默认寄存器值（可选，但有助于测试）
        _logger?.LogDebug("初始化 MelsecA1EServer 模拟器，端口: {Port}", port);

        // 定期更新模拟数据（在启动后才会真正更新）
        _dataUpdateTimer = new Timer(UpdateSimulatedData, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    }

    /// <summary>
    /// 启动模拟器
    /// </summary>
    public void Start()
    {
        try
        {
            _logger?.LogInformation("正在启动 PLC 模拟器，端口: {Port}...", _server.Port);

            // 确保服务器处于停止状态
            if (_server.IsStarted)
            {
                _logger?.LogWarning("服务器已在运行，先停止...");
                _server.ServerClose();
                Thread.Sleep(100);
            }

            // 启动服务器（ServerStart 可能返回 void，需要检查 IsStarted 属性）
            _server.ServerStart(_server.Port);

            // 等待服务器启动并绑定端口
            var maxWaitTime = 3000; // 最多等待 3 秒
            var waited = 0;
            while (!_server.IsStarted && waited < maxWaitTime)
            {
                Thread.Sleep(100);
                waited += 100;
            }

            if (!_server.IsStarted)
            {
                throw new InvalidOperationException($"服务器启动失败：在 {maxWaitTime}ms 内未能成功启动");
            }

            // 初始化心跳寄存器
            var writeResult = _server.Write("D100", (ushort)0);
            if (!writeResult.IsSuccess)
            {
                _logger?.LogWarning("初始化心跳寄存器失败: {Message}", writeResult.Message);
            }

            _isRunning = true;
            _logger?.LogInformation("✓ PLC 模拟器已成功启动，监听端口: {Port}，服务器状态: {IsStarted}",
                _server.Port, _server.IsStarted);
        }
        catch (Exception ex)
        {
            _isRunning = false;
            _logger?.LogError(ex, "✗ 启动 PLC 模拟器失败");
            throw;
        }
    }

    /// <summary>
    /// 停止模拟器
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
        _server.ServerClose();
        _logger?.LogInformation("PLC 模拟器已停止");
    }

    /// <summary>
    /// 设置寄存器值（直接使用服务器的 Write 方法）
    /// </summary>
    public bool SetRegister(string address, ushort value)
    {
        try
        {
            var result = _server.Write(address, value);
            if (result.IsSuccess)
            {
                _logger?.LogDebug("设置寄存器 {Address} = {Value}", address, value);
            }
            return result.IsSuccess;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "设置寄存器失败: {Address}", address);
            return false;
        }
    }

    /// <summary>
    /// 读取寄存器值
    /// </summary>
    public ushort? GetRegister(string address)
    {
        try
        {
            var result = _server.ReadUInt16(address);
            return result.IsSuccess ? result.Content : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 更新模拟数据（定期执行）
    /// </summary>
    private void UpdateSimulatedData(object? state)
    {
        if (!_isRunning) return;

        try
        {
            var now = DateTime.Now;
            var timeBase = now.Second + now.Millisecond * 0.001;

            // 心跳寄存器自动递增
            _heartbeatCounter = (_heartbeatCounter + 1) % 65536;
            _server.Write("D100", (ushort)_heartbeatCounter);

            // 批量数据起始地址：D6000
            // 索引0: 温度 (200-300, 单位0.1°C，实际20-30°C)
            var temperature = (short)(2500 + Math.Sin(timeBase * 0.1) * 500);
            _server.Write("D6000", (ushort)temperature);

            // 索引2: 压力 (100-200, 单位0.1MPa，实际10-20MPa)
            var pressure = (short)(1500 + Math.Cos(timeBase * 0.15) * 500);
            _server.Write("D6001", (ushort)pressure);

            // 索引4: 电流 (0-500, 单位0.1A，实际0-50A)
            var current = (short)(250 + Math.Sin(timeBase * 0.2) * 250);
            _server.Write("D6002", (ushort)current);

            // 索引6: 电压 (3800-4200, 单位0.1V，实际380-420V)
            var voltage = (short)(4000 + Math.Cos(timeBase * 0.12) * 200);
            _server.Write("D6003", (ushort)voltage);

            // 索引8: 光栅位置 (0-1000, 单位mm)
            var lightBarrierPos = (short)(500 + Math.Sin(timeBase * 0.08) * 500);
            _server.Write("D6004", (ushort)lightBarrierPos);

            // 索引10: 伺服速度 (0-3000, 单位rpm)
            var servoSpeed = (short)(1500 + Math.Cos(timeBase * 0.18) * 1500);
            _server.Write("D6005", (ushort)servoSpeed);

            // 索引12: 生产序号
            // 逻辑：每个序号持续10秒，然后变为0持续5秒，然后序号+1
            // 模式：1111111111, 00000, 2222222222, 00000, 3333333333, 00000...
            var totalSeconds = (int)(DateTime.Now - _simulatorStartTime).TotalSeconds;
            var cycleSeconds = totalSeconds % 15; // 15秒一个周期（10秒序号 + 5秒0）
            var cycleNumber = totalSeconds / 15; // 当前是第几个周期（从0开始）
            var currentProductionNumber = cycleNumber + 1; // 当前生产序号（从1开始）

            // 如果在一个周期内的前10秒，显示生产序号；后5秒显示0
            var productionSerial = cycleSeconds < 10 ? currentProductionNumber : 0;
            _server.Write("D6006", (ushort)productionSerial);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "更新模拟数据失败");
        }
    }

    public void Dispose()
    {
        Stop();
        _dataUpdateTimer?.Dispose();
        _server?.Dispose();
    }
}
