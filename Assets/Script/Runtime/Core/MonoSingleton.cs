using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class MonoSingleton<T> : MonoBehaviour
    where T : MonoBehaviour
{
    private static T s_instance;

    public static T Instance
    {
        get
        {
            if (s_instance == null)
            {
                s_instance = FindObjectOfType<T>();
                if (s_instance == null)
                {
                    GameObject go = new GameObject(typeof(T).Name);
                    s_instance = go.AddComponent<T>();
                    DontDestroyOnLoad(go);
                }
            }
            return s_instance;
        }
    }

    protected virtual void Awake()
    {
        if (s_instance == null)
        {
            s_instance = this as T;
            DontDestroyOnLoad(gameObject);
            OnInit();
        }
        else if (s_instance != this)
        {
            Destroy(gameObject);
        }
    }

    protected virtual void OnInit() { }

    protected virtual void OnDestroy() { }
}
