// Copyright 2007-2015 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace MassTransit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Castle.MicroKernel;
    using Castle.Windsor;
    using ConsumeConfigurators;
    using Internals.Extensions;
    using Saga;
    using Saga.SubscriptionConfigurators;
    using WindsorIntegration;


    /// <summary>
    /// Extension methods for the Windsor IoC container.
    /// </summary>
    public static class WindsorContainerExtensions
    {
        /// <summary>
        /// Specify that the service bus should load its subscribers from the container passed as an argument.
        /// </summary>
        /// <param name="configurator">The configurator the extension method works on.</param>
        /// <param name="container">The Windsor container.</param>
        public static void LoadFrom(this IReceiveEndpointConfigurator configurator, IWindsorContainer container)
        {
            if (container == null)
                throw new ArgumentNullException("container");

            LoadFrom(configurator, container.Kernel);
        }

        /// <summary>
        /// Specify that the service bus should load its subscribers from the container passed as an argument.
        /// </summary>
        /// <param name="configurator">The configurator the extension method works on.</param>
        /// <param name="container">The Windsor container.</param>
        public static void LoadFrom(this IReceiveEndpointConfigurator configurator, IKernel container)
        {
            if (configurator == null)
                throw new ArgumentNullException("configurator");
            if (container == null)
                throw new ArgumentNullException("container");

            IList<Type> consumerTypes = FindTypes<IConsumer>(container, x => !x.HasInterface<ISaga>());
            if (consumerTypes.Count > 0)
            {
                foreach (Type type in consumerTypes)
                    ConsumerConfiguratorCache.Configure(type, configurator, container);
            }

            IList<Type> sagaTypes = FindTypes<ISaga>(container, x => true);
            if (sagaTypes.Count > 0)
            {
                foreach (Type sagaType in sagaTypes)
                    SagaConfiguratorCache.Configure(sagaType, configurator, container);
            }
        }

        /// <summary>
        /// Register the type as a type to load from the container as a consumer.
        /// </summary>
        /// <typeparam name="T">The type of the consumer that consumes messages</typeparam>
        /// <param name="configurator">configurator</param>
        /// <param name="container">The container that the consumer should be loaded from.</param>
        /// <param name="configure"></param>
        /// <returns>The configurator</returns>
        public static void Consumer<T>(this IReceiveEndpointConfigurator configurator, IKernel container, Action<IConsumerConfigurator<T>> configure = null)
            where T : class, IConsumer
        {
            if (configurator == null)
                throw new ArgumentNullException("configurator");
            if (container == null)
                throw new ArgumentNullException("container");

            var consumerFactory = new WindsorConsumerFactory<T>(container);

            configurator.Consumer(consumerFactory, configure);
        }

        /// <summary>
        /// Load the saga of the generic type from the windsor container,
        /// by loading it directly from the container.
        /// </summary>
        /// <typeparam name="T">The type of the saga</typeparam>
        /// <param name="configurator">The configurator</param>
        /// <param name="container">The windsor container</param>
        /// <param name="configure"></param>
        /// <returns>The configurator</returns>
        public static void Saga<T>(this IReceiveEndpointConfigurator configurator, IKernel container, Action<ISagaConfigurator<T>> configure = null)
            where T : class, ISaga
        {
            if (configurator == null)
                throw new ArgumentNullException("configurator");
            if (container == null)
                throw new ArgumentNullException("container");

            var sagaRepository = container.Resolve<ISagaRepository<T>>();

            var windsorSagaRepository = new WindsorSagaRepository<T>(sagaRepository, container);

            configurator.Saga(windsorSagaRepository, configure);
        }

        /// <summary>
        /// Enables message scope lifetime for windsor containers
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="configurator"></param>
        public static void EnableMessageScope<T>(this IPipeConfigurator<T> configurator)
            where T : class, PipeContext
        {
            if (configurator == null)
                throw new ArgumentNullException("configurator");

            var pipeBuilderConfigurator = new WindsorMessageScopePipeSpecification<T>();

            configurator.AddPipeSpecification(pipeBuilderConfigurator);
        }

        static IList<Type> FindTypes<T>(IKernel container, Func<Type, bool> filter)
        {
            return container
                .GetAssignableHandlers(typeof(T))
                .Select(h => h.ComponentModel.Implementation)
                .Where(filter)
                .ToList();
        }
    }
}