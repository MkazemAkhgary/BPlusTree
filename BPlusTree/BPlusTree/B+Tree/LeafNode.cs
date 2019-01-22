using System.Diagnostics;

namespace BPlusTree
{
    public partial class BPTree<TKey, TValue>
    {
        private sealed partial class LeafNode : Node
        {
            public readonly RingArray<KeyValueItem> Items;

            // leaf node siblings are linked to gether to make doubly linked list.
            public LeafNode Previous;
            public LeafNode Next;

            #region Constructors

            public LeafNode(RingArray<KeyValueItem> items)
            {
                Items = items;
            }

            public LeafNode(int capacity)
            {
                Items = RingArray<KeyValueItem>.NewFixedCapacityArray(capacity);
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

            #endregion

            #region Insert

            public override KeyNodeItem? Insert<TArg>(ref InsertArguments<TArg> args, in NodeRelatives relatives)
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
                        if (CanSpillTo(Previous))
                        {
                            var first = Items.InsertPopFirst(index, item);
                            Previous.Items.PushLast(first); // move smallest item to left sibling.

                            // update ancestors key.
                            var pl = relatives.LeftAncestor.Items[relatives.LeftAncestorIndex];
                            KeyNodeItem.ChangeKey(ref pl, Items.First.Key);
                            relatives.LeftAncestor.Items[relatives.LeftAncestorIndex] = pl;

                            Validate(this);
                            Validate(Previous);
                        }
                        else if (CanSpillTo(Next))
                        {
                            var last = Items.InsertPopLast(index, item);
                            Next.Items.PushFirst(last);

                            // update ancestors key.
                            var pr = relatives.RightAncestor.Items[relatives.RightAncestorIndex];
                            KeyNodeItem.ChangeKey(ref pr, last.Key);
                            relatives.RightAncestor.Items[relatives.RightAncestorIndex] = pr;

                            Validate(this);
                            Validate(Next);
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

                            Validate(this);
                            Validate(rightNode);
                        }
                    }

                    // splits right side to new node and keeps left side for current node.
                    LeafNode SplitRight()
                    {
                        var right = new LeafNode(Items.SplitRight());
                        if (Next != null)
                        {
                            Next.Previous = right;
                            right.Next = Next; // to make linked list.
                        }
                        right.Previous = this;
                        Next = right;
                        return right;
                    }

                    bool CanSpillTo(LeafNode leaf)
                    {
                        return leaf?.IsFull == false;
                    }
                }
                else
                {
                    var item = Items[index]; // old item
                    KeyValueItem.ChangeValue(ref item, args.GetUpdateValue(item.Value)); // update item value
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
                        if (CanBorrowFrom(Previous)) // left sibling
                        {
                            var last = Previous.Items.PopLast();
                            Items.PushFirst(last);

                            var p = relatives.LeftAncestor.Items[relatives.LeftAncestorIndex];
                            KeyNodeItem.ChangeKey(ref p, last.Key);
                            relatives.LeftAncestor.Items[relatives.LeftAncestorIndex] = p;

                            Validate(this);
                            Validate(Previous);
                        }
                        else if (CanBorrowFrom(Next)) // right sibling
                        {
                            var first = Next.Items.PopFirst();
                            Items.PushLast(first);

                            var p = relatives.RightAncestor.Items[relatives.RightAncestorIndex];
                            KeyNodeItem.ChangeKey(ref p, Next.Items.First.Key);
                            relatives.RightAncestor.Items[relatives.RightAncestorIndex] = p;
                            
                            Validate(this);
                            Validate(Next);
                        }
                        else // merge with either sibling.
                        {
                            merge = true; // set merge falg
                            if (relatives.HasTrueLeftSibling) // current node will be removed from parent.
                            {
                                Previous.Items.MergeLeft(Items); // merge from left to keep items in order.
                                Previous.Next = Next; // fix linked list
                                if (Next != null) Next.Previous = Previous;
                                
                                Validate(Previous);
                                Validate(Next);
                            }
                            else if (relatives.HasTrueRightSibling) // right sibling will be removed from parent
                            {
                                Items.MergeLeft(Next.Items); // merge from right to keep items in order. 
                                Next = Next.Next; // fix linked list
                                if (Next != null) Next.Previous = this;

                                Validate(this);
                                Validate(Next);
                            }
                            else Debug.Fail("leaf must either have true left or true right sibling.");
                        }
                    }

                    bool CanBorrowFrom(LeafNode leaf)
                    {
                        if (leaf == null) return false;
                        return leaf.Items.Count > leaf.Items.Capacity / 2;
                    }
                }

                return merge; // true if merge happened.
            }

            #endregion

            #region Debug
            
            [Conditional("DEBUG")]
            private static void Validate(LeafNode node)
            {
                if (node == null) return;
                Debug.Assert(node.IsHalfFull);
                Debug.Assert(node.Previous == null || node.Previous.Next == node);
                Debug.Assert(node.Next == null || node.Next.Previous == node);
            }

            #endregion
        }
    }
}
