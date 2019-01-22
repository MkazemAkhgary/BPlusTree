using System;
using System.Collections.Generic;

namespace BPlusTree
{
    public interface IReadOnlyBPTree<TKey, out TValue> : IEnumerable<TValue> /*: IEnumerable<TKey, TValue>*/
    {
        int InternalNodeCapacity { get; }
        int LeafCapacity { get; }
        int Height { get; }
        int Count { get; }
        
        TValue this[TKey key] { get; }

        bool ContainsKey(TKey key);
        TValue NextNearest(TKey key);

        IEnumerable<TValue> AsEnumerable(bool moveForward = true);
        IEnumerable<TValue> AsEnumerable(TKey start, bool moveForward = true);
    }

    public interface IBPTree<TKey, TValue> : IReadOnlyBPTree<TKey, TValue>
    {
        void Add(TKey key, TValue value);
        bool TryAdd(TKey key, TValue value);
        bool AddOrReplace(TKey key, TValue value);
        bool AddOrUpdate(TKey key, TValue value, Func<(TKey key, TValue newValue, TValue oldValue), TValue> updateFunction);
        bool AddOrUpdateFromArg<TArg>(TKey key, TArg arg, Func<(TKey key, TArg arg), TValue> addFunction, Func<(TKey key, TArg arg, TValue oldValue), TValue> updateFunction);

        bool RemoveFirst(out TValue first);
        bool RemoveLast(out TValue last);
        bool Remove(TKey key, out TValue value);

        void Clear();
        
        bool TryGet(TKey key, out TValue value); // todo make this covariance.

        (TKey Key, TValue Value) First { get; } // todo make this covariance.
        (TKey Key, TValue Value) Last { get; } // todo make this covariance.

        IEnumerable<(TKey Key, TValue Value)> AsPairEnumerable(bool moveForward = true);
        IEnumerable<(TKey Key, TCast Value)> AsPairEnumerable<TCast>(bool filter = true, bool moveForward = true);
        IEnumerable<(TKey Key, TValue Value)> AsPairEnumerable(TKey start, bool moveForward = true);
        IEnumerable<(TKey Key, TCast Value)> AsPairEnumerable<TCast>(TKey start, bool filter = true, bool moveForward = true);
    }
}
