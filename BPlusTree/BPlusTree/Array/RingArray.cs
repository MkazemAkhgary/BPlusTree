using System;
using System.Collections;
using System.Collections.Generic;

namespace BPlusTree
{
    /// <summary>
    /// circular array that supports push and pop at both ends and has better insertion/deletion than traditional list.
    /// always chooses minimum shift direction to insert or remove an item.
    /// supports split and merge and binary search.
    /// </summary>
    public class RingArray<T> : IList<T>, IReadOnlyList<T>
    {
        private T[] array;
        private int Start; // index of first item.
        
        #region Properties

        /// <summary>
        /// gets end index. end is exclusive which means if array is not full, end index is empty.
        /// </summary>
        private int End => Adjust(Start + Count);

        /// <summary>
        /// if array content is rotated this property is true.
        /// </summary>
        private bool IsRotated => Start + Count > Capacity;

        /// <summary>
        /// if array items reaches its capacity and is about to resize.
        /// </summary>
        public bool IsFull => Count == Capacity;

        /// <summary>
        /// if there are at least (<see cref="Capacity"/> / 2) items.
        /// </summary>
        public bool IsHalfFull => Count >= Capacity / 2;

        /// <inheritdoc />
        bool ICollection<T>.IsReadOnly => false;

        /// <summary>
        /// number of items in this array.
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// current capacity of array. 
        /// </summary>
        public int Capacity => array.Length;

        /// <summary>
        /// retrieves last item from the array.
        /// </summary>
        /// <exception cref="InvalidOperationException">throws exception if array is empty.</exception>
        public T Last
        {
            get => Get(Count - 1);
            set => Set(Count - 1, value);
        }

        /// <summary>
        /// retrieves first item from array.
        /// </summary>
        /// <exception cref="InvalidOperationException">throws exception if array is empty.</exception>
        public T First
        {
            get => array[Start];
            set => array[Start] = value;
        }

        #endregion

        #region Constructors

        /// <summary>
        /// initializes a new instance of <see cref="RingArray{T}"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public RingArray(int capacity)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            array = new T[capacity];
            Count = 0;
            Start = 0;
        }

        //public RingArray(IEnumerable<T> enumerable)
        //{
        //    array = enumerable?.ToArray() ?? throw new ArgumentNullException(nameof(enumerable));
        //    Start = 0;
        //    Length = array.Length;
        //}

        //public RingArray(T[] array, int start, int initialLength)
        //{
        //    if (start < 0 || start >= array.Length) throw new ArgumentOutOfRangeException(nameof(start));
        //    if (initialLength < 0 || initialLength > array.Length) throw new ArgumentOutOfRangeException(nameof(initialLength));
        //    this.array = array?.ToArray() ?? throw new ArgumentNullException(nameof(array));
        //    Start = start;
        //    Length = initialLength;
        //}

        #endregion

        #region Insert/Remove

        /// <summary>
        /// inserts an item in this array with minimum shift required.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public virtual void Insert(int index, T item)
        {
            if (OutOfRangeExclusive(index)) throw new IndexOutOfRangeException(nameof(index));
            if (Count >= Capacity) ExpandCapacity();

            var lsh = index; // length of left shift
            var rsh = Count - index; // length of right shift

            if (lsh < rsh) // choose least shifts required
            {
                LeftShift(array, Start, lsh); // move Start to Start-1
                Set(index - 1, item);
                DecrementStart();
            }
            else
            {
                RightShift(array, Adjust(Start + index), rsh); // move End to End+1
                Set(index, item);
            }

            Count++;
        }

        /// <summary>
        /// remove item from this array with minimum shift required.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException"></exception>
        /// <exception cref="InvalidOperationException">no items to remove.</exception>
        public virtual T RemoveAt(int index)
        {
            if (Count <= 0) throw new InvalidOperationException("no items to remove");
            if (OutOfRange(index)) throw new IndexOutOfRangeException(nameof(index));

            var item = Get(index);

            var lsh = Count - index - 1; // length of left shift
            var rsh = index; // length of right shift

            if (rsh < lsh) // choose least shifts required
            {
                RightShift(array, Start, rsh); // move Start to Start+1
                array[Start] = default(T);
                IncrementStart();
            }
            else
            {
                LeftShift(array, Adjust(Start + index + 1), lsh); // move End to End-1
                Set(Count - 1, default(T)); // remove last item
            }

            Count--;
            return item;
        }

        /// <summary>
        /// removes an item from this array.
        /// </summary>
        public bool Remove(T item)
        {
            var index = IndexOf(item);
            if (index < 0) return false;
            RemoveAt(index);
            return true;
        }

        /// <summary>
        /// adds an item to the end of this array.
        /// </summary>
        public void Add(T item) => Insert(Count, item);
        
