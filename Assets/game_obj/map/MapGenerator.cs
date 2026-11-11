using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;


//注意，只有服务器可以调用这个脚本里的方法
public class MapGenerator : NetworkBehaviour
{
    Vector2Int mapSize = new Vector2Int(100, 100);
    public Vector2Int MapSize { get { return mapSize; } }
    public Vector2 ScaledMapSize { get { return new Vector2(mapSize.x * mapScale, mapSize.y * mapScale); } }
    public GameObject roadblock;
    private static readonly float mapScale = 3.0f;
    public GameObject mapContainer;
    public GameObject tankObj;

    //服务器生成后保留格子数据，电脑玩家用它做 A* 寻路
    public MapDataContainer CurrentMap { get; private set; }

    /// <summary>测试专用：直接注入一张构造好的地图，并同步格子尺寸。</summary>
    internal void SetMapForTests(MapDataContainer mapData)
    {
        CurrentMap = mapData;
        mapSize = mapData.Size;
    }

    readonly List<GameObject> spawnedBots = new List<GameObject>();

    public void ResetMap()
    {
        //TO DO: 重置地图
        CurrentMap = null;
        spawnedBots.Clear();
        foreach(Transform child in mapContainer.transform)
        {
            child.GetComponent<NetworkObject>().Despawn(true);
        }
        GameObject[] tankList = GameObject.FindGameObjectsWithTag("Tank");
        foreach(GameObject tank in tankList)
        {
            tank.GetComponent<NetworkObject>().Despawn(true);
        }
        GameObject[] bulletList = GameObject.FindGameObjectsWithTag("Bullet");
        foreach (GameObject bullet in bulletList)
        {
            bullet.GetComponent<NetworkObject>().Despawn(true);
        }
        foreach (var data in FindObjectOfType<PlayerListManager>().PlayerList)
        {
            GameObject tank = Instantiate(tankObj, new Vector3(0, 0, 0), Quaternion.identity);
            if (data.isBot)
            {
                //电脑玩家由服务器直接持有，不是真实连接，不能走 SpawnAsPlayerObject
                tank.GetComponent<NetworkObject>().Spawn(true);
                var controller = tank.GetComponent<TankController>();
                controller.isBot = true;
                controller.SetPlayerId(data.clientId);
                spawnedBots.Add(tank);
            }
            else
            {
                tank.GetComponent<NetworkObject>().SpawnAsPlayerObject(data.clientId, true);
            }
        }
    }

    public void ResetAndGenerate()
    {
        //房主中途按“开始”会直接重开，取消可能还挂着的本局结算
        CancelInvoke(nameof(EndRound));
        ResetMap();
        GenerateMap();
        StartBotBrains();
    }

    void StartBotBrains()
    {
        if (!IsServer) return;
        foreach (var tank in spawnedBots)
        {
            if (tank == null) continue;
            var brain = tank.GetComponent<AiTankController>();
            if (brain == null) brain = tank.AddComponent<AiTankController>();
            brain.Initialize(this);
            Debug.Log($"[AI] 电脑玩家 {tank.GetComponent<TankController>().playerId} 已开始行动");
        }
        spawnedBots.Clear();
    }

    public void ResetAndGenerateLater(float delay)
    {
        CancelInvoke(nameof(EndRound));
        Invoke(nameof(EndRound), delay);
    }

    /// <summary>
    /// 一局结束：先结算最终存活，再切换地图。
    /// 只有活到切图前一刻的坦克才算存活；等待期间被流弹击毁的不算。
    /// </summary>
    void EndRound()
    {
        AwardSurvivals();
        ResetAndGenerate();
    }

    void AwardSurvivals()
    {
        if (!IsServer) return;
        var playerListManager = FindObjectOfType<PlayerListManager>();
        if (playerListManager == null) return;
        foreach (var tank in FindObjectsOfType<TankController>())
        {
            if (tank == null || !tank.IsSpawned) continue;
            playerListManager.AddSurvival(tank.playerId);
        }
    }

    [ClientRpc]
    private void SetCameraData_ClientRpc(Vector2 ScaledMapSize)
    {
        Camera.main.transform.position = new Vector3(ScaledMapSize.x / 2, ScaledMapSize.y / 2, -10);
        Camera.main.orthographicSize = Mathf.Max(ScaledMapSize.x / Camera.main.aspect, ScaledMapSize.y) / 2.0f;
    }

