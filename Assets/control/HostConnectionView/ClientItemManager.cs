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
        id.text = "ID: " + data.clientId;
        var score = element.Q<Label>("score");
        if (score != null) score.text = "分数：" + data.score;
        var kick_btn = element.Q<Button>("kick");
        if(!NetworkManager.Singleton.IsServer)kick_btn.style.display = DisplayStyle.None;
        else if(NetworkManager.Singleton.LocalClientId == data.clientId) kick_btn.SetEnabled(false);
        kick_btn.clicked += () =>
            {
                if (NetworkManager.Singleton.IsServer)
                {
                    if (NetworkManager.Singleton.LocalClientId != data.clientId)
                        NetworkManager.Singleton.DisconnectClient(data.clientId);
                }
            };
    }
}
