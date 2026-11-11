using System;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// 房主端的房间公告：每隔一秒把房间信息发到局域网，并单播回应客户端的探测。
/// 发现功能只是锦上添花，任何一步失败都不会影响 StartHost 之后的正常联机。
/// </summary>
public class RoomBeacon : MonoBehaviour
{
    [Tooltip("房间名。留空时自动用“<电脑名> 的房间”，同一局域网里多间房也能区分开。")]
    public string roomName = "";

    [Tooltip("公告间隔（秒）。客户端超过 4 秒收不到公告就会把房间移除。")]
    public float announceInterval = 1f;

    DiscoveryChannel channel;
    PlayerListManager playerList;
    ConnectionViewManager lobby;
    ulong roomId;
    string hostName;
    string resolvedRoomName;
    ushort gamePort = 7777;
    float nextAnnounceTime;

    void Start()
    {
        hostName = ClientViewManager.clientName;
        if (string.IsNullOrWhiteSpace(hostName)) hostName = "房主";
        resolvedRoomName = string.IsNullOrWhiteSpace(roomName) ? MakeDefaultRoomName() : roomName;
        roomId = DiscoveryProtocol.NewRoomId();

        var transport = NetworkManager.Singleton != null
            ? NetworkManager.Singleton.GetComponent<UnityTransport>()
            : null;
        if (transport != null && transport.ConnectionData.Port != 0)
        {
            gamePort = transport.ConnectionData.Port;
        }

        if (announceInterval < 0.2f) announceInterval = 0.2f;

        channel = new DiscoveryChannel();
        if (!channel.Start(DiscoveryProtocol.DefaultPort, DiscoveryProtocol.DefaultMulticastAddress))
        {
            Debug.LogWarning("[LAN] 房间发现启动失败：没有找到可用的网络接口，其他玩家需要手动填写地址。");
            channel = null;
            return;
        }

        Announce();
    }

    void Update()
    {
        if (channel == null) return;

        // 有客户端来问就立刻回一包，让刷新按钮点下去马上有结果
        while (channel.TryDequeue(out var packet))
        {
            if (!DiscoveryProtocol.TryDecode(packet.Data, packet.Length, out var type, out _)) continue;
            if (type != DiscoveryProtocol.TypeProbe) continue;
            channel.Reply(packet, DiscoveryProtocol.EncodeAnnounce(BuildInfo()));
        }

        if (Time.unscaledTime >= nextAnnounceTime) Announce();
    }

    void Announce()
    {
        nextAnnounceTime = Time.unscaledTime + announceInterval;
        channel.Send(DiscoveryProtocol.EncodeAnnounce(BuildInfo()));
    }

    DiscoveryProtocol.RoomInfo BuildInfo()
    {
        if (playerList == null) playerList = FindObjectOfType<PlayerListManager>();
        if (lobby == null) lobby = FindObjectOfType<ConnectionViewManager>();

        // 房主自己至少算一名玩家，即使玩家列表还没来得及生成
        var players = playerList != null ? playerList.PlayerList.Count : 0;
        if (players < 1) players = 1;

        return new DiscoveryProtocol.RoomInfo
        {
            roomId = roomId,
            roomName = resolvedRoomName,
            hostName = hostName,
            gamePort = gamePort,
            players = (byte)Mathf.Clamp(players, 0, 255),
            inGame = lobby != null && lobby.GameStarted,
        };
    }

    static string MakeDefaultRoomName()
    {
        // 同一局域网里可能开着好几间房，用电脑名区分比全都叫“房主的房间”清楚
        var baseName = Environment.MachineName;
        const string suffix = "的房间";
        // 房间名也有 64 字节上限，先把前缀截到放得下后缀的长度
        var prefixBytes = DiscoveryProtocol.TruncateUtf8(
            baseName,
            DiscoveryProtocol.MaxNameBytes - Encoding.UTF8.GetByteCount(suffix));
        return Encoding.UTF8.GetString(prefixBytes) + suffix;
    }

    void OnDestroy()
    {
        if (channel != null)
        {
            channel.Stop();
            channel = null;
        }
    }
}
