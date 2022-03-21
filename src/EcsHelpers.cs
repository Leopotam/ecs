// ----------------------------------------------------------------------------
// The Proprietary or MIT-Red License
// Copyright (c) 2012-2022 Leopotam <leopotam@yandex.ru>
// ----------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;

namespace Leopotam.Ecs {
    /// <summary>
    /// Fast List replacement for growing only collections.
    /// </summary>
    /// <typeparam name="T">Type of item.</typeparam>
    public class EcsGrowList<T> {
        public T[] Items;
        public int Count;

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public EcsGrowList (int capacity) {
#if DEBUG
            if (capacity <= 0) { throw new Exception ("Capacity should be greater than zero."); }
#endif
            Items = new T[capacity];
            Count = 0;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public void Add (T item) {
            if (Items.Length == Count) {
                Array.Resize (ref Items, Items.Length << 1);
            }
            Items[Count++] = item;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public void EnsureCapacity (int count) {
            if (Items.Length < count) {
                var len = Items.Length << 1;
                while (len <= count) {
                    len <<= 1;
                }
                Array.Resize (ref Items, len);
            }
        }
    }
}

#if ENABLE_IL2CPP
// Unity IL2CPP performance optimization attribute.
namespace Unity.IL2CPP.CompilerServices {
    enum Option {
        NullChecks = 1,
        ArrayBoundsChecks = 2
    }

    [AttributeUsage (AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
    class Il2CppSetOptionAttribute : Attribute {
        public Option Option { get; private set; }
        public object Value { get; private set; }

        public Il2CppSetOptionAttribute (Option option, object value) { Option = option; Value = value; }
    }
}
#endif