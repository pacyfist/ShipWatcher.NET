using System.Text;
using Serilog;

namespace ShipWatcher.NET.Sources;

/// <summary>
/// Parses NMEA AIS sentences (!AIVDM/!AIVDO) and decodes 6-bit armored AIS payloads.
/// Handles multi-part messages (e.g., message type 5 which spans 2 sentences).
/// </summary>
public class NmeaParser
{
    private static readonly ILogger Log = Serilog.Log.ForContext<NmeaParser>();

    private static readonly TimeSpan FragmentTimeout = TimeSpan.FromSeconds(30);

    // Real feeds interleave fragments from different radio channels (A/B) and
    // sequence ids, so reassembly is keyed per (channel, sequence id) instead
    // of assuming one multi-part message in flight at a time.
    private readonly Dictionary<(string Channel, int SeqId), FragmentSet> _multiPartBuffer = new();

    private sealed class FragmentSet
    {
        public required int FragmentCount { get; init; }
        public required DateTime FirstSeen { get; init; }
        public Dictionary<int, string> Parts { get; } = new();
        public int LastFragmentFillBits { get; set; }
    }

    public NmeaParseResult? Parse(string sentence)
    {
        // Example: !AIVDM,1,1,,A,13u@dt002s000000000000000000,0*33
        if (string.IsNullOrWhiteSpace(sentence))
            return null;

        // Strip leading/trailing whitespace and any \r\n
        sentence = sentence.Trim();

        // Kystverket lines have a tag block prefix like \s:2573205,c:1776280407*06\
        // Strip everything up to and including the last backslash before the sentence
        var sentenceStart = sentence.LastIndexOf('\\');
        if (sentenceStart >= 0 && sentenceStart < sentence.Length - 1)
            sentence = sentence[(sentenceStart + 1)..];

        // Accept !AIVDM, !AIVDO (standard), !BSVDM, !BSVDO (Norwegian), and other regional variants
        if (sentence.Length < 6 || sentence[0] != '!' ||
            !(sentence[3..6] is "VDM" or "VDO"))
            return null;

        // Verify checksum if present
        var checksumIdx = sentence.LastIndexOf('*');
        if (checksumIdx > 0)
        {
            var body = sentence[1..checksumIdx];
            var expectedHex = sentence[(checksumIdx + 1)..];
            byte computed = 0;
            foreach (var c in body)
                computed ^= (byte)c;

            if (expectedHex.Length >= 2 &&
                byte.TryParse(expectedHex[..2], System.Globalization.NumberStyles.HexNumber, null, out var expected) &&
                computed != expected)
            {
                Log.Debug("Checksum mismatch for sentence: {Sentence}", sentence);
                return null;
            }
        }

        var parts = sentence.Split(',');
        if (parts.Length < 7)
            return null;

        if (!int.TryParse(parts[1], out var fragmentCount) ||
            !int.TryParse(parts[2], out var fragmentNumber))
            return null;

        int.TryParse(parts[3], out var sequentialMessageId);
        var channel = parts[4];
        var payload = parts[5];
        // Fill bits from field 6 (before checksum)
        var fillField = parts[6];
        var starIdx = fillField.IndexOf('*');
        int.TryParse(starIdx >= 0 ? fillField[..starIdx] : fillField, out var fillBits);

        if (fragmentCount == 1)
        {
            return DecodePayload(payload, fillBits);
        }

        if (fragmentNumber < 1 || fragmentNumber > fragmentCount)
            return null;

        // Multi-part message
        var now = DateTime.UtcNow;
        EvictStaleFragments(now);

        var key = (channel, sequentialMessageId);
        if (!_multiPartBuffer.TryGetValue(key, out var set) || set.FragmentCount != fragmentCount)
        {
            set = new FragmentSet { FragmentCount = fragmentCount, FirstSeen = now };
            _multiPartBuffer[key] = set;
        }

        set.Parts[fragmentNumber] = payload;
        if (fragmentNumber == fragmentCount)
        {
            // Fill bits apply to the final fragment's payload only
            set.LastFragmentFillBits = fillBits;
        }

        if (set.Parts.Count < set.FragmentCount)
            return null; // Waiting for more parts

        _multiPartBuffer.Remove(key);

        var combined = new StringBuilder();
        for (int i = 1; i <= set.FragmentCount; i++)
        {
            if (!set.Parts.TryGetValue(i, out var part))
                return null;
            combined.Append(part);
        }

        return DecodePayload(combined.ToString(), set.LastFragmentFillBits);
    }

