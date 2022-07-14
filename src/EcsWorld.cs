// ----------------------------------------------------------------------------
// The Proprietary or MIT-Red License
// Copyright (c) 2012-2022 Leopotam <leopotam@yandex.ru>
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Leopotam.Ecs {
    /// <summary>
    /// Ecs data context.
    /// </summary>
#if ENABLE_IL2CPP
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption (Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption (Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
#endif
    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
    public class EcsWorld {
        protected EcsEntityData[] Entities;
        protected int EntitiesCount;
        protected readonly EcsGrowList<int> FreeEntities;
        protected readonly EcsGrowList<EcsFilter> Filters;
        protected readonly Dictionary<int, EcsGrowList<EcsFilter>> FilterByIncludedComponents;
        protected readonly Dictionary<int, EcsGrowList<EcsFilter>> FilterByExcludedComponents;

        // just for world stats.
        int _usedComponentsCount;

        internal readonly EcsWorldConfig Config;
        readonly object[] _filterCtor;

        /// <summary>
        /// Creates new ecs-world instance.
        /// </summary>
        /// <param name="config">Optional config for default cache sizes. On zero or negative value - default value will be used.</param>
        public EcsWorld (EcsWorldConfig config = default) {
            var finalConfig = new EcsWorldConfig {
                EntityComponentsCacheSize = config.EntityComponentsCacheSize <= 0
                    ? EcsWorldConfig.DefaultEntityComponentsCacheSize
                    : config.EntityComponentsCacheSize,
                FilterEntitiesCacheSize = config.FilterEntitiesCacheSize <= 0
                    ? EcsWorldConfig.DefaultFilterEntitiesCacheSize
                    : config.FilterEntitiesCacheSize,
                WorldEntitiesCacheSize = config.WorldEntitiesCacheSize <= 0
                    ? EcsWorldConfig.DefaultWorldEntitiesCacheSize
                    : config.WorldEntitiesCacheSize,
                WorldFiltersCacheSize = config.WorldFiltersCacheSize <= 0
                    ? EcsWorldConfig.DefaultWorldFiltersCacheSize
                    : config.WorldFiltersCacheSize,
                WorldComponentPoolsCacheSize = config.WorldComponentPoolsCacheSize <= 0
                    ? EcsWorldConfig.DefaultWorldComponentPoolsCacheSize
                    : config.WorldComponentPoolsCacheSize
            };
            Config = finalConfig;
            Entities = new EcsEntityData[Config.WorldEntitiesCacheSize];
            FreeEntities = new EcsGrowList<int> (Config.WorldEntitiesCacheSize);
            Filters = new EcsGrowList<EcsFilter> (Config.WorldFiltersCacheSize);
            FilterByIncludedComponents = new Dictionary<int, EcsGrowList<EcsFilter>> (Config.WorldFiltersCacheSize);
            FilterByExcludedComponents = new Dictionary<int, EcsGrowList<EcsFilter>> (Config.WorldFiltersCacheSize);
            ComponentPools = new IEcsComponentPool[Config.WorldComponentPoolsCacheSize];
            _filterCtor = new object[] { this };
        }

        /// <summary>
        /// Component pools cache.
        /// </summary>
        public IEcsComponentPool[] ComponentPools;

        protected bool IsDestroyed;
#if DEBUG
        internal readonly List<IEcsWorldDebugListener> DebugListeners = new List<IEcsWorldDebugListener> (4);
        readonly EcsGrowList<EcsEntity> _leakedEntities = new EcsGrowList<EcsEntity> (256);
        bool _inDestroying;

        /// <summary>
        /// Adds external event listener.
        /// </summary>
        /// <param name="listener">Event listener.</param>
        public void AddDebugListener (IEcsWorldDebugListener listener) {
            if (listener == null) { throw new Exception ("Listener is null."); }
            DebugListeners.Add (listener);
        }

        /// <summary>
        /// Removes external event listener.
        /// </summary>
        /// <param name="listener">Event listener.</param>
        public void RemoveDebugListener (IEcsWorldDebugListener listener) {
            if (listener == null) { throw new Exception ("Listener is null."); }
            DebugListeners.Remove (listener);
        }
#endif

        /// <summary>
        /// Destroys world and exist entities.
        /// </summary>
        public virtual void Destroy () {
#if DEBUG
            if (IsDestroyed || _inDestroying) { throw new Exception ("EcsWorld already destroyed."); }
            _inDestroying = true;
            CheckForLeakedEntities ("Destroy");
#endif
            EcsEntity entity;
            entity.Owner = this;
            for (var i = EntitiesCount - 1; i >= 0; i--) {
                ref var entityData = ref Entities[i];
                if (entityData.ComponentsCountX2 > 0) {
                    entity.Id = i;
                    entity.Gen = entityData.Gen;
                    entity.Destroy ();
                }
            }
            for (int i = 0, iMax = Filters.Count; i < iMax; i++) {
                Filters.Items[i].Destroy ();
            }

            IsDestroyed = true;
#if DEBUG
            for (var i = DebugListeners.Count - 1; i >= 0; i--) {
                DebugListeners[i].OnWorldDestroyed (this);
            }
#endif
        }

        /// <summary>
        /// Is world not destroyed.
        /// </summary>
        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public bool IsAlive () {
            return !IsDestroyed;
        }

        /// <summary>
        /// Creates new entity.
        /// </summary>
        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public EcsEntity NewEntity () {
#if DEBUG
            if (IsDestroyed) { throw new Exception ("EcsWorld already destroyed."); }
#endif
            EcsEntity entity;
            entity.Owner = this;
            // try to reuse entity from pool.
            if (FreeEntities.Count > 0) {
                entity.Id = FreeEntities.Items[--FreeEntities.Count];
                ref var entityData = ref Entities[entity.Id];
                entity.Gen = entityData.Gen;
                entityData.ComponentsCountX2 = 0;
            } else {
                // create new entity.
                if (EntitiesCount == Entities.Length) {
                    Array.Resize (ref Entities, EntitiesCount << 1);
                }
                entity.Id = EntitiesCount++;
                ref var entityData = ref Entities[entity.Id];
                entityData.Components = new int[Config.EntityComponentsCacheSize * 2];
                entityData.Gen = 1;
                entity.Gen = entityData.Gen;
                entityData.ComponentsCountX2 = 0;
            }
#if DEBUG
            _leakedEntities.Add (entity);
            foreach (var debugListener in DebugListeners) {
                debugListener.OnEntityCreated (entity);
            }
#endif
            return entity;
        }

        /// <summary>
        /// Restores EcsEntity from internal id and gen. For internal use only!
        /// </summary>
        /// <param name="id">Internal id.</param>
        /// <param name="gen">Generation. If less than 0 - will be filled from current generation value.</param>
        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public EcsEntity RestoreEntityFromInternalId (int id, int gen = -1) {
            EcsEntity entity;
            entity.Owner = this;
            entity.Id = id;
            if (gen < 0) {
                entity.Gen = 0;
                ref var entityData = ref GetEntityData (entity);
                entity.Gen = entityData.Gen;
            } else {
                entity.Gen = (ushort) gen;
            }
            return entity;
        }

        /// <summary>
        /// Request exist filter or create new one. For internal use only!
        /// </summary>
        /// <param name="filterType">Filter type.</param>
        /// <param name="createIfNotExists">Create filter if not exists.</param>
        public EcsFilter GetFilter (Type filterType, bool createIfNotExists = true) {
#if DEBUG
            if (filterType == null) { throw new Exception ("FilterType is null."); }
            if (!filterType.IsSubclassOf (typeof (EcsFilter))) { throw new Exception ($"Invalid filter type: {filterType}."); }
            if (IsDestroyed) { throw new Exception ("EcsWorld already destroyed."); }
#endif
            // check already exist filters.
            for (int i = 0, iMax = Filters.Count; i < iMax; i++) {
                if (Filters.Items[i].GetType () == filterType) {
                    return Filters.Items[i];
                }
            }
            if (!createIfNotExists) {
                return null;
            }
            // create new filter.
            var filter = (EcsFilter) Activator.CreateInstance (filterType, BindingFlags.NonPublic | BindingFlags.Instance, null, _filterCtor, CultureInfo.InvariantCulture);
#if DEBUG
            for (var filterIdx = 0; filterIdx < Filters.Count; filterIdx++) {
                if (filter.AreComponentsSame (Filters.Items[filterIdx])) {
                    throw new Exception (
                        $"Invalid filter \"{filter.GetType ()}\": Another filter \"{Filters.Items[filterIdx].GetType ()}\" already has same components, but in different order.");
                }
            }
#endif
            Filters.Add (filter);
            // add to component dictionaries for fast compatibility scan.
            for (int i = 0, iMax = filter.IncludedTypeIndices.Length; i < iMax; i++) {
                if (!FilterByIncludedComponents.TryGetValue (filter.IncludedTypeIndices[i], out var filtersList)) {
                    filtersList = new EcsGrowList<EcsFilter> (8);
                    FilterByIncludedComponents[filter.IncludedTypeIndices[i]] = filtersList;
                }
                filtersList.Add (filter);
            }
            if (filter.ExcludedTypeIndices != null) {
                for (int i = 0, iMax = filter.ExcludedTypeIndices.Length; i < iMax; i++) {
                    if (!FilterByExcludedComponents.TryGetValue (filter.ExcludedTypeIndices[i], out var filtersList)) {
                        filtersList = new EcsGrowList<EcsFilter> (8);
                        FilterByExcludedComponents[filter.ExcludedTypeIndices[i]] = filtersList;
                    }
                    filtersList.Add (filter);
                }
            }
#if DEBUG
            foreach (var debugListener in DebugListeners) {
                debugListener.OnFilterCreated (filter);
            }
#endif
            // scan exist entities for compatibility with new filter.
            EcsEntity entity;
            entity.Owner = this;
            for (int i = 0, iMax = EntitiesCount; i < iMax; i++) {
                ref var entityData = ref Entities[i];
                if (entityData.ComponentsCountX2 > 0 && filter.IsCompatible (entityData, 0)) {
                    entity.Id = i;
                    entity.Gen = entityData.Gen;
                    filter.OnAddEntity (entity);
                }
            }
            return filter;
        }

        /// <summary>
        /// Gets stats of internal data.
        /// </summary>
        public EcsWorldStats GetStats () {
            var stats = new EcsWorldStats () {
                ActiveEntities = EntitiesCount - FreeEntities.Count,
                ReservedEntities = FreeEntities.Count,
                Filters = Filters.Count,
                Components = _usedComponentsCount
            };
            return stats;
        }

        /// <summary>
        /// Recycles internal entity data to pool.
        /// </summary>
        /// <param name="id">Entity id.</param>
        /// <param name="entityData">Entity internal data.</param>
        protected internal void RecycleEntityData (int id, ref EcsEntityData entityData) {
#if DEBUG
            if (entityData.ComponentsCountX2 != 0) { throw new Exception ("Cant recycle invalid entity."); }
#endif
            entityData.ComponentsCountX2 = -2;
            entityData.Gen++;
            if (entityData.Gen == 0) { entityData.Gen = 1; }
            FreeEntities.Add (id);
        }

#if DEBUG
        /// <summary>
        /// Checks exist entities but without components.
        /// </summary>
        /// <param name="errorMsg">Prefix for error message.</param>
        public bool CheckForLeakedEntities (string errorMsg) {
            if (_leakedEntities.Count > 0) {
                for (int i = 0, iMax = _leakedEntities.Count; i < iMax; i++) {
                    if (GetEntityData (_leakedEntities.Items[i]).ComponentsCountX2 == 0) {
                        if (errorMsg != null) {
                            throw new Exception ($"{errorMsg}: Empty entity detected, possible memory leak.");
                        }
                        return true;
                    }
                }
                _leakedEntities.Count = 0;
            }
            return false;
        }
#endif

        /// <summary>
        /// Updates filters.
        /// </summary>
        /// <param name="typeIdx">Component type index.abstract Positive for add operation, negative for remove operation.</param>
        /// <param name="entity">Target entity.</param>
        /// <param name="entityData">Target entity data.</param>
        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        protected internal void UpdateFilters (int typeIdx, in EcsEntity entity, in EcsEntityData entityData) {
#if DEBUG
            if (IsDestroyed) { throw new Exception ("EcsWorld already destroyed."); }
#endif
            EcsGrowList<EcsFilter> filters;
            if (typeIdx < 0) {
                // remove component.
                if (FilterByIncludedComponents.TryGetValue (-typeIdx, out filters)) {
                    for (int i = 0, iMax = filters.Count; i < iMax; i++) {
                        if (filters.Items[i].IsCompatible (entityData, 0)) {
#if DEBUG
                            if (!filters.Items[i].GetInternalEntitiesMap ().TryGetValue (entity.GetInternalId (), out var filterIdx)) { filterIdx = -1; }
                            if (filterIdx < 0) { throw new Exception ("Entity not in filter."); }
#endif
                            filters.Items[i].OnRemoveEntity (entity);
                        }
                    }
                }
                if (FilterByExcludedComponents.TryGetValue (-typeIdx, out filters)) {
                    for (int i = 0, iMax = filters.Count; i < iMax; i++) {
                        if (filters.Items[i].IsCompatible (entityData, typeIdx)) {
#if DEBUG
                            if (!filters.Items[i].GetInternalEntitiesMap ().TryGetValue (entity.GetInternalId (), out var filterIdx)) { filterIdx = -1; }
                            if (filterIdx >= 0) { throw new Exception ("Entity already in filter."); }
#endif
                            filters.Items[i].OnAddEntity (entity);
                        }
                    }
                }
            } else {
                // add component.
                if (FilterByIncludedComponents.TryGetValue (typeIdx, out filters)) {
                    for (int i = 0, iMax = filters.Count; i < iMax; i++) {
                        if (filters.Items[i].IsCompatible (entityData, 0)) {
#if DEBUG
                            if (!filters.Items[i].GetInternalEntitiesMap ().TryGetValue (entity.GetInternalId (), out var filterIdx)) { filterIdx = -1; }
                            if (filterIdx >= 0) { throw new Exception ("Entity already in filter."); }
#endif
                            filters.Items[i].OnAddEntity (entity);
                        }
                    }
                }
                if (FilterByExcludedComponents.TryGetValue (typeIdx, out filters)) {
                    for (int i = 0, iMax = filters.Count; i < iMax; i++) {
                        if (filters.Items[i].IsCompatible (entityData, -typeIdx)) {
#if DEBUG
                            if (!filters.Items[i].GetInternalEntitiesMap ().TryGetValue (entity.GetInternalId (), out var filterIdx)) { filterIdx = -1; }
                            if (filterIdx < 0) { throw new Exception ("Entity not in filter."); }
#endif
                            filters.Items[i].OnRemoveEntity (entity);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns internal state of entity. For internal use!
        /// </summary>
        /// <param name="entity">Entity.</param>
        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public ref EcsEntityData GetEntityData (in EcsEntity entity) {
#if DEBUG
            if (IsDestroyed) { throw new Exception ("EcsWorld already destroyed."); }
            if (entity.Id < 0 || entity.Id > EntitiesCount) { throw new Exception ($"Invalid entity {entity.Id}"); }
#endif
            return ref Entities[entity.Id];
        }

        /// <summary>
        /// Internal state of entity.
        /// </summary>
        [StructLayout (LayoutKind.Sequential, Pack = 2)]
        public struct EcsEntityData {
            public ushort Gen;
            public short ComponentsCountX2;
            public int[] Components;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public EcsComponentPool<T> GetPool<T> () where T : struct {
            var typeIdx = EcsComponentType<T>.TypeIndex;
            if (ComponentPools.Length <= typeIdx) {
                var len = ComponentPools.Length << 1;
                while (len <= typeIdx) {
                    len <<= 1;
                }
                Array.Resize (ref ComponentPools, len);
            }
            var pool = (EcsComponentPool<T>) ComponentPools[typeIdx];
            if (pool == null) {
                pool = new EcsComponentPool<T> ();
                ComponentPools[typeIdx] = pool;
                _usedComponentsCount++;
            }
            return pool;
        }

        /// <summary>
        /// Gets all alive entities.
        /// </summary>
        /// <param name="entities">List to put results in it. if null - will be created. If not enough space - will be resized.</param>
        /// <returns>Amount of alive entities.</returns>
        public int GetAllEntities (ref EcsEntity[] entities) {
            var count = EntitiesCount - FreeEntities.Count;
            if (entities == null || entities.Length < count) {
                entities = new EcsEntity[count];
            }
            EcsEntity e;
            e.Owner = this;
            var id = 0;
            for (int i = 0, iMax = EntitiesCount; i < iMax; i++) {
                ref var entityData = ref Entities[i];
                // should we skip empty entities here?
                if (entityData.ComponentsCountX2 >= 0) {
                    e.Id = i;
                    e.Gen = entityData.Gen;
                    entities[id++] = e;
                }
            }
            return count;
        }
    }

    /// <summary>
    /// Stats of EcsWorld instance.
    /// </summary>
    public struct EcsWorldStats {
        /// <summary>
        /// Amount of active entities.
        /// </summary>
        public int ActiveEntities;

        /// <summary>
        /// Amount of cached (not in use) entities.
        /// </summary>
        public int ReservedEntities;

        /// <summary>
        /// Amount of registered filters.
        /// </summary>
        public int Filters;

        /// <summary>
        /// Amount of registered component types.
        /// </summary>
        public int Components;
    }

    /// <summary>
    /// World config to setup default caches.
    /// </summary>
    public struct EcsWorldConfig {
        /// <summary>
        /// World.Entities cache size.
        /// </summary>
        public int WorldEntitiesCacheSize;
        /// <summary>
        /// World.Filters cache size.
        /// </summary>
        public int WorldFiltersCacheSize;
        /// <summary>
        /// World.ComponentPools cache size.
        /// </summary>
        public int WorldComponentPoolsCacheSize;
        /// <summary>
        /// Entity.Components cache size (not doubled).
        /// </summary>
        public int EntityComponentsCacheSize;
        /// <summary>
        /// Filter.Entities cache size.
        /// </summary>
        public int FilterEntitiesCacheSize;
        /// <summary>
        /// World.Entities default cache size.
        /// </summary>
        public const int DefaultWorldEntitiesCacheSize = 1024;
        /// <summary>
        /// World.Filters default cache size.
        /// </summary>
        public const int DefaultWorldFiltersCacheSize = 128;
        /// <summary>
        /// World.ComponentPools default cache size.
        /// </summary>
        public const int DefaultWorldComponentPoolsCacheSize = 512;
        /// <summary>
        /// Entity.Components default cache size (not doubled).
        /// </summary>
        public const int DefaultEntityComponentsCacheSize = 8;
        /// <summary>
        /// Filter.Entities default cache size.
        /// </summary>
        public const int DefaultFilterEntitiesCacheSize = 256;
    }

#if DEBUG
    /// <summary>
    /// Debug interface for world events processing.
    /// </summary>
    public interface IEcsWorldDebugListener {
        void OnEntityCreated (EcsEntity entity);
        void OnEntityDestroyed (EcsEntity entity);
        void OnFilterCreated (EcsFilter filter);
        void OnComponentListChanged (EcsEntity entity);
        void OnWorldDestroyed (EcsWorld world);
    }
#endif
}