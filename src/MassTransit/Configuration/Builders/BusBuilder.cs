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
namespace MassTransit.Builders
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Mime;
    using BusConfigurators;
    using Pipeline;
    using Serialization;
    using Transports;
    using Util;


    public abstract class BusBuilder
    {
        readonly BusObservable _busObservable;
        readonly Lazy<IConsumePipe> _consumePipe;
        readonly IConsumePipeSpecification _consumePipeSpecification;
        readonly Lazy<IMessageDeserializer> _deserializer;
        readonly IDictionary<string, DeserializerFactory> _deserializerFactories;
        readonly IBusHostControl[] _hosts;
        readonly Lazy<Uri> _inputAddress;
        readonly Lazy<IPublishEndpointProvider> _publishSendEndpointProvider;
        readonly IDictionary<string, IReceiveEndpoint> _receiveEndpoints;
        readonly Lazy<ISendEndpointProvider> _sendEndpointProvider;
        readonly Lazy<ISendTransportProvider> _sendTransportProvider;
        readonly Lazy<IMessageSerializer> _serializer;
        Func<IMessageSerializer> _serializerFactory;

        protected BusBuilder(IConsumePipeSpecification consumePipeSpecification, IEnumerable<IBusHostControl> hosts)
        {
            _consumePipeSpecification = consumePipeSpecification;
            _deserializerFactories = new Dictionary<string, DeserializerFactory>(StringComparer.OrdinalIgnoreCase);
            _receiveEndpoints = new Dictionary<string, IReceiveEndpoint>();
            _serializerFactory = () => new JsonMessageSerializer();
            _busObservable = new BusObservable();
            _serializer = new Lazy<IMessageSerializer>(CreateSerializer);
            _deserializer = new Lazy<IMessageDeserializer>(CreateDeserializer);
            _sendTransportProvider = new Lazy<ISendTransportProvider>(CreateSendTransportProvider);
            _sendEndpointProvider = new Lazy<ISendEndpointProvider>(CreateSendEndpointProvider);
            _publishSendEndpointProvider = new Lazy<IPublishEndpointProvider>(CreatePublishSendEndpointProvider);

            _inputAddress = new Lazy<Uri>(GetInputAddress);
            _consumePipe = new Lazy<IConsumePipe>(GetConsumePipe);
            _hosts = hosts.ToArray();

            AddMessageDeserializer(JsonMessageSerializer.JsonContentType,
                (s, p) => new JsonMessageDeserializer(JsonMessageSerializer.Deserializer, s, p));
            AddMessageDeserializer(BsonMessageSerializer.BsonContentType,
                (s, p) => new BsonMessageDeserializer(BsonMessageSerializer.Deserializer, s, p));
            AddMessageDeserializer(XmlMessageSerializer.XmlContentType,
                (s, p) => new XmlMessageDeserializer(JsonMessageSerializer.Deserializer, s, p));
        }

        protected BusObservable BusObservable => _busObservable;

        protected IEnumerable<IReceiveEndpoint> ReceiveEndpoints => _receiveEndpoints.Values;

        public IMessageSerializer MessageSerializer => _serializer.Value;

        public IMessageDeserializer MessageDeserializer => _deserializer.Value;

        protected ISendEndpointProvider SendEndpointProvider => _sendEndpointProvider.Value;

        public ISendTransportProvider SendTransportProvider => _sendTransportProvider.Value;

        public IPublishEndpointProvider PublishEndpoint => _publishSendEndpointProvider.Value;

        protected Uri InputAddress => _inputAddress.Value;

        protected IConsumePipe ConsumePipe => _consumePipe.Value;

        protected abstract Uri GetInputAddress();
        protected abstract IConsumePipe GetConsumePipe();

        public void AddMessageDeserializer(ContentType contentType, DeserializerFactory deserializerFactory)
        {
            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));
            if (deserializerFactory == null)
                throw new ArgumentNullException(nameof(deserializerFactory));

            if (_deserializer.IsValueCreated)
                throw new ConfigurationException("The deserializer has already been created, no additional deserializers can be added.");

            if (_deserializerFactories.ContainsKey(contentType.MediaType))
                return;

            _deserializerFactories[contentType.MediaType] = deserializerFactory;
        }

        public void SetMessageSerializer(Func<IMessageSerializer> serializerFactory)
        {
            if (serializerFactory == null)
                throw new ArgumentNullException(nameof(serializerFactory));

            if (_serializer.IsValueCreated)
                throw new ConfigurationException("The serializer has already been created, the serializer cannot be changed at this time.");

            _serializerFactory = serializerFactory;
        }

        public IConsumePipe CreateConsumePipe(params IConsumePipeSpecification[] specifications)
        {
            var builder = new ConsumePipeBuilder();

            _consumePipeSpecification.Apply(builder);

            for (int i = 0; i < specifications.Length; i++)
                specifications[i].Apply(builder);

            return builder.Build();
        }

        IMessageSerializer CreateSerializer()
        {
            return _serializerFactory();
        }

        IMessageDeserializer CreateDeserializer()
        {
            IMessageDeserializer[] deserializers =
                _deserializerFactories.Values.Select(x => x(SendEndpointProvider, PublishEndpoint)).ToArray();

            return new SupportedMessageDeserializers(deserializers);
        }

        public void AddReceiveEndpoint(string endpointKey, IReceiveEndpoint receiveEndpoint)
        {
            if (endpointKey == null)
                throw new ArgumentNullException(nameof(endpointKey));

            if (_receiveEndpoints.ContainsKey(endpointKey))
                throw new ConfigurationException("A receive endpoint with the same key was already added: " + endpointKey);

            _receiveEndpoints.Add(endpointKey, receiveEndpoint);
        }

        public ConnectHandle ConnectBusObserver(IBusObserver observer)
        {
            return _busObservable.Connect(observer);
        }

        public IBusControl Build()
        {
            try
            {
                PreBuild();

                var bus = new MassTransitBus(InputAddress, ConsumePipe, SendEndpointProvider, PublishEndpoint, ReceiveEndpoints, _hosts, BusObservable);

                TaskUtil.Await(() => _busObservable.PostCreate(bus));

                return bus;
            }
            catch (Exception exception)
            {
                TaskUtil.Await(() => BusObservable.CreateFaulted(exception));

                throw;
            }
        }

        protected virtual void PreBuild()
        {
        }

        protected abstract ISendTransportProvider CreateSendTransportProvider();

        protected abstract ISendEndpointProvider CreateSendEndpointProvider();

        protected abstract IPublishEndpointProvider CreatePublishSendEndpointProvider();
    }
}