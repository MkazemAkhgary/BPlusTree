# BPlusTree

Implementation of B+ tree in C# 7. There is a Builder that supports bulk-loading of items.

This tree can be converted to `IEnumerable<(TKey, TValue)>` in order to perform range queries effectively.

if documentations are not clear let me know. your feedback is much appreciated.

# Constructors

```c#
BPTree(IComparer<TKey> keyComparer = null, 
       int internalNodeCapacity = 32, 
       int leafCapacity = 32)

BPTree(IEnumerable<(TKey key, TValue value)> source, 
       IComparer<TKey> keyComparer = null, 
       int internalNodeCapacity = 32, 
       int leafCapacity = 32)
```

# Properties

```c#
int InternalNodeCapacity { get; }
int LeafCapacity { get; }
int Height { get; }
int Count { get; }
IComparer<TKey> KeyComparer { get; }
```

# Add/Update Methods

```c#
// add new entry. 
// if key is duplicate, an exception is thrown.
void Add(TKey key, TValue value)

// add new entry if key is not duplicate. 
// returns true if entry is added
bool TryAdd(TKey key, TValue value)

// add new entry or replace the old one. 
// returns true if entry is added
bool AddOrReplace(TKey key, TValue value)

// add new entry or update from old entry.
// updateFunction is used to get the updated entry.
bool AddOrUpdate(TKey key, TValue value, 
  Func<(TKey key, TValue newValue, TValue oldValue), TValue> updateFunction)

// pass key and argument of any type.
// if key does not exist, addFunction is used to get a new entry from key and argument.
// if key does exist, updateFunction is used to get updated entry from key, argument and old entry.
AddOrUpdateFromArg<TArg>(TKey key, TArg arg, 
  Func<(TKey key, TArg arg), TValue> addFunction, 
  Func<(TKey key, TArg arg, TValue oldValue), TValue> updateFunction)
---

# Remove Methods

---c#
// remove and take first entry.
// returns true if first entry is removed. (if tree is not empty)
bool RemoveFirst(out TValue first)

// remove and take last entry.
// returns true if last entry is removed. (if tree is not empty)
bool RemoveLast(out TValue last)

// remove and take an entry.
// returns true if the entry is removed.
bool Remove(TKey key, out TValue value)
---

# Other Methods

```c#

// readonly indexer to retrieve values by keys.
TValue this[TKey key] { get; }

// retrieve the value by key.
// returns true if entry exists.
bool TryGet(TKey key, out TValue value)

// returns true if entry with given key exists.
bool ContainsKey(TKey key)

// retrieves the nearest value with given key.
TValue NextNearest(TKey key) 

// get first entry.
// throws exception if collection is empty.
(TKey Key, TValue Value) First { get; }

// get last entry.
// throws exception if collection is empty.
(TKey Key, TValue Value) Last { get; }

// it does what it says.
void Clear()
```

# AsEnumerable Methods

Non-concurrent version of B+ tree throws exception if collection is modified during enumeration. (Concurrent version is not implemented yet)

```c#

// convert collection to enumerable of keys and values.
//
// moveForward: if false is passed, enumeration is from end to begining without copying the collection.
IEnumerable<(TKey Key, TValue Value)> 
  AsPairEnumerable(bool moveForward = true)
  
// convert collection to enumerable of keys and values. 
// values are filtered/cast by/to given generic type argument.
//
// moveForward: if false is passed, enumeration is from end to begining without copying the collection.
// filter: if false is passed, cast is performed rather than filter. (invalid casts throw exception)
IEnumerable<(TKey Key, TCast Value)> 
  AsPairEnumerable<TCast>(bool filter = true, 
                          bool moveForward = true)
                          
// convert collection to enumerable of keys and values. 
// enumeration starts from nearest key to the given "start" parameter.
//
// start: starts from nearest entry in O(log N) speed.
// moveForward: if false is passed, enumeration is from "start" to begining without copying the collection.
IEnumerable<(TKey Key, TValue Value)> 
  AsPairEnumerable(TKey start, 
                   bool moveForward = true)
                   
// convert collection to enumerable of keys and values.
// enumeration starts from nearest key to the given "start" parameter.
// values are filtered/cast by/to given generic type argument.
//
// start: starts from nearest entry in O(log N) speed.
// filter: if false is passed, cast is performed rather than filter. (invalid casts throw exception)
// moveForward: if false is passed, enumeration is from "start" to begining without copying the collection.
IEnumerable<(TKey Key, TCast Value)> 
  AsPairEnumerable<TCast>(TKey start, 
                          bool filter = true, 
                          bool moveForward = true)

// convert collection to enumerable of values
//
// moveForward: if false is passed, enumeration is from end to begining without copying the collection.
IEnumerable<TValue> AsEnumerable(bool moveForward = true)

// convert collection to enumerable of values
// enumeration starts from nearest key to the given "start" parameter.
//
// start: starts from nearest entry in O(log N) speed.
// moveForward: if false is passed, enumeration is from "start" to begining without copying the collection.
IEnumerable<TValue> AsEnumerable(TKey start, bool moveForward = true)

// for use with linq or foreach. but you can call AsPairEnumerable to enumerate keys as well.
IEnumerator<TValue> GetEnumerator()
```

# B+Tree Builder

If you have source of already sorted items, you can use `BPTree<TKey, TValue>.Builder` to build the tree in O(N) time. this implementation uses bulk-loading. as soon as an out of order item is passed to the builder, builder automatically builds the tree.

by calling `Build` you can build or retrive already built tree from the builder.

# SparseArray

Sparse array is a collection based on B+ tree, it supports inserting duplicate items. it can be converted to both `IEnumerable<(K Key, IReadOnlyList<T> Values)>` and `IEnumerable<(K Key, T Value)>`.

# RingArray

Ring array is a collection used as an underlying structure of tree nodes. This array can act as a list, queue or stack since it can push or pop from both ends of the array at O(1) speed.

This collection can shift items in either direction for inserting/removing items to/from middle of array, a direction which requires the least amount of shifts is always chosen.

It has special Split/Merge operations dedicated to b+ tree.

# Remarks

 - This collection is not thread safe. a concurrent version of this collection will be included in future.

# Implementation details

 - When a node overflows it will give an item to adjacent siblings if possible, other wise the node is split to two nodes.
 - When a node underflows it will take an item from adjacent siblings if possible, other wise the node is merged with one of the siblings.
 - Underlying array of nodes is a special collection designed for faster insertion/removal operations.
