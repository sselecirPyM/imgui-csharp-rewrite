// The golden harness and input tests share the process-global ImGuiContext
// (static GImGui), so xunit must never run test classes in parallel here.
using Xunit;
[assembly: CollectionBehavior(DisableTestParallelization = true)]
