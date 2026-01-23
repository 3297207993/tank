using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class ConnectionViewManager : NetworkBehaviour
{
    // Start is called before the first frame update
    VisualElement ui;
    ListView content;
    public VisualTreeAsset item;
    void Start()
    {
        ui = GetComponent<UIDocument>().rootVisualElement;
        ui.Q<Button>("start").clicked += () => { startGame_ClientRpc(); };
        ui.Q<Button>("start").SetEnabled(false);

        //绑定listview
        content = ui.Q<ListView>("list");
        content.itemsSource = connectedClients;
        content.makeItem = () =>
        {
            var newListInstance = item.Instantiate();
            return newListInstance;
        };
        content.bindItem = (VisualElement element, int index) =>
        {
            var label = element.Q<Label>("name");
            label.text = connectedClients[index].data.name;
            var id = element.Q<Label>("id");
            id.text = connectedClients[index].data.clientId.ToString();
        };

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
    }

    private void OnEnable()
    {
        //开局时把摄像机移开，防止看到场景
        Camera.main.transform.position = new Vector3(10000, 0, -10);
    }
    private void OnDisable()
    {
        Camera.main.transform.position = new Vector3(0, 0, -10);
    }
    List<ServerData> connectedClients = new List<ServerData>();
    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            ui.Q<Button>("start").SetEnabled(true);
            askForMessage_ClientRpc();
        }
    }
    private void OnClientDisconnect(ulong clientId)
    {
        connectedClients.RemoveAll((item) => item.data.clientId == clientId);
        content.RefreshItems();
    }
    struct ClientData : INetworkSerializable
    {
        public ulong clientId;
        public string name;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref clientId);
            serializer.SerializeValue(ref name);
        }
    }
    struct ServerData : INetworkSerializable
    {
        public ClientData data;
        public bool isEnabled;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref data);
            serializer.SerializeValue(ref isEnabled);
        }
    }
    [ClientRpc]
    void askForMessage_ClientRpc() {
        var data = new ClientData
        {
            clientId = NetworkManager.Singleton.LocalClientId,
            name = ClientVieManager.clientName
        };
        addClientData_ServerRpc(data);
    }
    [ServerRpc(RequireOwnership = false)]
    void addClientData_ServerRpc(ClientData data, ServerRpcParams rpcParams = default) { 
        foreach (var item in connectedClients)
        {
            if (item.data.clientId == data.clientId)
            {
                return;
            }
        }
        connectedClients.Add(new ServerData
        {
            data = new ClientData() { name = data.name, clientId = rpcParams.Receive.SenderClientId },
            isEnabled = false
        });
        content.RefreshItems();
        foreach (var i in connectedClients){updateConnectionData_ClientRpc(i);}
    }
    [ClientRpc]
    void updateConnectionData_ClientRpc(ServerData data) {
        if (connectedClients.Contains(data)) return;
        else if (connectedClients.FindAll((ServerData item)=> item.data.clientId == data.data.clientId).Count != 0)
        {
            for(int i = 0; i < connectedClients.Count; i++)
            {
                if (connectedClients[i].data.clientId == data.data.clientId)
                {
                    connectedClients[i] = data;
                    break;
                }
            }
        }
        else
        {
            connectedClients.Add(data);
        }
        content.RefreshItems();
    }
    [ClientRpc]
    public void startGame_ClientRpc()
    {
        this.gameObject.SetActive(false);
    }
}
