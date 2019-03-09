// ----------------------------------------------------------------------------
// The MIT License
// Simple Entity Component System framework https://github.com/Leopotam/ecs
// Copyright (c) 2017-2019 Leopotam <leopotam@gmail.com>
// ----------------------------------------------------------------------------

using System;

namespace Leopotam.Ecs {
    /// <summary>
    /// Mask for components selection.
    /// </summary>
#if ENABLE_IL2CPP
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption (Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption (Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
#endif
    public sealed class EcsComponentMask {
        public int[] Bits = new int[8];

        public int BitsCount;

#if DEBUG
        internal bool IsEquals (EcsComponentMask mask) {
            if (BitsCount != mask.BitsCount) {
                return false;
            }
            for (var i = 0; i < BitsCount; i++) {
                var j = mask.BitsCount - 1;
                var bit = Bits[i];
                for (; j >= 0; j--) {
                    if (mask.Bits[j] == bit) {
                        break;
                    }
                }
                if (j == -1) {
                    return false;
                }
            }
            return true;
        }

        public override string ToString () {
            var str = "[";
            for (var i = 0; i < BitsCount; i++) {
                str = string.Format ("{0}{1}{2}", str, i > 0 ? "," : "", Bits[i]);
            }
            return str + "]";
        }
#endif

#if NET46 || NETSTANDARD2_0
        [System.Runtime.CompilerServices.MethodImpl (System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
#endif
        public void SetBit (int bitId, bool state) {
            var i = BitsCount - 1;
            for (; i >= 0; i--) {
                if (Bits[i] == bitId) {
                    break;
                }
            }
            if (state) {
                if (i == -1) {
                    if (BitsCount == Bits.Length) {
                        Array.Resize (ref Bits, BitsCount << 1);
                    }
                    Bits[BitsCount++] = bitId;
                }
            } else {
                if (i != -1) {
                    BitsCount--;
                    Array.Copy (Bits, i + 1, Bits, i, BitsCount - i);
                }
            }
        }

#if NET46 || NETSTANDARD2_0
        [System.Runtime.CompilerServices.MethodImpl (System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
#endif
        public bool GetBit (int bitId) {
            var i = BitsCount - 1;
            for (; i >= 0; i--) {
                if (Bits[i] == bitId) {
                    break;
                }
            }
            return i != -1;
        }

#if NET46 || NETSTANDARD2_0
        [System.Runtime.CompilerServices.MethodImpl (System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
#endif
        public void CopyFrom (EcsComponentMask mask) {
            BitsCount = mask.BitsCount;
            if (Bits.Length < BitsCount) {
                Bits = new int[mask.Bits.Length];
            }
            Array.Copy (mask.Bits, 0, Bits, 0, BitsCount);
        }

#if NET46 || NETSTANDARD2_0
        [System.Runtime.CompilerServices.MethodImpl (System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
#endif
        public bool IsCompatible (EcsFilter filter) {
            if (BitsCount > 0 && filter.IncludeMask.BitsCount <= BitsCount) {
                int i = filter.IncludeMask.BitsCount - 1;
                var maxJ = BitsCount - 1;
                for (; i >= 0; i--) {
                    var j = maxJ;
                    var bit = filter.IncludeMask.Bits[i];
                    for (; j >= 0; j--) {
                        if (Bits[j] == bit) {
                            break;
                        }
                    }
                    if (j == -1) {
                        break;
                    }
                }
                if (i == -1) {
                    return !IsIntersects (filter.ExcludeMask);
                }
            }
            return false;
        }

#if NET46 || NETSTANDARD2_0
        [System.Runtime.CompilerServices.MethodImpl (System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
#endif
        public bool IsIntersects (EcsComponentMask mask) {
            if (BitsCount > 0 && mask.BitsCount > 0) {
                for (var i = 0; i < BitsCount; i++) {
                    var bit = Bits[i];
                    for (var j = 0; j < mask.BitsCount; j++) {
                        if (mask.Bits[j] == bit) {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }
}