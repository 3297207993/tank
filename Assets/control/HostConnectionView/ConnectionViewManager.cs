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
    ListView content;
    readonly List<PlayerListManager.ClientData> playerList = new List<PlayerListManager.ClientData>();
    PlayerListManager playerListManager;
    public VisualTreeAsset item;
    public GameObject TopCanvas;
    void Start()
    {
        ui = GetComponent<UIDocument>().rootVisualElement;
        ui.Q<Button>("start").clicked += () => { startGame_ClientRpc(); };
        ui.Q<Button>("start").SetEnabled(false);
        ui.Q<Button>("quit").clicked += () => { 
            SceneManager.LoadScene("start"); 
        };

        playerListManager = FindObjectOfType<PlayerListManager>();
        //绑定listview
        content = ui.Q<ListView>("list");
        content.itemsSource = playerList;
        content.makeItem = () =>
        {
            var newListInstance = item.Instantiate();
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

    private void RefreshPlayerList()
    {
        playerList.Clear();
        foreach (var data in playerListManager.PlayerList) playerList.Add(data);
        content.RefreshItems();
        if (IsServer) ui.Q<Button>("start").SetEnabled(playerList.Count > 1);
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
        TopCanvas.SetActive(!show);
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Show(!ui.visible);
        }
    }
    [ClientRpc]
    public void startGame_ClientRpc()
    {
        Show(false);
        ResetMap();
    }
}
