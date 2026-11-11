using NUnit.Framework;
using UnityEngine;

/// <summary>
/// MapDataContainer 是地图生成和 A* 共用的格子数据。
/// 这里覆盖尺寸、四方向读写、相邻格共享墙、连通性判定和批量遍历。
/// </summary>
public class MapDataContainerTests
{
    static Vector2Int Cell(int x, int y)
    {
        return new Vector2Int(x, y);
    }

    [Test]
    public void Constructor_ReportsRequestedSize()
    {
        var map = new MapDataContainer(4, 3);

        Assert.AreEqual(4, map.xCount);
        Assert.AreEqual(3, map.yCount);
        Assert.AreEqual(new Vector2Int(4, 3), map.Size);
    }

    [Test]
    public void NewMap_HasNoWalls()
    {
        var map = new MapDataContainer(5, 4);

        Assert.AreEqual(0, map.getCount(true));
    }

    [Test]
    public void SetData_ReadsBackInEveryDirection()
    {
        var map = new MapDataContainer(4, 4);
        var cell = Cell(2, 2);

        foreach (MapDataContainer.BlockDirction direction in new[]
                 {
                     MapDataContainer.BlockDirction.Up,
                     MapDataContainer.BlockDirction.Down,
                     MapDataContainer.BlockDirction.Left,
                     MapDataContainer.BlockDirction.Right,
                 })
        {
            map.SetData(cell, direction, true);
            Assert.IsTrue(map.GetData(cell, direction), $"方向 {direction} 写入后应读到 true");

            map.SetData(cell, direction, false);
            Assert.IsFalse(map.GetData(cell, direction), $"方向 {direction} 覆盖后应读到 false");
        }
    }

    [Test]
    public void AdjacentCells_ShareTheSameWall()
    {
        var map = new MapDataContainer(3, 3);

        map.SetData(Cell(1, 1), MapDataContainer.BlockDirction.Right, true);
        Assert.IsTrue(map.GetData(Cell(2, 1), MapDataContainer.BlockDirction.Left));

        map.SetData(Cell(1, 1), MapDataContainer.BlockDirction.Up, true);
        Assert.IsTrue(map.GetData(Cell(1, 2), MapDataContainer.BlockDirction.Down));
    }

    [Test]
    public void BorderWalls_CanBeSetWithoutThrowing()
    {
        var map = new MapDataContainer(3, 3);

        Assert.DoesNotThrow(() =>
        {
            map.SetData(Cell(0, 0), MapDataContainer.BlockDirction.Left, true);
            map.SetData(Cell(0, 0), MapDataContainer.BlockDirction.Down, true);
            map.SetData(Cell(2, 2), MapDataContainer.BlockDirction.Right, true);
            map.SetData(Cell(2, 2), MapDataContainer.BlockDirction.Up, true);
        });

        Assert.IsTrue(map.GetData(Cell(0, 0), MapDataContainer.BlockDirction.Left));
        Assert.IsTrue(map.GetData(Cell(2, 2), MapDataContainer.BlockDirction.Up));
    }

    [Test]
    public void GetCount_CountsEveryStoredWallOnce()
    {
        var map = new MapDataContainer(3, 2);

        map.SetData(Cell(0, 0), MapDataContainer.BlockDirction.Right, true);
        map.SetData(Cell(0, 0), MapDataContainer.BlockDirction.Up, true);
        map.SetData(Cell(1, 1), MapDataContainer.BlockDirction.Left, true);
        map.SetData(Cell(2, 0), MapDataContainer.BlockDirction.Down, true);

        Assert.AreEqual(4, map.getCount(true));
        Assert.Greater(map.getCount(false), 0);
    }

    [Test]
    public void ForEach_AppliesReturnedValueToEverySlot()
    {
        var map = new MapDataContainer(2, 2);

        map.ForEach(_ => true);
        Assert.AreEqual(0, map.getCount(false));

        map.ForEach(_ => false);
        Assert.AreEqual(0, map.getCount(true));
    }

    [Test]
    public void ForEach_ReceivesCurrentValue()
    {
        var map = new MapDataContainer(2, 2);
        map.SetData(Cell(0, 0), MapDataContainer.BlockDirction.Right, true);

        int trueSeen = 0;
        map.ForEach(value =>
        {
            if (value) trueSeen++;
            return value;
        });

        Assert.AreEqual(1, trueSeen);
    }

    [Test]
    public void IsConnected_OpenMap_ConnectsOppositeCorners()
    {
        var map = new MapDataContainer(4, 4);

        Assert.IsTrue(map.isConnected(Cell(0, 0), Cell(3, 3)));
        Assert.IsTrue(map.isConnected(Cell(3, 3), Cell(0, 0)));
    }

    [Test]
    public void IsConnected_SameCell_ReturnsTrue()
    {
        var map = new MapDataContainer(3, 3);

        Assert.IsTrue(map.isConnected(Cell(1, 1), Cell(1, 1)));
    }

    [Test]
    public void IsConnected_WallCanBeWalkedAround()
    {
        var map = new MapDataContainer(2, 2);
        map.SetData(Cell(0, 0), MapDataContainer.BlockDirction.Right, true);

        Assert.IsTrue(map.isConnected(Cell(0, 0), Cell(1, 0)));
    }

    [Test]
    public void IsConnected_SealedCell_IsNotConnected()
    {
        var map = new MapDataContainer(3, 3);
        var sealedCell = Cell(1, 1);

        map.SetData(sealedCell, MapDataContainer.BlockDirction.Up, true);
        map.SetData(sealedCell, MapDataContainer.BlockDirction.Down, true);
        map.SetData(sealedCell, MapDataContainer.BlockDirction.Left, true);
        map.SetData(sealedCell, MapDataContainer.BlockDirction.Right, true);

        Assert.IsFalse(map.isConnected(sealedCell, Cell(0, 0)));
        Assert.IsFalse(map.isConnected(Cell(0, 0), sealedCell));
    }

    [Test]
    public void IsConnected_WallAcrossWholeMap_SeparatesBothSides()
    {
        var map = new MapDataContainer(3, 3);
        for (int y = 0; y < map.yCount; y++)
        {
            map.SetData(Cell(1, y), MapDataContainer.BlockDirction.Right, true);
        }

        Assert.IsFalse(map.isConnected(Cell(0, 0), Cell(2, 0)));
    }
}
