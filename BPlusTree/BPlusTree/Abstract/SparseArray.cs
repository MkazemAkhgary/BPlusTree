using System;
using System.Collections.Generic;
using System.Linq;

namespace BPlusTree
{
    /// <summary>
    /// represents an sparse array of <see cref="T"/>. supports duplicate items to be inserted in one key.
    /// </summary>
    public class SparseArray<K, T> : BPTree<K, RingArray<T>>
    {
        #region Fields/Properties
        
        private static readonly Func<(K, T arg), RingArray<T>> _add = t => RingArray<T>.NewArray(Enumerable.Repeat(t.arg, 1), 4) ;
        private static readonly Func<(K, T arg, RingArray<T> oldValue), RingArray<T>> _update = t =>
        {
            t.oldValue.Add(t.arg);
            return t.oldValue;
        };

        public new(K Key, IReadOnlyList<T> Values) Last => base.Last;
        public new(K Key, IReadOnlyList<T> Values) First => base.First;

        #endregion

        #region Constructors

        /// <summary>
        /// initializes a new <see cref="SparseArray{T}"/>.
        /// </summary>
        public SparseArray(IComparer<K> keyComparer = null, int internalNodeCapacity = 32, int leafCapacity = 32) 
            : base(keyComparer, internalNodeCapacity, leafCapacity)
        {
        }

        /// <summary>
        /// initializes a new <see cref="SparseArray{T}"/>.
        /// </summary>
        public SparseArray(IEnumerable<(K key, T value)> source, IComparer<K> keyComparer = null, int internalNodeCapacity = 32, int leafCapacity = 32)
            : this(keyComparer, internalNodeCapacity, leafCapacity)
        {
            var builder = new Builder(this);
            foreach ((K key, T value) in source) builder.Add(key, value);
            builder.Build();
        }

        #endregion

        #region Get/Add

        /// <summary>
        /// Add single item to sparse array.
        /// </summary>
        public void Add(K key, T item)
        {
            AddOrUpdateFromArg(key, item, _add, _update);
        }

        #endregion

        #region AsEnumerable

        /// <summary>
        /// returns an enumerable for this sparse array.
        /// </summary>
        public new IEnumerable<(K Key, T Value)> AsPairEnumerable(bool moveForward = true)
        {
            return GetEnumerable(base.AsPairEnumerable(moveForward), moveForward);
        }

        /// <summary>
        /// returns an enumerable for this sparse array.
        /// </summary>
        /// <typeparam name="TCast">target type to cast items while enumerating</typeparam>
        /// <param name="filter">if true is passed, filters the sequence otherwise casts the sequence values.</param>
        public new IEnumerable<(K Key, TCast Value)> AsPairEnumerable<TCast>(bool filter = true, bool moveForward = true)
        {
            var enumerable = AsPairEnumerable(moveForward);
            if (filter) enumerable = enumerable.Where(x => x.Value is TCast);
            return enumerable.Select(x => (x.Key, (TCast)(object)x.Value));
        }

        /// <summary>
        /// returns an enumerable for this sparse array.
        /// </summary>
        /// <param name="start">start of enumerable.</param>
        public new IEnumerable<(K Key, T Value)> AsPairEnumerable(K start, bool moveForward = true)
        {
            return GetEnumerable(base.AsPairEnumerable(start, moveForward), moveForward);
        }

        /// <summary>
        /// returns an enumerable for this sparse array.
        /// </summary>
        /// <typeparam name="TCast">target type to cast items while enumerating</typeparam>
        /// <param name="start">start of enumerable.</param>
        /// <param name="filter">if true is passed, filters the sequence otherwise casts the sequence values.</param>
        public new IEnumerable<(K Key, TCast Value)> AsPairEnumerable<TCast>(K start, bool filter = true, bool moveForward = true)
        {
            var enumerable = AsPairEnumerable(start, moveForward);
            if (filter) enumerable = enumerable.Where(x => x.Value is TCast);
            return enumerable.Select(x => (x.Key, (TCast)(object)x.Value));
        }

