﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Elsa.Services;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Elsa.Activities.AzureServiceBus.Services
{
    public class ServiceBusTopicsStarter : IServiceBusTopicsStarter
    {
        private readonly ITopicMessageReceiverFactory _receiverFactory;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IServiceProvider _serviceProvider;
        private readonly ICollection<TopicWorker> _workers;

        public ServiceBusTopicsStarter(
            ITopicMessageReceiverFactory receiverFactory,
            IServiceScopeFactory scopeFactory,
            IServiceProvider serviceProvider)
        {
            _receiverFactory = receiverFactory;
            _scopeFactory = scopeFactory;
            _serviceProvider = serviceProvider;
            _workers = new List<TopicWorker>();
        }

        public async Task CreateWorkersAsync(CancellationToken stoppingToken)
        {
            var cancellationToken = stoppingToken;
            await DisposeExistingWorkersAsync();
            var entities = (await GetTopicSubscriptionNamesAsync(cancellationToken).ToListAsync(cancellationToken)).Distinct();

            foreach (var entity in entities)
            {
                var receiver = await _receiverFactory.GetTopicReceiverAsync(entity.topicName, entity.subscriptionName, cancellationToken);
                var worker = ActivatorUtilities.CreateInstance<TopicWorker>(_serviceProvider, receiver, (Func<IReceiverClient, Task>) DisposeReceiverAsync);
                _workers.Add(worker);
            }
        }

        private async Task DisposeExistingWorkersAsync()
        {
            foreach (var worker in _workers.ToList())
            {
                await worker.DisposeAsync();
                _workers.Remove(worker);
            }
        }

        private async Task DisposeReceiverAsync(IReceiverClient messageReceiver) => await _receiverFactory.DisposeReceiverAsync(messageReceiver);

        private async IAsyncEnumerable<(string topicName, string subscriptionName)> GetTopicSubscriptionNamesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var workflowRegistry = scope.ServiceProvider.GetRequiredService<IWorkflowRegistry>();
            var workflowBlueprintReflector = scope.ServiceProvider.GetRequiredService<IWorkflowBlueprintReflector>();
            var workflows = await workflowRegistry.ListAsync(cancellationToken);

            var query =
                from workflow in workflows
                from activity in workflow.Activities
                where activity.Type == nameof(AzureServiceBusTopicMessageReceived)
                select workflow;

            foreach (var workflow in query)
            {
                var workflowBlueprintWrapper = await workflowBlueprintReflector.ReflectAsync(scope.ServiceProvider, workflow, cancellationToken);

                foreach (var activity in workflowBlueprintWrapper.Filter<AzureServiceBusTopicMessageReceived>())
                {
                    var topicName = await activity.GetPropertyValueAsync(x => x.TopicName, cancellationToken);
                    var subscriptionName = await activity.GetPropertyValueAsync(x => x.SubscriptionName, cancellationToken);
                    yield return (topicName, subscriptionName)!;
                }
            }
        }
    }
}