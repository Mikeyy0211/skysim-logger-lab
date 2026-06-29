## Why

This is a cleanup-only change. No design decisions are needed — the work is straightforward deletion of duplicate enum files and updating references to string constants.

## Approach

1. Delete 4 enum files from `Skysim.Logger.Contracts/Constants`
2. Find and update all usages of enum types to string constants
3. Build and test
