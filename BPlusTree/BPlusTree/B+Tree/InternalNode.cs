using System;
using System.Diagnostics;
using System.Linq;

namespace BPlusTree
{
    public partial class BPTree<TKey, TValue>
    {
        [DebuggerDisplay("{ToString()}")]
        private sealed class InternalNode : Node
        {
            public readonly RingArray<KeyNodeItem> Items;

            public Node Left; // left most child. 

            public override string ToString()
            {
                return $"{{{string.Join(", ",Items.Select(x => x.Key))}}}/{{{Left}, {string.Join(", ", Items.Select(x => x.Right))}}}";
            }

            #region Constructors

            public InternalNode(RingArray<KeyNodeItem> items)
            {
                Items = items;
            }

            public InternalNode(int capacity)
            {
                Items = new RingArray<KeyNodeItem>(capacity);
            }

            #endregion

            #region Properties

            public override bool IsLeaf => false;
            public override bool IsFull => Items.IsFull;
            public override bool IsHalfFull => Items.IsHalfFull;
            public override int Length => Items.Count;
            public override TKey FirstKey => Items.First.Key;

            #endregion

            #region Find/Traverse

            public override int Find(in TKey key, in NodeComparer comparer)
            {
                return Items.BinarySearch(new KeyNodeItem(key, null), comparer);
            }

            public override Node GetChild(int index)
            {
                return index < 0 ? Left : Items[index].Right;
            }

            public override Node GetNearestChild(TKey key, NodeComparer comparer)
            {
                var index = Find(key, comparer);
                if (index < 0) index = ~index - 1; // get next nearest item.
                return GetChild(index);
            }

            public Node GetLastChild() => Items.Last.Right;
            public Node GetFirstChild() => Left;

            #endregion

            #region Insert

            public override KeyNodeItem? Insert<TArg>(in InsertArguments<TArg> args, in NodeRelatives relatives)
            {
                var index = Find(args.Key, args.Comparer);

                // -1 because items smaller than key have to go inside left child. 
                // since items at each index point to right child, index is decremented to get left child.
                if (index < 0) index = ~index - 1;

                Debug.Assert(index >= -1 && index < Items.Count);

                // get child to traverse through.
                var child = GetChild(index);
                var childRelatives = NodeRelatives.Create(child, index, this, relatives);
                var rightChild = child.Insert(args, childRelatives);

                if (rightChild is KeyNodeItem middle) // if splitted, add middle key to this node.
                {
                    // +1 because middle is always right side which is fresh node. 
                    // items at index already point to left node after split. so middle must go after index.
                    index++;

                    rightChild = null;
                    if (!IsFull)
                    {
                        Items.Insert(index, middle);
                    }
                    else
                    {
                        // if left sbiling has spacem, spill left child of this item to left sibling.
                        if (CanSpillTo(relatives.LeftSibling, out var leftSibling))
                        {
                            #region Fix Pointers after share
                            // give first item to left sibling.
                            //
                            //        [x][x]       [F][x]
                            //       /   \  \     // \\\ \ 
                            //
                            //        [x][x][F]       [x]
                            //       /   \  \ \\     /// \    
                            #endregion

                            var first = Items.InsertPopFirst(index, middle);

                            Swap(ref Left, ref first.Right); // swap left and right pointers.

                            var pl = relatives.LeftAncestor.Items[relatives.LeftAncestorIndex];
                            Swap(ref pl.Key, ref first.Key); // swap ancestor key with item.
                            relatives.LeftAncestor.Items[relatives.LeftAncestorIndex] = pl;

                            leftSibling.Items.PushLast(first);
                        }
                        else if (CanSpillTo(relatives.RightSibling, out var rightSibling)) // if right sibling has space
                        {
                            #region Fix Pointers after share
                            // give last item to right sibling.
                            //
                            //        [x][L]       [x][x]
                            //       /   \ \\     /// \  \
                            //
                            //        [x]        [L][x][x]
                            //       /   \      // \\\ \  \
                            #endregion

                            var last = Items.InsertPopLast(index, middle);

                            Swap(ref rightSibling.Left, ref last.Right); // swap left and right pointers.

                            var pr = relatives.RightAncestor.Items[relatives.RightAncestorIndex];
                            Swap(ref pr.Key, ref last.Key); // swap ancestor key with item.
                            relatives.RightAncestor.Items[relatives.RightAncestorIndex] = pr;

                            rightSibling.Items.PushFirst(last);
                        }
                        else // split, then promote middle item
                        {
                            #region Fix Pointers after split
                            // ==============================================================
                            //
                            // if [left] and [right] were leafs
                            //
                            //     [][]...[N]...[][]
                            //               \   <= if we were here,
                            //             [left][mid][right]
                            //
                            // for insertion, make new key-node item with [mid] as key and [right] as node.
                            // simply add this item next to [N].
                            //
                            //     [][]...[N][mid]..[][]
                            //               \    \
                            //            [left][right]
                            //
                            // ==============================================================
                            //
                            // if [left] and [right] were internal nodes.
                            //
                            //     [middle]        [rightNode]       
                            //            \\       *         \     <= left pointer of [rightNode] is null 
                            //
                            //  Becomes
                            //
                            //    [middle]
                            //           \
                            //         [rightNode]
                            //        //          \
                            //
                            // ==============================================================
                            #endregion

                            var rightNode = new InternalNode(Items.SplitRight());

                            // find middle key to promote
                            if (index < Items.Count)
                            {
                                middle = Items.InsertPopLast(index, middle);
                            }
                            else if (index > Items.Count)
                            {
                                middle = rightNode.Items.InsertPopFirst(index - Items.Count, middle);
                            }

                            rightNode.Left = middle.Right;
                            middle.Right = rightNode;
                            rightChild = middle;

                            Debug.Assert(IsHalfFull);
                            Debug.Assert(rightNode.IsHalfFull);
                            Debug.Assert(rightNode.Left != null);
                        }
                    }

                    bool CanSpillTo(Node node, out InternalNode inode)
                    {
                        inode = (InternalNode)node;
                        return inode?.IsFull == false;
                    }

                    void Swap<T>(ref T t1, ref T t2)
                    {
                        var temp = t1;
                        t1 = t2;
                        t2 = temp;
                    }
                }

                return rightChild;
            }

