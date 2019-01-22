using System;
using System.Collections.Generic;
using System.Linq;

namespace BPlusTree
{
    public partial class BPTree<TKey, TValue>
    {
        /// <summary>
        /// Builder for B+ tree. supports bulk loading and faster insertions before building the tree.
        /// </summary>
        /// <remarks>
        /// This class is used to lazily build the tree. if initial tree is empty or null, bulk loading approach is used to insert items.
        /// as long as items are in order, no tree is built until <see cref="Build"/> is called.
        /// if an item without order is inserted, builder automatically builds the tree and switchs to iterative insertion mode.
        /// removing an item also causes builder to switch from bulkloading to iterative insertion.
        /// tree is built only once regardless of number of calls to <see cref="Build"/>.
        /// if initial tree is not empty bulk loading is not used (although we could retrieve LinkedList and use bulkloading on it).  
        /// </remarks>
        public sealed class Builder
        {
            #region Fields

            // contains nodes at any level. each level is seperated by null reference.
            private RingArray<Node> nodes;

            // when true, tree is not built yet and builder is on bulk loading mode.
            // when false, tree is built (!= null) and  builder is on iterative insertion mode.
            private bool _bulkLoading;

            // when true, no item is added yet.
            // when false, builder contains atleast one item.
            private bool _initialize;

            // previous key is used to check if items are being inserted in order or not.
            private TKey _prevKey;
            private LeafNode _currentLeaf;
            
            private LeafNode _linkList; // reference to first leaf (i.e linked list)
            private Node _root;
            private int _count; // count of items during bulk load.
            private int _height; // final height after building tree.

            BPTree<TKey, TValue> _tree; // build result. 

            #endregion

            #region Properties

            public int InternalNodeCapacity { get; }

            public int LeafCapacity { get; }
            
            public IComparer<TKey> KeyComparer { get; }

            #endregion

            #region Constructors

            public Builder(IComparer<TKey> keyComparer = null, int internalNodeCapacity = 32, int leafCapacity = 32)
            {
                InternalNodeCapacity = internalNodeCapacity;
                LeafCapacity = leafCapacity;
                _bulkLoading = _initialize = true;
                KeyComparer = keyComparer ?? Comparer<TKey>.Default;
            }
            
            public Builder(BPTree<TKey, TValue> tree) : this(tree.KeyComparer, tree.InternalNodeCapacity, tree.LeafCapacity)
            {
                _tree = tree;
                if (tree?.Count > 0) _bulkLoading = _initialize = false; // if tree is already initialized bulkloading is not supported
            }

            public Builder(BPTree<TKey, TValue> tree, IEnumerable<(TKey key, TValue value)> source) : this(tree)
            {
                if (source == null) throw new ArgumentNullException(nameof(source));

                foreach (var (key, value) in source) Add(key, value);
            }

            public Builder(IEnumerable<(TKey key, TValue value)> source, IComparer<TKey> keyComparer = null, int internalNodeCapacity = 32, int leafCapacity = 32)
                : this(new BPTree<TKey, TValue>(keyComparer, internalNodeCapacity, leafCapacity), source)
            {
            }

            #endregion

            #region Add

            public void Add(TKey key, TValue value)
            {
                Add(key, value, _ => throw new InvalidOperationException("item with same key already exist."));
            }

            public void Add(TKey key, TValue value, Func<(TKey key, TValue newValue, TValue oldValue), TValue> updateFunction)
            {
                Add(key, value, t => t.arg, updateFunction);
            }

