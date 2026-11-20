using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Singleton<T>
    where T : class, new()
{
    private static T s_instance;
    private static readonly object s_lock = new object();

    public static T Instance
    {
        get
        {
            if (s_instance == null)
            {
                lock (s_lock)
                {
                    if (s_instance == null)
                    {
                        s_instance = new T();
                        (s_instance as Singleton<T>)?.OnInit();
                    }
                }
            }
            return s_instance;
        }
    }

    protected virtual void OnInit() { }
}