    private void EvictStaleFragments(DateTime now)
    {
        if (_multiPartBuffer.Count == 0)
            return;

        List<(string, int)>? stale = null;
        foreach (var (key, set) in _multiPartBuffer)
        {
            if (now - set.FirstSeen > FragmentTimeout)
                (stale ??= []).Add(key);
        }

        if (stale is null)
            return;

        foreach (var key in stale)
            _multiPartBuffer.Remove(key);
        Log.Debug("Evicted {Count} stale incomplete multi-part message(s)", stale.Count);
    }

    private static NmeaParseResult? DecodePayload(string payload, int fillBits)
    {
        var bits = DecodeSixBit(payload);

        // Fill bits pad the last armored character and are not message content
        if (fillBits > 0 && fillBits < 6 && fillBits <= bits.Length)
            Array.Resize(ref bits, bits.Length - fillBits);

        if (bits.Length < 6)
            return null;

        var messageType = GetUnsigned(bits, 0, 6);

        return messageType switch
        {
            1 or 2 or 3 => DecodePositionReport(bits, (int)messageType),
            5 => DecodeStaticData(bits),
            18 => DecodeClassBPosition(bits),
            _ => null // We only care about position and static data messages
        };
    }

    private static NmeaParseResult? DecodePositionReport(bool[] bits, int messageType)
    {
        if (bits.Length < 168)
            return null;

        var mmsi = GetUnsigned(bits, 8, 30);
        var navStatus = (int)GetUnsigned(bits, 38, 4);
        var sogRaw = GetUnsigned(bits, 50, 10);
        var lonRaw = GetSigned(bits, 61, 28);
        var latRaw = GetSigned(bits, 89, 27);
        var cogRaw = GetUnsigned(bits, 116, 12);
        var heading = (int)GetUnsigned(bits, 128, 9);

        var sog = sogRaw / 10.0;
        var lon = lonRaw / 600000.0;
        var lat = latRaw / 600000.0;
        var cog = cogRaw / 10.0;

        // Validate ranges
        if (lat < -90 || lat > 90 || lon < -180 || lon > 180)
            return null;
        if (mmsi == 0)
            return null;

        return new NmeaParseResult
        {
            MessageType = NmeaMessageType.PositionReport,
            MMSI = (long)mmsi,
            Latitude = lat,
            Longitude = lon,
            Sog = sog,
            Cog = cog,
            TrueHeading = heading,
            NavigationalStatus = navStatus,
        };
    }

    private static NmeaParseResult? DecodeStaticData(bool[] bits)
    {
        if (bits.Length < 424)
            return null;

        var mmsi = GetUnsigned(bits, 8, 30);
        var callSign = GetString(bits, 70, 42).Trim();
        var name = GetString(bits, 112, 120).Trim();
        var shipType = (int)GetUnsigned(bits, 232, 8);
        var draughtRaw = GetUnsigned(bits, 294, 8);
        var draught = draughtRaw / 10.0;

        var etaMonth = (int)GetUnsigned(bits, 274, 4);
        var etaDay = (int)GetUnsigned(bits, 278, 5);
        var etaHour = (int)GetUnsigned(bits, 283, 5);
        var etaMinute = (int)GetUnsigned(bits, 288, 6);

        var destination = GetString(bits, 302, 120).Trim();

        if (mmsi == 0)
            return null;

        return new NmeaParseResult
        {
            MessageType = NmeaMessageType.StaticData,
            MMSI = (long)mmsi,
            Name = CleanAisString(name),
            CallSign = CleanAisString(callSign),
            Destination = CleanAisString(destination),
            ShipType = shipType,
            Draught = draught,
            EtaMonth = etaMonth,
            EtaDay = etaDay,
            EtaHour = etaHour,
            EtaMinute = etaMinute,
        };
    }

