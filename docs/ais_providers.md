# AIS Message Providers

This document lists the primary providers of Automatic Identification System (AIS) data, including both terrestrial and satellite AIS (S-AIS) feeds.

## Free & Community-Driven Providers

### AISstream.io
Provides a real-time WebSocket feed of global AIS data.
- **Best For:** Hobbyists, researchers, and open-source projects.
- **Website:** [aisstream.io](https://aisstream.io)

### AISHub
A community-driven AIS sharing network.
- **Model:** Contributors who host an AIS receiver get free access to the global feed.
- **Website:** [aishub.net](https://www.aishub.net)

### Kystverket (Norwegian Coastal Administration)
Provides free AIS data for the Norwegian economic zone.
- **Website:** [kystverket.no](https://www.kystverket.no/en/maritime-services/ais/)

---

## Major Commercial Providers

### Kpler (MarineTraffic / FleetMon / Spire Maritime)
Kpler is the dominant player in maritime data following a series of major acquisitions.
- **Data Sources:** Over 13,000 terrestrial AIS receivers and Spire's nanosatellite constellation.
- **API Tech:** GraphQL (Maritime 2.0), REST.
- **Best For:** Enterprise-grade global tracking and historical analytics.
- **Website:** [kpler.com](https://www.kpler.com) | [Developer Portal](https://developers.kpler.com)

### S&P Global Market Intelligence (ORBCOMM / IHS Markit / exactEarth)
Following the acquisition of ORBCOMM's AIS business and integration with IHS Markit.
- **Data Sources:** Proprietary OG2 satellite constellation and extensive shore-based networks.
- **API Tech:** REST, FTP.
- **Best For:** Risk, compliance, supply chain visibility, and government intelligence.
- **Website:** [spglobal.com](https://www.spglobal.com/marketintelligence/en/solutions/maritime-portal)

### VesselFinder
One of the few major independent providers with transparent pricing.
- **Data Sources:** Combined terrestrial and satellite AIS.
- **API Tech:** REST (JSON/XML).
- **Best For:** Small-to-medium projects and developers needing "pay-as-you-go" satellite data.
- **Website:** [vesselfinder.com](https://www.vesselfinder.com) | [API Docs](https://www.vesselfinder.com/api)

### Datalastic
A developer-first provider known for ease of integration.
- **API Tech:** REST.
- **Best For:** Startups and rapid prototyping; self-service API keys.
- **Website:** [datalastic.com](https://datalastic.com)

### SeaVantage
Specializes in logistics and predictive analytics.
- **Best For:** Container tracking, port congestion, and accurate ETAs.
- **Website:** [seavantage.com](https://www.seavantage.com)

---

## Technical Summary

| Provider | Global Coverage | API Type | Primary Focus |
| :--- | :--- | :--- | :--- |
| **Kpler** | High (Sat + Terr) | GraphQL / REST | Commercial Analytics |
| **S&P Global** | High (Sat + Terr) | REST / FTP | Compliance & Risk |
| **VesselFinder** | High (Sat + Terr) | REST | Developer Friendly |
| **AISstream** | High (Real-time) | WebSocket | Free / Testing |
| **Datalastic** | High (Sat + Terr) | REST | Ease of Integration |
