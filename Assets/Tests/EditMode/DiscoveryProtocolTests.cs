using System.Text;
using NUnit.Framework;

/// <summary>
/// 局域网发现报文的编解码：名字较长（含中文）时必须完整保留，超长时按字符边界截断。
/// </summary>
public class DiscoveryProtocolTests
{
    static DiscoveryProtocol.RoomInfo Sample(string roomName = "房主的房间", string hostName = "房主")
    {
        return new DiscoveryProtocol.RoomInfo
        {
            roomId = 0x1122334455667788UL,
            roomName = roomName,
            hostName = hostName,
            gamePort = 7788,
            players = 3,
            inGame = true,
        };
    }

    static byte[] Announce(DiscoveryProtocol.RoomInfo room)
    {
        var data = DiscoveryProtocol.EncodeAnnounce(room);
        Assert.IsTrue(DiscoveryProtocol.TryDecode(data, out var type, out _),
            "编码出的公告包应该能被自己解开");
        Assert.AreEqual(DiscoveryProtocol.TypeAnnounce, type);
        return data;
    }

    [Test]
    public void Announce_RoundTripsAllFields()
    {
        var data = Announce(Sample());

        Assert.IsTrue(DiscoveryProtocol.TryDecode(data, out var type, out var room));
        Assert.AreEqual(DiscoveryProtocol.TypeAnnounce, type);
        Assert.AreEqual(0x1122334455667788UL, room.roomId);
        Assert.AreEqual("房主的房间", room.roomName);
        Assert.AreEqual("房主", room.hostName);
        Assert.AreEqual(7788, room.gamePort);
        Assert.AreEqual(3, room.players);
        Assert.IsTrue(room.inGame);
    }

    [Test]
    public void Announce_KeepsNamesLongerThan32Bytes()
    {
        // 16 个汉字 = 48 字节，32 字节的上限会被截断，64 字节不会
        const string name = "这是一个二十个汉字的玩家名字测试";
        Assert.Greater(Encoding.UTF8.GetByteCount(name), 32);

        var data = Announce(Sample(name, name));

        Assert.IsTrue(DiscoveryProtocol.TryDecode(data, out _, out var room));
        Assert.AreEqual(name, room.roomName);
        Assert.AreEqual(name, room.hostName);
    }

    [Test]
    public void Announce_KeepsTwentyChineseCharacters()
    {
        // 20 个汉字 = 60 字节，正好落在 64 字节上限内
        var name = new string('坦', 20);
        Assert.AreEqual(60, Encoding.UTF8.GetByteCount(name));

        var data = Announce(Sample(name, name));

        Assert.IsTrue(DiscoveryProtocol.TryDecode(data, out _, out var room));
        Assert.AreEqual(name, room.roomName);
        Assert.AreEqual(name, room.hostName);
    }

    [Test]
    public void Announce_TruncatesOverlongNamesOnCharacterBoundary()
    {
        var name = new string('你', 40); // 120 字节，远超上限

        var data = Announce(Sample(name, name));

        Assert.IsTrue(DiscoveryProtocol.TryDecode(data, out _, out var room));
        Assert.AreEqual(new string('你', 21), room.roomName); // 21 * 3 = 63 字节
        Assert.AreEqual(new string('你', 21), room.hostName);
        Assert.LessOrEqual(Encoding.UTF8.GetByteCount(room.roomName), DiscoveryProtocol.MaxNameBytes);
    }

    [Test]
    public void Announce_AllowsEmptyNames()
    {
        var data = Announce(Sample(string.Empty, string.Empty));

        Assert.IsTrue(DiscoveryProtocol.TryDecode(data, out _, out var room));
        Assert.AreEqual(string.Empty, room.roomName);
        Assert.AreEqual(string.Empty, room.hostName);
    }

    [Test]
    public void Probe_RoundTrips()
    {
        var data = DiscoveryProtocol.EncodeProbe();

        Assert.IsTrue(DiscoveryProtocol.TryDecode(data, out var type, out _));
        Assert.AreEqual(DiscoveryProtocol.TypeProbe, type);
    }

    [Test]
    public void TryDecode_RejectsWrongMagic()
    {
        var data = DiscoveryProtocol.EncodeProbe();
        data[0] = (byte)'X';

        Assert.IsFalse(DiscoveryProtocol.TryDecode(data, out _, out _));
    }

    [Test]
    public void TryDecode_RejectsWrongVersion()
    {
        var data = DiscoveryProtocol.EncodeProbe();
        data[4] = 99;

        Assert.IsFalse(DiscoveryProtocol.TryDecode(data, out _, out _));
    }

    [Test]
    public void TryDecode_RejectsUnknownType()
    {
        var data = DiscoveryProtocol.EncodeProbe();
        data[5] = 99;

        Assert.IsFalse(DiscoveryProtocol.TryDecode(data, out _, out _));
    }

    [Test]
    public void TryDecode_RejectsTruncatedAnnounce()
    {
        var data = DiscoveryProtocol.EncodeAnnounce(Sample());

        for (var length = 0; length < data.Length; length++)
        {
            Assert.IsFalse(DiscoveryProtocol.TryDecode(data, length, out _, out _),
                $"截断到 {length} 字节的公告包不应该被接受");
        }
    }

    [Test]
    public void TruncateUtf8_NeverSplitsACharacter()
    {
        var bytes = DiscoveryProtocol.TruncateUtf8("你好", 4);

        Assert.AreEqual(3, bytes.Length); // 只保留“你”，不会切出半个字符
        Assert.AreEqual("你", Encoding.UTF8.GetString(bytes));
    }

    [Test]
    public void NewRoomId_IsNeverZero()
    {
        Assert.AreNotEqual(0UL, DiscoveryProtocol.NewRoomId());
    }
}
