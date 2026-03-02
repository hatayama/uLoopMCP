---
name: uloop-device-connect
description: "Connect to a Device Agent running on a physical device over USB. Use when you need to: (1) establish connection to Android/iOS device for automated testing, (2) set up ADB port forwarding, (3) authenticate with the Device Agent. Performs ADB forward (Android) or iproxy (iOS) and auth.login handshake."
---

# uloop device-connect

Connect to a Device Agent running on a physical Android or iOS device over USB.

## Usage

```bash
uloop device-connect [options]
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--port` | integer | `8800` | Device Agent TCP port |
| `--token` | string | - | Authentication token for the Device Agent |
| `--platform` | string | - | Target platform: `android` or `ios` |

## Examples

```bash
# Connect to Android device with default port
uloop device-connect --platform android

# Connect to iOS device with default port
uloop device-connect --platform ios

# Connect with a specific port
uloop device-connect --platform android --port 9900

# Connect with authentication token
uloop device-connect --platform android --token my-secret-token
```

## Output

Returns JSON with:
- `Connected`: Whether the connection was established successfully
- `DeviceSerial`: Serial number of the connected device
- `Capabilities`: List of supported Device Agent capabilities

## Notes

- For Android, ADB port forwarding is set up automatically
- For iOS, iproxy is used for USB tunneling
- An auth.login handshake is performed after the connection is established
- The default Device Agent port is 8800
