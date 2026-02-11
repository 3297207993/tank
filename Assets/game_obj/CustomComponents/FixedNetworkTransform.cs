using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Unity.Netcode;
using UnityEngine;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

public class FixedNetworkTransform : NetworkBehaviour
{
    ulong tickCount = 0;
    bool shouldUpdate = true;
    public float updateFrequency = 0.05f;
    struct MatrixData:INetworkSerializable
    {
        public Matrix4x4 matrix;
        public ulong tickCount;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref matrix.m00);
            serializer.SerializeValue(ref matrix.m01);
            serializer.SerializeValue(ref matrix.m02);
            serializer.SerializeValue(ref matrix.m03);
            serializer.SerializeValue(ref matrix.m10);
            serializer.SerializeValue(ref matrix.m11);
            serializer.SerializeValue(ref matrix.m12);
            serializer.SerializeValue(ref matrix.m13);
            serializer.SerializeValue(ref matrix.m20);
            serializer.SerializeValue(ref matrix.m21);
            serializer.SerializeValue(ref matrix.m22);
            serializer.SerializeValue(ref matrix.m23);
            serializer.SerializeValue(ref matrix.m30);
            serializer.SerializeValue(ref matrix.m31);
            serializer.SerializeValue(ref matrix.m32);
            serializer.SerializeValue(ref matrix.m33);
            serializer.SerializeValue(ref tickCount);
        }
    }
    LinkedList<MatrixData> matrixList = new();
    public void addMatrix(Matrix4x4 matrix)
    {
        tickCount++;
        MatrixData data = new MatrixData
        {
            matrix = matrix,
            tickCount = tickCount
        };
        applyMatrix(matrix);
        if(!IsServer){
            matrixList.AddLast(data);
            sendMatrix_ServerRpc(data);
        }
        else shouldUpdate = true;
    }

    public void addMovement(Vector3 movement)
    {
        addMatrix(Matrix4x4.Translate(movement));
    }

    public void addRotate(Quaternion rotation)
    {
        addMatrix(Matrix4x4.Rotate(rotation));
    }

    public void addRotate(Vector3 rotate)
    {
        addMatrix(Matrix4x4.Rotate(Quaternion.Euler(rotate)));
    }

    public void setScale(Vector3 scale)
    {
        addMatrix(Matrix4x4.Scale(scale));
    }

    [ServerRpc(Delivery = RpcDelivery.Unreliable)]
    void sendMatrix_ServerRpc(MatrixData data)
    {
        applyMatrix(data.matrix);
        tickCount = data.tickCount;
        shouldUpdate = true;
    }

    public void applyMatrix(Matrix4x4 matrix)
    {
        Matrix4x4 mat = transform.localToWorldMatrix * matrix;
        Vector3 pos = mat.GetPosition();
        Quaternion rotate = mat.rotation;
        Vector3 scale = matrix.lossyScale;
        transform.localScale = scale;
        transform.position = pos;
        transform.rotation = rotate;
    }

    private void Start()
    {
        if(IsServer)
            InvokeRepeating(nameof(updateTransform),updateFrequency,updateFrequency);
    }

    void updateTransform()
    {
        if (shouldUpdate)
        {
            updateTransform_ClientRpc(transform.position, transform.rotation, transform.localScale, tickCount);
            shouldUpdate = false;
        }
    }
    public void InvalidateTranform()
    {
        shouldUpdate = true;
    }
    [ClientRpc(Delivery = RpcDelivery.Unreliable)]
    void updateTransform_ClientRpc(Vector3 pos,Quaternion rotate,Vector3 scale,ulong tickCount)
    {
        transform.position = pos;
        transform.rotation = rotate;
        transform.localScale = scale;
        while(matrixList.Count > 0)
        {
            if (matrixList.First().tickCount <= tickCount)
            {
                matrixList.RemoveFirst();
            }
            else
            {
                break;
            }
        }
        Matrix4x4 matrix = Matrix4x4.identity;
        foreach (var i in matrixList)
        {
            matrix *= i.matrix;
        }
        applyMatrix(matrix);
    }
}
