using UnityEngine;

/// <summary>
/// 普通子弹：沿用基类的通用逻辑，只在这里给出自己的参数。
/// 以后新增子弹类型时，从基类派生并重写需要的钩子即可。
/// </summary>
public class CommonBulletController : BulletController
{
    [Header("子弹参数")]
    [SerializeField] float bulletSpeed = 6f;
    [SerializeField] float bulletLifeTime = 30f;

    public override float BulletSpeed => bulletSpeed;
    public override float BulletLifeTime => bulletLifeTime;
}
