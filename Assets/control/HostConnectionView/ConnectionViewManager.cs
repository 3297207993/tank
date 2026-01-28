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
            new ClientItemManager(connectedClients[index]).bind(element);
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
        ResetMap();
    }
    public void ResetMap()
    {
        if (IsServer)
        {
            MapGenerator gen =  FindObjectOfType<MapGenerator>();
            gen.ResetAndGenerate();
        }
    }
    List<ClientData> connectedClients = new List<ClientData>();
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
        connectedClients.RemoveAll((item) => item.clientId == clientId);
        content.RefreshItems();
    }
    public struct ClientData : INetworkSerializable
    {
        public ulong clientId;
        public string name;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref clientId);
            serializer.SerializeValue(ref name);
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
            if (item.clientId == data.clientId)
            {
                return;
            }
        }
        connectedClients.Add(new ClientData() { name = data.name, clientId = rpcParams.Receive.SenderClientId });
        content.RefreshItems();
        foreach (var i in connectedClients){updateConnectionData_ClientRpc(i);}
    }
    [ClientRpc]
    void updateConnectionData_ClientRpc(ClientData data) {
        if (connectedClients.Contains(data)) return;
        else if (connectedClients.FindAll((ClientData item)=> item.clientId == data.clientId).Count != 0)
        {
            for(int i = 0; i < connectedClients.Count; i++)
            {
                if (connectedClients[i].clientId == data.clientId)
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
