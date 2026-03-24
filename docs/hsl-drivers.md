# Hsl 驱动目录

本项目默认通过 `HslStandardPlcDriverProvider` 提供 HslCommunication 驱动目录。配置时只使用完整 `Driver` 名称，不提供协议别名。

## 配置示例

```json
{
  "PlcCode": "PLC01",
  "Driver": "melsec-mc",
  "Host": "192.168.1.100",
  "Port": 6000,
  "ProtocolOptions": {
    "station": "0"
  }
}
```

## Driver 列表

- `allen-bradley-connected-cip`
- `allen-bradley-micro-cip`
- `allen-bradley-net`
- `allen-bradley-pccc`
- `allen-bradley-slc`
- `beckhoff-ads`
- `delta-serial-ascii-over-tcp`
- `delta-serial-over-tcp`
- `delta-tcp`
- `fatek-program`
- `freedom-tcp`
- `freedom-udp`
- `fuji-command-setting-type`
- `fuji-spb`
- `fuji-sph`
- `ge-srtp`
- `inovance-serial-over-tcp`
- `inovance-tcp`
- `keyence-mc`
- `keyence-mc-ascii`
- `keyence-nano-serial-over-tcp`
- `lsis-cnet-over-tcp`
- `lsis-fast-enet`
- `megmeet-serial-over-tcp`
- `megmeet-tcp`
- `melsec-a1e`
- `melsec-a1e-ascii`
- `melsec-a3c`
- `melsec-cip`
- `melsec-fxlinks`
- `melsec-fxserial`
- `melsec-mc`
- `melsec-mc-ascii`
- `melsec-mc-ascii-udp`
- `melsec-mc-udp`
- `melsec-mcr`
- `omron-cip`
- `omron-connected-cip`
- `omron-fins`
- `omron-hostlink`
- `omron-hostlink-cmode`
- `panasonic-mc`
- `panasonic-mewtocol`
- `siemens-fetch-write`
- `siemens-ppi-over-tcp`
- `siemens-s7`
- `toyota-toyopuc`
- `turck-reader`
- `vigor-serial-over-tcp`
- `xinje-internal`
- `xinje-serial-over-tcp`
- `xinje-tcp`
- `yamatake-digitron-cpl-over-tcp`
- `yaskawa-memobus-tcp`
- `yaskawa-memobus-udp`
- `yokogawa-link`

## ProtocolOptions

不同 Hsl 驱动可能需要额外参数，统一通过 `ProtocolOptions` 提供。当前项目官方支持并文档化的参数包括：

- `station`
- `series`
- `plc`
- `cpuType`
- `slotNo`
- `connect-timeout-ms`
- `receive-timeout-ms`

其中 `cpuType`、`slotNo` 这类驼峰写法会被兼容解析为内部规范名称。

未在当前驱动支持清单中的 `ProtocolOptions` 会在运行时被拒绝，并返回明确错误。

当前文档明确示例和官方支持项的驱动包括：

| Driver | Supported ProtocolOptions |
|------|------|
| `beckhoff-ads` | 无 |
| `inovance-tcp` | `series`, `station` |
| `melsec-a1e` | 无 |
| `melsec-mc` | 无 |
| `omron-fins` | 无 |
| `siemens-s7` | `plc` |

例如西门子：

```json
{
  "Driver": "siemens-s7",
  "Host": "192.168.1.20",
  "ProtocolOptions": {
    "plc": "S1200"
  }
}
```

例如汇川：

```json
{
  "Driver": "inovance-tcp",
  "Host": "192.168.1.30",
  "Port": 502,
  "ProtocolOptions": {
    "station": "1",
    "series": "AM"
  }
}
```

## 设计说明

- Hsl 是默认驱动方式，不是唯一方式
- 框架核心只依赖 `IPlcClientService` 与 `IPlcDriverProvider`
- 如果用户不用 Hsl，可以注册自己的 provider
