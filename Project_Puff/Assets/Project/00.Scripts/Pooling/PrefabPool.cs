using System;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 프리팹 오브젝트 풀 (UnityEngine.Pool.ObjectPool 래퍼).
/// usePooling = false 이면 Get = Instantiate, Release = Destroy 로 동작해서
/// 같은 코드로 "풀링 전/후"를 Profiler로 비교할 수 있다. (포트폴리오 측정용 스위치)
/// </summary>
public class PrefabPool<T> where T : Component
{
    private readonly T prefab;
    private readonly Transform parent;
    private readonly bool usePooling;
    private readonly Action<T, PrefabPool<T>> onCreated;
    private readonly ObjectPool<T> pool;

    public PrefabPool(T prefab, Transform parent, bool usePooling, int defaultCapacity = 8, int maxSize = 64,
                      Action<T, PrefabPool<T>> onCreated = null)
    {
        this.prefab = prefab;
        this.parent = parent;
        this.usePooling = usePooling;
        this.onCreated = onCreated;

        if (usePooling)
        {
            pool = new ObjectPool<T>(
                createFunc: Create,
                actionOnGet: item => item.gameObject.SetActive(true),
                actionOnRelease: item => item.gameObject.SetActive(false),
                actionOnDestroy: item => { if (item != null) UnityEngine.Object.Destroy(item.gameObject); },
                collectionCheck: false,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize);
        }
    }

    private T Create()
    {
        T item = UnityEngine.Object.Instantiate(prefab, parent);
        onCreated?.Invoke(item, this);
        return item;
    }

    public T Get() => usePooling ? pool.Get() : Create();

    public void Release(T item)
    {
        if (item == null) return;

        if (usePooling) pool.Release(item);
        else UnityEngine.Object.Destroy(item.gameObject);
    }
}
