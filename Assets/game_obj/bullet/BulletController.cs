using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 子弹基类：统一处理发射、命中判定与到点销毁，具体子弹只需给出参数或重写对应钩子。
/// </summary>
public abstract class BulletController : NetworkBehaviour
{
    //由服务器在 Spawn 之前写入，用于命中时给射手加分（只在服务器使用，不需要同步）
    [System.NonSerialized] public ulong shooterClientId = ulong.MaxValue;

    /// <summary>子弹初速度，坦克用它解算射击提前量。由具体子弹类型提供。</summary>
    public abstract float BulletSpeed { get; }

    /// <summary>子弹存活时间，到点自动销毁。由具体子弹类型提供。</summary>
    public abstract float BulletLifeTime { get; }

    protected virtual void Start()
    {
        Launch();
        Invoke(nameof(DestroySelf), BulletLifeTime);
    }

    /// <summary>发射：默认沿自身朝向（本地 Y 轴）施加一次冲量。</summary>
    protected virtual void Launch()
    {
        var body = GetComponent<Rigidbody2D>();
        if (body != null) body.AddRelativeForce(Vector2.up * BulletSpeed, ForceMode2D.Impulse);
    }

    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        if (ShouldDestroyOnHit(collision)) DestroySelf();
    }

    /// <summary>命中判定：默认撞到坦克就销毁。</summary>
    protected virtual bool ShouldDestroyOnHit(Collision2D collision)
    {
        return collision.gameObject.tag == "Tank";
    }

    /// <summary>销毁子弹：只有服务器能真正移除网络对象。</summary>
    protected virtual void DestroySelf()
    {
        if (!IsServer) return;
        var networkObject = GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned) networkObject.Despawn();
    }
}
