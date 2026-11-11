using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// AiNavigation 是电脑玩家的 A* 寻路，直接读 MapDataContainer 的墙数据。
/// 测试用 MapGenerator.SetMapForTests 注入手工构造的小地图，不启动联网。
/// </summary>
public class AiNavigationTests
{
    GameObject mapObject;
    MapGenerator generator;

    [TearDown]
    public void TearDown()
    {
        if (mapObject != null) Object.DestroyImmediate(mapObject);
        mapObject = null;
        generator = null;
    }

    MapGenerator CreateGenerator(MapDataContainer map)
    {
        mapObject = new GameObject("AiNavigationTests");
        generator = mapObject.AddComponent<MapGenerator>();
        generator.SetMapForTests(map);
        return generator;
    }

    static Vector3 Center(MapGenerator gen, int x, int y)
    {
        return gen.CellCenter(new Vector2Int(x, y));
    }

    static void AssertClose(Vector3 expected, Vector3 actual, string message = null)
    {
        Assert.Less(Vector3.Distance(expected, actual), 0.001f, message);
    }

    [Test]
    public void Repath_WithoutMap_ReturnsFalse()
    {
        mapObject = new GameObject("AiNavigationTests");
        generator = mapObject.AddComponent<MapGenerator>();
        var navigation = new AiNavigation(generator);

        Assert.IsFalse(navigation.Repath(Vector3.zero, new Vector3(6f, 6f, 0f)));
        Assert.IsFalse(navigation.HasPath);
    }

    [Test]
    public void Repath_StraightCorridor_ReturnsEveryCellInOrder()
    {
        var navigation = new AiNavigation(CreateGenerator(new MapDataContainer(6, 1)));
        Vector3 from = Center(generator, 0, 0);
        Vector3 to = Center(generator, 5, 0);

        Assert.IsTrue(navigation.Repath(from, to));

        Assert.AreEqual(5, navigation.Points.Count);
        for (int i = 0; i < navigation.Points.Count; i++)
        {
            AssertClose(Center(generator, i + 1, 0), navigation.Points[i], $"第 {i} 个路点");
        }
        AssertClose(to, navigation.Points[navigation.Points.Count - 1]);
    }

    [Test]
    public void Repath_SameCell_ReturnsTrueWithoutPath()
    {
        var navigation = new AiNavigation(CreateGenerator(new MapDataContainer(4, 4)));
        Vector3 position = Center(generator, 2, 2);

        Assert.IsTrue(navigation.Repath(position, position));
        Assert.IsFalse(navigation.HasPath);
    }

    [Test]
    public void Repath_Wall_RoutesAroundAndNeverCrossesTheWall()
    {
        var map = new MapDataContainer(3, 3);
        map.SetData(new Vector2Int(1, 1), MapDataContainer.BlockDirction.Right, true);
        var navigation = new AiNavigation(CreateGenerator(map));
        Vector3 from = Center(generator, 0, 1);
        Vector3 to = Center(generator, 2, 1);

        Assert.IsTrue(navigation.Repath(from, to));
        AssertClose(to, navigation.Points[navigation.Points.Count - 1]);

        Vector3 previous = from;
        foreach (var point in navigation.Points)
        {
            Vector2Int previousCell = generator.WorldToCell(previous);
            Vector2Int currentCell = generator.WorldToCell(point);
            bool crossedWall =
                (previousCell == new Vector2Int(1, 1) && currentCell == new Vector2Int(2, 1)) ||
                (previousCell == new Vector2Int(2, 1) && currentCell == new Vector2Int(1, 1));
            Assert.IsFalse(crossedWall, "路径不应穿过 (1,1) 与 (2,1) 之间的墙");
            previous = point;
        }
    }

    [Test]
    public void Repath_UnreachableTarget_StopsAtCellClosestToTarget()
    {
        var map = new MapDataContainer(3, 3);
        var target = new Vector2Int(2, 2);
        map.SetData(target, MapDataContainer.BlockDirction.Up, true);
        map.SetData(target, MapDataContainer.BlockDirction.Down, true);
        map.SetData(target, MapDataContainer.BlockDirction.Left, true);
        map.SetData(target, MapDataContainer.BlockDirction.Right, true);
        var navigation = new AiNavigation(CreateGenerator(map));

        Assert.IsTrue(navigation.Repath(Center(generator, 0, 0), Center(generator, 2, 2)));

        Vector2Int lastCell = generator.WorldToCell(navigation.Points[navigation.Points.Count - 1]);
        Assert.AreEqual(1, Mathf.Abs(lastCell.x - target.x) + Mathf.Abs(lastCell.y - target.y),
            "目标不可达时应停在离目标一格的位置");
    }

    [Test]
    public void Repath_ClearCallback_StraightensPathToVisiblePoint()
    {
        var navigation = new AiNavigation(CreateGenerator(new MapDataContainer(6, 1)));
        Vector3 from = Center(generator, 0, 0);
        Vector3 to = Center(generator, 5, 0);

        Assert.IsTrue(navigation.Repath(from, to, (_, __) => true));

        Assert.AreEqual(1, navigation.Points.Count);
        AssertClose(to, navigation.Points[0]);
    }

    [Test]
    public void CurrentWaypoint_AdvancesPastReachedWaypoints()
    {
        var navigation = new AiNavigation(CreateGenerator(new MapDataContainer(6, 1)));
        Vector3 from = Center(generator, 0, 0);
        Vector3 to = Center(generator, 5, 0);
        navigation.Repath(from, to);

        Vector3 second = navigation.Points[1];
        Vector3 waypoint = navigation.CurrentWaypoint(navigation.Points[0], 0.5f);
        AssertClose(second, waypoint, "已经到达的路点应被丢弃");

        // 依次走到每个路点，路径会被逐个消费；走完后返回当前位置
        var points = new List<Vector3>(navigation.Points);
        foreach (var point in points)
        {
            waypoint = navigation.CurrentWaypoint(point, 0.5f);
        }
        AssertClose(points[points.Count - 1], waypoint, "路径走完后返回当前所在位置");
        Assert.IsFalse(navigation.HasPath);
    }

    [Test]
    public void Clear_RemovesPath()
    {
        var navigation = new AiNavigation(CreateGenerator(new MapDataContainer(4, 1)));
        navigation.Repath(Center(generator, 0, 0), Center(generator, 3, 0));
        Assert.IsTrue(navigation.HasPath);

        navigation.Clear();

        Assert.IsFalse(navigation.HasPath);
        Assert.AreEqual(0, navigation.Points.Count);
    }
}
