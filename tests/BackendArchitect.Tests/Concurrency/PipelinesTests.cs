using BackendArchitect.Concurrency.Pipelines;

namespace BackendArchitect.Tests.Concurrency;

public class PipelinesTests
{
    private static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan Slow = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan Arrival = TimeSpan.FromMilliseconds(8);

    private static StageSpec[] Stages(int priceWorkers) =>
    [
        new("validate", 1, Fast),
        new("price", priceWorkers, Slow),
        new("store", 1, TimeSpan.FromMilliseconds(5))
    ];

    [Fact]
    public async Task Every_order_reaches_the_end_of_the_pipeline()
    {
        var run = await OrderPipeline.RunAsync(40, Stages(priceWorkers: 2), capacity: 32, arrivalInterval: Arrival);

        Assert.Equal(40, run.Processed);
    }

    [Fact]
    public async Task The_queue_in_front_of_the_slow_stage_is_the_deepest()
    {
        var run = await OrderPipeline.RunAsync(40, Stages(priceWorkers: 1), capacity: 32, arrivalInterval: Arrival);

        Assert.Equal("price", run.Bottleneck.Name);
    }

    [Fact]
    public async Task Scaling_the_bottleneck_raises_throughput()
    {
        var one = await OrderPipeline.RunAsync(40, Stages(priceWorkers: 1), capacity: 32, arrivalInterval: Arrival);
        var four = await OrderPipeline.RunAsync(40, Stages(priceWorkers: 4), capacity: 32, arrivalInterval: Arrival);

        Assert.True(four.OrdersPerSecond > one.OrdersPerSecond,
            $"4 workers: {four.OrdersPerSecond:F1}/s should beat 1 worker: {one.OrdersPerSecond:F1}/s");
    }

    [Fact]
    public async Task A_bounded_channel_never_queues_more_than_its_capacity()
    {
        StageSpec[] slowStore =
        [
            new("validate", 1, Fast),
            new("price", 1, Fast),
            new("store", 1, TimeSpan.FromMilliseconds(25))
        ];

        var run = await OrderPipeline.RunAsync(40, slowStore, capacity: 8, arrivalInterval: Arrival);

        // Capacity plus the items each worker is holding - the point is that it is BOUNDED.
        Assert.All(run.Stages, stage => Assert.True(stage.PeakQueueDepth <= 12,
            $"{stage.Name} queued {stage.PeakQueueDepth}"));
    }

    [Fact]
    public async Task An_unbounded_channel_lets_the_backlog_grow_without_limit()
    {
        StageSpec[] slowStore =
        [
            new("validate", 1, Fast),
            new("price", 1, Fast),
            new("store", 1, TimeSpan.FromMilliseconds(25))
        ];

        var run = await OrderPipeline.RunAsync(40, slowStore, capacity: null, arrivalInterval: Arrival);

        Assert.True(run.Stages.Last().PeakQueueDepth > 12,
            "with no backpressure the whole batch piles up in front of the slow stage");
    }

    [Fact]
    public async Task A_try_outside_the_loop_lets_bad_items_kill_every_worker()
    {
        var run = await PipelineFaults.UnprotectedLoopAsync(items: 200, workers: 4, poisonEvery: 10);

        Assert.Equal(0, run.WorkersSurviving);
        Assert.True(run.Processed < 200, "the pipeline stops well short of the full batch");
    }

    [Fact]
    public async Task A_try_inside_the_loop_costs_one_message_not_the_worker()
    {
        var run = await PipelineFaults.ProtectedLoopAsync(items: 200, workers: 4, poisonEvery: 10);

        Assert.Equal(4, run.WorkersSurviving);
        Assert.Equal(19, run.DeadLettered);
        Assert.Equal(181, run.Processed);
        Assert.Equal(200, run.Processed + run.DeadLettered);
    }

    [Fact]
    public async Task One_worker_completing_the_channel_strands_its_siblings()
    {
        var run = await PipelineFaults.PrematureCompletionAsync(items: 40, workers: 4);

        Assert.True(run.ChannelClosedFailures > 0, "the slow workers were still holding items");
        Assert.True(run.Delivered < 40, "their work never reached the next stage");
    }

    [Fact]
    public async Task Completing_after_every_worker_has_finished_delivers_everything()
    {
        var run = await PipelineFaults.CompletionAfterAllWorkersAsync(items: 40, workers: 4);

        Assert.Equal(0, run.ChannelClosedFailures);
        Assert.Equal(40, run.Delivered);
    }

    [Fact]
    public async Task Parallel_workers_on_one_queue_reorder_events_within_a_key()
    {
        var run = await PipelineFaults.UnpartitionedAsync(keys: 12, eventsPerKey: 6, workers: 4);

        Assert.True(run.KeysOutOfOrder > 0, "uneven work reorders events that share a key");
    }

    [Fact]
    public async Task Partitioning_by_key_preserves_ordering_within_each_key()
    {
        var run = await PipelineFaults.PartitionedByKeyAsync(keys: 12, eventsPerKey: 6, workers: 4);

        Assert.Equal(12, run.KeysChecked);
        Assert.Equal(0, run.KeysOutOfOrder);
    }
}