        void IList<T>.RemoveAt(int index) => RemoveAt(index);

        #endregion

        #region IndexOf/Contains

        /// <inheritdoc />
        public int IndexOf(T item)
        {
            var comparer = EqualityComparer<T>.Default;

            int i = Start;
            var end = Start + Count;
            var fix = 0;
            if (IsRotated) // search right side first
            {
                for (; i < Capacity; i++)
                {
                    if (comparer.Equals(item, array[i])) // remove if item is found
                    {
                        return i - Start; // convert abs index to relative
                    }
                }
                i = 0;
                fix = Capacity;
                end = end - Capacity; // to search remaining left side
            }
            for (; i < end; i++)
            {
                if (comparer.Equals(item, array[i])) // remove if item is found
                {
                    return i - Start + fix;
                }
            }

            return -1;
        }

        /// <inheritdoc />
        public bool Contains(T item) => IndexOf(item) >= 0;

        #endregion

        #region Push/Pop

        /// <summary>
        /// pushes an item to the first of this array.
        /// </summary>
        public void PushFirst(T item)
        {
            if (Count >= Capacity) ExpandCapacity();

            DecrementStart();
            First = item;
            Count++;
        }

        /// <summary>
        /// pushes an item to the end of this array.
        /// </summary>
        public void PushLast(T item)
        {
            if (Count >= Capacity) ExpandCapacity();

            Count++;
            Last = item;
        }

        /// <summary>
        /// pops an item from start of this array. 
        /// </summary>
        /// <exception cref="InvalidOperationException">no items to remove.</exception>
        public T PopFirst()
        {
            if (Count <= 0) throw new InvalidOperationException("no items to remove.");
            
            var temp = First;
            First = default(T);
            IncrementStart();
            Count--;
            return temp;
        }

        /// <summary>
        /// pops an item from end of this array. 
        /// </summary>
        /// <exception cref="InvalidOperationException">no items to remove.</exception>
        public T PopLast()
        {
            if (Count <= 0) throw new InvalidOperationException("no items to remove.");
            
            Count--;
            var end = End;
            var temp = array[end];
            array[end] = default(T);
            return temp;
        }

        /// <summary>
        /// inserts an item to this array and pops first item without altering length and capacity.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public T InsertPopFirst(int index, T item)
        {
            if (OutOfRangeExclusive(index)) throw new IndexOutOfRangeException(nameof(index));
            
            if (index == 0) return item;
            var value = PopFirst();
            Insert(index - 1, item);
            return value;
        }

        /// <summary>
        /// inserts an item to this array and pops last item without altering length and capacity.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public T InsertPopLast(int index, T item)
        {
            if (OutOfRangeExclusive(index)) throw new IndexOutOfRangeException(nameof(index));

            if (index == Count) return item;
            var value = PopLast();
            Insert(index, item);
            return value;
        }

        #endregion

        #region BinarySearch/Sorted

        /// <summary>
        /// performs binary search on this array and returns index of the item.
        /// if no item is found, complement of index of next nearst item is returned.
        /// </summary>
        public int BinarySearch(T item) => BinarySearch(item, Comparer<T>.Default);

        /// <summary>
        /// performs binary search on this array and returns index of the item.
        /// if no item is found, complement of index of next nearst item is returned.
        /// </summary>
        public int BinarySearch(T item, IComparer<T> comparer)
        {
            if (IsRotated)
            {
                if (comparer.Compare(item, array[Capacity - 1]) <= 0) // search right side if item is smaller than last item in array.
                {
                    var find = Array.BinarySearch(array, Start, Capacity - Start, item, comparer);
                    return find - Start * find.Sign();
                }
                else // search left side
                {
                    var find = Array.BinarySearch(array, 0, End, item, comparer);
                    return find + (Capacity - Start) * find.Sign();
                }
            }
            else
            {
                var find = Array.BinarySearch(array, Start, Count, item, comparer);
                return find - Start * find.Sign();
            }
        }

        /// <summary>
        /// inserts an item in order using binarysearch.
        /// </summary>
        public void InsertOrdered(T item) => InsertOrdered(item, Comparer<T>.Default);

        /// <summary>
        /// inserts an item in order using binarysearch.
        /// </summary>
        public void InsertOrdered(T item, IComparer<T> comparer)
        {
            var find = BinarySearch(item, comparer);
            if (find < 0) find = ~find;
            Insert(find, item);
        }

        //public bool RemoveOrdered(T item)
        //{
        //    var find = BinarySearch(item);
        //    if (find < 0) return false;
        //    RemoveAt(find);
        //    return true;
        //}

        #endregion

        #region Boundary Check/Adjustment

