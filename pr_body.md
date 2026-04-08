## Summary
- Switched from `{x:Bind}` to `{Binding}` in DataGrid templates to ensure robust DataContext resolution on Uno/Skia platforms.
- Updated `PropertyGridControl` to recreate `CollectionViewSource` completely when properties update to prevent stale row contexts.
- Manually populated `PropertyValue` on DataGrid grouping headers since Uno's built-in grouping extraction is compiled out.

## Test Plan
- [x] Verified all unit tests pass successfully.
- [ ] Verify Property Grid items group correctly without rendering errors on target platforms.