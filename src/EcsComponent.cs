// ----------------------------------------------------------------------------
// The Proprietary or MIT-Red License
// Copyright (c) 2012-2022 Leopotam <leopotam@yandex.ru>
// ----------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;
using System.Threading;

// ReSharper disable ClassNeverInstantiated.Global

namespace Leopotam.Ecs {
    /// <summary>
    /// Marks component type to be not auto-filled as GetX in filter.
    /// </summary>
    public interface IEcsIgnoreInFilter { }

    /// <summary>
    /// Marks component type for custom reset behaviour.
    /// </summary>
    /// <typeparam name="T">Type of component, should be the same as main component!</typeparam>
    public interface IEcsAutoReset<T> where T : struct {
        void AutoReset (ref T c);
    }

    /// <summary>
    /// Marks field of IEcsSystem class to be ignored during dependency injection.
    /// </summary>
    public sealed class EcsIgnoreInjectAttribute : Attribute { }

    /// <summary>
    /// Global descriptor of used component type.
    /// </summary>
    /// <typeparam name="T">Component type.</typeparam>
    public static class EcsComponentType<T> where T : struct {
        // ReSharper disable StaticMemberInGenericType
        public static readonly int TypeIndex;
        public static readonly Type Type;
        public static readonly bool IsIgnoreInFilter;
        public static readonly bool IsAutoReset;
        // ReSharper restore StaticMemberInGenericType

        static EcsComponentType () {
            TypeIndex = Interlocked.Increment (ref EcsComponentPool.ComponentTypesCount);
            Type = typeof (T);
            IsIgnoreInFilter = typeof (IEcsIgnoreInFilter).IsAssignableFrom (Type);
            IsAutoReset = typeof (IEcsAutoReset<T>).IsAssignableFrom (Type);
#if DEBUG
            if (!IsAutoReset && Type.GetInterface ("IEcsAutoReset`1") != null) {
                throw new Exception ($"IEcsAutoReset should have <{typeof (T).Name}> constraint for component \"{typeof (T).Name}\".");
            }
#endif
        }
    }

    public sealed class EcsComponentPool {
        /// <summary>
        /// Global component type counter.
        /// First component will be "1" for correct filters updating (add component on positive and remove on negative).
        /// </summary>
        internal static int ComponentTypesCount;
    }

    public interface IEcsComponentPool {
        Type ItemType { get; }
        object GetItem (int idx);
        void Recycle (int idx);
        int New ();
        void CopyData (int srcIdx, int dstIdx);
    }

