using Xunit;

// All UI tests share one headless Avalonia session (single dispatcher).
// Parallel test classes interleave session resets and background pipeline
// posts, which crashes layout with missing platform services — serialize.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
