# Satellite Ship Tracking APIs

This document lists APIs and services that provide ship locations using satellite-based technologies, including Satellite AIS (S-AIS), Synthetic Aperture Radar (SAR), and Radio Frequency (RF) geolocation.

## Free & Open Access Satellite Data

### Global Fishing Watch (GFW)
- **Service:** Processed SAR vessel detections from Sentinel-1.
- **Best For:** Researching "dark vessels" (ships with AIS off) and fishing activity.
- **Website:** [globalfishingwatch.org](https://globalfishingwatch.org/our-apis/)

### sarapi.io
- **Service:** Screening API for vessels and oil spills using Sentinel-1.
- **Free Tier:** Monthly renewable free tier for basic screening and testing.
- **Website:** [sarapi.io](https://sarapi.io)

### Copernicus Data Space Ecosystem
- **Service:** Official source for raw Sentinel-1 (SAR) and Sentinel-2 (Optical) imagery.
- **Best For:** Building custom detection models and accessing raw earth observation data.
- **Website:** [dataspace.copernicus.eu](https://dataspace.copernicus.eu)

---

## Satellite AIS (S-AIS) Providers
S-AIS detects AIS transponders from space, providing coverage in the open ocean where terrestrial stations cannot reach.

### Spire Global (via Kpler)
- **Constellation:** Large fleet of Lemur nanosatellites.
- **Capabilities:** High-frequency AIS updates and weather data.
- **API:** GraphQL (Maritime 2.0).
- **Website:** [spire.com](https://spire.com)

### Orbcomm / exactEarth (via S&P Global)
- **Constellation:** OG2 and exactView constellations.
- **Capabilities:** Low latency and high detection rates.
- **API:** REST.
- **Website:** [spglobal.com](https://www.spglobal.com/marketintelligence/en/solutions/maritime-portal)

---

## Non-AIS Satellite Tracking (Dark Vessel Detection)
These services can detect ships even when their AIS transponders are turned off.

### Synthetic Aperture Radar (SAR)
SAR uses radar to create images, working through clouds and darkness.

- **ICEYE:** Operates the world's largest SAR constellation. Provides "Ocean Vision" for automated vessel detection.
  - **Website:** [iceye.com](https://www.iceye.com)
- **Capella Space:** High-resolution SAR with automated AI vessel classification.
  - **Website:** [capellaspace.com](https://www.capellaspace.com)
- **sarapi.io:** Developer-friendly REST API for vessel detection using Sentinel-1 (free) and commercial SAR.
  - **Website:** [sarapi.io](https://sarapi.io)

### Radio Frequency (RF) Geolocation
RF tracking locates ships by geolocating their electromagnetic emissions (radar, satphones, VHF).

- **Unseenlabs:** Proprietary constellation (BRO) for high-precision RF fingerprinting.
  - **Website:** [unseenlabs.space](https://unseenlabs.space)
- **HawkEye 360:** Geolocation of various RF signals (VHF, UHF, X-band).
  - **Website:** [he360.com](https://www.he360.com)

### Optical Imagery & Hybrid Services
- **BlackSky:** High-resolution optical imagery with low-latency "Tip and Cue" (e.g., cued by AIS or RF).
  - **Website:** [blacksky.com](https://www.blacksky.com)
- **SkyFi:** A marketplace API for tasking multiple SAR and optical satellite providers.
  - **Website:** [skyfi.com](https://www.skyfi.com)

---

## Technical Summary: Satellite Technologies

| Technology | Signal Used | Works in Dark/Clouds | Detects "Dark" Ships | Best For |
| :--- | :--- | :--- | :--- | :--- |
| **S-AIS** | AIS Transponder | Yes | No | Global vessel monitoring |
| **SAR** | Radar Reflection | Yes | Yes | All-weather vessel detection |
| **RF Geolocation** | Radar/Comms | Yes | Yes | Identifying uncooperative vessels |
| **Optical** | Visual Light | No | Yes | High-resolution identification |
