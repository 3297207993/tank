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

        PlayerListManager manager = FindObjectOfType<PlayerListManager>();
        //绑定listview
        content = ui.Q<ListView>("list");
        content.itemsSource = manager.PlayerList;
        content.makeItem = () =>
        {
            var newListInstance = item.Instantiate();
            return newListInstance;
        };
        content.bindItem = (VisualElement element, int index) =>
        {
            new ClientItemManager(manager.PlayerList[index]).bind(element);
        };
        manager.OnPlayerListChanged += () =>
        {
            content.RefreshItems();
            if(IsServer)ui.Q<Button>("start").SetEnabled(manager.PlayerList.Count > 1);
        };

        //开局时把摄像机移开，防止看到场景
        Camera.main.transform.position = new Vector3(10000, 0, -10);
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
