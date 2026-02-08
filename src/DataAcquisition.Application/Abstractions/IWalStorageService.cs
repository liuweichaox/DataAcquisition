using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataAcquisition.Application.Abstractions;

/// <summary>
///     WAL（Write-Ahead Log）存储服务，用于数据持久化降级和重试。
/// </summary>
public interface IWalStorageService
{
    /// <summary>写入 WAL 文件，返回文件路径。</summary>
    Task<string> WriteAsync(List<DataMessage> messages);

    /// <summary>读取 WAL 文件，返回数据消息列表。</summary>
    Task<List<DataMessage>> ReadAsync(string filePath);

    /// <summary>删除已处理的 WAL 文件。</summary>
    Task DeleteAsync(string filePath);

    /// <summary>将 WAL 文件移入重试队列。</summary>
    Task MoveToRetryAsync(string filePath);

    /// <summary>获取所有待重试的 WAL 文件。</summary>
    Task<List<string>> GetRetryFilesAsync();
}