        private IEnumerable<(K Key, T Value)> GetEnumerable(IEnumerable<(K Key, RingArray<T> Values)> enumerable, bool moveForward)
        {
            return moveForward ? enumerable.SelectMany(x => x.Values, (x,y) => (x.Key, y))
                               : enumerable.SelectMany(x => x.Values.ToReversingList(), (x, y) => (x.Key, y));
        }

        /// <summary>
        /// returns an enumerable for this sparse array.
        /// </summary>
        public new IEnumerable<T> AsEnumerable(bool moveForward = true)
        {
            return AsPairEnumerable(moveForward).Select(pair => pair.Value);
        }

        /// <summary>
        /// returns an enumerable for this sparse array.
        /// </summary>
        /// <param name="start">start of enumerable.</param>
        public new IEnumerable<T> AsEnumerable(K start, bool moveForward = true)
        {
            return AsPairEnumerable(start, moveForward).Select(pair => pair.Value);
        }

        public new IEnumerator<T> GetEnumerator()
        {
            return AsEnumerable().GetEnumerator();
        }

        #endregion

        #region AsGrouping

        /// <summary>
        /// returns an enumerable for this sparse array.
        /// </summary>
        public IEnumerable<(K Key, IReadOnlyList<T> Value)> AsGrouping(bool moveForward = true)
        {
            return GetGrouping(base.AsPairEnumerable(moveForward), moveForward);
        }

        /// <summary>
        /// returns an enumerable for this sparse array.
        /// </summary>
        /// <typeparam name="TCast">target type to cast items while enumerating</typeparam>
        /// <param name="filter">if true is passed, filters the sequence otherwise casts the sequence values.</param>
        public IEnumerable<(K Key, IEnumerable<TCast> Value)> AsGrouping<TCast>(bool filter = true, bool moveForward = true)
        {
            var enumerable = base.AsPairEnumerable(moveForward);
            if (filter) return enumerable.Select(x => (x.Key, x.Value.OfType<TCast>()));
            else return enumerable.Select(x => (x.Key, x.Value.Cast<TCast>()));
        }

        /// <summary>
        /// returns an enumerable for this sparse array.
        /// </summary>
        /// <param name="start">start of enumerable.</param>
        public IEnumerable<(K Key, IReadOnlyList<T> Value)> AsGrouping(K start, bool moveForward = true)
        {
            return GetGrouping(base.AsPairEnumerable(start, moveForward), moveForward);
        }

        /// <summary>
        /// returns an enumerable for this sparse array.
        /// </summary>
        /// <typeparam name="TCast">target type to cast items while enumerating</typeparam>
        /// <param name="start">start of enumerable.</param>
        /// <param name="filter">if true is passed, filters the sequence otherwise casts the sequence values.</param>
        public IEnumerable<(K Key, IEnumerable<TCast> Value)> AsGrouping<TCast>(K start, bool filter = true, bool moveForward = true)
        {
            var enumerable = base.AsPairEnumerable(start, moveForward);
            if (filter) return enumerable.Select(x => (x.Key, x.Value.OfType<TCast>()));
            else return enumerable.Select(x => (x.Key, x.Value.Cast<TCast>()));
        }

        private IEnumerable<(K Key, IReadOnlyList<T> Value)> GetGrouping(IEnumerable<(K Key, RingArray<T> Values)> enumerable, bool moveForward)
        {
            return moveForward ? enumerable.Select(x => (x.Key, x.Values.ToReadOnlyList()))
                               : enumerable.Select(x => (x.Key, x.Values.ToReversingReadOnlyList()));
        }

        #endregion

        #region Builder

        internal new sealed class Builder // todo make this public, use builder interface.
        {
            private BPTree<K, RingArray<T>>.Builder builder;
            
            public Builder(SparseArray<K, T> tree)
            {
                builder = new BPTree<K, RingArray<T>>.Builder(tree);
            }

            public void Add(K key, T value) => builder.Add(key, value, _add, _update);

            public SparseArray<K, T> Build() => (SparseArray<K, T>) builder.Build();
        }

        #endregion
    }
}
