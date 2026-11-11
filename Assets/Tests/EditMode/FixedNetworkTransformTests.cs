using NUnit.Framework;
using UnityEngine;

/// <summary>
/// FixedNetworkTransform.applyMatrix 是把增量矩阵叠加到 Transform 上的核心换算。
/// </summary>
public class FixedNetworkTransformTests
{
    GameObject targetObject;
    FixedNetworkTransform networkTransform;

    [SetUp]
    public void SetUp()
    {
        targetObject = new GameObject("FixedNetworkTransformTests");
        networkTransform = targetObject.AddComponent<FixedNetworkTransform>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(targetObject);
    }

    [Test]
    public void ApplyMatrix_TranslatesInWorldSpace()
    {
        networkTransform.applyMatrix(Matrix4x4.Translate(new Vector3(1f, 2f, 0f)));

        Assert.AreEqual(new Vector3(1f, 2f, 0f), targetObject.transform.position);
    }

    [Test]
    public void ApplyMatrix_RotatesTransform()
    {
        networkTransform.applyMatrix(Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 90f)));

        Assert.AreEqual(90f, targetObject.transform.eulerAngles.z, 0.001f);
    }

    [Test]
    public void ApplyMatrix_ScalesTransform()
    {
        networkTransform.applyMatrix(Matrix4x4.Scale(new Vector3(2f, 3f, 1f)));

        Assert.AreEqual(new Vector3(2f, 3f, 1f), targetObject.transform.localScale);
    }
}
