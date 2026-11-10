using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelfDestroy : MonoBehaviour
{
    public float destroyTime = 5f;
    // Start is called before the first frame update
    void Start()
    {
        Invoke(nameof(DestroyGameobject), destroyTime);
    }

    void DestroyGameobject()
    {
        Destroy(gameObject);
    }
}
