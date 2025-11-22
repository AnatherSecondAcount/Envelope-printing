namespace Envelope_printing.Utils
{
    public class LruCache<TKey, TValue>
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>> _map;
        private readonly LinkedList<(TKey Key, TValue Value)> _list;
        private readonly ReaderWriterLockSlim _lock = new();

        public LruCache(int capacity = 128)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            _map = new Dictionary<TKey, LinkedListNode<(TKey, TValue)>>();
            _list = new LinkedList<(TKey, TValue)>();
        }

        public bool TryGet(TKey key, out TValue value)
        {
            _lock.EnterUpgradeableReadLock();
            try
            {
                if (_map.TryGetValue(key, out var node))
                {
                    _lock.EnterWriteLock();
                    try
                    {
                        _list.Remove(node);
                        _list.AddFirst(node);
                    }
                    finally { _lock.ExitWriteLock(); }
                    value = node.Value.Value;
                    return true;
                }
            }
            finally { _lock.ExitUpgradeableReadLock(); }
            value = default;
            return false;
        }

        public void Add(TKey key, TValue value)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_map.TryGetValue(key, out var existing))
                {
                    _list.Remove(existing);
                    _map.Remove(key);
                }
                var node = new LinkedListNode<(TKey, TValue)>((key, value));
                _list.AddFirst(node);
                _map[key] = node;
                if (_map.Count > _capacity)
                {
                    var last = _list.Last;
                    if (last != null)
                    {
                        _map.Remove(last.Value.Key);
                        _list.RemoveLast();
                    }
                }
            }
            finally { _lock.ExitWriteLock(); }
        }

        public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
        {
            if (TryGet(key, out var existing)) return existing;
            var val = valueFactory(key);
            Add(key, val);
            return val;
        }

        public void Clear()
        {
            _lock.EnterWriteLock();
            try
            {
                _map.Clear();
                _list.Clear();
            }
            finally { _lock.ExitWriteLock(); }
        }
    }
}
