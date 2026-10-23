using UnityEngine;
using UnityEngine.UIElements;

//开始菜单：只负责把三个按钮接到 StartSceneControler，行为和原来完全一致
public class StartViewManager : MonoBehaviour
{
    void Start()
    {
        var controller = GetComponent<StartSceneControler>();
        var root = GetComponent<UIDocument>().rootVisualElement;
        root.Q<Button>("newRoom").clicked += () => controller.NewRoom();
        root.Q<Button>("joinRoom").clicked += () => controller.JoinRoom();
        root.Q<Button>("quit").clicked += () => controller.QuitGame();
    }
}
