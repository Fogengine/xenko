﻿// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.

using System;
using System.Collections.Generic;
using System.Reflection;
using SiliconStudio.Core;
using SiliconStudio.Core.Collections;
using SiliconStudio.Core.Diagnostics;
using SiliconStudio.Xenko.Games;
using SiliconStudio.Xenko.Rendering;

namespace SiliconStudio.Xenko.Engine
{
    /// <summary>Entity processor, triggered on various <see cref="EntityManager"/> events such as Entity and Component additions and removals.</summary>
    public abstract class EntityProcessor
    {
        internal ProfilingKey UpdateProfilingKey;
        internal ProfilingKey DrawProfilingKey;
        private readonly TypeInfo mainTypeInfo;
        private readonly Dictionary<Type, bool> componentTypesSupportedAsRequired;

        /// <summary>
        /// Tags associated to this entity processor
        /// </summary>
        public PropertyContainer Tags;

        /// <summary>
        /// Update Profiling state of this entity processor for the current frame.
        /// Pay attention this is a struct, use directly.
        /// Useful to add custom Mark events into processors
        /// </summary>
        public ProfilingState UpdateProfilingState;

        /// <summary>
        /// Draw Profiling state of this entity processor for the current frame.
        /// Pay attention this is a struct, use directly.
        /// Useful to add custom Mark events into processors
        /// </summary>
        public ProfilingState DrawProfilingState;

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityProcessor"/> class.
        /// </summary>
        /// <param name="mainComponentType">Type of the main component.</param>
        /// <param name="additionalTypes">The additional types required by this processor.</param>
        /// <exception cref="System.ArgumentNullException">If parameteters are null</exception>
        /// <exception cref="System.ArgumentException">If a type does not inherit from EntityComponent</exception>
        protected EntityProcessor(Type mainComponentType, Type[] additionalTypes)
        {
            if (mainComponentType == null) throw new ArgumentNullException(nameof(mainComponentType));
            if (additionalTypes == null) throw new ArgumentNullException(nameof(additionalTypes));

            MainComponentType = mainComponentType;
            mainTypeInfo = MainComponentType.GetTypeInfo();
            Enabled = true;

            RequiredTypes = new TypeInfo[additionalTypes.Length];

            // Check that types are valid
            for (int i = 0; i < additionalTypes.Length; i++)
            {
                var requiredType = additionalTypes[i];
                if (!typeof(EntityComponent).IsAssignableFrom(requiredType))
                {
                    throw new ArgumentException($"Invalid required type [{requiredType}]. Expecting only an EntityComponent type");
                }

                RequiredTypes[i] = requiredType.GetTypeInfo();
            }

            if (RequiredTypes.Length > 0)
            {
                componentTypesSupportedAsRequired = new Dictionary<Type, bool>();
            }

            UpdateProfilingKey = new ProfilingKey(GameProfilingKeys.GameUpdate, this.GetType().Name);
            DrawProfilingKey = new ProfilingKey(GameProfilingKeys.GameDraw, this.GetType().Name);
        }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="EntityProcessor"/> is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets the primary component type handled by this processor
        /// </summary>
        public Type MainComponentType { get; }

        /// <summary>
        /// Gets the required components for an entity to be added to this entity processor.
        /// </summary>
        public TypeInfo[] RequiredTypes { get; }

        /// <summary>
        /// Gets a value indicating whether this processor is requiring some components to be present in addition to the main component of <see cref="MainComponentType"/>.
        /// </summary>
        public bool HasRequiredComponents => componentTypesSupportedAsRequired != null;

        /// <summary>
        /// Gets or sets the order of this processor.
        /// </summary>
        public int Order { get; protected set; }

        /// <summary>
        /// Gets the current entity manager.
        /// </summary>
        public EntityManager EntityManager { get; internal set; }

        /// <summary>
        /// Gets the services.
        /// </summary>
        public IServiceRegistry Services { get; internal set; }


        /// <summary>
        /// Performs work related to this processor.
        /// </summary>
        /// <param name="time"></param>
        public virtual void Update(GameTime time)
        {
        }

        /// <summary>
        /// Performs work related to this processor.
        /// </summary>
        /// <param name="context"></param>
        public virtual void Draw(RenderContext context)
        {
        }

