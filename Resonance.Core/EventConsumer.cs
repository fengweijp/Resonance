﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Resonance.Models;
using Newtonsoft.Json;
using Resonance.Repo;

namespace Resonance
{
    public class EventConsumer : IEventConsumer
    {
        private IEventingRepoFactory _repoFactory;

        public EventConsumer(IEventingRepoFactory repoFactory)
        {
            _repoFactory = repoFactory;
        }

        #region Sync

        public IEnumerable<ConsumableEvent> ConsumeNext(string subscriptionName, int visibilityTimeout = 120, int maxCount = 1)
        {
            return ConsumeNextAsync(subscriptionName, visibilityTimeout, maxCount).GetAwaiter().GetResult();
        }

        public IEnumerable<ConsumableEvent<T>> ConsumeNext<T>(string subscriptionName, int visibilityTimeout = 120, int maxCount = 1)
        {
            return ConsumeNextAsync<T>(subscriptionName, visibilityTimeout, maxCount).GetAwaiter().GetResult();
        }

        public void MarkConsumed(long id, string deliveryKey)
        {
            MarkConsumedAsync(id, deliveryKey).GetAwaiter().GetResult();
        }

        public void MarkFailed(long id, string deliveryKey, Reason reason)
        {
            MarkFailedAsync(id, deliveryKey, reason).GetAwaiter().GetResult();
        }

        public IEnumerable<Subscription> GetSubscriptions(long? topicId = default(long?))
        {
            return GetSubscriptionsAsync(topicId).GetAwaiter().GetResult();
        }

        public Subscription GetSubscription(long id)
        {
            return GetSubscriptionAsync(id).GetAwaiter().GetResult();
        }

        public Subscription GetSubscriptionByName(string name)
        {
            return GetSubscriptionByNameAsync(name).GetAwaiter().GetResult();
        }

        public Subscription AddOrUpdateSubscription(Subscription subscription)
        {
            return AddOrUpdateSubscriptionAsync(subscription).GetAwaiter().GetResult();
        }

        public void DeleteSubscription(long id)
        {
            DeleteSubscriptionAsync(id).GetAwaiter().GetResult();
        }
        #endregion

        #region Async
        public async Task<Subscription> AddOrUpdateSubscriptionAsync(Subscription subscription)
        {
            using (var repo = _repoFactory.CreateRepo())
                return await repo.AddOrUpdateSubscription(subscription).ConfigureAwait(false);
        }

        public async Task DeleteSubscriptionAsync(Int64 id)
        {
            using (var repo = _repoFactory.CreateRepo())
                await repo.DeleteSubscription(id).ConfigureAwait(false);
        }

        public async Task<Subscription> GetSubscriptionAsync(Int64 id)
        {
            using (var repo = _repoFactory.CreateRepo())
                return await repo.GetSubscription(id).ConfigureAwait(false);
        }

        public async Task<Subscription> GetSubscriptionByNameAsync(string name)
        {
            using (var repo = _repoFactory.CreateRepo())
                return await repo.GetSubscriptionByName(name).ConfigureAwait(false);
        }

        public async Task<IEnumerable<Subscription>> GetSubscriptionsAsync(Int64? topicId = null)
        {
            using (var repo = _repoFactory.CreateRepo())
                return await repo.GetSubscriptions(topicId).ConfigureAwait(false);
        }

        public async Task<IEnumerable<ConsumableEvent>> ConsumeNextAsync(string subscriptionName, int visibilityTimeout = 120, int maxCount = 1)
        {
            using (var repo = _repoFactory.CreateRepo())
                return await repo.ConsumeNext(subscriptionName, visibilityTimeout, maxCount).ConfigureAwait(false);
        }

        public async Task<IEnumerable<ConsumableEvent<T>>> ConsumeNextAsync<T>(string subscriptionName, int visibilityTimeout = 120, int maxCount = 1)
        {
            var ces = new List<ConsumableEvent<T>>();

            foreach (var ce in await ConsumeNextAsync(subscriptionName, visibilityTimeout, maxCount).ConfigureAwait(false))
            {
                // Deserialize the payload
                T payloadAsObject = JsonConvert.DeserializeObject<T>(ce.Payload);

                ces.Add(new ConsumableEvent<T>
                {
                    Id = ce.Id,
                    FunctionalKey = ce.FunctionalKey,
                    DeliveryKey = ce.DeliveryKey,
                    InvisibleUntilUtc = ce.InvisibleUntilUtc,
                    Payload = payloadAsObject,
                });
            }

            return ces;
        }

        public async Task MarkConsumedAsync(Int64 id, string deliveryKey)
        {
            using (var repo = _repoFactory.CreateRepo())
                await repo.MarkConsumed(id, deliveryKey).ConfigureAwait(false);
        }

        public async Task MarkFailedAsync(Int64 id, string deliveryKey, Reason reason)
        {
            using (var repo = _repoFactory.CreateRepo())
                await repo.MarkFailed(id, deliveryKey, reason).ConfigureAwait(false);
        }
        #endregion
    }
}