            #endregion

            #region Remove

            public override bool Remove(ref RemoveArguments args, in NodeRelatives relatives)
            {
                var merge = false;
                var index = Find(args.Key, args.Comparer);
                if (index < 0) index = ~index - 1;

                Debug.Assert(index >= -1 && index < Items.Count);

                var child = GetChild(index);
                var childRelatives = NodeRelatives.Create(child, index, this, relatives);
                var childMerged = child.Remove(ref args, childRelatives);

                if (childMerged)
                {
                    Items.RemoveAt(Math.Max(0, index)); // removes right sibling of child if left most child is merged, other wise merged child is removed.

                    if (!Items.IsHalfFull) // borrow or merge
                    {
                        if (CanBorrowFrom(relatives.LeftSibling, out InternalNode leftSibling))
                        {
                            var last = leftSibling.Items.PopLast();

                            Swap(ref Left, ref last.Right); // swap left and right pointers.

                            var pr = relatives.LeftAncestor.Items[relatives.LeftAncestorIndex];
                            Swap(ref pr.Key, ref last.Key); // swap ancestor key with item.
                            relatives.LeftAncestor.Items[relatives.LeftAncestorIndex] = pr;

                            Items.PushFirst(last);
                        }
                        else if (CanBorrowFrom(relatives.RightSibling, out InternalNode rightSibling))
                        {
                            var first = rightSibling.Items.PopFirst();

                            Swap(ref rightSibling.Left, ref first.Right); // swap left and right pointers.

                            var pl = relatives.RightAncestor.Items[relatives.RightAncestorIndex];
                            Swap(ref pl.Key, ref first.Key); // swap ancestor key with item.
                            relatives.RightAncestor.Items[relatives.RightAncestorIndex] = pl;

                            Items.PushLast(first);
                        }
                        else // merge
                        {
                            merge = true;
                            if (relatives.HasTrueLeftSibling)
                            {
                                var pkey = relatives.LeftAncestor.Items[relatives.LeftAncestorIndex].Key; // demote key
                                var mid = new KeyNodeItem(pkey, Left); 
                                leftSibling.Items.PushLast(mid);
                                leftSibling.Items.MergeLeft(Items); // merge from left to keep items in order. (current node will be removed from parent)
                            }
                            else if (relatives.HasTrueRightSibling)
                            {
                                var pkey = relatives.RightAncestor.Items[relatives.RightAncestorIndex].Key; // demote key
                                var mid = new KeyNodeItem(pkey, rightSibling.Left);
                                Items.PushLast(mid);
                                Items.MergeLeft(rightSibling.Items); // merge from right to keep items in order. (right sibling will be removed from parent)
                            }
                        }
                    }

                    bool CanBorrowFrom(Node node, out InternalNode inode)
                    {
                        inode = (InternalNode)node;
                        if (inode == null) return false;
                        return inode.Items.Count > inode.Items.Capacity / 2;
                    }

                    void Swap<T>(ref T t1, ref T t2)
                    {
                        var temp = t1;
                        t1 = t2;
                        t2 = temp;
                    }
                }

                return merge; // true if merge happened.
            }

            #endregion
        }
    }
}
