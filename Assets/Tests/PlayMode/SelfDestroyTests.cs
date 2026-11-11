using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 爆炸特效等临时物件靠 SelfDestroy 到点删除。
/// </summary>
public class SelfDestroyTests
{
    [UnityTest]
    public IEnumerator DestroysObjectAfterConfiguredTime()
    {
        var effect = new GameObject("Effect");
        var selfDestroy = effect.AddComponent<SelfDestroy>();
        selfDestroy.destroyTime = 0.1f;

        yield return new WaitForSeconds(0.3f);

        Assert.IsTrue(effect == null);
    }
}
