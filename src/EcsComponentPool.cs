// ----------------------------------------------------------------------------
// The MIT License
// Simple Entity Component System framework https://github.com/Leopotam/ecs
// Copyright (c) 2017-2019 Leopotam <leopotam@gmail.com>
// ----------------------------------------------------------------------------

using System;

namespace Leopotam.Ecs {
    /// <summary>
    /// Marks component class to be not autofilled as ComponentX in filter.
    /// </summary>
    [AttributeUsage (AttributeTargets.Class)]
    public sealed class EcsIgnoreInFilterAttribute : Attribute { }

    /// <summary>
    /// Marks component class to be auto removed from world.
    /// </summary>
    [AttributeUsage (AttributeTargets.Class)]
    public sealed class EcsOneFrameAttribute : Attribute { }

    /// <summary>
    /// Marks component class as resettable with custom logic.
    /// </summary>
    public interface IEcsAutoResetComponent {
        void Reset ();
    }

    /// <summary>
    /// Marks field of component to be not checked for null on component removing.
    /// Works only in DEBUG mode!
    /// </summary>
    [System.Diagnostics.Conditional ("DEBUG")]
    [AttributeUsage (AttributeTargets.Field)]
    public sealed class EcsIgnoreNullCheckAttribute : Attribute { }

    public interface IEcsComponentPool {
        object GetExistItemById (int idx);
        void RecycleById (int id);
        int GetComponentTypeIndex ();
        bool IsOneFrameComponent ();
    }

    /// <summary>
    /// Components pool container.
    /// </summary>
#if ENABLE_IL2CPP
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption (Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption (Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
#endif
    public sealed class EcsComponentPool<T> : IEcsComponentPool where T : class, new () {
        const int MinSize = 8;

        public static readonly EcsComponentPool<T> Instance = new EcsComponentPool<T> ();

        public T[] Items = new T[MinSize];

        public readonly bool IsIgnoreInFilter = Attribute.IsDefined (typeof (T), typeof (EcsIgnoreInFilterAttribute));

        public readonly bool IsOneFrame = Attribute.IsDefined (typeof (T), typeof (EcsOneFrameAttribute));

        public readonly bool IsAutoReset = typeof (IEcsAutoResetComponent).IsAssignableFrom (typeof (T));

        public readonly int TypeIndex;

        public int[] ReservedItems = new int[MinSize];

        public int ItemsCount;

        public int ReservedItemsCount;

        Func<T> _creator;

#if DEBUG
        System.Collections.Generic.List<System.Reflection.FieldInfo> _nullableFields = new System.Collections.Generic.List<System.Reflection.FieldInfo> (8);
#endif

        EcsComponentPool () {
            TypeIndex = Internals.EcsHelpers.ComponentPoolsCount++;
            Internals.EcsHelpers.ComponentPools[TypeIndex] = this;
#if DEBUG
            // collect all marshal-by-reference fields.
            var fields = typeof (T).GetFields ();
            for (var i = 0; i < fields.Length; i++) {
                var field = fields[i];
                var type = field.FieldType;
                if (!type.IsValueType || (Nullable.GetUnderlyingType (type) != null) && !Nullable.GetUnderlyingType (type).IsValueType) {
                    if (type != typeof (string) && !Attribute.IsDefined (field, typeof (EcsIgnoreNullCheckAttribute))) {
                        _nullableFields.Add (fields[i]);
                    }
                }
            }
#endif
        }

#if NET46 || NETSTANDARD2_0
        [System.Runtime.CompilerServices.MethodImpl (System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
#endif
        public int RequestNewId () {
            int id;
            if (ReservedItemsCount > 0) {
                id = ReservedItems[--ReservedItemsCount];
            } else {
                id = ItemsCount;
                if (ItemsCount == Items.Length) {
                    Array.Resize (ref Items, ItemsCount << 1);
                }
                Items[ItemsCount++] = _creator != null ? _creator () : (T) Activator.CreateInstance (typeof (T));
            }
            return id;
        }

#if NET46 || NETSTANDARD2_0
        [System.Runtime.CompilerServices.MethodImpl (System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
#endif
        public void RecycleById (int id) {
            if (IsAutoReset) {
                ((IEcsAutoResetComponent) Items[id]).Reset ();
            }
#if DEBUG
            // check all marshal-by-reference typed fields for nulls.
            var obj = Items[id];
            for (var i = 0; i < _nullableFields.Count; i++) {
                if (_nullableFields[i].GetValue (obj) != null) {
                    throw new Exception (string.Format (
                        "Memory leak for \"{0}\" component: \"{1}\" field not nulled. If you are sure that it's not - mark field with [EcsIgnoreNullCheck] attribute",
                        typeof (T).Name, _nullableFields[i].Name));
                }
            }
#endif
            if (ReservedItemsCount == ReservedItems.Length) {
                Array.Resize (ref ReservedItems, ReservedItemsCount << 1);
            }
            ReservedItems[ReservedItemsCount++] = id;
        }

#if NET46 || NETSTANDARD2_0
        [System.Runtime.CompilerServices.MethodImpl (System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
#endif
        public object GetExistItemById (int idx) {
            return Items[idx];
        }

#if NET46 || NETSTANDARD2_0
        [System.Runtime.CompilerServices.MethodImpl (System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
#endif
        public int GetComponentTypeIndex () {
            return TypeIndex;
        }

#if NET46 || NETSTANDARD2_0
        [System.Runtime.CompilerServices.MethodImpl (System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
#endif
        public bool IsOneFrameComponent () {
            return IsOneFrame;
        }

        /// <summary>
        /// Registers custom activator for creating instances of specified type.
        /// </summary>
        /// <param name="creator">Custom callback for instance creation.</param>
        public void SetCreator (Func<T> creator) {
            _creator = creator;
        }

        /// <summary>
        /// Sets new capacity (if more than current amount).
        /// </summary>
        /// <param name="capacity">New value.</param>
        public void SetCapacity (int capacity) {
            if (capacity < Items.Length) {
                return;
            }
            Array.Resize (ref Items, capacity);
        }

        /// <summary>
        /// Shrinks empty space after last allocated item.
        /// </summary>
        public void Shrink () {
            int capacity;
            capacity = ItemsCount < MinSize ? MinSize : Internals.EcsHelpers.GetPowerOfTwoSize (ItemsCount);
            if (Items.Length != capacity) {
                Array.Resize (ref Items, capacity);
            }
            capacity = ReservedItemsCount < MinSize ? MinSize : Internals.EcsHelpers.GetPowerOfTwoSize (ReservedItemsCount);
            if (ReservedItems.Length != capacity) {
                Array.Resize (ref ReservedItems, capacity);
            }
        }
    }
}