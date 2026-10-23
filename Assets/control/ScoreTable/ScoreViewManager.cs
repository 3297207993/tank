using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

//计分板：按分数从高到低显示所有玩家
public class ScoreViewManager : MonoBehaviour
{
    public VisualTreeAsset item;
    public GameObject TopCanvas;

    VisualElement root;
    ListView list;
    PlayerListManager playerListManager;
    readonly List<PlayerListManager.ClientData> ranking = new List<PlayerListManager.ClientData>();

    public bool IsOpen { get; private set; }

    void Start()
    {
        root = GetComponent<UIDocument>().rootVisualElement;
        root.style.display = DisplayStyle.None;

        list = root.Q<ListView>("list");
        list.itemsSource = ranking;
        list.selectionType = SelectionType.None;
        list.makeItem = () => item.Instantiate();
        list.bindItem = (element, index) =>
        {
            if (index < 0 || index >= ranking.Count) return;
            new ScoreItemManager(ranking[index], index + 1, IsLocalPlayer(ranking[index])).bind(element);
        };

        root.Q<Button>("close").clicked += () => Show(false);

        playerListManager = FindObjectOfType<PlayerListManager>();
        if (playerListManager != null)
        {
            playerListManager.PlayerList.OnListChanged += OnPlayerListChanged;
            if (playerListManager.IsSpawned) RefreshRanking();
            else StartCoroutine(RefreshRankingWhenSpawned());
        }
    }

    void OnDestroy()
    {
        if (playerListManager != null) playerListManager.PlayerList.OnListChanged -= OnPlayerListChanged;
    }

    public void Show(bool show)
    {
        if (root == null) root = GetComponent<UIDocument>().rootVisualElement;
        IsOpen = show;
        root.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        if (TopCanvas != null) TopCanvas.SetActive(!show);
        if (show) RefreshRanking();
    }

    //Esc 由 ConnectionViewManager 统一处理，这里只负责刷新和显隐
    void OnPlayerListChanged(NetworkListEvent<PlayerListManager.ClientData> changeEvent)
    {
        //分数变了要重新排序，必须整个重建；单纯改名/上下线也重建，列表最多十几个人，开销可以忽略
        RefreshRanking();
    }

    IEnumerator RefreshRankingWhenSpawned()
    {
        yield return new WaitUntil(() => playerListManager.IsSpawned);
        RefreshRanking();
    }

    void RefreshRanking()
    {
        if (playerListManager == null) return;

        ranking.Clear();
        foreach (var data in playerListManager.PlayerList) ranking.Add(data);
        ranking.Sort((a, b) =>
        {
            int byScore = b.score.CompareTo(a.score);
            return byScore != 0 ? byScore : a.clientId.CompareTo(b.clientId);
        });

        if (list != null) list.RefreshItems();
    }

    bool IsLocalPlayer(PlayerListManager.ClientData data)
    {
        var networkManager = NetworkManager.Singleton;
        return networkManager != null && networkManager.IsClient && networkManager.LocalClientId == data.clientId;
    }
}
