using System;

namespace BPlusTree
{
    internal interface IReadOnlyBPTree<TKey, out TValue> /*: IEnumerable<TKey, TValue>*/
    {
        int InternalNodeCapacity { get; }
        int LeafCapacity { get; }
        int Height { get; }
        int Count { get; }
        
        TValue this[TKey key] { get; }
    }

    internal interface IBPTree<TKey, TValue> : IReadOnlyBPTree<TKey, TValue>
    {
        void Add(TKey key, TValue value);
        void Add(TKey key, TValue value, Func<(TKey key, TValue newValue, TValue oldValue), TValue> updateFunction);
        void Add<TArg>(TKey key, TArg arg, Func<(TKey key, TArg arg), TValue> addFunction, Func<(TKey key, TArg arg, TValue oldValue), TValue> updateFunction);

        bool Remove(TKey key, out TValue value);

        void Clear();
        
        bool TryGet(TKey key, out TValue value); // todo make this covariance.

        (TKey Key, TValue Value) First { get; } // todo make this covariance.
        (TKey Key, TValue Value) Last { get; } // todo make this covariance.
    }
}