            public void Add<TArg>(TKey key, TArg arg, Func<(TKey key, TArg arg), TValue> addFunction, Func<(TKey key, TArg arg, TValue oldValue), TValue> updateFunction)
            {
                if (_initialize)
                {
                    var item = new KeyValueItem(key, addFunction((key, arg)));
                    _currentLeaf = NewLeafNode(item);
                    _linkList = _currentLeaf; // initialize linked list
                    nodes = RingArray<Node>.NewArray(Enumerable.Repeat(_currentLeaf, 1), 32);
                    _height = _count = 1;
                    _initialize = false;
                }
                else if (!_bulkLoading)
                {
                    _tree.AddOrUpdateFromArg(key, arg, addFunction, updateFunction);
                }
                else
                {
                    // check if insertions are in order so we can continue bulk loading
                    var c = KeyComparer.Compare(key, _prevKey); 
                    if (c == 0)
                    {
                        var lastItem = _currentLeaf.Items.Last;
                        KeyValueItem.ChangeValue(ref lastItem, updateFunction((key, arg, lastItem.Value)));
                        _currentLeaf.Items.Last = lastItem;
                    }
                    else if (c < 0) // insertions are not in order
                    {
                        // switch to iterative insertion mode.
                        Build().AddOrUpdateFromArg(key, arg, addFunction, updateFunction);
                    }
                    else
                    {
                        var item = new KeyValueItem(key, addFunction((key, arg)));

                        if (!_currentLeaf.IsFull) // current leaf has space for new items
                        {
                            _currentLeaf.Items.PushLast(item);
                        }
                        else // initialize next node
                        {
                            var newLeaf = NewLeafNode(item);
                            _currentLeaf.Next = newLeaf; // build linked list
                            newLeaf.Previous = _currentLeaf;
                            _currentLeaf = newLeaf;
                            nodes.PushLast(_currentLeaf);
                        }

                        _count++; // new item added during bulk load
                    }
                }

                _prevKey = key;

                LeafNode NewLeafNode(KeyValueItem firstItem)
                {
                    var newLeaf = new LeafNode(LeafCapacity);
                    newLeaf.Items.Add(firstItem);
                    return newLeaf;
                }
            }

            #endregion

            #region Remove
            
            public bool Remove(TKey key, out TValue value)
            {
                return Build().Remove(key, out value); // switch to iterative insertion mode.
            } 

            #endregion

            #region Build

            public BPTree<TKey, TValue> Build()
            {
                if (!_bulkLoading) return _tree; // return tree if its already built.

                if(_tree == null)
                    _tree = new BPTree<TKey, TValue>(KeyComparer, InternalNodeCapacity, LeafCapacity);

                if (!_initialize) // if tree contains at least one item.
                {
                    if (nodes.Count == 1) // if root is the only existing node.
                    {
                        _root = _currentLeaf;
                    }
                    else
                    {
                        var leftLeaf = (LeafNode)nodes[nodes.Count - 2];
                        while (!_currentLeaf.IsHalfFull) // while last leaf is not half full
                        {
                            _currentLeaf.Items.PushFirst(leftLeaf.Items.PopLast()); // borrow from its left
                        }

                        // build top levels from bottom up to root.
                        var internalNode = IncreaseHeight();

                        // loop until one node is left. ("count > 2" because an extra null item indicates end of each level)
                        while (nodes.Count > 2)
                        {
                            var node = nodes.PopFirst();

                            if (node == null) // end of level
                            {
                                var leftNode = (InternalNode)nodes[nodes.Count - 2];

                                while (!internalNode.IsHalfFull) // while last node is not half full borrow from its left
                                {
                                    var last = leftNode.Items.PopLast(); // should become left of current node.
                                    var left = internalNode.Left; // should become first item of current node.
                                    var item = new KeyNodeItem(left.FirstKey, left);
                                    internalNode.Items.PushFirst(item);
                                    internalNode.Left = last.Right;
                                }

                                internalNode = IncreaseHeight();
                            }
                            else if (internalNode.Left == null)
                            {
                                internalNode.Left = node;
                            }
                            else if (!internalNode.IsFull) // internal node has space for items
                            {
                                var item = new KeyNodeItem(node.FirstKey, node);
                                internalNode.Items.PushLast(item);
                            }
                            else // initialize next node
                            {
                                internalNode = NewInternalNode();
                                internalNode.Left = node;
                            }
                        }

                        _root = nodes.PopLast();

                        InternalNode IncreaseHeight()
                        {
                            _height++;
                            nodes.PushLast(null); // null indicates end of each level.
                            return NewInternalNode(); // first node for new level.
                        }

                        InternalNode NewInternalNode()
                        {
                            var newNode = new InternalNode(InternalNodeCapacity);
                            nodes.PushLast(newNode);
                            return newNode;
                        }
                    }
                }

                var lastLeaf = _root;
                if (lastLeaf != null)
                {
                    while (!lastLeaf.IsLeaf) // find last leaf
                    {
                        lastLeaf = ((InternalNode)lastLeaf).GetLastChild();
                    }
                }

                _tree.Root = _root;
                _tree.LinkList = _linkList;
                _tree.ReverseLinkList = (LeafNode)lastLeaf;
                _tree.Count = _count;
                _tree.Height = _height;
                _bulkLoading = false; // tree is built.
                return _tree;
            }

            #endregion
        }
    }
}
