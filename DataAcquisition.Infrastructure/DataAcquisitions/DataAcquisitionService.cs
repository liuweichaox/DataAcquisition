using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAcquisition.Application.Abstractions;
using DataAcquisition.Domain.Models;
using DataAcquisition.Application;

namespace DataAcquisition.Infrastructure.DataAcquisitions
{    
    /// <summary>
    /// 数据采集器实现
    /// </summary>
    public class DataAcquisitionService : IDataAcquisitionService
    {
        private readonly IPlcStateManager _plcStateManager;
        private readonly IDeviceConfigService _deviceConfigService;
        private readonly IPlcClientFactory _plcClientFactory;
        private readonly IOperationalEventsService _events;
        private readonly IQueueService _queue;
        private readonly IHeartbeatMonitor _heartbeatMonitor;
        private readonly IModuleCollector _moduleCollector;

        /// <summary>
        /// 数据采集器
        /// </summary>
        public DataAcquisitionService(IDeviceConfigService deviceConfigService,
            IPlcClientFactory plcClientFactory,
            IOperationalEventsService events,
            IQueueService queue,
            IPlcStateManager plcStateManager,
            IHeartbeatMonitor heartbeatMonitor,
            IModuleCollector moduleCollector)
        {
            _deviceConfigService = deviceConfigService;
            _plcClientFactory = plcClientFactory;
            _events = events;
            _queue = queue;
            _plcStateManager = plcStateManager;
            _heartbeatMonitor = heartbeatMonitor;
            _moduleCollector = moduleCollector;
        }
        
        /// <summary>
        /// 开始所有采集任务
        /// </summary>
        public async Task StartCollectionTasks()
        {
            var dataAcquisitionConfigs = await _deviceConfigService.GetConfigs();
            foreach (var config in dataAcquisitionConfigs.Where(config => config.IsEnabled))
            {
                StartCollectionTask(config);
            }
        }

        /// <summary>
        /// 启动单个采集任务（如果任务已存在则直接返回）
        /// </summary>
        private void StartCollectionTask(DeviceConfig config)
        {
            if (_plcStateManager.Runtimes.ContainsKey(config.Code))
            {
                return;
            }

            _plcStateManager.PlcConnectionHealth[config.Code] = false;

            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            var client = CreatePlcClient(config);
                    
            var tasks = new List<Task> { _heartbeatMonitor.MonitorAsync(config, ct) };

            foreach (var module in config.Modules)
            {
                tasks.Add(_moduleCollector.CollectAsync(config, module, client, ct));
            }
                    
            var running = Task.WhenAll(tasks);
            _ = running.ContinueWith(async t =>
            {
                if (t.Exception != null)
                    await _events.ErrorAsync(config.Code, $"采集任务异常: {t.Exception.Flatten().InnerException?.Message}", t.Exception.Flatten().InnerException);
            }, TaskContinuationOptions.OnlyOnFaulted);
                    
            _plcStateManager.Runtimes.TryAdd(config.Code, new PlcRuntime(cts, running));
        }

        /// <summary>
        /// 创建 PLC 客户端（若已存在则直接返回）
        /// </summary>
        private IPlcClientService CreatePlcClient(DeviceConfig config)
        {
            if (_plcStateManager.PlcClients.TryGetValue(config.Code, out var client))
            {
                return client;
            }

            client = _plcClientFactory.Create(config);
            _plcStateManager.PlcClients.TryAdd(config.Code, client);
            _plcStateManager.PlcLocks.TryAdd(config.Code, new SemaphoreSlim(1, 1));
            return client;
        }

        /// <summary>
        /// 停止所有数据采集任务并释放相关资源
        /// </summary>
        public async Task StopCollectionTasks()
        {
            // Cancel the data acquisition tasks.
            foreach (var kvp in _plcStateManager.Runtimes)
            {
                await kvp.Value.Cts.CancelAsync();
            }

            foreach (var kv in _plcStateManager.Runtimes)
            {
                try { await kv.Value.Running; } catch (OperationCanceledException) { }
                kv.Value.Cts.Dispose();
            }

            // Close and clean up all PLC clients.
            foreach (var client in _plcStateManager.PlcClients.Values)
            {
                await client.ConnectCloseAsync();
            }

            foreach (var sem in _plcStateManager.PlcLocks.Values)
            {
                sem.Dispose();
            }

            // Complete and dispose the queue.
            await _queue.DisposeAsync();

            _plcStateManager.Clear();
        }

        /// <summary>
        /// 写入 PLC 寄存器
        /// </summary>
        /// <param name="plcCode">PLC 编号</param>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入值</param>
        /// <param name="dataType">数据类型</param>
        /// <param name="ct"></param>
        /// <returns>写入结果</returns>
        public async Task<PlcWriteResult> WritePlcAsync(string plcCode, string address, object value,
            string dataType, CancellationToken ct = default)
        {
            if (!_plcStateManager.PlcClients.TryGetValue(plcCode, out var client))
            {
                return new PlcWriteResult
                {
                    IsSuccess = false,
                    Message = $"未找到 PLC {plcCode}"
                };
            }

            var locker = _plcStateManager.PlcLocks[plcCode];
            await locker.WaitAsync(ct);
            try
            {
                return dataType switch
                {
                    "ushort" => await client.WriteUShortAsync(address, Convert.ToUInt16(value)),
                    "uint" => await client.WriteUIntAsync(address, Convert.ToUInt32(value)),
                    "ulong" => await client.WriteULongAsync(address, Convert.ToUInt64(value)),
                    "short" => await client.WriteShortAsync(address, Convert.ToInt16(value)),
                    "int" => await client.WriteIntAsync(address, Convert.ToInt32(value)),
                    "long" => await client.WriteLongAsync(address, Convert.ToInt64(value)),
                    "float" => await client.WriteFloatAsync(address, Convert.ToSingle(value)),
                    "double" => await client.WriteDoubleAsync(address, Convert.ToDouble(value)),
                    "string" => await client.WriteStringAsync(address, Convert.ToString(value) ?? string.Empty),
                    "bool" => await client.WriteBoolAsync(address, Convert.ToBoolean(value)),
                    _ => new PlcWriteResult { IsSuccess = false, Message = $"不支持的数据类型: {dataType}" }
                };
            }
            finally
            {
                locker.Release();
            }
        }

        /// <summary>
        /// 获取当前所有 PLC 连接状态
        /// </summary>
        public SortedDictionary<string, bool> GetPlcConnectionStatus()
        {
            return new SortedDictionary<string, bool>(_plcStateManager.PlcConnectionHealth);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            StopCollectionTasks().Wait();
        }
    }
}