        /// <summary>
        /// checkes wether index is in valid range or not. edges are valid.
        /// </summary>
        private bool OutOfRangeExclusive(int index)
        {
            return index > Count || index < 0;
        }

        /// <summary>
        /// checkes wether index is in valid range or not. edges are not valid.
        /// </summary>
        private bool OutOfRange(int index)
        {
            return index >= Count || index < 0;
        }

        /// <summary>
        /// maps the given index into correct position in this array.
        /// </summary>
        private int Adjust(int index)
        {
            //return ((index % Cap) + Cap) % Cap; // slow
            if (index < 0 || index >= Capacity)
                return index + Capacity * (-index).Sign(); // index>=Cap ? index-Cap : index<0 ? index+Cap : index
            return index;
        }

        /// <summary>
        /// derement and adjust start position.
        /// </summary>
        private void DecrementStart()
        {
            if (Start == 0) Start = Capacity - 1;
            else Start--;
        }

        /// <summary>
        /// increment and adjust start position.
        /// </summary>
        private void IncrementStart()
        {
            if (Start == Capacity - 1) Start = 0;
            else Start++;
        }

        #endregion

        #region Random Access

        /// <summary>
        /// gets an item from specified index without boundary checks. (only adjustment)
        /// </summary>
        private T Get(int index)
        {
            return array[Adjust(Start + index)];
        }

        /// <summary>
        /// sets an item at specified index without boundary checks. (only adjustment)
        /// </summary>
        private void Set(int index, T value)
        {
            array[Adjust(Start + index)] = value;
        }

        /// <summary>
        /// gets or sets an item from specified index.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public virtual T this[int index]
        {
            get
            {
                if (OutOfRange(index)) throw new IndexOutOfRangeException(nameof(index));
                return Get(index);
            }
            set
            {
                if (OutOfRange(index)) throw new IndexOutOfRangeException(nameof(index));
                Set(index, value);
            }
        }

        #endregion

        #region Clear/Expand
        
        /// <summary>
        /// expands the capacity of this array so it can hold more items.
        /// </summary>
        private void ExpandCapacity()
        {
            var newCap = Capacity == 0 ? 4 : Capacity * 2;
            var newArr = new T[newCap];

            if (IsRotated)
            {
                Array.Copy(array, Start, newArr, 0, Capacity - Start);
                Array.Copy(array, 0, newArr, Capacity - Start, Start + Count - Capacity);
            }
            else
            {
                Array.Copy(array, Start, newArr, 0, Count);
            }

            array = newArr;
            Start = 0;
        }

        /// <inheritdoc />
        public virtual void Clear()
        {
            Array.Clear(array, 0, array.Length);
            Start = 0;
            Count = 0;
        }

