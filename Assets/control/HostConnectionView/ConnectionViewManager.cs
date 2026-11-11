using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class ConnectionViewManager : NetworkBehaviour
{
    // Start is called before the first frame update
    VisualElement ui;
    VisualElement scrim;
    ListView content;
    Button startButton;
    Button addBotButton;
    Label countLabel;
    Label hintLabel;
    bool gameStarted;
    //给房间发现用：公告里要告诉别人这间房是不是已经开局了
    public bool GameStarted => gameStarted;
    readonly List<PlayerListManager.ClientData> playerList = new List<PlayerListManager.ClientData>();
    PlayerListManager playerListManager;
    ScoreViewManager scoreViewManager;
    public VisualTreeAsset item;
    public GameObject TopCanvas;
    void Start()
    {
        scoreViewManager = FindObjectOfType<ScoreViewManager>();
        ui = GetComponent<UIDocument>().rootVisualElement;
        scrim = ui.Q<VisualElement>("scrim");
        startButton = ui.Q<Button>("start");
        countLabel = ui.Q<Label>("count");
        hintLabel = ui.Q<Label>("hint");
        startButton.clicked += () => { startGame_ClientRpc(); };
        startButton.SetEnabled(false);
        ui.Q<Button>("quit").clicked += () => { 
            SceneManager.LoadScene("start"); 
        };

        playerListManager = FindObjectOfType<PlayerListManager>();

        addBotButton = ui.Q<Button>("addBot");
        if (addBotButton != null)
        {
            addBotButton.clicked += AddBotClicked;
            //等网络起来、确认是房主后再显示
            addBotButton.style.display = DisplayStyle.None;
        }

        //绑定listview
        content = ui.Q<ListView>("list");
        content.itemsSource = playerList;
        content.makeItem = () =>
        {
            var newListInstance = item.Instantiate();
            //元素会被 ListView 复用，点击回调只注册一次；目标玩家从 userData 里取，避免反复 bind 后重复触发
            var kickButton = newListInstance.Q<Button>("kick");
            kickButton.clicked += () =>
            {
                if (!NetworkManager.Singleton.IsServer) return;
                if (kickButton.userData is not PlayerListManager.ClientData target) return;
                if (NetworkManager.Singleton.LocalClientId == target.clientId) return;
                if (target.isBot)
                {
                    //电脑玩家没有真实连接，直接移除条目并清掉场上坦克
                    RemoveBotTank(target.clientId);
                    playerListManager.RemoveBot(target.clientId);
                    return;
                }
                NetworkManager.Singleton.DisconnectClient(target.clientId);
            };
            return newListInstance;
        };
        content.bindItem = (VisualElement element, int index) =>
        {
            new ClientItemManager(playerList[index]).bind(element);
        };
        playerListManager.PlayerList.OnListChanged += OnPlayerListChanged;
        if (playerListManager.IsSpawned) RefreshPlayerList();
        else StartCoroutine(RefreshPlayerListWhenSpawned());

        //开局时把摄像机移开，防止看到场景
        Camera.main.transform.position = new Vector3(10000, 0, -10);
    }

    public override void OnDestroy()
    {
        if (playerListManager != null) playerListManager.PlayerList.OnListChanged -= OnPlayerListChanged;
        base.OnDestroy();
    }

    private void OnPlayerListChanged(NetworkListEvent<PlayerListManager.ClientData> changeEvent)
    {
        RefreshPlayerList();
    }

    private IEnumerator RefreshPlayerListWhenSpawned()
    {
        yield return new WaitUntil(() => playerListManager.IsSpawned);
        RefreshPlayerList();
    }

    void RemoveBotTank(ulong botId)
    {
        foreach (var tank in FindObjectsOfType<TankController>())
        {
            if (!tank.isBot || tank.playerId != botId) continue;
            tank.GetComponent<NetworkObject>().Despawn(true);
            return;
        }
    }

    void AddBotClicked()
    {
        if (!IsServer || gameStarted || playerListManager == null || !playerListManager.IsSpawned) return;
        playerListManager.AddBot();
    }

    void RefreshAddBotButton()
    {
        if (addBotButton == null) return;
        bool canAdd = IsServer && !gameStarted && playerListManager != null && playerListManager.IsSpawned;
        addBotButton.style.display = canAdd ? DisplayStyle.Flex : DisplayStyle.None;
        addBotButton.SetEnabled(canAdd);
    }

    private void RefreshPlayerList()
    {
        playerList.Clear();
        foreach (var data in playerListManager.PlayerList) playerList.Add(data);
        content.RefreshItems();

        var count = playerList.Count;
        var ready = count > 1;
        if (countLabel != null) countLabel.text = $"{count} 名玩家";
        if (hintLabel != null)
        {
            if (ready) hintLabel.text = IsServer ? "人都到齐了，可以开始" : "等待房主开始游戏";
            else hintLabel.text = IsServer ? "等待其他玩家加入，或点“添加电脑”开始" : "等待房主开始游戏";
            hintLabel.EnableInClassList("lobby-hint--ready", ready);
        }
        if (IsServer) startButton.SetEnabled(ready);
        RefreshAddBotButton();
    }
    public void ResetMap()
    {
        if (IsServer)
        {
            MapGenerator gen =  FindObjectOfType<MapGenerator>();
            gen.ResetAndGenerate();
        }
    }
    public void Show(bool show)
    {
        ui.visible = show;
        //开局前遮住空场景（不透明蓝底），开局后压暗战局，和计分板保持一致
        if (scrim != null) scrim.EnableInClassList("lobby-scrim--dim", show && gameStarted);
        TopCanvas.SetActive(!show);
        RefreshAddBotButton();
    }
    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        //Esc 统一在这里处理，避免和计分板抢同一个按键
        if (scoreViewManager != null && scoreViewManager.IsOpen) scoreViewManager.Show(false);
        else Show(!ui.visible);
    }
    [ClientRpc]
    public void startGame_ClientRpc()
    {
        gameStarted = true;
        Show(false);
        //按“开始”视为开新一局，战绩清零
        if (IsServer) playerListManager.ClearScores();
        ResetMap();
    }
}
