using UnityEngine.UIElements;

//计分板里的一行：排名 / 玩家名 / 击杀 / 死亡 / 最终存活
public class ScoreItemManager
{
    readonly PlayerListManager.ClientData data;
    readonly int rank;
    readonly bool isLocalPlayer;

    public ScoreItemManager(PlayerListManager.ClientData data, int rank, bool isLocalPlayer)
    {
        this.data = data;
        this.rank = rank;
        this.isLocalPlayer = isLocalPlayer;
    }

    public void bind(VisualElement element)
    {
        var rankLabel = element.Q<Label>("rank");
        rankLabel.text = rank.ToString();
        //元素会被 ListView 复用，三档名次样式都要显式设置，不能只添加
        rankLabel.EnableInClassList("rank--1", rank == 1);
        rankLabel.EnableInClassList("rank--2", rank == 2);
        rankLabel.EnableInClassList("rank--3", rank == 3);

        var name = data.name.ToString();
        element.Q<Label>("name").text = string.IsNullOrEmpty(name) ? $"玩家 {data.clientId}" : name;

        element.Q<Label>("kills").text = data.kills.ToString();
        element.Q<Label>("deaths").text = data.deaths.ToString();
        element.Q<Label>("survivals").text = data.survivals.ToString();

        element.Q<VisualElement>("row").EnableInClassList("score-row--local", isLocalPlayer);
    }
}
