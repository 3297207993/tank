using Unity.Netcode;
using UnityEngine.UIElements;

public class ClientItemManager
{
    public PlayerListManager.ClientData data { get; set; }
    public ClientItemManager(PlayerListManager.ClientData data)
    {
        this.data = data;
    }

    public void bind(VisualElement element)
    {
        var label = element.Q<Label>("name");
        label.text = data.name.ToString();
        var id = element.Q<Label>("id");
        id.text = data.isBot ? "电脑" : "ID: " + data.clientId;
        //大厅里只展示击杀，详细战绩看计分板
        var kills = element.Q<Label>("kills");
        if (kills != null) kills.text = data.kills.ToString();

        var localId = NetworkManager.Singleton.LocalClientId;
        var isLocal = localId == data.clientId;
        var isHost = data.clientId == NetworkManager.ServerClientId;

        element.Q<VisualElement>("row").EnableInClassList("player-row--local", isLocal);

        var role = element.Q<Label>("role");
        if (role != null)
        {
            role.text = isHost ? "房主" : (isLocal ? "你" : "");
            role.EnableInClassList("is-hidden", role.text.Length == 0);
        }

        // 点击回调在 makeItem 里只注册一次，这里只负责同步按钮状态和当前玩家数据
        var kick_btn = element.Q<Button>("kick");
        kick_btn.userData = data;
        kick_btn.style.display = NetworkManager.Singleton.IsServer ? DisplayStyle.Flex : DisplayStyle.None;
        kick_btn.SetEnabled(!isLocal);
    }
}
