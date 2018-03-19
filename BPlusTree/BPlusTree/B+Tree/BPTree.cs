using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace BPlusTree
{
    /// <summary>
    /// represents an efficient dynamic B+ tree with builder support.
    /// </summary>
    [DebuggerDisplay("Count = {Count}")]
    public partial class BPTree<TKey, TValue> : IBPTree<TKey, TValue>
    {
        #region Properties

        /// <summary>
        /// maximum number of keys in internal node.
        /// </summary>
        public int InternalNodeCapacity { get; }

        /// <summary>
        /// maximum number of keys in leaf node.
        /// </summary>
        public int LeafCapacity { get; }

        /// <summary>
        /// current height of this tree.
        /// </summary>
        public int Height { get; private set; }

        /// <summary>
        /// number of items in this tree.
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// key comparer used in this tree to compare keys against each other.
        /// </summary>
        public IComparer<TKey> KeyComparer => _comparer.KeyComparer;

        #endregion

        #region Fields

        private readonly NodeComparer _comparer;
        private Node Root;
        private LeafNode LinkList; // pointer to first leaf in tree.
        private int _version;

        #endregion

        #region Constructors

        /// <summary>
        /// initializes a new <see cref="BPTree{TKey, TValue}"/>.
        /// </summary>
        public BPTree(IComparer<TKey> keyComparer = null, int internalNodeCapacity = 32, int leafCapacity = 32)
        { 
            if (internalNodeCapacity < 2)
                throw new ArgumentOutOfRangeException(nameof(internalNodeCapacity), "internal node capacity must be greater than 1.");
            if (leafCapacity < 1)
                throw new ArgumentOutOfRangeException(nameof(leafCapacity), "leaf capacity must be greater than 0.");
            
            _comparer = new NodeComparer(keyComparer);

            InternalNodeCapacity = internalNodeCapacity;
            LeafCapacity = leafCapacity;
        }

        /// <summary>
        /// initializes a new <see cref="BPTree{TKey, TValue}"/>.
        /// </summary>
        public BPTree(IEnumerable<(TKey key, TValue value)> source, IComparer<TKey> keyComparer = null, int internalNodeCapacity = 32, int leafCapacity = 32) 
            : this(keyComparer, internalNodeCapacity, leafCapacity)
        {
            new Builder(this, source).Build();
        }

        #endregion

        #region Add

        /// <summary>
        /// adds an specified key and value to the tree.
        /// </summary>
        /// <exception cref="InvalidOperationException">item with same key already exist.</exception>
        public void Add(TKey key, TValue value)
        {
            Add(key, value, _ => throw new InvalidOperationException("item with same key already exist."));
        }

        /// <summary>
        /// adds an specified key and value to the tree, in case key is duplicate, update function is used to update existing value.
        /// </summary>
        public void Add(TKey key, TValue value, Func<(TKey key, TValue newValue, TValue oldValue), TValue> updateFunction)
        {
            Add(key, value, t => t.arg, updateFunction);
        }

        /// <summary>
        /// adds an specified key and value to the tree. in case key is duplicate, update function is used to update existing value. 
        /// in case key is not duplicate, add function is used with optional argument to produce a value.
        /// </summary>
        public void Add<TArg>(TKey key, TArg arg, Func<(TKey key, TArg arg), TValue> addFunction, Func<(TKey key, TArg arg, TValue oldValue), TValue> updateFunction)
        {
            AddOrUpdateCore(new InsertArguments<TArg>(key, arg, addFunction, updateFunction, _comparer));
        }

        /// <summary>
        /// core method used to add an item to the tree using <see cref="InsertArguments{TArg}"/>.
        /// </summary>
        private void AddOrUpdateCore<TArg>(in InsertArguments<TArg> args)
        {
            if (Root == null)
            {
                Root = new LeafNode(LeafCapacity); // first root is leaf
                LinkList = (LeafNode)Root;
                Height++;
            }

            var rightSplit = Root.Insert(args, new NodeRelatives());
            Count++;
            _version++;

            // if split occured at root, make a new root and increase height.
            if (rightSplit is KeyNodeItem middle)
            {
                var newRoot = new InternalNode(InternalNodeCapacity) { Left = Root };
                newRoot.Items.Insert(0, middle);
                Root = newRoot;
                Height++;
            }
        }

        #endregion

        #region Remove

        /// <summary>
        /// removes the value with specified key from the tree. returns true if the value was removed.
        /// </summary>
        public bool Remove(TKey key, out TValue value)
        {
            value = default;
            if (Root == null) return false;

            var args = new RemoveArguments(key, _comparer);
            var merge = Root.Remove(ref args, new NodeRelatives());
            if (args.Removed)
            {
                Count--;
                _version++;
            }

            if (merge && Root.Length == 0)
            {
                Root = Root.GetChild(-1); // left most child becomes root. (returns null for leafs)
                if (Root == null) LinkList = null;
                Height--;
            }

            value = args.Value;
            return args.Removed;
        }

        #endregion

        #region Clear

        /// <summary>
        /// removes all elements from the tree.
        /// </summary>
        public void Clear()
        {
            _version++;
            Count = 0;
            Height = 0;
            if (Root != null)
            {
                Root = LinkList; // first root is leaf
                LinkList.Items.Clear();
                LinkList.Next = null;
            }
        }

        #endregion

        #region Indexer

        /// <summary>
        /// retrieves the value associated with the specified key.
        /// </summary>
        /// <exception cref="KeyNotFoundException"></exception>
        public TValue this[TKey key]
        {
            get
            {
                if (!TryGet(key, out var value)) throw new KeyNotFoundException();
                return value;
            }
        }

        /// <summary>
        /// retrieves the value associated with the specified key.
        /// </summary>
        public bool TryGet(TKey key, out TValue value)
        {
            value = default(TValue);
            var leaf = FindLeaf(key, out var index);
            if (index >= 0) value = leaf.Items[index].Value;
            return index >= 0;
        }

        /// <summary>
        /// find the leaf and index of the key. if key is not found complement of the index is returned.
        /// </summary>
        private LeafNode FindLeaf(TKey key, out int index)
        {
            index = -1;
            if (Root == null) return null;

            var node = Root;
            while (!node.IsLeaf) node = node.GetNearestChild(key, _comparer);
            index = node.Find(key, _comparer);
            return (LeafNode)node;
        }

        /// <summary>
        /// retrieves the value associated with nearest key to the specified key.
        /// </summary>
        public TValue NextNearest(TKey key)
        {
            var leaf = FindLeaf(key, out var leafIndex);
            if (leafIndex < 0) leafIndex = ~leafIndex; // get nearest
            if (leafIndex >= leaf.Items.Count) leafIndex--;
            return leaf.Items[leafIndex].Value;
        }

        /// <summary>
        /// retrieves first item from the tree.
        /// </summary>
        /// <exception cref="InvalidOperationException">collection is empty.</exception>
        public (TKey Key, TValue Value) First
        {
            get
            {
                if (LinkList == null) throw new InvalidOperationException("collection is empty.");
                var firstItem = LinkList.GetFirstItem();
                return (firstItem.Key, firstItem.Value);
            }
        }

        /// <summary>
        /// retrieves last item from the tree.
        /// </summary>
        /// <exception cref="InvalidOperationException">collection is empty.</exception>
        public (TKey Key, TValue Value) Last
        {
            get
            {
                if (Root == null) throw new InvalidOperationException("collection is empty.");
                var node = Root;
                while (!node.IsLeaf) node = ((InternalNode)node).GetLastChild();
                var lastItem = ((LeafNode)node).GetLastItem();
                return (lastItem.Key, lastItem.Value);
            }
        }

        #endregion

        #region AsEnumerable

        /// <summary>
        /// returns an enumerable for this tree.
        /// </summary>
        public IEnumerable<(TKey Key, TValue Value)> AsEnumerable()
        {
            return GetEnumerable(LinkList, 0);
        }

        /// <summary>
        /// returns an enumerable for this tree.
        /// </summary>
        /// <typeparam name="TCast">target type to cast items while enumerating</typeparam>
        /// <param name="filter">if true is passed, filters the sequence otherwise casts the sequence values.</param>
        public IEnumerable<(TKey Key, TCast Value)> AsEnumerable<TCast>(bool filter = true)
        {
            var enumerable = AsEnumerable();
            if (filter) enumerable = enumerable.Where(x => x.Value is TCast);
            return enumerable.Select(x => (x.Key, (TCast)(object)x.Value));
        }

        /// <summary>
        /// returns an enumerable for this tree.
        /// </summary>
        /// <param name="start">start of enumerable.</param>
        public IEnumerable<(TKey Key, TValue Value)> AsEnumerable(TKey start)
        {
            var leaf = FindLeaf(start, out var index);
            if (index < 0) index = ~index;
            return GetEnumerable(leaf, index);
        }

        /// <summary>
        /// returns an enumerable for this tree.
        /// </summary>
        /// <typeparam name="TCast">target type to cast items while enumerating</typeparam>
        /// <param name="start">start of enumerable.</param>
        /// <param name="filter">if true is passed, filters the sequence otherwise casts the sequence values.</param>
        public IEnumerable<(TKey Key, TCast Value)> AsEnumerable<TCast>(TKey start, bool filter = true)
        {
            var enumerable = AsEnumerable(start);
            if (filter) enumerable = enumerable.Where(x => x.Value is TCast);
            return enumerable.Select(x => (x.Key, (TCast)(object)x.Value));
        }

        private IEnumerable<(TKey Key, TValue Value)> GetEnumerable(LeafNode leaf, int index)
        {
            var version = _version;
            while (leaf != null)
            {
                for (; index < leaf.Items.Count; index++)
                {
                    var item = leaf.Items[index];
                    yield return (item.Key, item.Value);
                    if (version != _version) throw new InvalidOperationException("collection was modified.");
                }
                leaf = leaf.Next; index = 0;
            }
        }

        #endregion
    }
}
