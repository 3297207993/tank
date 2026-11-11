using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class HostController : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        //延时触发，确保其他部分初始化完成
        Invoke(nameof(StartHost), 0.1f);
    }

    void StartHost()
    {
        if (NetworkManager.Singleton.IsClient) return;

        if (!NetworkManager.Singleton.StartHost()) return;

        //网络起来后再开始局域网广播；发现失败不影响联机，所以不检查结果
        if (GetComponent<RoomBeacon>() == null) gameObject.AddComponent<RoomBeacon>();
    }

    // Update is called once per frame
    void Update()
    {
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            NetworkManager.Singleton.Shutdown();
        }
        Destroy(NetworkManager.Singleton.gameObject);
    }
}