    public void GenerateMap()
    {
        GameObject[] tankList = GameObject.FindGameObjectsWithTag("Tank");
        mapSize = new Vector2Int(UnityEngine.Random.Range(5, 20), UnityEngine.Random.Range(5, 20));
        MapDataContainer mapData = new MapDataContainer(mapSize);
        List<Vector2Int> tankPos = new List<Vector2Int>();
        for(int i = 0; i < tankList.Length; i++)
        {
            Vector2Int pos;
            do
            {
                pos = new Vector2Int(UnityEngine.Random.Range(0, mapSize.x), UnityEngine.Random.Range(0, mapSize.y));
            } while (tankPos.Contains(pos));
            tankPos.Add(pos);
        }
        bool IsConnectedAll()
        {
            //只要确保tankPos[0]和其他位置互通，就说明所有位置互通
            for (int i = 1;i < tankPos.Count;i++)
            {
                if (!mapData.isConnected(tankPos[i], tankPos[0])) return false;
            }
            return true;
        }
        mapData.ForEach((bool oldValue) =>
        {
            return true;
        });
        //消除10%的路障，直到所有坦克位置互通
        while (!IsConnectedAll())
        {
            mapData.ForEach((bool oldValue) =>
            {
                if(oldValue) {
                    if (UnityEngine.Random.Range(0, 100) < 10)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    return false;
                }
            });
        }
        //让边缘全是路障
        for(int i=0;i<mapData.xCount;i++)
        {
            mapData.SetData(new MapDataContainer.BlockPosition(new Vector2Int(i, 0), MapDataContainer.BlockDirction.Down), true);
            mapData.SetData(new MapDataContainer.BlockPosition(new Vector2Int(i, mapData.yCount - 1), MapDataContainer.BlockDirction.Up), true);
        }
        for(int i=0;i<mapData.yCount;i++)
        {
            mapData.SetData(new MapDataContainer.BlockPosition(new Vector2Int(0, i), MapDataContainer.BlockDirction.Left), true);
            mapData.SetData(new MapDataContainer.BlockPosition(new Vector2Int(mapData.xCount - 1, i), MapDataContainer.BlockDirction.Right), true);
        }
        //设置坦克位置
        for (int i = 0; i < tankList.Length; i++)
        {
            Vector2Int pos = tankPos[i];
            tankList[i].transform.position = 
                new Vector3((pos.x + 0.5f) * mapScale, (pos.y + 0.5f) * mapScale,0);
        }
        for(int i=0;i<mapData.xCount;i++)
        {
            for(int j=0;j<mapData.yCount;j++)
            {
                Vector3 blockPos = new Vector3(i * mapScale, j * mapScale,0);
                //下
                if (mapData.GetData(new MapDataContainer.BlockPosition(new Vector2Int(i, j), MapDataContainer.BlockDirction.Down)))
                {
                    GameObject obj = Instantiate(roadblock, blockPos + new Vector3(mapScale / 2, 0, 0), Quaternion.Euler(0, 0, 90), mapContainer.transform);
                    obj.GetComponent<NetworkObject>().Spawn(true);
                    obj.transform.SetParent(mapContainer.transform);
                }
                //左
                if (mapData.GetData(new MapDataContainer.BlockPosition(new Vector2Int(i, j), MapDataContainer.BlockDirction.Left)))
                {
                    GameObject obj = Instantiate(roadblock, blockPos + new Vector3(0, mapScale / 2, 0), Quaternion.identity, mapContainer.transform);
                    obj.GetComponent<NetworkObject>().Spawn(true);
                    obj.transform.SetParent(mapContainer.transform);
                }
            }
        }
        for(int i=0;i<mapData.xCount;i++)
        {
            //上
            if (mapData.GetData(new MapDataContainer.BlockPosition(new Vector2Int(i, mapData.yCount - 1), MapDataContainer.BlockDirction.Up)))
            {
                Vector3 blockPos = new Vector3(i * mapScale, mapData.yCount * mapScale,0);
                GameObject obj = Instantiate(roadblock, blockPos + new Vector3(mapScale / 2, 0, 0), Quaternion.Euler(0, 0, 90), mapContainer.transform);
                obj.GetComponent<NetworkObject>().Spawn(true);
                obj.transform.SetParent(mapContainer.transform);
            }
        }
        for(int i=0;i<mapData.yCount;i++)
        {
            //右
            if (mapData.GetData(new MapDataContainer.BlockPosition(new Vector2Int(mapData.xCount - 1, i), MapDataContainer.BlockDirction.Right)))
            {
                Vector3 blockPos = new Vector3(mapData.xCount * mapScale, i * mapScale,0);
                GameObject obj = Instantiate(roadblock, blockPos + new Vector3(0, mapScale / 2, 0), Quaternion.identity, mapContainer.transform);
                obj.GetComponent<NetworkObject>().Spawn(true);
                obj.transform.SetParent(mapContainer.transform);
            }
        }
        //地图数据已经定稿，交给电脑玩家寻路使用
        CurrentMap = mapData;
        SetCameraData_ClientRpc(ScaledMapSize);
    }

    public static float CellSize => mapScale;

    public Vector2Int WorldToCell(Vector3 world)
    {
        return new Vector2Int(
            Mathf.Clamp(Mathf.FloorToInt(world.x / mapScale), 0, Mathf.Max(0, mapSize.x - 1)),
            Mathf.Clamp(Mathf.FloorToInt(world.y / mapScale), 0, Mathf.Max(0, mapSize.y - 1)));
    }

