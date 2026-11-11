using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 子弹发射依赖物理和 Start 生命周期，放在 PlayMode 里验证。
/// </summary>
public class BulletLaunchTests
{
    [UnityTest]
    public IEnumerator Launch_AppliesImpulseAlongLocalUp()
    {
        var bulletObject = new GameObject("Bullet");
        var body = bulletObject.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        bulletObject.AddComponent<CommonBulletController>();

        yield return new WaitForFixedUpdate();
        yield return null;

        Assert.AreEqual(6f, body.velocity.magnitude, 0.01f);
        Assert.Greater(Vector2.Dot(body.velocity.normalized, Vector2.up), 0.99f);

        Object.Destroy(bulletObject);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Launch_FollowsRotatedBullet()
    {
        var bulletObject = new GameObject("Bullet");
        bulletObject.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
        var body = bulletObject.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        bulletObject.AddComponent<CommonBulletController>();

        yield return new WaitForFixedUpdate();
        yield return null;

        Assert.Greater(Vector2.Dot(body.velocity.normalized, Vector2.right), 0.99f);

        Object.Destroy(bulletObject);
        yield return null;
    }
}
