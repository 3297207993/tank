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
        NetworkManager.Singleton.StartHost();
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
    }
}
