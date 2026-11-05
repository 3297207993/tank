using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerListManager:NetworkBehaviour
{
    public struct ClientData : INetworkSerializable, IEquatable<ClientData>
    {
        public ulong clientId;
        public FixedString64Bytes name;
        public int score;
        public bool isBot;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref clientId);
            serializer.SerializeValue(ref name);
            serializer.SerializeValue(ref score);
            serializer.SerializeValue(ref isBot);
        }

        public bool Equals(ClientData other)
        {
            return clientId == other.clientId && name.Equals(other.name) && score == other.score && isBot == other.isBot;
        }

        public override bool Equals(object obj)
        {
            return obj is ClientData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(clientId, name, score, isBot);
        }
    }

    //NetworkList 由服务器维护，增删改会自动同步到所有客户端
    public readonly NetworkList<ClientData> PlayerList = new NetworkList<ClientData>();

    //电脑玩家用高位 id，避免和真实连接 id 冲突
    const ulong BotIdBase = 1UL << 63;
    int nextBotIndex;
    [SerializeField] int botCount = 0;

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

        //大厅里按需补充电脑玩家；默认 0 个，由房主在界面上添加
        for (int i = 0; i < botCount; i++)
        {
            AddBot();
        }
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

    /// <summary>
    /// 给指定玩家加分。只能由服务器调用，NetworkList 会自动同步给所有客户端。
    /// </summary>
    public void AddScore(ulong clientId, int amount = 1)
    {
        if (!IsServer) return;
        for (int i = 0; i < PlayerList.Count; i++)
        {
            if (PlayerList[i].clientId != clientId) continue;
            var data = PlayerList[i];
            data.score += amount;
            PlayerList[i] = data;
            return;
        }
    }

    /// <summary>
    /// 清空所有玩家的分数（开始新一局时调用）。
    /// </summary>
    public void ClearScores()
    {
        if (!IsServer) return;
        for (int i = 0; i < PlayerList.Count; i++)
        {
            var data = PlayerList[i];
            if (data.score == 0) continue;
            data.score = 0;
            PlayerList[i] = data;
        }
    }

    /// <summary>
    /// 添加一个由服务器控制的电脑玩家，自动命名。只能由服务器调用。
    /// </summary>
    public ulong AddBot()
    {
        return AddBot(string.Empty);
    }

    /// <summary>
    /// 添加一个由服务器控制的电脑玩家。只能由服务器调用。
    /// </summary>
    public ulong AddBot(string botName)
    {
        if (!IsServer) return 0;
        if (string.IsNullOrEmpty(botName)) botName = $"电脑 {nextBotIndex + 1}";
        ulong botId = BotIdBase + (ulong)nextBotIndex++;
        PlayerList.Add(new ClientData
        {
            clientId = botId,
            name = botName,
            isBot = true
        });
        return botId;
    }

    /// <summary>
    /// 移除一个电脑玩家。只能由服务器调用。
    /// </summary>
    public void RemoveBot(ulong clientId)
    {
        if (!IsServer) return;
        for (int i = PlayerList.Count - 1; i >= 0; i--)
        {
            if (PlayerList[i].clientId == clientId && PlayerList[i].isBot)
                PlayerList.RemoveAt(i);
        }
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
