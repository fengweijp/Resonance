﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.PlatformAbstractions;
using Resonance.Models;
using Resonance.Repo;
using Resonance.Repo.Database;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Resonance.Demo
{
    public class Program
    {
        private const int WORKER_COUNT = 5; // Multiple parallel workers, to make sure any issues related to parallellisation occur, if any.

        private static IServiceProvider serviceProvider;

        #region Payloads
        private static string payload100 = "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789";
        private static string payload2000 = "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789" +
            "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789" +
            "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789" +
            "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789" +
            "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789" +
            "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789" +
            "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789" +
            "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789" +
            "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789" +
            "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789" +
            "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789" +
            "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789" +
            "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789" +
            "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789" +
            "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789" +
            "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789" +
            "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789" +
            "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789" +
            "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789" +
            "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789";
        #endregion

        public static void Main(string[] args)
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            serviceProvider = serviceCollection.BuildServiceProvider();

            var publisher = serviceProvider.GetRequiredService<IEventPublisher>();
            var consumer = serviceProvider.GetRequiredService<IEventConsumer>();

            // Make sure the topic exists
            var topic1 = publisher.GetTopicByName("Demo Topic 1") ?? publisher.AddOrUpdateTopic(new Topic { Name = "Demo Topic 1" });
            var subscription1 = consumer.GetSubscriptionByName("Demo Subscription 1");
            if (subscription1 == null)
                subscription1 = consumer.AddOrUpdateSubscription(new Subscription
                {
                    Name = "Demo Subscription 1",
                    MaxDeliveries = 2,
                    Ordered = true,
                    TopicSubscriptions = new List<TopicSubscription>
                    {
                        new TopicSubscription
                        {
                            TopicId = topic1.Id.Value, Enabled = true,
                        },
                    },
                });

            if (1 == 1) // Change to enable/disable the adding of data to the subscription
            {
                var sw = new Stopwatch();
                sw.Start();

                var arrLen = 500;
                var nrs = new List<int>(arrLen);
                for (int i = 0; i < arrLen; i++) { nrs.Add(i); };

                nrs.AsParallel().ForAll((i) => 
                //Task.WhenAll(nrs.Select(async (i) => 

                Task.Run(async () => // Threadpool task to wait for async parts in inner task
                {
                    Console.WriteLine($"Run {i:D4} - Start  [{Thread.CurrentThread.ManagedThreadId}]");
                    for (int fk = 1; fk <= 100; fk++) // 1000 different functional keys, 4 TopicEvents per fk
                    {
                        //await Task.Delay(1).ConfigureAwait(false);
                        var fkAsString = fk.ToString();
                        await publisher.PublishAsync(topic1.Name, functionalKey: fkAsString, payload: payload100);
                    }
                    Console.WriteLine($"Run {i:D4} - Finish [{Thread.CurrentThread.ManagedThreadId}]");
                }

                ).GetAwaiter().GetResult() // Block until all async work is done (ForAll does not await on Task as result-type)
                //)

                );
                sw.Stop();
                Console.WriteLine($"Total time for publishing: {sw.Elapsed.TotalSeconds} sec");
            }

            //var ce = consumer.ConsumeNext(subscription1.Name).FirstOrDefault();
            //if (ce != null)
            //    consumer.MarkConsumed(ce.Id, ce.DeliveryKey);

            var workers = new EventConsumptionWorker[WORKER_COUNT];
            for (int i = 0; i < WORKER_COUNT; i++)
            {
                workers[i] = CreateWorker(consumer, "Demo Subscription 1");
                workers[i].Start();
            }
            Console.WriteLine("Press a key to stop the worker(s)...");
            Console.ReadKey();
            for (int i = 0; i < WORKER_COUNT; i++)
            {
                if (workers[i].IsRunning())
                    workers[i].Stop();
            }

            //consumer.DeleteSubscription(subscription1.Id);
            //publisher.DeleteTopic(topic1.Id, true);
        }

        public static EventConsumptionWorker CreateWorker(IEventConsumer consumer, string subscriptionName)
        {
            var worker = new EventConsumptionWorker(
                eventConsumer: consumer,
                subscriptionName: subscriptionName,
                consumeAction: async (ceW) =>
                {
                    Console.WriteLine($"Consumed {ceW.Id} from thread {System.Threading.Thread.CurrentThread.ManagedThreadId}.");
                    //var delayMs = DateTime.UtcNow.Second * 10;
                    //Thread.Sleep(delayMs); // Different processing time on every run
                    await Task.Delay(1);
                    if (DateTime.UtcNow.Millisecond == 1)
                        throw new Exception("Sorry");

                    return ConsumeResult.Succeeded;
                },
                logger: serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<EventConsumptionWorker>()
                );
            return worker;
        }

        private static void ConfigureServices(IServiceCollection serviceCollection)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(PlatformServices.Default.Application.ApplicationBasePath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            //builder.AddEnvironmentVariables();
            var config = builder.Build();

            // Add IConfiguration dependency (reason: allows access to config from any injected component)
            serviceCollection.AddSingleton<IConfiguration>(config);

            ILoggerFactory loggerFactory = new LoggerFactory()
                .AddConsole(LogLevel.Warning)
                .AddDebug(LogLevel.Trace);
            serviceCollection.AddSingleton<ILoggerFactory>(loggerFactory);

            // Configure IEventingRepoFactory dependency (reason: the repo that must be used in this app)

            // To use MSSQLServer:
            //var connectionString = config.GetConnectionString("Resonance.MsSql");
            //serviceCollection.AddTransient<IEventingRepoFactory>((p) =>
            //{
            //    return new MsSqlEventingRepoFactory(connectionString);
            //});

            // To use MySQL:
            var connectionString = config.GetConnectionString("Resonance.MySql");
            serviceCollection.AddTransient<IEventingRepoFactory>((p) =>
            {
                return new MySqlEventingRepoFactory(connectionString);
            });

            // Configure EventPublisher
            serviceCollection.AddTransient<IEventPublisher, EventPublisher>();

            // Configure EventConsumer
            serviceCollection.AddTransient<IEventConsumer, EventConsumer>();
        }
    }
}
