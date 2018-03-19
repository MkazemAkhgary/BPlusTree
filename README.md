# BPlusTree

This is an implmentation of B+ tree in c# 7.2

Its simple to use and is also flexible. if you have questions feel free to ask.

this implementation does perform spill/borrow before split/merge when a modified node overflow or underflows.

also there is a builder that supports bulkloading for efficient initialization, which is useful for loading large data sets.

there are flexible add operations, for example you can perform an update if the given key is duplicate.

this tree can easily be converted to `IEnumerable<(TKey, TValue)>` in order to perform range queries effectively.

if documentations are not clear let me know. your feedback is much appreciated.
