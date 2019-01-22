using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace BPlusTree
{
    /// <summary>
    /// circular array that supports push and pop at both ends and has better insertion/deletion than traditional list.
    /// always chooses minimum shift direction to insert or remove an item.
    /// supports split and merge and binary search.
    /// </summary>
    [DebuggerTypeProxy(typeof(RingArray<>.DebugView))]
    [DebuggerDisplay("Count = {Count}{IsRotated ? \", Rotated\" : System.String.Empty,nq}")]
    public sealed class RingArray<T> : IList<T>, IReadOnlyList<T>
    {
        private const int DefaultCapacity = 4;

        private T[] array;
        private int Start; // index of first item.
        private int version;
        
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
        bool ICollection<T>.IsReadOnly => Constraints != RingArrayConstraints.ReadOnly;

        /// <summary>
        /// number of items in this array.
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// current capacity of array. 
        /// </summary>
        public int Capacity
        {
            get
            {
                return array.Length;
            }
            private set
            {
                var newArr = new T[value];

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
        }

        /// <summary>
        /// constraints for this array.
        /// </summary>
        public RingArrayConstraints Constraints { get; }

        /// <summary>
        /// retrieves last item from the array.
        /// </summary>
        /// <exception cref="InvalidOperationException">throws exception if array is empty.</exception>
        public T Last
        {
            get => this[Count - 1];
            set => this[Count - 1] = value;
        }

        /// <summary>
        /// retrieves first item from array.
        /// </summary>
        /// <exception cref="InvalidOperationException">throws exception if array is empty.</exception>
        public T First
        {
            get => this[0];
            set => this[0] = value;
        }

        #endregion

        #region Constructors

        private RingArray(T[] array, int count, int start, RingArrayConstraints constraints)
        {
            this.array = array;
            this.Count = count;
            this.Start = start;
            this.version = 0;
            this.Constraints = constraints;
        }
        
        /// <summary>
        /// initializes a new instance of <see cref="RingArray{T}"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static RingArray<T> NewArray(int capacity = DefaultCapacity)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            return new RingArray<T>(new T[capacity], 0, 0, RingArrayConstraints.None);
        }

        public static RingArray<T> NewArray(IEnumerable<T> source, int initialCapacity = DefaultCapacity)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (initialCapacity < 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            var array = NewArray(initialCapacity);
            foreach (var x in source) array.PushLast(x);
            array.version = 0;
            return array;
        }

        public static RingArray<T> NewFixedCapacityArray(int capacity)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            return new RingArray<T>(new T[capacity], 0, 0, RingArrayConstraints.FixedCapacity);
        }

        public static RingArray<T> NewFixedCapacityArray(IEnumerable<T> source, int additionalCapacity, int initialCapacity = DefaultCapacity)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (additionalCapacity < 0) throw new ArgumentOutOfRangeException(nameof(additionalCapacity));
            if (initialCapacity < 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            var ring = NewArray(source, initialCapacity);
            if(additionalCapacity != 0) ring.Capacity = ring.Count + additionalCapacity;
            return new RingArray<T>(ring.array, ring.Count, ring.Start, RingArrayConstraints.FixedCapacity);
        }

        public static RingArray<T> NewFixedSizeArray(int size)
        {
            if (size < 0) throw new ArgumentOutOfRangeException(nameof(size));
            return new RingArray<T>(new T[size], size, 0, RingArrayConstraints.FixedSize);
        }

        public static RingArray<T> NewFixedSizeArray(IEnumerable<T> source, int initialCapacity = DefaultCapacity)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (initialCapacity < 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            var ring = NewFixedCapacityArray(source, 0, initialCapacity);
            return new RingArray<T>(ring.array, ring.Count, ring.Start, RingArrayConstraints.FixedSize);
        }

        public static RingArray<T> NewReadOnlyArray(IEnumerable<T> source, int initialCapacity = DefaultCapacity)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (initialCapacity < 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            var ring = NewFixedSizeArray(source, initialCapacity);
            return new RingArray<T>(ring.array, ring.Count, ring.Start, RingArrayConstraints.ReadOnly);
        }

        #endregion

        #region Insert/Remove

        /// <summary>
        /// inserts an item in this array with minimum shift required.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public void Insert(int index, T item)
        {
            if (Constraints == RingArrayConstraints.ReadOnly) throw new InvalidOperationException("can not modify readonly collection.");
            if (Constraints == RingArrayConstraints.FixedSize) throw new InvalidOperationException("can not insert to fixed size collection.");

            InsertInternal(index, item);
        }

        private void InsertInternal(int index, T item)
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
            version++;
        }

        /// <summary>
        /// remove item from this array with minimum shift required.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException"></exception>
        /// <exception cref="InvalidOperationException">no items to remove.</exception>
        public T RemoveAt(int index)
        {
            if (Constraints == RingArrayConstraints.ReadOnly) throw new InvalidOperationException("can not modify readonly collection.");
            if (Constraints == RingArrayConstraints.FixedSize) throw new InvalidOperationException("can not remove from fixed size collection.");

            return RemoveAtInternal(index);
        }

        private T RemoveAtInternal(int index)
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
            version++;
            return item;
        }

        /// <summary>
        /// removes an item from this array.
        /// </summary>
        public bool Remove(T item)
        {
            if (Constraints == RingArrayConstraints.ReadOnly) throw new InvalidOperationException("can not modify readonly collection.");
            if (Constraints == RingArrayConstraints.FixedSize) throw new InvalidOperationException("can not remove from fixed size collection.");
            var index = IndexOf(item);
            if (index < 0) return false;
            RemoveAtInternal(index);
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
                    if (comparer.Equals(item, array[i])) // if item is found
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
                if (comparer.Equals(item, array[i])) // if item is found
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
            if (Constraints == RingArrayConstraints.ReadOnly) throw new InvalidOperationException("can not modify readonly collection.");
            if (Constraints == RingArrayConstraints.FixedSize) throw new InvalidOperationException("can not insert to fixed size collection.");

            PushFirstInternal(item);
        }

        private void PushFirstInternal(T item)
        {
            if (Count >= Capacity) ExpandCapacity();

            DecrementStart();
            array[Start] = item;
            Count++;
            version++;
        }

        /// <summary>
        /// pushes an item to the end of this array.
        /// </summary>
        public void PushLast(T item)
        {
            if (Constraints == RingArrayConstraints.ReadOnly) throw new InvalidOperationException("can not modify readonly collection.");
            if (Constraints == RingArrayConstraints.FixedSize) throw new InvalidOperationException("can not insert to fixed size collection.");

            PushLastInternal(item);
        }

        private void PushLastInternal(T item)
        {
            if (Count >= Capacity) ExpandCapacity();

            Count++;
            Set(Count - 1, item);
            version++;
        }

        /// <summary>
        /// pops an item from start of this array. 
        /// </summary>
        /// <exception cref="InvalidOperationException">no items to remove.</exception>
        public T PopFirst()
        {
            if (Constraints == RingArrayConstraints.ReadOnly) throw new InvalidOperationException("can not modify readonly collection.");
            if (Constraints == RingArrayConstraints.FixedSize) throw new InvalidOperationException("can not remove from fixed size collection.");
            
            return PopFirstInternal();
        }

        private T PopFirstInternal()
        {
            if (Count <= 0) throw new InvalidOperationException("no items to remove.");

            var temp = array[Start];
            array[Start] = default(T);
            IncrementStart();
            Count--;
            version++;
            return temp;
        }

        /// <summary>
        /// pops an item from end of this array. 
        /// </summary>
        /// <exception cref="InvalidOperationException">no items to remove.</exception>
        public T PopLast()
        {
            if (Constraints == RingArrayConstraints.ReadOnly) throw new InvalidOperationException("can not modify readonly collection.");
            if (Constraints == RingArrayConstraints.FixedSize) throw new InvalidOperationException("can not remove from fixed size collection.");

            return PopLastInternal();
        }

        private T PopLastInternal()
        {
            if (Count <= 0) throw new InvalidOperationException("no items to remove.");

            Count--;
            var end = End;
            var temp = array[end];
            array[end] = default(T);
            version++;
            return temp;
        }

        /// <summary>
        /// inserts an item to this array and pops first item without altering length and capacity.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public T InsertPopFirst(int index, T item)
        {
            if (Constraints == RingArrayConstraints.ReadOnly) throw new InvalidOperationException("can not modify readonly collection.");
            if (OutOfRangeExclusive(index)) throw new IndexOutOfRangeException(nameof(index));
            
            if (index == 0) return item;
            var value = PopFirstInternal();
            InsertInternal(index - 1, item);
            return value;
        }

        /// <summary>
        /// inserts an item to this array and pops last item without altering length and capacity.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public T InsertPopLast(int index, T item)
        {
            if (Constraints == RingArrayConstraints.ReadOnly) throw new InvalidOperationException("can not modify readonly collection.");
            if (OutOfRangeExclusive(index)) throw new IndexOutOfRangeException(nameof(index));

            if (index == Count) return item;
            var value = PopLastInternal();
            InsertInternal(index, item);
            return value;
        }

        ///// <summary>
        ///// pushes an item to the first of this array and removes item at specified index without altering length and capacity.
        ///// </summary>
        ///// <exception cref="IndexOutOfRangeException"></exception>
        //public T PushFirstRemoveAt(int index, T item)
        //{
        //    throw new NotImplementedException();
        //}

        ///// <summary>
        ///// pushes an item to the first of this array and removes item at specified index without altering length and capacity.
        ///// </summary>
        ///// <exception cref="IndexOutOfRangeException"></exception>
        //public T PushLastRemoveAt(int index, T item)
        //{
        //    throw new NotImplementedException();
        //}

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
        /// inserts an item in order using binary search.
        /// this method does not work correctly if existing array items are not in order.
        /// </summary>
        public void InsertOrdered(T item) => InsertOrdered(item, Comparer<T>.Default);

        /// <summary>
        /// inserts an item in order using binary search.
        /// this method does not work correctly if existing array items are not in order.
        /// </summary>
        public void InsertOrdered(T item, IComparer<T> comparer)
        {
            if (Constraints == RingArrayConstraints.ReadOnly) throw new InvalidOperationException("can not modify readonly collection.");
            if (Constraints == RingArrayConstraints.FixedSize) throw new InvalidOperationException("can not insert to fixed size collection.");
            var find = BinarySearch(item, comparer);
            if (find < 0) find = ~find;
            InsertInternal(find, item);
        }

        /// <summary>
        /// removes an item using binary search. 
        /// this method is faster than <see cref="Remove(T)"/> but does not work correctly if array is not sorted.
        /// </summary>
        public bool RemoveOrdered(T item)
        {
            if (Constraints == RingArrayConstraints.ReadOnly) throw new InvalidOperationException("can not modify readonly collection.");
            if (Constraints == RingArrayConstraints.FixedSize) throw new InvalidOperationException("can not insert to fixed size collection.");
            var find = BinarySearch(item);
            if (find < 0) return false;
            RemoveAtInternal(find);
            return true;
        }

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
        /// Replace an item at specified index and return the replaced item.
        /// </summary>
        public T Replace(int index, T value)
        {
            if (Constraints == RingArrayConstraints.ReadOnly) throw new InvalidOperationException("can not modify readonly collection.");

            var replace = this[index];
            Set(index, value);
            version++;
            return replace;
        }

        /// <summary>
        /// gets or sets an item from specified index.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public T this[int index]
        {
            get
            {
                if (OutOfRange(index)) throw new IndexOutOfRangeException(nameof(index));
                return Get(index);
            }
            set
            {
                if (Constraints == RingArrayConstraints.ReadOnly) throw new InvalidOperationException("can not modify readonly collection.");
                if (OutOfRange(index)) throw new IndexOutOfRangeException(nameof(index));
                Set(index, value);
                version++;
            }
        }

        #endregion

        #region Clear/Expand/TrimCapacity
        
        /// <summary>
        /// expands the capacity of this array so it can hold more items.
        /// </summary>
        private void ExpandCapacity()
        {
            if (Constraints == RingArrayConstraints.FixedCapacity) throw new InvalidOperationException("can not expand fixed capacity collection.");
            var newCap = Capacity == 0 ? DefaultCapacity : Capacity * 2;
            Capacity = newCap;
        }

        /// <summary>
        /// Clear items from this array. Count will become zero.
        /// </summary>
        public void Clear()
        {
            Clear(true);
        }

        /// <summary>
        /// Clear items from this array. default value of resetCount is true.
        /// </summary>
        /// <param name="resetCount">if true, Count will become zero. otherwise, only items get cleared without changing the Count.</param>
        public void Clear(bool resetCount)
        {
            if (Constraints == RingArrayConstraints.ReadOnly) throw new InvalidOperationException("can not modify readonly collection.");
            if (resetCount && Constraints == RingArrayConstraints.FixedSize) throw new InvalidOperationException("can not remove from fixed size collection.");
            Array.Clear(array, 0, array.Length);
            Start = 0;
            if(resetCount) Count = 0;
            version++;
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

        /// <summary>
        /// Trim capacity, readonly or fixed size array can be trimmed but fixed capacity array can not be trimmed.
        /// </summary>
        public void TrimCapacity()
        {
            if (Constraints == RingArrayConstraints.FixedCapacity) throw new InvalidOperationException("can not trim fixed capacity collection.");
            if (Count == Capacity) return;
            Capacity = Count;
        }

        /// <summary>
        /// Unrotate array if it is rotated.
        /// </summary>
        public void UnRotate()
        {
            if (!IsRotated) return;
            Capacity = Capacity;
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
            if (Constraints == RingArrayConstraints.ReadOnly) throw new InvalidOperationException("can not modify readonly collection.");
            if (Constraints == RingArrayConstraints.FixedSize) throw new InvalidOperationException("can not split fixed size collection.");
            var right = new RingArray<T>(new T[Capacity], 0, 0, Constraints);

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

            version++;
            return right;
        }

        /// <summary>
        /// merges right array with this (i.e left) array.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public void MergeLeft(RingArray<T> right)
        {
            if (Constraints == RingArrayConstraints.ReadOnly) throw new InvalidOperationException("can not modify readonly collection.");
            if (Constraints == RingArrayConstraints.FixedSize) throw new InvalidOperationException("can not merge to fixed size collection.");
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
            version++;
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
            private RingArray<T> array;
            private int version;
            private int position; // points to next position after current.
            private T current;

            /// <summary>
            /// initializes a new instance of <see cref="Enumerator"/>.
            /// </summary>
            public Enumerator(RingArray<T> array)
            {
                this.array = array;
                version = array?.version ?? 0;
                position = 0;
                current = default;
            }

            /// <inheritdoc />
            public T Current
            {
                get
                {
                    if (array == null)
                        throw new InvalidOperationException("enumerator has no array. it's either disposed or initialized with null array.");
                    if (version != array.version)
                        throw new InvalidOperationException("collection was modified.");
                    if (position == 0)
                        throw new InvalidOperationException("enumerator cursor is not moved yet.");
                    if (position == array.Count + 1)
                        throw new InvalidOperationException("enumerator cursor has reached to the end.");
                    return current;
                }
            }

            /// <inheritdoc />
            public bool MoveNext()
            {
                if (array == null) return false;
                if (version != array.version) throw new InvalidOperationException("collection was modified.");

                if (position < array.Count)
                {
                    current = array.Get(position++);
                    return true;
                }
                else
                {
                    position = array.Count + 1; // end marker
                    current = default;
                    return false;
                }
            }

            /// <inheritdoc />
            public void Reset()
            {
                version = array?.version ?? 0;
                position = 0;
                current = default;
            }

            object IEnumerator.Current => Current;

            /// <inheritdoc />
            public void Dispose()
            {
                array = null;
                Reset();
            }
        }

        #endregion

        #region Debug View

        [DebuggerNonUserCode]
        private sealed class DebugView
        {
            private readonly RingArray<T> _array;

            private DebugView(RingArray<T> array)
            {
                _array = array ?? throw new ArgumentNullException(nameof(array));
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public T[] Items => _array.ToArray(); 
        }

        #endregion
    }
}
