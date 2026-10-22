using System;
using Unity.Collections;
using Unity.Netcode;

public class PlayerListManager:NetworkBehaviour
{
    public struct ClientData : INetworkSerializable, IEquatable<ClientData>
    {
        public ulong clientId;
        public FixedString64Bytes name;
        public int score;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref clientId);
            serializer.SerializeValue(ref name);
            serializer.SerializeValue(ref score);
        }

        public bool Equals(ClientData other)
        {
            return clientId == other.clientId && name.Equals(other.name) && score == other.score;
        }

        public override bool Equals(object obj)
        {
            return obj is ClientData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(clientId, name, score);
        }
    }

    //NetworkList 由服务器维护，增删改会自动同步到所有客户端
    public readonly NetworkList<ClientData> PlayerList = new NetworkList<ClientData>();

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            SetName_ServerRpc(ClientViewManager.clientName);
            return;
        }

        NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;

        foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
        {
            SetName(clientId, default);
        }
        SetName(NetworkManager.LocalClientId, ClientViewManager.clientName);
    }

    public override void OnNetworkDespawn()
    {
        NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    [ServerRpc(RequireOwnership = false)]
    void SetName_ServerRpc(FixedString64Bytes name, ServerRpcParams rpcParams = default)
    {
        SetName(rpcParams.Receive.SenderClientId, name);
    }

    void SetName(ulong clientId, FixedString64Bytes name)
    {
        for (int i = 0; i < PlayerList.Count; i++)
        {
            if (PlayerList[i].clientId != clientId) continue;
            var data = PlayerList[i];
            data.name = name;
            PlayerList[i] = data;
            return;
        }
        PlayerList.Add(new ClientData { clientId = clientId, name = name });
    }

    private void OnClientDisconnected(ulong clientId)
    {
        for (int i = PlayerList.Count - 1; i >= 0; i--)
        {
            if (PlayerList[i].clientId == clientId) PlayerList.RemoveAt(i);
        }
    }
}
