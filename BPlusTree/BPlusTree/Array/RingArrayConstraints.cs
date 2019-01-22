namespace BPlusTree
{
    public enum RingArrayConstraints
    {
        /// <summary>
        /// Array has dynamic size and capacity.
        /// </summary>
        None,
        /// <summary>
        /// Array has dynamic size and fixed capacity.
        /// </summary>
        FixedCapacity,
        /// <summary>
        /// Array size is fixed.
        /// </summary>
        FixedSize,
        /// <summary>
        /// Array is readonly.
        /// </summary>
        ReadOnly
    }
}
