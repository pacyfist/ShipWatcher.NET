# Free AIS and Satellite Ship Tracking Sources

This document summarizes free and open-access sources for real-time and historical ship tracking data.

## Real-Time AIS Streams (Free)

### AISstream.io
- **Service:** Global real-time AIS data via WebSockets.
- **Best For:** Developers building live dashboards or alerts.
- **Access:** [aisstream.io](https://aisstream.io)

### Kystverket (Norway)
- **Service:** Raw AIS TCP stream for the Norwegian Economic Zone.
- **Access:** `153.44.253.27:5631`
- **License:** NLOD 2.0 (Open Government Data).

### Digitraffic (Finland)
- **Service:** Real-time and historical data for Finnish waters.
- **APIs:** MQTT, WebSockets, and REST.
- **Access:** [digitraffic.fi](https://www.digitraffic.fi/en/marine-traffic/)

### BarentsWatch (Norway)
- **Service:** Developer-friendly API for live and historical vessel positions in the North.
- **Access:** [barentswatch.no](https://www.barentswatch.no/en/developer/)

### AISHub (Contribution-Based)
- **Service:** Global community-sharing network.
- **Model:** Access is free if you contribute a feed from your own AIS receiver.
- **Access:** [aishub.net](https://www.aishub.net)

---

## Satellite-Based Tracking (Free)

### Global Fishing Watch (GFW)
- **Service:** Processed Synthetic Aperture Radar (SAR) vessel detections from Sentinel-1.
- **Best For:** Researching "dark vessels" (ships with AIS off).
- **Access:** [globalfishingwatch.org/our-apis/](https://globalfishingwatch.org/our-apis/)

### Skylight
- **Service:** Maritime intelligence platform and open-source detection models.
- **Best For:** Conservation and maritime security.
- **Access:** [skylight.global](https://www.skylight.global)

### sarapi.io
- **Service:** Screening API for vessels and oil spills using Sentinel-1.
- **Model:** Monthly renewable free tier for basic screening.
- **Access:** [sarapi.io](https://sarapi.io)

### Copernicus Data Space Ecosystem (Raw Data)
- **Service:** Official source for raw Sentinel-1 (SAR) and Sentinel-2 (Optical) imagery.
- **Best For:** Users building their own computer vision detection models.
- **Access:** [dataspace.copernicus.eu](https://dataspace.copernicus.eu)

---

## Historical Data (Free Downloads)

| Source | Region | Format | Link |
| :--- | :--- | :--- | :--- |
| **MarineCadastre** | USA | CSV/GeoParquet | [marinecadastre.gov](https://marinecadastre.gov/ais/) |
| **AMSA** | Australia | Shapefile | [amsa.gov.au](https://www.amsa.gov.au) |
| **EMODnet** | Europe | Density Maps | [emodnet-humanactivities.eu](https://www.emodnet-humanactivities.eu) |
| **Danish Maritime** | Denmark | CSV | [dma.dk](https://www.dma.dk) |
