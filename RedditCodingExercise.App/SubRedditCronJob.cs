using Microsoft.Extensions.Options;
using RandomSkunk.Hosting.Cron;

namespace RedditCodingExercise.App;

public class SubRedditCronJob(
    SubRedditListener subRedditListener,
    IOptionsMonitor<CronJobOptions> cronJobOptions,
    ILogger<SubRedditCronJob> logger)
    : CronJob(cronJobOptions, logger)
{
    private readonly SubRedditListener _subRedditListener = subRedditListener;

    protected override Task DoWork(CancellationToken cancellationToken) =>
        _subRedditListener.SynchronizePostsAsync(cancellationToken);
}
