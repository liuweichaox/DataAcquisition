# Hsl Driver Catalog

This project uses `HslStandardPlcDriverProvider` as the default HslCommunication driver catalog. Configuration uses full `Driver` names only; protocol aliases are not provided.

## Example

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

## Driver List

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

Different Hsl drivers may require extra parameters via `ProtocolOptions`. The officially supported and documented options are:

- `station`
- `series`
- `plc`
- `cpuType`
- `slotNo`
- `connect-timeout-ms`
- `receive-timeout-ms`

CamelCase forms such as `cpuType` and `slotNo` are accepted and normalized internally.

`ProtocolOptions` that are not listed for the current driver are rejected at runtime with a clear error message.

The drivers with documented official options and examples are:

| Driver | Supported ProtocolOptions |
|------|------|
| `beckhoff-ads` | none |
| `inovance-tcp` | `series`, `station` |
| `melsec-a1e` | none |
| `melsec-mc` | none |
| `omron-fins` | none |
| `siemens-s7` | `plc` |

Siemens example:

```json
{
  "Driver": "siemens-s7",
  "Host": "192.168.1.20",
  "ProtocolOptions": {
    "plc": "S1200"
  }
}
```

Inovance example:

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

## Design Notes

- Hsl is the default driver mechanism, not the only one
- The framework core only depends on `IPlcClientService` and `IPlcDriverProvider`
- Users who do not want Hsl can register their own provider