    public Vector3 CellCenter(Vector2Int cell)
    {
        return new Vector3((cell.x + 0.5f) * mapScale, (cell.y + 0.5f) * mapScale, 0f);
    }
}


public class MapDataContainer
{
    bool[][] data;
    public MapDataContainer(int xCount, int yCount)
    {
        data = new bool[xCount * 2 + 1][];
        for (int i = 0; i < xCount * 2 + 1; i++)
        {
            if (i % 2 == 0) data[i] = new bool[yCount];
            else data[i] = new bool[yCount + 1];
        }
    }
    public MapDataContainer(Vector2Int size): this(size.x, size.y)
    {
    }

    public int DataCount
    {
        get { 
            return (xCount+1) * yCount + xCount * (yCount - 1);
        }
    }

    public int xCount
    {
        get
        {
            int xCount = data.Length;
            xCount -= 1;
            xCount /= 2;
            return xCount;
        }
    }

    public int yCount
    {
        get
        {
            return data[0].Length;
        }
    }

    public Vector2Int Size
    {
        get
        {
            return new Vector2Int(xCount, yCount);
        }
    }

    public int getCount(bool value)
    {
        int count = 0;
        for(int i = 0; i < data.Length; i++)
        {
            for(int j = 0; j < data[i].Length; j++)
            {
                if (data[i][j] == value) count++;
            }
        }
        return count;
    }

    public bool GetData(BlockPosition pos)
    {
        Vector2Int v = pos.pos;
        switch (pos.dir)
        {
            case BlockDirction.Up:
                return data[v.x * 2 + 1][v.y + 1];
            case BlockDirction.Down:
                return data[v.x * 2 + 1][v.y];
            case BlockDirction.Left:
                return data[v.x * 2][v.y];
            case BlockDirction.Right:
                return data[v.x * 2 + 2][v.y];
            default:
                return false;
        }
    }

    public bool GetData(Vector2Int pos, BlockDirction dir)
    {
        return GetData(new BlockPosition(pos, dir));
    }

    public bool isConnected(Vector2Int pos1, Vector2Int pos2)
    {
        bool[][] grid = new bool[xCount][];
        for(int i = 0; i < xCount; i++)
        {
            grid[i] = new bool[yCount];
        }
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(pos1);
        grid[pos1.x][pos1.y] = true;
        while (queue.Count > 0)
        {
            Vector2Int cur = queue.Dequeue();
            if (cur == pos2) return true;
            //上
            if (cur.y + 1 < yCount && !GetData(cur, BlockDirction.Up) && !grid[cur.x][cur.y + 1])
            {
                grid[cur.x][cur.y + 1] = true;
                queue.Enqueue(new Vector2Int(cur.x, cur.y + 1));
            }
            //下
            if (cur.y - 1 >= 0 && !GetData(cur, BlockDirction.Down) && !grid[cur.x][cur.y - 1])
            {
                grid[cur.x][cur.y - 1] = true;
                queue.Enqueue(new Vector2Int(cur.x, cur.y - 1));
            }
            //左
            if (cur.x - 1 >= 0 && !GetData(cur, BlockDirction.Left) && !grid[cur.x - 1][cur.y])
            {
                grid[cur.x - 1][cur.y] = true;
                queue.Enqueue(new Vector2Int(cur.x - 1, cur.y));
            }
            //右
            if (cur.x + 1 < xCount && !GetData(cur, BlockDirction.Right) && !grid[cur.x + 1][cur.y])
            {
                grid[cur.x + 1][cur.y] = true;
                queue.Enqueue(new Vector2Int(cur.x + 1, cur.y));
            }
        }
        return false;
    }

    public void SetData(BlockPosition pos, bool value)
    {
        Vector2Int v = pos.pos;
        switch (pos.dir)
        {
            case BlockDirction.Up:
                data[v.x * 2 + 1][v.y + 1] = value;
                break;
            case BlockDirction.Down:
                data[v.x * 2 + 1][v.y] = value;
                break;
            case BlockDirction.Left:
                data[v.x * 2][v.y] = value;
                break;
            case BlockDirction.Right:
                data[v.x * 2 + 2][v.y] = value;
                break;
        }
    }

    public void SetData(Vector2Int pos1, BlockDirction dir, bool value)
    {
        SetData(new BlockPosition(pos1, dir), value);
    }

    public void ForEach(Func<bool,bool> callBack)
    {
        for(int i = 0; i < data.Length; i++)
        {
            for(int j = 0; j < data[i].Length; j++)
            {
                data[i][j] = callBack(data[i][j]);
            }
        }
    }


    public struct BlockPosition
    {
        public Vector2Int pos;
        public BlockDirction dir;
        public BlockPosition(Vector2Int pos,BlockDirction dir)
        {
            this.pos = pos;
            this.dir = dir;
        }
    }

    public enum BlockDirction
    {
        Up,
        Down,
        Left,
        Right
    }
}
