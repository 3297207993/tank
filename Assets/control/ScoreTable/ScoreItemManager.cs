using UnityEngine;
using UnityEngine.UIElements;

//计分板里的一行：排名 / 玩家名 / 分数
public class ScoreItemManager
{
    static readonly Color FirstColor = new Color(1f, 0.82f, 0.35f);
    static readonly Color SecondColor = new Color(0.78f, 0.80f, 0.84f);
    static readonly Color ThirdColor = new Color(0.80f, 0.55f, 0.35f);
    static readonly Color NormalColor = new Color(0.63f, 0.63f, 0.63f);
    static readonly Color LocalPlayerBackground = new Color(1f, 0.82f, 0.35f, 0.18f);

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
        rankLabel.style.color = GetRankColor(rank);

        var name = data.name.ToString();
        element.Q<Label>("name").text = string.IsNullOrEmpty(name) ? $"玩家 {data.clientId}" : name;

        element.Q<Label>("score").text = data.score.ToString();

        var row = element.Q<VisualElement>("row");
        if (isLocalPlayer)
        {
            row.style.backgroundColor = LocalPlayerBackground;
        }
        else
        {
            row.style.backgroundColor = new Color(1f, 1f, 1f, 0.06f);
        }
    }

    static Color GetRankColor(int rank)
    {
        switch (rank)
        {
            case 1: return FirstColor;
            case 2: return SecondColor;
            case 3: return ThirdColor;
            default: return NormalColor;
        }
    }
}
