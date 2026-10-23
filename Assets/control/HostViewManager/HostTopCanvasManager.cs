using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HostTopCanvasManager : MonoBehaviour
{
    public GameObject ConnectionView;
    public ScoreViewManager ScoreView;

    public void onUserButtonClick()
    {
        this.gameObject.SetActive(false);
        ConnectionView.GetComponent<ConnectionViewManager>().Show(true);
    }

    public void onScoreButtonClick()
    {
        this.gameObject.SetActive(false);
        ScoreView.Show(true);
    }
}