    private static NmeaParseResult? DecodeClassBPosition(bool[] bits)
    {
        if (bits.Length < 168)
            return null;

        var mmsi = GetUnsigned(bits, 8, 30);
        var sogRaw = GetUnsigned(bits, 46, 10);
        var lonRaw = GetSigned(bits, 57, 28);
        var latRaw = GetSigned(bits, 85, 27);
        var cogRaw = GetUnsigned(bits, 112, 12);
        var heading = (int)GetUnsigned(bits, 124, 9);

        var sog = sogRaw / 10.0;
        var lon = lonRaw / 600000.0;
        var lat = latRaw / 600000.0;
        var cog = cogRaw / 10.0;

        if (lat < -90 || lat > 90 || lon < -180 || lon > 180)
            return null;
        if (mmsi == 0)
            return null;

        return new NmeaParseResult
        {
            MessageType = NmeaMessageType.ClassBPositionReport,
            MMSI = (long)mmsi,
            Latitude = lat,
            Longitude = lon,
            Sog = sog,
            Cog = cog,
            TrueHeading = heading,
        };
    }

    private static bool[] DecodeSixBit(string payload)
    {
        var bits = new bool[payload.Length * 6];
        for (int i = 0; i < payload.Length; i++)
        {
            int val = payload[i] - 48;
            if (val > 40) val -= 8;

            for (int b = 5; b >= 0; b--)
            {
                bits[i * 6 + (5 - b)] = ((val >> b) & 1) == 1;
            }
        }
        return bits;
    }

    private static uint GetUnsigned(bool[] bits, int start, int length)
    {
        uint val = 0;
        for (int i = 0; i < length && (start + i) < bits.Length; i++)
        {
            val = (val << 1) | (bits[start + i] ? 1u : 0u);
        }
        return val;
    }

    private static int GetSigned(bool[] bits, int start, int length)
    {
        uint raw = GetUnsigned(bits, start, length);
        // Two's complement
        if ((raw & (1u << (length - 1))) != 0)
        {
            return (int)(raw | (0xFFFFFFFF << length));
        }
        return (int)raw;
    }

    private static string GetString(bool[] bits, int start, int bitLength)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < bitLength; i += 6)
        {
            var charVal = (int)GetUnsigned(bits, start + i, 6);
            if (charVal == 0) // '@' padding
                break;
            // AIS 6-bit ASCII: 0-31 map to '@'-'_' (64-95), 32-63 map to ' '-'?' (32-63)
            char c = charVal < 32 ? (char)(charVal + 64) : (char)charVal;
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static string CleanAisString(string s)
    {
        // Remove @ padding characters and trailing spaces
        return s.Replace("@", "").Trim();
    }
}

public enum NmeaMessageType
{
    PositionReport,
    StaticData,
    ClassBPositionReport,
}

public class NmeaParseResult
{
    public NmeaMessageType MessageType { get; init; }
    public long MMSI { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double Sog { get; init; }
    public double Cog { get; init; }
    public int TrueHeading { get; init; }
    public int NavigationalStatus { get; init; }
    public string Name { get; init; } = "";
    public string CallSign { get; init; } = "";
    public string Destination { get; init; } = "";
    public int ShipType { get; init; }
    public double Draught { get; init; }
    public int EtaMonth { get; init; }
    public int EtaDay { get; init; }
    public int EtaHour { get; init; }
    public int EtaMinute { get; init; }
}
