using NUnit.Framework;
using UnityEngine;

/// <summary>
/// TankController 里不依赖联网状态的部分：初始弹药和子弹速度。
/// </summary>
public class TankControllerTests
{
    GameObject tankObject;
    GameObject bulletObject;

    [TearDown]
    public void TearDown()
    {
        if (tankObject != null) Object.DestroyImmediate(tankObject);
        if (bulletObject != null) Object.DestroyImmediate(bulletObject);
    }

    TankController CreateTank()
    {
        tankObject = new GameObject("Tank");
        return tankObject.AddComponent<TankController>();
    }

    [Test]
    public void NewTank_StartsWithFiveAmmo()
    {
        var tank = CreateTank();

        Assert.AreEqual(5, tank.Ammo);
    }

    [Test]
    public void BulletSpeed_WithoutBulletPrefab_IsZero()
    {
        var tank = CreateTank();

        Assert.AreEqual(0f, tank.BulletSpeed, 0.0001f);
    }

    [Test]
    public void BulletSpeed_IsReadFromBulletPrefab()
    {
        var tank = CreateTank();
        bulletObject = new GameObject("Bullet");
        bulletObject.AddComponent<CommonBulletController>();
        tank.bullet = bulletObject;

        Assert.AreEqual(6f, tank.BulletSpeed, 0.0001f);
    }
}
