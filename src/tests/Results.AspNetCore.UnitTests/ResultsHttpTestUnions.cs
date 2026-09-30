using Purview.Results.SourceGeneration;

namespace Purview.Results.AspNetCore;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types")]
[GenerateResult]
public readonly union HttpTestError(ItemNotFound, ItemConflict, ItemRejected);

public readonly record struct ItemNotFound(int ItemId);

public readonly record struct ItemConflict(int ItemId, string Reason);

public readonly record struct ItemRejected(int ItemId);
