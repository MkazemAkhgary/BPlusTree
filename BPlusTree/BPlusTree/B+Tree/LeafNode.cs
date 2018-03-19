using System.Diagnostics;
using System.Linq;

namespace BPlusTree
{
    public partial class BPTree<TKey, TValue>
    {
        [DebuggerDisplay("{ToString()}")]
        private sealed class LeafNode : Node
        {
            public readonly RingArray<KeyValueItem> Items;

            public LeafNode Next; // leaf node siblings are linked to gether to make linked list.
            
            public override string ToString()
            {
                return $"[{string.Join(", ", Items.Select(x => x.Key))}]";
            }
            
            #region Constructors

            public LeafNode(RingArray<KeyValueItem> items)
            {
                Items = items;
            }

            public LeafNode(int capacity)
            {
                Items = new RingArray<KeyValueItem>(capacity);
            }

            #endregion

            #region Properties

            public override bool IsLeaf => true;
            public override bool IsFull => Items.IsFull;
            public override bool IsHalfFull => Items.IsHalfFull;
            public override int Length => Items.Count;
            public override TKey FirstKey => Items.First.Key;

            #endregion

            #region Find/Traverse

            public override int Find(in TKey key, in NodeComparer comparer)
            {
                return Items.BinarySearch(new KeyValueItem(key, default), comparer); // find value in this bucket
            }

            public override Node GetChild(int index) => null;
            public override Node GetNearestChild(TKey key, NodeComparer comparer) => null;

            public KeyValueItem GetLastItem() => Items.Last;
            public KeyValueItem GetFirstItem() => Items.First;

            #endregion

            #region Insert

            public override KeyNodeItem? Insert<TArg>(in InsertArguments<TArg> args, in NodeRelatives relatives)
            {
                KeyNodeItem? rightLeaf = null;

                var index = Find(args.Key, args.Comparer);

                if (index < 0)
                {
                    index = ~index;

                    Debug.Assert(index >= 0 && index <= Items.Count);

                    var item = new KeyValueItem(args.Key, args.GetValue()); // item to add

                    if (!IsFull) // if there is space, add and return.
                    {
                        Items.Insert(index, item); // insert value and return.
                    }
                    else // cant add, spill or split
                    {
                        if (CanSpillTo(relatives.LeftSibling, out var leftSibling))
                        {
                            var first = Items.InsertPopFirst(index, item);
                            leftSibling.Items.PushLast(first); // move smallest item to left sibling.

                            // update ancestors key.
                            var pl = relatives.LeftAncestor.Items[relatives.LeftAncestorIndex];
                            pl.Key = Items.First.Key;
                            relatives.LeftAncestor.Items[relatives.LeftAncestorIndex] = pl;
                        }
                        else if (CanSpillTo(relatives.RightSibling, out var rightSibling))
                        {
                            var last = Items.InsertPopLast(index, item);
                            rightSibling.Items.PushFirst(last);

                            // update ancestors key.
                            var pr = relatives.RightAncestor.Items[relatives.RightAncestorIndex];
                            pr.Key = last.Key;
                            relatives.RightAncestor.Items[relatives.RightAncestorIndex] = pr;
                        }
                        else // split, then promote middle item
                        {
                            var rightNode = SplitRight();

                            // insert item and find middle value to promote
                            if (index <= Items.Count)
                            {
                                // when adding item to this node, pop last item and give it to right node.
                                // this way, this and right split always have equal length or maximum 1 difference. (also avoids overflow when capacity = 1)
                                rightNode.Items.PushFirst(Items.InsertPopLast(index, item));
                            }
                            else if (index > Items.Count)
                            {
                                rightNode.Items.Insert(index - Items.Count, item);
                            }

                            rightLeaf = new KeyNodeItem(rightNode.Items.First.Key, rightNode);

                            Debug.Assert(IsHalfFull);
                            Debug.Assert(rightNode.IsHalfFull);
                        }
                    }

                    // splits right side to new node and keeps left side for current node.
                    LeafNode SplitRight()
                    {
                        var right = new LeafNode(Items.SplitRight());
                        right.Next = Next; // to make linked list.
                        Next = right;
                        return right;
                    }

                    bool CanSpillTo(Node node, out LeafNode leaf)
                    {
                        leaf = (LeafNode)node;
                        return leaf?.IsFull == false;
                    }
                }
                else
                {
                    var item = Items[index]; // old item
                    args.UpdateValue(ref item.Value); // update item value
                    Items[index] = item; // set new item
                }

                return rightLeaf;
            }

            #endregion

            #region Remove

            public override bool Remove(ref RemoveArguments args, in NodeRelatives relatives)
            {
                var merge = false;
                var index = Find(args.Key, args.Comparer);

                if (index >= 0)
                {
                    Debug.Assert(index >= 0 && index <= Items.Count);
                    
                    args.SetRemovedValue(Items.RemoveAt(index).Value); // remove item

                    if (!IsHalfFull) // borrow or merge
                    {
                        if (CanBorrowFrom(relatives.LeftSibling, out var leftSibling))
                        {
                            var last = leftSibling.Items.PopLast();
                            Items.PushFirst(last);

                            var p = relatives.LeftAncestor.Items[relatives.LeftAncestorIndex];
                            p.Key = last.Key;
                            relatives.LeftAncestor.Items[relatives.LeftAncestorIndex] = p;
                        }
                        else if (CanBorrowFrom(relatives.RightSibling, out var rightSibling))
                        {
                            var first = rightSibling.Items.PopFirst();
                            Items.PushLast(first);

                            var p = relatives.RightAncestor.Items[relatives.RightAncestorIndex];
                            p.Key = rightSibling.Items.First.Key;
                            relatives.RightAncestor.Items[relatives.RightAncestorIndex] = p;
                        }
                        else // merge with either sibling.
                        {
                            merge = true; // set merge falg
                            if (relatives.HasTrueLeftSibling)
                            {
                                leftSibling.Items.MergeLeft(Items); // merge from left to keep items in order. (current node will be removed from parent)
                                leftSibling.Next = rightSibling; // fix linked list
                            }
                            else if (relatives.HasTrueRightSibling)
                            {
                                Items.MergeLeft(rightSibling.Items); // merge from right to keep items in order. (right sibling will be removed from parent)
                                Next = rightSibling.Next;
                            }
                        }
                    }

                    bool CanBorrowFrom(Node node, out LeafNode leaf)
                    {
                        leaf = (LeafNode)node;
                        if (leaf == null) return false;
                        return leaf.Items.Count > leaf.Items.Capacity / 2;
                    }
                }

                return merge; // true if merge happened.
            }

            #endregion
        }
    }
}
