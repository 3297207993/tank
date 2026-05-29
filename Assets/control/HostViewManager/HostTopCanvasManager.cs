using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HostTopCanvasManager : MonoBehaviour
{
    public GameObject ConnectionView;
    public void onUserButtonClick()
    {
        this.gameObject.SetActive(false);
        ConnectionView.GetComponent<ConnectionViewManager>().Show(true);
    }
}
