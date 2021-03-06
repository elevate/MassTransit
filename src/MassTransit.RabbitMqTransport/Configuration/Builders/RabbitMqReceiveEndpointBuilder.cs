﻿// Copyright 2007-2015 Chris Patterson, Dru Sellers, Travis Smith, et. al.
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
namespace MassTransit.RabbitMqTransport.Configuration.Builders
{
    using System.Collections.Generic;
    using MassTransit.Pipeline;
    using Topology;
    using Transports;


    public class RabbitMqReceiveEndpointBuilder :
        IReceiveEndpointBuilder
    {
        readonly IConsumePipe _consumePipe;
        readonly List<ExchangeBindingSettings> _exchangeBindings;
        readonly IMessageNameFormatter _messageNameFormatter;

        public RabbitMqReceiveEndpointBuilder(IConsumePipe consumePipe, IMessageNameFormatter messageNameFormatter)
        {
            _consumePipe = consumePipe;
            _messageNameFormatter = messageNameFormatter;
            _exchangeBindings = new List<ExchangeBindingSettings>();
        }

        ConnectHandle IConsumePipeConnector.ConnectConsumePipe<T>(IPipe<ConsumeContext<T>> pipe)
        {
            _exchangeBindings.Add(typeof(T).GetExchangeBinding(_messageNameFormatter));

            return _consumePipe.ConnectConsumePipe(pipe);
        }

        ConnectHandle IConsumeMessageObserverConnector.ConnectConsumeMessageObserver<T>(IConsumeMessageObserver<T> observer)
        {
            return _consumePipe.ConnectConsumeMessageObserver(observer);
        }

        public IEnumerable<ExchangeBindingSettings> GetExchangeBindings()
        {
            return _exchangeBindings;
        }
    }
}