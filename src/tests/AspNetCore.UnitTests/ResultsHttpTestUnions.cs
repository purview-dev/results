using Purview.Results.SourceGeneration;

namespace Purview.Results.AspNetCore;

// CA1815 is answered by the package's suppressor for every union opted in with [GenerateResult].
[GenerateResult]
public readonly union HttpTestError(ItemNotFound, ItemConflict, ItemRejected);

public readonly record struct ItemNotFound(int ItemId);

public readonly record struct ItemConflict(int ItemId, string Reason);

public readonly record struct ItemRejected(int ItemId);
