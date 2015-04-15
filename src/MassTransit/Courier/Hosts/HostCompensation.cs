// Copyright 2007-2014 Chris Patterson, Dru Sellers, Travis Smith, et. al.
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
namespace MassTransit.Courier.Hosts
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts;
    using Exceptions;
    using Pipeline;


    public class HostCompensation<TLog> :
        Compensation<TLog>
        where TLog : class
    {
        readonly ActivityLog _activityLog;
        readonly CompensateLog _compensateLog;
        readonly ConsumeContext<RoutingSlip> _context;
        readonly TLog _data;
        readonly HostInfo _host;
        readonly SanitizedRoutingSlip _routingSlip;
        readonly DateTime _startTimestamp;
        readonly Stopwatch _timer;

        public HostCompensation(HostInfo host, ConsumeContext<RoutingSlip> context)
        {
            _host = host;
            _context = context;

            _timer = Stopwatch.StartNew();
            _startTimestamp = DateTime.UtcNow;

            _routingSlip = new SanitizedRoutingSlip(context);
            if (_routingSlip.CompensateLogs.Count == 0)
                throw new ArgumentException("The routingSlip must contain at least one activity log");

            _compensateLog = _routingSlip.CompensateLogs.Last();

            _activityLog = _routingSlip.ActivityLogs.SingleOrDefault(x => x.ExecutionId == _compensateLog.ExecutionId);
            if (_activityLog == null)
            {
                throw new RoutingSlipException("The compensation log did not have a matching activity log entry: "
                    + _compensateLog.ExecutionId);
            }

            _data = _routingSlip.GetCompensateLogData<TLog>();
        }

        Task IPublishEndpoint.Publish<T>(T message, CancellationToken cancellationToken = new CancellationToken())
        {
            return _context.Publish(message, cancellationToken);
        }

        Task IPublishEndpoint.Publish<T>(T message, IPipe<PublishContext<T>> publishPipe,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return _context.Publish(message, publishPipe, cancellationToken);
        }

        Task IPublishEndpoint.Publish<T>(T message, IPipe<PublishContext> publishPipe,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return _context.Publish(message, publishPipe, cancellationToken);
        }

        Task IPublishEndpoint.Publish(object message, CancellationToken cancellationToken = new CancellationToken())
        {
            return _context.Publish(message, cancellationToken);
        }

        Task IPublishEndpoint.Publish(object message, IPipe<PublishContext> publishPipe,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return _context.Publish(message, publishPipe, cancellationToken);
        }

        Task IPublishEndpoint.Publish(object message, Type messageType, CancellationToken cancellationToken = new CancellationToken())
        {
            return _context.Publish(message, messageType, cancellationToken);
        }

        Task IPublishEndpoint.Publish(object message, Type messageType, IPipe<PublishContext> publishPipe,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return _context.Publish(message, messageType, publishPipe, cancellationToken);
        }

        Task IPublishEndpoint.Publish<T>(object values, CancellationToken cancellationToken = new CancellationToken())
        {
            return _context.Publish<T>(values, cancellationToken);
        }

        Task IPublishEndpoint.Publish<T>(object values, IPipe<PublishContext<T>> publishPipe,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return _context.Publish(values, publishPipe, cancellationToken);
        }

        Task IPublishEndpoint.Publish<T>(object values, IPipe<PublishContext> publishPipe,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return _context.Publish<T>(values, publishPipe, cancellationToken);
        }

        TLog Compensation<TLog>.Log
        {
            get { return _data; }
        }

        Guid Compensation<TLog>.TrackingNumber
        {
            get { return _routingSlip.TrackingNumber; }
        }

        public HostInfo Host
        {
            get { return _host; }
        }

        public DateTime StartTimestamp
        {
            get { return _startTimestamp; }
        }

        public TimeSpan ElapsedTime
        {
            get { return _timer.Elapsed; }
        }

        public ConsumeContext ConsumeContext
        {
            get { return _context; }
        }

        public string ActivityName
        {
            get { return _activityLog.Name; }
        }

        public Guid ActivityTrackingNumber
        {
            get { return _activityLog.ExecutionId; }
        }

        CompensationResult Compensation<TLog>.Compensated()
        {
            return new CompensatedCompensationResult<TLog>(this, _compensateLog, _routingSlip);
        }

        CompensationResult Compensation<TLog>.Compensated(object values)
        {
            if (values == null)
                throw new ArgumentNullException("values");

            return new CompensatedWithVariablesCompensationResult<TLog>(this, _compensateLog, _routingSlip,
                RoutingSlipBuilder.GetObjectAsDictionary(values));
        }

        CompensationResult Compensation<TLog>.Compensated(IDictionary<string, object> variables)
        {
            if (variables == null)
                throw new ArgumentNullException("variables");

            return new CompensatedWithVariablesCompensationResult<TLog>(this, _compensateLog, _routingSlip, variables);
        }

        CompensationResult Compensation<TLog>.Failed()
        {
            var exception = new RoutingSlipException("The routing slip compensation failed");

            return new FailedCompensationResult<TLog>(this, _compensateLog, _routingSlip, exception);
        }

        CompensationResult Compensation<TLog>.Failed(Exception exception)
        {
            return new FailedCompensationResult<TLog>(this, _compensateLog, _routingSlip, exception);
        }

        public Task<ISendEndpoint> GetSendEndpoint(Uri address)
        {
            // TODO intercept to ensure SourceAddress is right, but it should be based on the InputAddress
            return _context.GetSendEndpoint(address);
        }
    }
}