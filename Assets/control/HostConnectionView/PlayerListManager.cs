using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerListManager:NetworkBehaviour
{
    public struct ClientData : INetworkSerializable
    {
        public ulong clientId;
        public string name;
        public int score;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref clientId);
            serializer.SerializeValue(ref name);
            serializer.SerializeValue(ref score);
        }
    }
    public Action OnPlayerListChanged;
    List<ClientData> playerList = new List<ClientData>();

    public void ApplyChanged()
    {
        OnPlayerListChanged?.Invoke();
    }
    public List<ClientData> PlayerList
    {
        get { return playerList; }
    }

    private void Start()
    {
        NetworkManager.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    void OnDestroy()
    {
        NetworkManager.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    [ClientRpc]
    void askForMessage_ClientRpc()
    {
        var data = new ClientData
        {
            clientId = NetworkManager.Singleton.LocalClientId,
            name = ClientViewManager.clientName
        };
        addClientData_ServerRpc(data);
    }
    [ServerRpc(RequireOwnership = false)]
    void addClientData_ServerRpc(ClientData data, ServerRpcParams rpcParams = default)
    {
        foreach (var item in playerList)
        {
            if (item.clientId == data.clientId)
            {
                return;
            }
        }
        playerList.Add(new ClientData() { name = data.name, clientId = rpcParams.Receive.SenderClientId });
        ApplyChanged();
        foreach (var i in playerList) { updatePlayerData_ClientRpc(i); }
    }
    [ClientRpc]
    public void updatePlayerData_ClientRpc(ClientData data)
    {
        if (playerList.Contains(data)) return;
        else if (playerList.FindAll((ClientData item) => item.clientId == data.clientId).Count != 0)
        {
            for (int i = 0; i < playerList.Count; i++)
            {
                if (playerList[i].clientId == data.clientId)
                {
                    playerList[i] = data;
                    break;
                }
            }
        }
        else
        {
            playerList.Add(data);
        }
        ApplyChanged();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        playerList.RemoveAll((item) => item.clientId == clientId);
        ApplyChanged();
    }

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            askForMessage_ClientRpc();
        }
    }
}