        /// <summary>
        /// Run when this <see cref="EntityProcessor" /> is added to an <see cref="EntityManager" />.
        /// </summary>
        protected internal abstract void OnSystemAdd();

        /// <summary>
        /// Run when this <see cref="EntityProcessor" /> is removed from an <see cref="EntityManager" />.
        /// </summary>
        protected internal abstract void OnSystemRemove();

        /// <summary>
        /// Checks if <see cref="Entity"/> needs to be either added or removed.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="entityComponent"></param>
        protected internal abstract void ProcessEntityComponent(Entity entity, EntityComponent entityComponent, bool forceRemove);

        /// <summary>
        /// Adds the entity to the internal list of the <see cref="EntityManager"/>.
        /// Exposed for inheriting class that has no access to SceneInstance as internal.
        /// </summary>
        /// <param name="entity">The entity.</param>
        protected internal void InternalAddEntity(Entity entity)
        {
            EntityManager.InternalAddEntity(entity);
        }

        /// <summary>
        /// Removes the entity to the internal list of the <see cref="EntityManager"/>.
        /// Exposed for inheriting class that has no access to SceneInstance as internal.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="removeParent">Indicate if entity should be removed from its parent</param>
        protected internal void InternalRemoveEntity(Entity entity, bool removeParent)
        {
            EntityManager.InternalRemoveEntity(entity, removeParent);
        }

        /// <summary>
        /// Checks if this processor is primarily working on the passed Entity component type.
        /// </summary>
        /// <param name="type">Type of the EntityComponent</param>
        /// <returns><c>true</c> if this processor is accepting the component type</returns>
        internal bool Accept(Type type)
        {
            return mainTypeInfo.IsAssignableFrom(type);
        }

        internal bool IsDependentOnComponentType(Type type)
        {
            // Cache component types
            var result = false;
            if (HasRequiredComponents)
            {
                if (!componentTypesSupportedAsRequired.TryGetValue(type, out result))
                {
                    for (int i = 0; i < RequiredTypes.Length; i++)
                    {
                        var typeInfo = RequiredTypes[i];
                        if (typeInfo.IsAssignableFrom(type))
                        {
                            result = true;
                            break;
                        }
                    }
                    componentTypesSupportedAsRequired.Add(type, result);
                }
            }
            return result;
        }
    }

    /// <summary>
    /// Helper class for <see cref="EntityProcessor" />, that will keep track of <see cref="Entity" /> matching certain <see cref="EntityComponent" /> requirements.
    /// </summary>
    /// <typeparam name="TComponent">The main type of the component this processor is looking for.</typeparam>
    /// <typeparam name="TData">The type of the associated data.</typeparam>
    /// <remarks>
    /// Additional precomputed data will be stored alongside the <see cref="Entity" /> to offer faster accesses and iterations.
    /// </remarks>
    public abstract class EntityProcessor<TComponent, TData> : EntityProcessor  where TData : class where TComponent : EntityComponent
    {
        protected readonly Dictionary<TComponent, TData> ComponentDatas = new Dictionary<TComponent, TData>();
        private readonly HashSet<Entity> reentrancyCheck = new HashSet<Entity>();
        private readonly FastList<Type> checkRequiredTypes = new FastList<Type>();

        protected EntityProcessor(params Type[] requiredAdditionalTypes) : base(typeof(TComponent), requiredAdditionalTypes)
        {
        }

        /// <inheritdoc/>
        protected internal override void OnSystemAdd()
        {
        }

        /// <inheritdoc/>
        protected internal override void OnSystemRemove()
        {
        }

