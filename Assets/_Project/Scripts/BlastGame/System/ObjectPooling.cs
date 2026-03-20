using System;
using System.Collections.Generic;

public class ObjectPooling<T> where T : class
{
    private readonly Stack<T> _pool = new Stack<T>();
    private readonly Func<T> _createFunc;
    private readonly Action<T> _onGet;
    private readonly Action<T> _onRelease;

    public ObjectPooling(
        Func<T> createFunc,
        Action<T> onGet = null,
        Action<T> onRelease = null)
    {
        if (createFunc == null) throw new ArgumentNullException(nameof(createFunc));

        _createFunc = createFunc;
        _onGet = onGet;
        _onRelease = onRelease;
    }

    public void CreatePooling(int count)
    {
        if (count <= 0) return;

        for (int i = 0; i < count; i++)
        {
            T item = _createFunc();
            _onRelease?.Invoke(item);
            _pool.Push(item);
        }
    }

    public T GetPooling()
    {
        T item = _pool.Count > 0 ? _pool.Pop() : _createFunc();
        _onGet?.Invoke(item);
        return item;
    }

    public void Release(T item)
    {
        if (item == null)
        {
            return;
        }

        _onRelease?.Invoke(item);
        _pool.Push(item);
    }
}