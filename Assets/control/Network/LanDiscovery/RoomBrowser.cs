using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 客户端搜到的房间。Address 用收到公告的来源地址，而不是房主自己报的 IP——
/// 房主不知道哪块网卡对这边可达。
/// </summary>
public sealed class DiscoveredRoom
{
    public DiscoveryProtocol.RoomInfo Info;
    public string Address;
    public long LastSeenTimestamp;
}

/// <summary>
/// 加入房间界面里的“附近的房间”：监听房主的组播公告，点一行就把地址、端口填进表单。
/// 手动填写地址的路径仍然保留，搜不到房间时照样能连。
/// </summary>
public class RoomBrowser : MonoBehaviour
{
    /// <summary>超过这个时间没再收到公告，就认为房间已经不存在了。</summary>
    public const float ExpireSeconds = 4f;

    public const float ProbeInterval = 1f;

    public event Action<DiscoveredRoom> RoomSelected;

    readonly Dictionary<ulong, DiscoveredRoom> rooms = new Dictionary<ulong, DiscoveredRoom>();
    readonly List<DiscoveredRoom> view = new List<DiscoveredRoom>();

    DiscoveryChannel channel;
    ListView list;
    Label hint;
    bool discoveryUnavailable;
    float nextProbeTime;

    void Start()
    {
        var document = GetComponent<UIDocument>();
        var root = document != null ? document.rootVisualElement : null;
        if (root != null)
        {
            list = root.Q<ListView>("rooms");
            hint = root.Q<Label>("discoverHint");
            var refresh = root.Q<Button>("refresh");
            if (refresh != null) refresh.clicked += ProbeNow;
        }

        if (list != null)
        {
            list.selectionType = SelectionType.None;
            list.itemsSource = view;
            list.makeItem = MakeRoomItem;
            list.bindItem = BindRoomItem;
        }

        channel = new DiscoveryChannel();
        if (channel.Start(DiscoveryProtocol.DefaultPort, DiscoveryProtocol.DefaultMulticastAddress))
        {
            ProbeNow();
            SetHint("正在搜索附近的房间…");
        }
        else
        {
            discoveryUnavailable = true;
            SetHint("无法搜索局域网房间，请手动填写房主地址", true);
        }
    }

    void Update()
    {
        if (channel == null || !channel.IsRunning) return;

        if (Time.unscaledTime >= nextProbeTime) ProbeNow();
        DrainIncoming();
    }

    void ProbeNow()
    {
        nextProbeTime = Time.unscaledTime + ProbeInterval;
        if (channel != null) channel.Send(DiscoveryProtocol.EncodeProbe());
    }

    void DrainIncoming()
    {
        var changed = false;
        var now = System.Diagnostics.Stopwatch.GetTimestamp();

        while (channel.TryDequeue(out var packet))
        {
            if (!DiscoveryProtocol.TryDecode(packet.Data, packet.Length, out var type, out var info)) continue;
            if (type != DiscoveryProtocol.TypeAnnounce) continue;

            if (!rooms.TryGetValue(info.roomId, out var room))
            {
                room = new DiscoveredRoom();
                rooms.Add(info.roomId, room);
                changed = true;
            }
            else if (!SameDisplayText(room.Info, info))
            {
                changed = true;
            }

            room.Info = info;
            room.Address = packet.Remote.Address.ToString();
            room.LastSeenTimestamp = now;
        }

        // 收不到公告就说明房主已经离开，或对方网络断了
        List<ulong> expired = null;
        foreach (var pair in rooms)
        {
            if (!IsExpired(pair.Value, now)) continue;
            if (expired == null) expired = new List<ulong>();
            expired.Add(pair.Key);
        }
        if (expired != null)
        {
            foreach (var id in expired) rooms.Remove(id);
            changed = true;
        }

        if (changed) Rebuild();
    }

    static bool IsExpired(DiscoveredRoom room, long now)
    {
        var elapsed = (now - room.LastSeenTimestamp) / (double)System.Diagnostics.Stopwatch.Frequency;
        return elapsed > ExpireSeconds;
    }

    static bool SameDisplayText(in DiscoveryProtocol.RoomInfo a, in DiscoveryProtocol.RoomInfo b)
    {
        return a.players == b.players
            && a.inGame == b.inGame
            && a.roomName == b.roomName
            && a.hostName == b.hostName;
    }

    void Rebuild()
    {
        view.Clear();
        view.AddRange(rooms.Values);
        view.Sort(CompareRooms);

        if (list != null) list.RefreshItems();
        if (discoveryUnavailable) return;

        SetHint(view.Count == 0
            ? "正在搜索附近的房间…也可以直接填写地址"
            : $"找到 {view.Count} 个房间，点击即可填入地址");
    }

    static int CompareRooms(DiscoveredRoom a, DiscoveredRoom b)
    {
        // 大厅中的房间排前面，人多的排前面
        if (a.Info.inGame != b.Info.inGame) return a.Info.inGame ? 1 : -1;
        if (a.Info.players != b.Info.players) return b.Info.players.CompareTo(a.Info.players);
        return string.Compare(a.Info.roomName, b.Info.roomName, StringComparison.Ordinal);
    }

    VisualElement MakeRoomItem()
    {
        var row = new VisualElement { pickingMode = PickingMode.Position };
        row.AddToClassList("room-row");

        var main = new VisualElement { pickingMode = PickingMode.Ignore };
        main.AddToClassList("room-main");

        var roomName = new Label { name = "roomName", pickingMode = PickingMode.Ignore };
        roomName.AddToClassList("room-name");
        var hostName = new Label { name = "hostName", pickingMode = PickingMode.Ignore };
        hostName.AddToClassList("room-host");
        main.Add(roomName);
        main.Add(hostName);

        var players = new Label { name = "roomPlayers", pickingMode = PickingMode.Ignore };
        players.AddToClassList("room-players");
        var state = new Label { name = "roomState", pickingMode = PickingMode.Ignore };
        state.AddToClassList("room-state");

        row.Add(main);
        row.Add(players);
        row.Add(state);

        row.RegisterCallback<ClickEvent>(_ =>
        {
            if (row.userData is DiscoveredRoom room) RoomSelected?.Invoke(room);
        });

        return row;
    }

    void BindRoomItem(VisualElement element, int index)
    {
        var room = view[index];
        element.userData = room;

        var roomName = room.Info.roomName;
        if (string.IsNullOrEmpty(roomName)) roomName = "未命名房间";
        element.Q<Label>("roomName").text = roomName;

        var hostName = room.Info.hostName;
        if (string.IsNullOrEmpty(hostName)) hostName = "未知房主";
        element.Q<Label>("hostName").text = hostName;

        element.Q<Label>("roomPlayers").text = $"{room.Info.players} 人";
        element.Q<Label>("roomState").text = room.Info.inGame ? "游戏中" : "大厅";
        element.EnableInClassList("room-row--in-game", room.Info.inGame);
    }

    void SetHint(string text, bool error = false)
    {
        if (hint == null) return;
        hint.text = text;
        hint.EnableInClassList("discover-hint--error", error);
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
