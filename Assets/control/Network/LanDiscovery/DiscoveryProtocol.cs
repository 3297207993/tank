using System;
using System.IO;
using System.Text;

/// <summary>
/// 局域网房间发现的报文格式。纯 C#，不依赖 UnityEngine，方便单元测试。
/// 报文走独立的 UDP 发现端口，和 UnityTransport 的游戏端口无关（游戏端口是包体里的一列数据）。
///
/// 探测包：客户端问“附近有房间吗”。
/// 公告包：房主周期广播房间信息，也用来单播回答探测。
/// </summary>
public static class DiscoveryProtocol
{
    public const ushort DefaultPort = 47777;
    public const string DefaultMulticastAddress = "239.255.42.99";

    public const byte Version = 1;
    public const byte TypeProbe = 1;
    public const byte TypeAnnounce = 2;

    /// <summary>
    /// 房间名和房主名各自的上限，单位是 UTF-8 字节。
    /// 和游戏内 FixedString64Bytes 的名字上限保持一致：20 个汉字约占 60 字节。
    /// </summary>
    public const int MaxNameBytes = 64;

    public const int MaxPacketBytes = 4 + 1 + 1 + 8 + 2 + 1 + 1 + (2 + MaxNameBytes) * 2;

    static readonly byte[] Magic = { (byte)'T', (byte)'A', (byte)'N', (byte)'K' };

    public struct RoomInfo
    {
        public ulong roomId;
        public string roomName;
        public string hostName;
        public ushort gamePort;
        public byte players;
        public bool inGame;
    }

    /// <summary>
    /// 生成一个随机的房间标识。客户端用它去重：同一个房间从多块网卡收到时只显示一条。
    /// </summary>
    public static ulong NewRoomId()
    {
        var bytes = Guid.NewGuid().ToByteArray();
        var id = BitConverter.ToUInt64(bytes, 0);
        return id == 0 ? 1UL : id;
    }

    public static byte[] EncodeProbe()
    {
        using var stream = new MemoryStream(8);
        using (var writer = new BinaryWriter(stream, Encoding.UTF8))
        {
            WriteHeader(writer, TypeProbe);
        }
        return stream.ToArray();
    }

    public static byte[] EncodeAnnounce(in RoomInfo room)
    {
        using var stream = new MemoryStream(MaxPacketBytes);
        using (var writer = new BinaryWriter(stream, Encoding.UTF8))
        {
            WriteHeader(writer, TypeAnnounce);
            writer.Write(room.roomId);
            writer.Write(room.gamePort);
            writer.Write(room.players);
            writer.Write(room.inGame ? (byte)1 : (byte)0);
            WriteName(writer, room.roomName);
            WriteName(writer, room.hostName);
        }
        return stream.ToArray();
    }

    public static bool TryDecode(byte[] data, out byte type, out RoomInfo room)
    {
        return TryDecode(data, data?.Length ?? 0, out type, out room);
    }

    /// <summary>
    /// 解一个 UDP 包。magic、版本、长度任何一项不对都返回 false。
    /// </summary>
    public static bool TryDecode(byte[] data, int length, out byte type, out RoomInfo room)
    {
        type = 0;
        room = default;

        if (data == null || length < Magic.Length + 2 || length > data.Length) return false;
        for (var i = 0; i < Magic.Length; i++)
        {
            if (data[i] != Magic[i]) return false;
        }
        if (data[4] != Version) return false;

        type = data[5];
        if (type == TypeProbe) return length == Magic.Length + 2;
        if (type != TypeAnnounce) return false;

        try
        {
            using var stream = new MemoryStream(data, 0, length, false);
            using var reader = new BinaryReader(stream, Encoding.UTF8);
            stream.Position = Magic.Length + 2;

            var info = new RoomInfo
            {
                roomId = reader.ReadUInt64(),
                gamePort = reader.ReadUInt16(),
                players = reader.ReadByte(),
                inGame = reader.ReadByte() != 0,
            };
            if (!TryReadName(reader, out info.roomName)) return false;
            if (!TryReadName(reader, out info.hostName)) return false;

            room = info;
            return true;
        }
        catch (EndOfStreamException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// 按 UTF-8 截断字符串，保证不会把一个汉字的三个字节切开。
    /// </summary>
    public static byte[] TruncateUtf8(string value, int maxBytes)
    {
        if (string.IsNullOrEmpty(value) || maxBytes <= 0) return Array.Empty<byte>();

        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length <= maxBytes) return bytes;

        // bytes[maxBytes] 是下一个字节：如果它是 10xxxxxx，说明正好切在一个字符中间，往回收
        var count = maxBytes;
        while (count > 0 && (bytes[count] & 0xC0) == 0x80) count--;

        var truncated = new byte[count];
        Array.Copy(bytes, truncated, count);
        return truncated;
    }

    static void WriteHeader(BinaryWriter writer, byte type)
    {
        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(type);
    }

    static void WriteName(BinaryWriter writer, string value)
    {
        var bytes = TruncateUtf8(value, MaxNameBytes);
        writer.Write((ushort)bytes.Length);
        writer.Write(bytes);
    }

    static bool TryReadName(BinaryReader reader, out string name)
    {
        name = string.Empty;
        var stream = reader.BaseStream;
        if (stream.Length - stream.Position < 2) return false;

        var length = reader.ReadUInt16();
        if (length > MaxNameBytes) return false;
        if (stream.Length - stream.Position < length) return false;

        var bytes = reader.ReadBytes(length);
        if (bytes.Length != length) return false;

        name = Encoding.UTF8.GetString(bytes);
        return true;
    }
}
