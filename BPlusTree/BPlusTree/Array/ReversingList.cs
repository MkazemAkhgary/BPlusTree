using System;
using System.Collections;
using System.Collections.Generic;

namespace BPlusTree
{
    internal sealed class ReversingList<T> : IList<T>, IReadOnlyList<T>
    {
        private readonly IList<T> source;
        private int version;

        public ReversingList(IList<T> source)
        {
            this.source = source ?? throw new ArgumentOutOfRangeException(nameof(source));
        }

        private int Reverse(int index)
        {
            return source.Count - 1 - index;
        }

        public T this[int index]
        {
            get => source[Reverse(index)];
            set
            {
                source[Reverse(index)] = value;
                version++;
            }
        }

        public int Count => source.Count;

        public bool IsReadOnly => source.IsReadOnly;

        public void Add(T item)
        {
            source.Insert(0, item);
            version++;
        }

        public void Clear()
        {
            source.Clear();
            version++;
        }

        public bool Contains(T item)
        {
            return source.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));
            if (arrayIndex < 0 || arrayIndex >= array.Length)
                throw new IndexOutOfRangeException($"{nameof(arrayIndex)} is out of range. start index must be greater than zero and smaller than array length.");
            if (Count > array.Length - arrayIndex)
                throw new InvalidOperationException("target array is small.");

            for (int i = 0; i < Count; i++)
            {
                array[arrayIndex + i] = this[i];
            }
        }

        public int IndexOf(T item)
        {
            return Reverse(source.IndexOf(item));
        }

        public void Insert(int index, T item)
        {
            source.Insert(Reverse(index), item);
            version++;
        }

        public bool Remove(T item)
        {
            bool removed = source.Remove(item);
            version++;
            return removed;
        }

        public void RemoveAt(int index)
        {
            source.RemoveAt(Reverse(index));
            version++;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// struct enumerator for <see cref="ReversingList{T}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<T>
        {
            private ReversingList<T> source;
            private int version;
            private int position; // points to next position after current.
            private T current;

            /// <summary>
            /// initializes a new instance of <see cref="Enumerator"/>.
            /// </summary>
            public Enumerator(ReversingList<T> source)
            {
                this.source = source;
                version = source?.version ?? 0;
                position = 0;
                current = default;
            }

            /// <inheritdoc />
            public T Current
            {
                get
                {
                    if (source == null)
                        throw new InvalidOperationException("enumerator has no array. it's either disposed or initialized with null array.");
                    if (version != source.version)
                        throw new InvalidOperationException("collection was modified.");
                    if (position == 0)
                        throw new InvalidOperationException("enumerator cursor is not moved yet.");
                    if (position == source.Count + 1)
                        throw new InvalidOperationException("enumerator cursor has reached to the end.");
                    return current;
                }
            }

            /// <inheritdoc />
            public bool MoveNext()
            {
                if (source == null) return false;
                if (version != source.version) throw new InvalidOperationException("collection was modified.");
                
                if (position < source.Count)
                {
                    current = source[position++];
                    return true;
                }
                else
                {
                    position = source.Count + 1; // end marker
                    current = default;
                    return false;
                }
            }

            /// <inheritdoc />
            public void Reset()
            {
                version = source?.version ?? 0;
                position = 0;
                current = default;
            }

            object IEnumerator.Current => Current;

            /// <inheritdoc />
            public void Dispose()
            {
                source = null;
                Reset();
            }
        }
    }
}