    /// <summary>
    /// Helper for save reference to component. 
    /// </summary>
    /// <typeparam name="T">Type of component.</typeparam>
    public struct EcsComponentRef<T> where T : struct {
        internal EcsComponentPool<T> Pool;
        internal int Idx;
        
        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static bool AreEquals (in EcsComponentRef<T> lhs, in EcsComponentRef<T> rhs) {
            return lhs.Idx == rhs.Idx && lhs.Pool == rhs.Pool;
        }
    }

#if ENABLE_IL2CPP
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption (Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption (Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
#endif
    public static class EcsComponentRefExtensions {
        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static ref T Unref<T> (in this EcsComponentRef<T> wrapper) where T : struct {
            return ref wrapper.Pool.Items[wrapper.Idx];
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public static bool IsNull<T> (in this EcsComponentRef<T> wrapper) where T : struct {
            return wrapper.Pool == null;
        }
    }

    public interface IEcsComponentPoolResizeListener {
        void OnComponentPoolResize ();
    }

#if ENABLE_IL2CPP
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption (Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption (Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
#endif
    public sealed class EcsComponentPool<T> : IEcsComponentPool where T : struct {
        delegate void AutoResetHandler (ref T component);

        public Type ItemType { get; }
        public T[] Items = new T[128];
        int[] _reservedItems = new int[128];
        int _itemsCount;
        int _reservedItemsCount;
        readonly AutoResetHandler _autoReset;
#if ENABLE_IL2CPP && !UNITY_EDITOR
        T _autoresetFakeInstance;
#endif
        IEcsComponentPoolResizeListener[] _resizeListeners;
        int _resizeListenersCount;

        internal EcsComponentPool () {
            ItemType = typeof (T);
            if (EcsComponentType<T>.IsAutoReset) {
                var autoResetMethod = typeof (T).GetMethod (nameof (IEcsAutoReset<T>.AutoReset));
#if DEBUG

                if (autoResetMethod == null) {
                    throw new Exception (
                        $"IEcsAutoReset<{typeof (T).Name}> explicit implementation not supported, use implicit instead.");
                }
#endif
                _autoReset = (AutoResetHandler) Delegate.CreateDelegate (
                    typeof (AutoResetHandler),
#if ENABLE_IL2CPP && !UNITY_EDITOR
                    _autoresetFakeInstance,
#else
                    null,
#endif
                    autoResetMethod);
            }
            _resizeListeners = new IEcsComponentPoolResizeListener[128];
            _reservedItemsCount = 0;
        }

        void RaiseOnResizeEvent () {
            for (int i = 0, iMax = _resizeListenersCount; i < iMax; i++) {
                _resizeListeners[i].OnComponentPoolResize ();
            }
        }

        public void AddResizeListener (IEcsComponentPoolResizeListener listener) {
#if DEBUG
            if (listener == null) { throw new Exception ("Listener is null."); }
#endif
            if (_resizeListeners.Length == _resizeListenersCount) {
                Array.Resize (ref _resizeListeners, _resizeListenersCount << 1);
            }
            _resizeListeners[_resizeListenersCount++] = listener;
        }

        public void RemoveResizeListener (IEcsComponentPoolResizeListener listener) {
#if DEBUG
            if (listener == null) { throw new Exception ("Listener is null."); }
#endif
            for (int i = 0, iMax = _resizeListenersCount; i < iMax; i++) {
                if (_resizeListeners[i] == listener) {
                    _resizeListenersCount--;
                    if (i < _resizeListenersCount) {
                        _resizeListeners[i] = _resizeListeners[_resizeListenersCount];
                    }
                    _resizeListeners[_resizeListenersCount] = null;
                    break;
                }
            }
        }

        /// <summary>
        /// Sets new capacity (if more than current amount).
        /// </summary>
        /// <param name="capacity">New value.</param>
        public void SetCapacity (int capacity) {
            if (capacity > Items.Length) {
                Array.Resize (ref Items, capacity);
                RaiseOnResizeEvent ();
            }
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public int New () {
            int id;
            if (_reservedItemsCount > 0) {
                id = _reservedItems[--_reservedItemsCount];
            } else {
                id = _itemsCount;
                if (_itemsCount == Items.Length) {
                    Array.Resize (ref Items, _itemsCount << 1);
                    RaiseOnResizeEvent ();
                }
                // reset brand new instance if custom AutoReset was registered.
                _autoReset?.Invoke (ref Items[_itemsCount]);
                _itemsCount++;
            }
            return id;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public ref T GetItem (int idx) {
            return ref Items[idx];
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public void Recycle (int idx) {
            if (_autoReset != null) {
                _autoReset (ref Items[idx]);
            } else {
                Items[idx] = default;
            }
            if (_reservedItemsCount == _reservedItems.Length) {
                Array.Resize (ref _reservedItems, _reservedItemsCount << 1);
            }
            _reservedItems[_reservedItemsCount++] = idx;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public void CopyData (int srcIdx, int dstIdx) {
            Items[dstIdx] = Items[srcIdx];
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public EcsComponentRef<T> Ref (int idx) {
            EcsComponentRef<T> componentRef;
            componentRef.Pool = this;
            componentRef.Idx = idx;
            return componentRef;
        }

        object IEcsComponentPool.GetItem (int idx) {
            return Items[idx];
        }
    }
}