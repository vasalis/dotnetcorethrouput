# Detecting Thread Saturation on .Net Core using Application Insights

## TL;DR

1. Collect these .Net Core Event Counters under ***System.Runtime***
    1. **threadpool-thread-count** and optionally
    2. **threadpool-queue-length**
2. Configure Application Insights to collect them, [starting v 2.1.50 these are **not** on by default](https://docs.microsoft.com/en-us/azure/azure-monitor/app/eventcounters#default-counters-collected)
    1. Have a look at [Startup.cs on ConfigureServices method](https://github.com/vasalis/dotnetcorethrouput/blob/master/CoreThroughput/CoreThroughput/Startup.cs) to see how it's configured on this project
    2. also see [here](https://docs.microsoft.com/en-us/azure/azure-monitor/app/eventcounters#customizing-counters-to-be-collected) from public documentation
3. Kusto Queries
    1. Use [this Kusto Query](https://github.com/vasalis/dotnetcorethrouput/blob/master/CoreThroughput/KustoQueries/ThreadSaturationAnalysis.txt) to see how **threadpool-thread-count** versus incoming requests rate and incoming requests duration behave.
        1. ***threadpool-thread-count*** values significantly higher than **vCores * 2** indicate thread saturation
        2. The theory is that ***threadpool-thread-count*** should have values in the vicinity of **vCores * 2** for a HyperThreaded CPU and vCores for SingleThreaded CPUs. When a .Net Core app has ***threadpool-thread-count*** in that vicinity, means that is implementing the [Async code pattern](https://docs.microsoft.com/en-us/dotnet/standard/async) properly. If values are higher, it means that the code is doing ***"actual work"*** on a threadpool thread, which is against the design of .Net core's async pattern (and can lead to Thread Saturation :roll_eyes::smirk::roll_eyes:).
    2. Use [this Kusto Query](https://github.com/vasalis/dotnetcorethrouput/blob/master/CoreThroughput/KustoQueries/ThreadSaturationDetection.txt) to detect Thread Saturation
    3. Use [this Kusto Query](https://github.com/vasalis/dotnetcorethrouput/blob/master/CoreThroughput/KustoQueries/ThreadSaturationDetectionAlert.txt) to [create an alert if required](https://docs.microsoft.com/en-us/azure/azure-monitor/alerts/alerts-log)
4. Examples on what to look for
    1. ![Thread Saturation Healthy example](https://github.com/vasalis/dotnetcorethrouput/blob/master/Screenshots/ThreadSaturation_HealthyExample.jpg) This is a healthy example, as in the begging of the time window there are ~800-1000 http request/time unit with an average of 5-7 secs response time. **Thread Pool Threads count** fluctuates between 2-5, even if there is load, or not. Notice that after ~4:45 the http requests drop to 0, while Thread Poll Threads count remains the same. This means that .Net core's the Async code pattern works perfectly and handles in a consistent way the workload in hand. This is the behavior that this code sample has when load testing the [***api/Employee/Employees*** method](https://github.com/vasalis/dotnetcorethrouput/blob/master/CoreThroughput/CoreThroughput/Controllers/EmployeeController.cs).
    2. ![Thread Saturation NOT Healthy example](https://github.com/vasalis/dotnetcorethrouput/blob/master/Screenshots/ThreadSaturation_NotHealthyExample.jpg). This is a **NOT** healthy example. The http requests rate is approximately the same as the healthy example, but response time is mess (x10 more) and ***Thread Pool Threads count*** is to the roof. This is the behavior that this code sample has when load testing the [***api/Employee/EmployeesWithWait*** method](https://github.com/vasalis/dotnetcorethrouput/blob/master/CoreThroughput/CoreThroughput/Controllers/EmployeeController.cs), on purpose this method is performing work on the Thread Pool Thread and not using the Async pattern.
    3. More reading about this, on this great article [here](https://docs.microsoft.com/en-us/archive/blogs/vancem/diagnosing-net-core-threadpool-starvation-with-perfview-why-my-service-is-not-saturating-all-cores-or-seems-to-stall?WT.mc_id=DT-MVP-5003493)

## Long read

This an Asp .Net Core code sample that demonstrates how to detect Thread Saturation using Azure Application Insights.

The idea is to load test two API methods, one properly coded and one poorly coded, and using Application Insights analyse how the application behaves. The analysis and the detection is performed using these [Kusto Queries](https://github.com/vasalis/dotnetcorethrouput/blob/master/CoreThroughput/KustoQueries/).

The basic functionality resides on the [Employees Controller](https://github.com/vasalis/dotnetcorethrouput/blob/master/CoreThroughput/CoreThroughput/Controllers/EmployeeController.cs), there are two methods that execute the same (heavy) SQL Query on a Cosmos Db instance:
> ***api/Employee/Employees*** is executing the Query in an Async mode - which of course is the **proper** way and
>
> ***api/Employee/EmployeesWithWait*** that is executing on the Thread Pool Thread - which leads to Thread Saturation

### Prerequisites

1. Basic theory on the subject (.Net Core Thread Saturation), make sure you have done some basic reading especially on the below great articles
    1. [Diagnosing .NET Core ThreadPool Starvation with PerfView](https://docs.microsoft.com/en-us/archive/blogs/vancem/diagnosing-net-core-threadpool-starvation-with-perfview-why-my-service-is-not-saturating-all-cores-or-seems-to-stall?WT.mc_id=DT-MVP-5003493)
    2. [.NET ThreadPool starvation, and how queuing makes it worse](https://medium.com/criteo-engineering/net-threadpool-starvation-and-how-queuing-makes-it-worse-512c8d570527)
    3. [EventCounters introduction](https://docs.microsoft.com/en-us/azure/azure-monitor/app/eventcounters) and focus [here](https://docs.microsoft.com/en-us/azure/azure-monitor/app/eventcounters#customizing-counters-to-be-collected)
    4. [Well-known EventCounters in .NET](https://docs.microsoft.com/en-us/dotnet/core/diagnostics/available-counters)
2. Basic understanding of Application Insights and Kusto Querying Language
    1. [Application Insights for ASP.NET Core applications](https://docs.microsoft.com/en-us/azure/azure-monitor/app/asp-net-core)
    2. [Kusto Querying Language Reference](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/query/)
3. An Azure Subscription for running the sample
    1. While you can run the .Net core app and JMeter anywhere with the proper runtime, CosmosDb is used, so this requires an active Azure Subscription.
4. An environment (for example an Azure VM, your dev box etc) running JMeter
    1. [Download JMeter](https://jmeter.apache.org/download_jmeter.cgi)
    2. [JMeter Documentation](https://jmeter.apache.org/usermanual/get-started.html)

### Running the sample

1. Clone the repository
2. Create a GitHub Secret with your Azure subscription credentials
3. Edit the {TODO} file and change (if needed) the names of the resources, starting with the resource group. When committing it should auto-trigger the CI/CD pipeline
4. Run CI/CD workflow if you skipped step 3
5. Navigate to Azure resource group, locate the App Service, and make a call to <App Service URL>/WeatherForecast/SystemDetails to see that the app is working. Outcome should be similar to this: ![Sample working ok](https://github.com/vasalis/dotnetcorethrouput/blob/master/Screenshots/DotNetThrouput_Working.jpg)
6. Create 1.000 Employee Entities on the database by calling the *api/Employee/CreateNewEmployeesBatch*. To make sure that it worked call *api/Employee/Employees* (careful, it's a POST method).
7. Edit *Stress DotNetCore_BaseLine.jmx, Stress DotNetCore.jmx* [JMeter tests](https://github.com/vasalis/dotnetcorethrouput/blob/master/CoreThroughput/JMeterTests) using JMeter GUI. Change the base URL of the *Path* property of *Get Employees (Azure PaaS) and Get Employees With Wait (Azure PaaS)* Tests in order to point to your App Service. By default the *Get Employees (Azure PaaS)* test should be enabled and *Get Employees With Wait (Azure PaaS)* test disabled, if not configure accordingly.
8. Using the command line (*jmeter -n -t [jmx file]*) run the *Stress DotNetCore_BaseLine.jmx*. This will create a base line work load, just to make sure the app is working under load.
9. Navigate to your Application Insights Instance, and see the ***Live Metrics***, you should start seeing the incoming workload. Let a couple of minutes pass in order for the app to reach a stable state.
10. Do not stop running the base line test, and using again the command line, run *Stress DotNetCore.jmx*. This should stress the application.
    1. When *Get Employees (Azure PaaS)* is enabled, the *request response time* should start increasing until it reaches ~5 secs. Other than that, the app should remain responsive even if CPU is ~180%-200%.
    2. Leave it running for ~5mins (or more) so there are enough data for running KQL later on
    3. Stop running both tests. Edit on GUI both files and disable *Get Employees (Azure PaaS)* Test and enable *Get Employees With Wait (Azure PaaS)* Test. Do the same steps as before (first *Stress DotNetCore_BaseLine.jmx* test and after stabilizing *Stress DotNetCore.jmx*). This time the app should start behaving strangely and becoming unresponsive - you might even see that the server is lost from Application Insights Live Metrics. This does not mean that the app is killed, it's a possibility, but most probably the app is so stressed that Application Insights SDK is not able to send telemetry. If you let a few minutes pass, you might see telemetry data again, and possibly a request response time ~5secs. This means that the Thread Pool Thread count has increased enough in order to catch up with the incoming load. Stressing even more the app (you could run many instance of the *Stress DotNetCore.jmx* test) can result in the OS killing the app as it consumes too many system threads.
11. After running the tests, navigate to Application Insights Logs section and play around with the provided [Kusto Queries](https://github.com/vasalis/dotnetcorethrouput/blob/master/CoreThroughput/KustoQueries/), you should be able to see pictures similar to this

> ![Thread Saturation Healthy example](https://github.com/vasalis/dotnetcorethrouput/blob/master/Screenshots/ThreadSaturation_HealthyExample.jpg) as a healthy example,

and this

> ![Thread Saturation NOT Healthy example](https://github.com/vasalis/dotnetcorethrouput/blob/master/Screenshots/ThreadSaturation_NotHealthyExample.jpg) as a NOT healthy example.