        /// <inheritdoc/>
        protected internal override void ProcessEntityComponent(Entity entity, EntityComponent entityComponentArg, bool forceRemove)
        {
            var entityComponent = (TComponent)entityComponentArg;
            // If forceRemove is true, no need to check if entity matches.
            bool entityMatch = !forceRemove && EntityMatch(entity);
            TData entityData;
            bool entityAdded = ComponentDatas.TryGetValue(entityComponent, out entityData);

            if (entityMatch && !entityAdded)
            {
                // Adding entity is not reentrant, so let's skip if already being called for current entity
                // (could happen if either GenerateComponentData, OnEntityPrepare or OnEntityAdd changes
                // any Entity components
                lock (reentrancyCheck)
                {
                    if (!reentrancyCheck.Add(entity))
                        return;
                }

                // Generate associated data
                var data = GenerateComponentData(entity, entityComponent);

                // Notify component being added
                OnEntityComponentAdding(entity, entityComponent, data);

                // Associate the component to its data
                ComponentDatas.Add(entityComponent, data);

                lock (reentrancyCheck)
                {
                    reentrancyCheck.Remove(entity);
                }
            }
            else if (entityAdded && !entityMatch)
            {
                // Notify component being removed
                OnEntityComponentRemoved(entity, entityComponent, entityData);

                // Removes it from the component => data map
                ComponentDatas.Remove(entityComponent);
            }
            else if (entityMatch) // && entityMatch
            {
                if (!IsAssociatedDataValid(entity, entityComponent, entityData))
                {
                    OnEntityComponentRemoved(entity, entityComponent, entityData);
                    entityData = GenerateComponentData(entity, entityComponent);
                    OnEntityComponentAdding(entity, entityComponent, entityData);
                    ComponentDatas[entityComponent] = entityData;
                }
            }
        }

        /// <summary>Generates associated data to the given entity.</summary>
        /// Called right before <see cref="OnEntityComponentAdding"/>.
        /// <param name="entity">The entity.</param>
        /// <param name="component"></param>
        /// <returns>The associated data.</returns>
        protected abstract TData GenerateComponentData(Entity entity, TComponent component);

        /// <summary>Checks if the current associated data is valid, or if readding the entity is required.</summary>
        /// <param name="entity">The entity.</param>
        /// <param name="component"></param>
        /// <param name="associatedData">The associated data.</param>
        /// <returns>True if the change in associated data requires the entity to be readded, false otherwise.</returns>
        protected virtual bool IsAssociatedDataValid(Entity entity, TComponent component, TData associatedData)
        {
            return GenerateComponentData(entity, component).Equals(associatedData);
        }

        /// <summary>Run when a matching entity is added to this entity processor.</summary>
        /// <param name="entity">The entity.</param>
        /// <param name="component"></param>
        /// <param name="data">  The associated data.</param>
        protected virtual void OnEntityComponentAdding(Entity entity, TComponent component, TData data)
        {
        }

        /// <summary>Run when a matching entity is removed from this entity processor.</summary>
        /// <param name="entity">The entity.</param>
        /// <param name="component"></param>
        /// <param name="data">  The associated data.</param>
        protected virtual void OnEntityComponentRemoved(Entity entity, TComponent component, TData data)
        {
        }

        private bool EntityMatch(Entity entity)
        {
            // When a processor has no requirement components, it always match with at least the component of entity
            if (!HasRequiredComponents)
            {
                return true;
            }

            checkRequiredTypes.Clear();
            for (int i = 0; i < RequiredTypes.Length; i++)
            {
                checkRequiredTypes.Add(RequiredTypes[i]);
            }

            var components = entity.Components;
            for (int i = 0; i < components.Count; i++)
            {
                var componentType = components[i].GetType();
                for (int j = checkRequiredTypes.Count - 1; j >= 0; j--)
                {
                    if (checkRequiredTypes.Items[j].IsAssignableFrom(componentType))
                    {
                        checkRequiredTypes.RemoveAt(j);

                        if (checkRequiredTypes.Count == 0)
                        {
                            return true;
                        }
                    }
                }
            }

            // If we are here, it means that required types were not found, so return false
            return false;
        }
    }

    /// <summary>
    /// Base implementation of <see cref="EntityProcessor{TComponent,TData}"/> when the TComponent and TData are the same
    /// </summary>
    /// <typeparam name="TComponent">The main type of the component this processor is looking for.</typeparam>
    public abstract class EntityProcessor<TComponent> : EntityProcessor<TComponent, TComponent> where TComponent : EntityComponent
    {
        protected EntityProcessor(params Type[] requiredAdditionalTypes) : base(requiredAdditionalTypes)
        {
        }

        protected override bool IsAssociatedDataValid(Entity entity, TComponent component, TComponent associatedData)
        {
            return component == associatedData;
        }
    }
}