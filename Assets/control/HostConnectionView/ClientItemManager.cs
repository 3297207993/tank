using Unity.Netcode;
using UnityEngine.UIElements;

public class ClientItemManager
{
    public ConnectionViewManager.ClientData data { get; set; }
    public ClientItemManager(ConnectionViewManager.ClientData data)
    {
        this.data = data;
    }

    public void bind(VisualElement element)
    {
        var label = element.Q<Label>("name");
        label.text = data.name;
        var id = element.Q<Label>("id");
        id.text = data.clientId.ToString();
        var kick_btn = element.Q<Button>("kick");
        kick_btn.clicked += () =>
        {
            if (NetworkManager.Singleton.IsServer)
            {
                if(NetworkManager.Singleton.LocalClientId!=data.clientId) 
                    NetworkManager.Singleton.DisconnectClient(data.clientId);
            }
        };
    }
}
