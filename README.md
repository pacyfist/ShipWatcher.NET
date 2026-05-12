# ShipWatcher.NET

A terminal-based live AIS vessel tracker.

## Data Sources

The system supports multiple AIS data sources:

1.  **Kystverket (Norway)**: Free, open real-time TCP stream for the Norwegian Economic Zone. No registration required.
2.  **Digitraffic (Finland)**: Free, open REST API for Finnish waters. No registration required.
3.  **aisstream.io**: Global real-time WebSocket feed. **Requires a free API key** from [aisstream.io](https://aisstream.io).

## Configuration

You can configure the application using environment variables:

- `AISSTREAM_API_KEY`: Your API key for aisstream.io.
- `SHIPWATCHER_LAT_MIN`, `SHIPWATCHER_LON_MIN`, `SHIPWATCHER_LAT_MAX`, `SHIPWATCHER_LON_MAX`: Bounding box for aisstream.io tracking.

## Usage

Run the application:
```bash
dotnet run --project ShipWatcher.NET
```

- **Tab**: Cycle between Map and Table views.
- **S**: Change data source and configuration.
- **F**: Filter vessels by name or MMSI.
- **Ctrl+R**: Reconnect to the active source.
- **Ctrl+Q**: Quit.
