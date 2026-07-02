using ShipWatcher.NET.Sources;

namespace ShipWatcher.NET.Tests;

/// <summary>
/// Golden tests for the NMEA/AIS parser. Sentences were generated with an
/// independent encoder implementing ITU-R M.1371 bit layouts, so expected
/// values do not derive from the code under test.
/// </summary>
public class NmeaParserTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void Parses_Type1_PositionReport()
    {
        var parser = new NmeaParser();

        // MMSI 259123000, nav 0, SOG 10.2, lon 5.32, lat 60.39, COG 123.4, HDG 42
        var result = parser.Parse("!AIVDM,1,1,,A,13o7W>0P1V0HFV0RSS44lQEp0000,0*10");

        Assert.NotNull(result);
        Assert.Equal(NmeaMessageType.PositionReport, result.MessageType);
        Assert.Equal(259123000, result.MMSI);
        Assert.Equal(0, result.NavigationalStatus);
        Assert.Equal(10.2, result.Sog, Tolerance);
        Assert.Equal(5.32, result.Longitude, Tolerance);
        Assert.Equal(60.39, result.Latitude, Tolerance);
        Assert.Equal(123.4, result.Cog, Tolerance);
        Assert.Equal(42, result.TrueHeading);
    }

    [Fact]
    public void Parses_Canonical_Gpsd_Type1_Sample()
    {
        var parser = new NmeaParser();

        // Canonical sample from the GPSD AIVDM protocol documentation.
        var result = parser.Parse("!AIVDM,1,1,,B,177KQJ5000G?tO`K>RA1wUbN0TKH,0*5C");

        Assert.NotNull(result);
        Assert.Equal(NmeaMessageType.PositionReport, result.MessageType);
        Assert.Equal(477553000, result.MMSI);
        Assert.Equal(5, result.NavigationalStatus);
        Assert.Equal(0.0, result.Sog, Tolerance);
        Assert.Equal(-122.34583333333333, result.Longitude, Tolerance);
        Assert.Equal(47.58283333333333, result.Latitude, Tolerance);
        Assert.Equal(51.0, result.Cog, Tolerance);
        Assert.Equal(181, result.TrueHeading);
    }

    [Fact]
    public void Parses_Type5_StaticData_Across_Two_Fragments()
    {
        var parser = new NmeaParser();

        // MMSI 230123250, call OJXY, name TEST VESSEL, type 70,
        // ETA 07-02 12:30, draught 5.5, destination HELSINKI
        var first = parser.Parse("!AIVDM,2,1,1,A,53KMVtP00000taQT001@E=B1HE=<Dh0000000016000001i<N=j1C4jCRj@0,0*71");
        var second = parser.Parse("!AIVDM,2,2,1,A,00000000000,2*25");

        Assert.Null(first); // waiting for the second fragment
        Assert.NotNull(second);
        Assert.Equal(NmeaMessageType.StaticData, second.MessageType);
        Assert.Equal(230123250, second.MMSI);
        Assert.Equal("OJXY", second.CallSign);
        Assert.Equal("TEST VESSEL", second.Name);
        Assert.Equal(70, second.ShipType);
        Assert.Equal(5.5, second.Draught, Tolerance);
        Assert.Equal(7, second.EtaMonth);
        Assert.Equal(2, second.EtaDay);
        Assert.Equal(12, second.EtaHour);
        Assert.Equal(30, second.EtaMinute);
        Assert.Equal("HELSINKI", second.Destination);
    }

    [Fact]
    public void Parses_Type18_ClassB_PositionReport()
    {
        var parser = new NmeaParser();

        // MMSI 257654321, SOG 7.5, lon -122.34, lat 47.58, COG 200.0, HDG 511
        var result = parser.Parse("!AIVDM,1,1,,A,B3mev<@0BmkwS@6kVr1u3wv00000,0*7A");

        Assert.NotNull(result);
        Assert.Equal(NmeaMessageType.ClassBPositionReport, result.MessageType);
        Assert.Equal(257654321, result.MMSI);
        Assert.Equal(7.5, result.Sog, Tolerance);
        Assert.Equal(-122.34, result.Longitude, Tolerance);
        Assert.Equal(47.58, result.Latitude, Tolerance);
        Assert.Equal(200.0, result.Cog, Tolerance);
        Assert.Equal(511, result.TrueHeading);
    }

    [Fact]
    public void Strips_Kystverket_TagBlock_Prefix()
    {
        var parser = new NmeaParser();

        var result = parser.Parse(@"\s:2573205,c:1776280407*06\!AIVDM,1,1,,A,13o7W>0P1V0HFV0RSS44lQEp0000,0*10");

        Assert.NotNull(result);
        Assert.Equal(259123000, result.MMSI);
    }

    [Fact]
    public void Accepts_BSVDM_Talker()
    {
        var parser = new NmeaParser();

        // Same body as the type 1 vector but with a BS talker id (checksum recomputed).
        var result = parser.Parse("!BSVDM,1,1,,A,13o7W>0P1V0HFV0RSS44lQEp0000,0*09");

        Assert.NotNull(result);
        Assert.Equal(259123000, result.MMSI);
    }

    [Fact]
    public void Rejects_Checksum_Mismatch()
    {
        var parser = new NmeaParser();

        var result = parser.Parse("!AIVDM,1,1,,A,13o7W>0P1V0HFV0RSS44lQEp0000,0*11");

        Assert.Null(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("$GPGGA,123519,4807.038,N,01131.000,E,1,08,0.9,545.4,M,46.9,M,,*47")]
    [InlineData("!AIVDM,1,1")]
    [InlineData("garbage")]
    public void Rejects_NonAis_Or_Malformed_Input(string line)
    {
        var parser = new NmeaParser();

        Assert.Null(parser.Parse(line));
    }

    [Fact]
    public void Ignores_Unsupported_Message_Types()
    {
        var parser = new NmeaParser();

        // Type 4 (base station report).
        var result = parser.Parse("!AIVDM,1,1,,A,402;rRiv@;h?I?04Vd2e`Ww`0a5H,0*23");

        Assert.Null(result);
    }
}
