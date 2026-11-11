using NUnit.Framework;
using UnityEngine;

/// <summary>
/// MapGenerator 的坐标换算：格子中心、世界坐标转格子、尺寸换算。
/// 地图生成本身依赖联网和预制体，这里只测不触网的部分。
/// </summary>
public class MapGeneratorTests
{
    GameObject mapObject;
    MapGenerator generator;

    [SetUp]
    public void SetUp()
    {
        mapObject = new GameObject("MapGeneratorTests");
        generator = mapObject.AddComponent<MapGenerator>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(mapObject);
    }

    [Test]
    public void CellSize_IsThreeWorldUnits()
    {
        Assert.AreEqual(3f, MapGenerator.CellSize);
    }

    [Test]
    public void DefaultMapSize_IsHundredByHundredCells()
    {
        Assert.AreEqual(new Vector2Int(100, 100), generator.MapSize);
        Assert.AreEqual(new Vector2(300f, 300f), generator.ScaledMapSize);
    }

    [Test]
    public void CellCenter_IsCenterOfTheCell()
    {
        Assert.AreEqual(new Vector3(1.5f, 4.5f, 0f), generator.CellCenter(new Vector2Int(0, 1)));
        Assert.AreEqual(new Vector3(7.5f, 10.5f, 0f), generator.CellCenter(new Vector2Int(2, 3)));
    }

    [Test]
    public void WorldToCell_FloorDividesAndClampsToMap()
    {
        Assert.AreEqual(new Vector2Int(0, 0), generator.WorldToCell(new Vector3(-5f, -5f, 0f)));
        Assert.AreEqual(new Vector2Int(2, 3), generator.WorldToCell(new Vector3(7.4f, 10.9f, 0f)));
        Assert.AreEqual(new Vector2Int(99, 99), generator.WorldToCell(new Vector3(999f, 999f, 0f)));
    }

    [Test]
    public void CellCenter_RoundTripsThroughWorldToCell()
    {
        for (int x = 0; x < 100; x += 7)
        {
            for (int y = 0; y < 100; y += 7)
            {
                var cell = new Vector2Int(x, y);
                Assert.AreEqual(cell, generator.WorldToCell(generator.CellCenter(cell)));
            }
        }
    }
}
