
using DailyPost.BackgroundWorker;
using Quartz;

namespace DailyPost.Api
{
    public static class ServiceExtention
    {
        public static IServiceCollection AddQuartzScheduler(this IServiceCollection services, IConfiguration configuration)
        {
            string backgroundJobInterval = configuration.GetValue<string>("Quartz:BackgroundJobInterVal");

            services.AddQuartz(q =>
            {
                q.UseMicrosoftDependencyInjectionJobFactory();

                var jobKey = new JobKey("Worker");
                q.AddJob<Worker>(opts => opts.WithIdentity(jobKey));

                q.AddTrigger(opts => opts
                    .ForJob(jobKey)
                    .WithIdentity("BackgroundJob-trigger")
                    .WithCronSchedule(backgroundJobInterval));
            });

            services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

            return services;
        }
    }
}
