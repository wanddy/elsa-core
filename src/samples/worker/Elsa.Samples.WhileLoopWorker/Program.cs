using Elsa.Samples.WhileLoopWorker.Services;
using Elsa.Samples.WhileLoopWorker.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Elsa.Samples.WhileLoopWorker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices(
                    (hostContext, services) =>
                    {
                        services
                            .AddElsa()
                            .AddConsoleActivities()
                            .AddTimerActivities()
                            .AddSingleton<PhoneCallService>()
                            .AddHostedService<PhoneCallWorker>()
                            .AddWorkflow<PhoneCallWorkflow>();
                    });
    }
}