        /// <inheritdoc />
        public void CopyTo(T[] array, int arrayIndex)
        {
            if (Count > array.Length - arrayIndex)
                throw new InvalidOperationException("target array is small.");

            if (IsRotated)
            {
                Array.Copy(this.array, Start, array, arrayIndex, Capacity - Start);
                Array.Copy(this.array, 0, array, Capacity - Start + arrayIndex, Start + Count - Capacity);
            }
            else
            {
                Array.Copy(this.array, Start, array, arrayIndex, Count);
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// left shifts range of items by 1.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        private static void LeftShift(T[] x, int index, int length)
        {
            if (length == 0) return;
            if (length < 0 || length > x.Length) throw new ArgumentOutOfRangeException(nameof(length));
            if (index < 0 || index >= x.Length) throw new ArgumentOutOfRangeException(nameof(length));

            if (index == 0)
            {
                var first = x[0];
                Array.Copy(x, 1, x, 0, length - 1);
                x[x.Length - 1] = first;
            }
            else if (index + length > x.Length)
            {
                var llength = index + length - x.Length - 1;
                var remaining = length - llength - 1;
                var first = x[0];
                Array.Copy(x, 1, x, 0, llength); // shift left side
                Array.Copy(x, index, x, index - 1, remaining); // shift remaining right side
                x[x.Length - 1] = first;
            }
            else
            {
                Array.Copy(x, index, x, index - 1, length);
            }
        }

        /// <summary>
        /// right shifts range of items by 1.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        private static void RightShift(T[] x, int index, int length)
        {
            if (length == 0) return;
            if (length < 0 || length > x.Length) throw new ArgumentOutOfRangeException(nameof(length));
            if (index < 0 || index >= x.Length) throw new ArgumentOutOfRangeException(nameof(length));

            var lastInd = x.Length - 1;
            if (index + length > lastInd) // if overflows, rotate.
            {
                var last = x[lastInd];
                var rlength = lastInd - index;
                var remaining = length - rlength - 1;
                Array.Copy(x, index, x, index + 1, rlength); // shift right side
                Array.Copy(x, 0, x, 1, remaining); // shift remaining left side
                x[0] = last;
            }
            else
            {
                Array.Copy(x, index, x, index + 1, length);
            }
        }

        /// <summary>
        /// splits right side to new array and keeps left side for current array.
        /// </summary>
        public RingArray<T> SplitRight()
        {
            var right = new RingArray<T>(Capacity);

            var lr = Count / 2; // length of right side
            var lrc = 1 + ((Count - 1) / 2); // length of right (ceiling of Length/2)
            var sr = Adjust(Start + lrc); // start of right side

            right.Count = lr;
            Count = Count - right.Count;

            if (sr + lr <= Capacity) // if right side is one piece
            {
                Array.Copy(array, sr, right.array, 0, lr);
                Array.Clear(array, sr, lr); // clear side effects for gc.
            }
            else
            {
                var length = Capacity - sr;
                Array.Copy(array, sr, right.array, 0, length);
                Array.Clear(array, sr, length);

                var remaining = lr - length;
                Array.Copy(array, 0, right.array, length, remaining);
                Array.Clear(array, 0, remaining);
            }

            return right;
        }

        /// <summary>
        /// merges right array with this (i.e left) array.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public void MergeLeft(RingArray<T> right)
        {
            if (Count + right.Count > Capacity)
                throw new InvalidOperationException("can not merge, there is not enough capacity for this array.");

            var end = Start + Count;

            if(IsRotated)
            {
                var start = end - Capacity;

                if (!right.IsRotated)
                {
                    Array.Copy(right.array, right.Start, array, start, right.Count);
                }
                else
                {
                    var srLen = right.Capacity - right.Start; // right length
                    var slLen = right.Count - srLen; // left length (remaining)

                    Array.Copy(right.array, right.Start, array, start, srLen);
                    Array.Copy(right.array, 0, array, start + srLen, slLen);
                }
            }
            else
            {
                bool copyIsOnePiece = end + right.Count <= Capacity;

                if (!right.IsRotated)
                {
                    if(copyIsOnePiece)
                    {
                        Array.Copy(right.array, right.Start, array, end, right.Count);
                    }
                    else
                    {
                        var length = Capacity - end;
                        var remaining = right.Count - length;

                        Array.Copy(right.array, right.Start, array, end, length);
                        Array.Copy(right.array, right.Start + length, array, 0, remaining);
                    }
                }
                else
                {
                    var srLen = right.Capacity - right.Start; // right length
                    var slLen = right.Count - srLen; // left length (remaining)

                    if (copyIsOnePiece)
                    {
                        Array.Copy(right.array, right.Start, array, end, srLen);
                        Array.Copy(right.array, 0, array, end + srLen, slLen);
                    }
                    else
                    {
                        var mergeEnd = end + srLen;

                        if (mergeEnd <= Capacity)
                        {
                            var secondCopyFirstLength = Capacity - mergeEnd;
                            var secondCopySecondLength = slLen - secondCopyFirstLength;

                            Array.Copy(right.array, right.Start, array, end, srLen);

                            Array.Copy(right.array, 0, array, mergeEnd, secondCopyFirstLength);
                            Array.Copy(right.array, secondCopyFirstLength, array, 0, secondCopySecondLength);
                        }
                        else
                        {
                            var firstCopyFirstLength = Capacity - end;
                            var firstCopySecondLength = srLen - firstCopyFirstLength;
                            var firstCopySecondStart = right.Start + firstCopyFirstLength;

                            Array.Copy(right.array, right.Start, array, end, firstCopyFirstLength);
                            Array.Copy(right.array, firstCopySecondStart, array, 0, firstCopySecondLength);

                            Array.Copy(right.array, 0, array, firstCopySecondLength, slLen);
                        }
                    }
                }
            }

            Count += right.Count; // correct array length.
        }

        #endregion
        
        #region Enumeration

        /// <summary>
        /// gets an struct enumerator for this array.
        /// </summary>
        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// struct enumerator for <see cref="RingArray{T}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<T>
        {
            readonly RingArray<T> array;
            int position;

            /// <summary>
            /// initializes a new instance of <see cref="Enumerator"/>.
            /// </summary>
            public Enumerator(RingArray<T> array)
            {
                this.array = array;
                position = -1;
            }

            /// <inheritdoc />
            public T Current => array[position];

            /// <inheritdoc />
            public bool MoveNext()
            {
                if (array == null) return false;
                if (position + 1 == array.Count) return false;
                position++;
                return true;
            }

            /// <inheritdoc />
            public void Reset()
            {
                position = -1;
            }

            object IEnumerator.Current => Current;

            /// <inheritdoc />
            public void Dispose() => Reset();
        }

        #endregion
    }
}
