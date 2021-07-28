using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.EventCounterCollector;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CoreThroughput
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddSingleton<Container>(GetContainer);
            services.AddApplicationInsightsTelemetry(Configuration["APPINSIGHTS_CONNECTIONSTRING"]);
            
            services.AddServiceProfiler();

            services.ConfigureTelemetryModule<EventCounterCollectionModule>(
            (module, o) =>
            {
                // This removes all default counters, if any.
                module.Counters.Clear();
                // Add Kestrel Performance Counters
                // https://github.com/dotnet/aspnetcore/blob/main/src/Servers/Kestrel/Core/src/Internal/Infrastructure/KestrelEventSource.cs

                var lKestrelNamespace = "Microsoft-AspNetCore-Server-Kestrel";
                module.Counters.Add(new EventCounterCollectionRequest(lKestrelNamespace, "connection-queue-length"));
                module.Counters.Add(new EventCounterCollectionRequest(lKestrelNamespace, "connections-per-second"));
                module.Counters.Add(new EventCounterCollectionRequest(lKestrelNamespace, "current-connections"));
                module.Counters.Add(new EventCounterCollectionRequest(lKestrelNamespace, "request-queue-length"));
                module.Counters.Add(new EventCounterCollectionRequest(lKestrelNamespace, "total-connections"));

                // Microsoft.AspNetCore.Http.Connections
                module.Counters.Add(new EventCounterCollectionRequest("Microsoft.AspNetCore.Http.Connections", "connections-duration"));
                module.Counters.Add(new EventCounterCollectionRequest("Microsoft.AspNetCore.Http.Connections", "current-connections"));
                module.Counters.Add(new EventCounterCollectionRequest("Microsoft.AspNetCore.Http.Connections", "connections-started"));

                // Microsoft.AspNetCore.Hosting
                module.Counters.Add(new EventCounterCollectionRequest("Microsoft.AspNetCore.Hosting", "current-requests"));
                module.Counters.Add(new EventCounterCollectionRequest("Microsoft.AspNetCore.Hosting", "failed-requests"));
                module.Counters.Add(new EventCounterCollectionRequest("Microsoft.AspNetCore.Hosting", "requests-per-second"));
                module.Counters.Add(new EventCounterCollectionRequest("Microsoft.AspNetCore.Hosting", "total-requests"));

                // System.Runtime
                module.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "cpu-usage"));
                module.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "threadpool-queue-length"));
                module.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "threadpool-thread-count"));

                // System.Net.Http
                module.Counters.Add(new EventCounterCollectionRequest("System.Net.Http", "requests-started"));
                module.Counters.Add(new EventCounterCollectionRequest("System.Net.Http", "requests-started-rate"));
                module.Counters.Add(new EventCounterCollectionRequest("System.Net.Http", "current-requests"));
                module.Counters.Add(new EventCounterCollectionRequest("System.Net.Http", "http11-requests-queue-duration"));
                module.Counters.Add(new EventCounterCollectionRequest("System.Net.Http", "http20-requests-queue-duration"));

                //Microsoft - Windows - DotNETRuntime
                module.Counters.Add(new EventCounterCollectionRequest("Microsoft-Windows-DotNETRuntime", "ThreadPoolWorkerThreadStart"));
                module.Counters.Add(new EventCounterCollectionRequest("Microsoft-Windows-DotNETRuntime", "ThreadPoolWorkerThreadStop"));
                module.Counters.Add(new EventCounterCollectionRequest("Microsoft-Windows-DotNETRuntime", "ThreadPoolWorkerThreadAdjustmentSample"));
            }
        );
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
                
        private Container GetContainer(IServiceProvider options)
        {
            try
            {
                string lDatePrefix = $"_{DateTime.Now.Year}_{DateTime.Now.Month}_{DateTime.Now.Day}";

                var lConnectionString = Configuration["CosmosDb:CosmosConnectionString"];
                var lCosmosDbName = Configuration["CosmosDb:CosmosDbName"];
                var lCosmosDbContainerName = $"{Configuration["CosmosDb:CosmosDbContainerName"]}{lDatePrefix}";
                var lCosmosDbPartionKey = Configuration["CosmosDb:CosmosDbPartitionKey"];

                var lClient = new CosmosClient(lConnectionString, new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Direct
                });

                // Autoscale throughput settings
                // Set autoscale max RU/s
                // WARNING: Be aware of MAX RU!!!
                ThroughputProperties lAutoscaleThroughputProperties = ThroughputProperties.CreateAutoscaleThroughput(100000);

                //Create the database with autoscale enabled
                lClient.CreateDatabaseIfNotExistsAsync(lCosmosDbName, throughputProperties: lAutoscaleThroughputProperties).Wait();
                var lDb = lClient.GetDatabase(lCosmosDbName);

                ContainerProperties lAutoscaleContainerProperties = new ContainerProperties(lCosmosDbContainerName, lCosmosDbPartionKey);
                lDb.CreateContainerIfNotExistsAsync(lAutoscaleContainerProperties, lAutoscaleThroughputProperties);

                return lDb.GetContainer(lCosmosDbContainerName);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
