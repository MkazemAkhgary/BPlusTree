namespace BPlusTree
{
    using System.Runtime.CompilerServices;
    
    internal static class NumericExtensions
    {
        /// <summary>
        /// fast sign function that uses bitwise operations instead of branches.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sign(this int x) => (x >> 31) | 1;
    }
}