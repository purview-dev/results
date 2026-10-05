namespace Purview.Results.AspNetCore;

// CA1815 is answered by the package's suppressor for every union opted in with [GenerateResult].
[GenerateResult]
public readonly union HttpTestError(ItemNotFound, ItemConflict, ItemRejected);

public readonly record struct ItemNotFound(int ItemId);

public readonly record struct ItemConflict(int ItemId, string Reason);

public readonly record struct ItemRejected(int ItemId);

public readonly record struct ItemBillingMissing(int ItemId);

public readonly record struct ItemBillingUnavailable(string Reason);

// A downstream service's error union, nested inside the outer operation-family union so the mapper has to
// unwrap two levels to reach the leaf.
[GenerateResult]
public readonly union HttpTestBillingError(ItemBillingMissing, ItemBillingUnavailable);

[GenerateResult]
public readonly union HttpTestOuterError(HttpTestError, HttpTestBillingError);
