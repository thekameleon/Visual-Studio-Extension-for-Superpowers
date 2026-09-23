# Prevent Duplicate Function Selection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop a user from ending up with two model-preference rows for the same Superpowers function. Today nothing prevents it while editing, and `SaveModelPreferencesAsync`'s dedup logic silently keeps the last row and discards the rest with no warning — a real data-loss surprise. This plan makes the conflict impossible to create in the first place, rather than silently resolving it after the fact.

**Architecture:** `ModelPreferenceRow.Function`'s setter gains an `isFunctionAvailable` callback, checked before accepting a new value. If the target function is already used by another row, the change is refused and a `reportFunctionConflict` callback fires (surfaced as a status message); the setter then re-raises its own `PropertyChanged` notification so the `ComboBox` visually snaps back to the row's actual (unchanged) function — WPF does not do this automatically just because a setter declined a value, the same class of footgun this file already handles for other properties. `AddPreferenceRowCommand` stops always defaulting new rows to `General` (which might already be taken) and instead picks the first function not yet used by any row, or refuses to add a row at all — with a status message — once every function already has one.

**Tech Stack:** C# / .NET 8, WPF via `Microsoft.VisualStudio.Extensibility.UI`. No new XAML — the existing `ComboBox` binding is unchanged; the fix lives entirely in the row's own property setter and the ViewModel wiring around it.

**Spec:** No separate spec document — follows directly from the in-chat design agreed on 2026-09-23. The binding requirement is captured in the Global Constraint below.

## Global Constraints

- Saved preference data can never contain a duplicate function — `SaveModelPreferencesAsync`'s existing dictionary-based dedup already guarantees this on the way to disk. This plan's job is purely to stop the UI from letting a user create the conflicting state in the first place; it does not change persistence, and the existing dedup logic in `SaveModelPreferencesAsync` stays as-is (a harmless, no-longer-normally-reachable safety net, not dead code to be deleted — leave it).
- Rows reconstructed from SAVED preferences in `LoadAsync` are always already unique (guaranteed by the constraint above), so the constructor does not need to validate against sibling rows at construction time — only user-driven edits AFTER a row exists (via the `Function` property setter) need the check.
- The rejection path must NOT silently do nothing from the user's point of view — WPF's `ComboBox` will visually show whatever the user clicked as "selected" the instant they click it, regardless of whether the bound setter accepts the value; if the setter declines without explicitly telling the binding to re-pull the old value, the ComboBox will show the wrong (rejected) selection while the underlying data still holds the old one. The setter must force a `PropertyChanged` raise for `Function` itself even when it doesn't change the backing field, to correct this.

---

### Task 1: Prevent duplicate function selection

**Files:**
- Modify: `TheKameleon.Superpowers.Vsix/SuperpowersViewModel.cs`

**Interfaces:**
- Consumes: nothing new from other files — this is entirely internal wiring within the existing `ModelPreferenceRow`/`SuperpowersViewModel` pair.
- Produces: `ModelPreferenceRow`'s constructor gains two new optional parameters (`isFunctionAvailable`, `reportFunctionConflict`); `SuperpowersViewModel` gains `IsFunctionAvailable`, `ReportFunctionConflict`, and `NextAvailableFunction` private members.

This task has no dedicated xUnit test — `ModelPreferenceRow` and `SuperpowersViewModel` are internal WPF classes with no test-project visibility (no `InternalsVisibleTo`), consistent with every other change to this file across this session's prior plans. Verification is manual (Task 2).

Read `TheKameleon.Superpowers.Vsix/SuperpowersViewModel.cs` in full as it currently exists before editing — the diff below was written against the live post-merge content read this same session, but confirm before applying.

- [ ] **Step 1: Extend `ModelPreferenceRow`'s constructor and `Function` setter**

Replace the class's field declarations, constructor, and `Function` property with:

```csharp
    [DataContract]
    internal sealed class ModelPreferenceRow : NotifyPropertyChangedObject
    {
        private readonly Func<SuperpowersFunction, string?>? suggestModel;
        private readonly Func<string, string?>? lookupProvider;
        private readonly Func<SuperpowersFunction, ModelPreferenceRow, bool>? isFunctionAvailable;
        private readonly Action<SuperpowersFunction>? reportFunctionConflict;
        private SuperpowersFunction function;
        private string model = string.Empty;
        private string? lastSuggestion;

        public ModelPreferenceRow(
            SuperpowersFunction function,
            Func<SuperpowersFunction, string?>? suggestModel = null,
            Func<string, string?>? lookupProvider = null,
            Func<SuperpowersFunction, ModelPreferenceRow, bool>? isFunctionAvailable = null,
            Action<SuperpowersFunction>? reportFunctionConflict = null)
        {
            this.suggestModel = suggestModel;
            this.lookupProvider = lookupProvider;
            this.isFunctionAvailable = isFunctionAvailable;
            this.reportFunctionConflict = reportFunctionConflict;
            this.function = function;
            this.model = suggestModel?.Invoke(function) ?? string.Empty;
            this.lastSuggestion = string.IsNullOrEmpty(this.model) ? null : this.model;
        }

        [DataMember]
        public SuperpowersFunction Function
        {
            get => this.function;
            set
            {
                if (EqualityComparer<SuperpowersFunction>.Default.Equals(this.function, value))
                {
                    return;
                }

                if (this.isFunctionAvailable is not null && !this.isFunctionAvailable(value, this))
                {
                    this.reportFunctionConflict?.Invoke(value);
                    // WPF's ComboBox shows the clicked item as selected immediately, regardless of
                    // whether the bound setter accepts it. Since we're declining without changing
                    // the backing field, we still have to raise PropertyChanged for Function so the
                    // binding re-pulls the (unchanged) value from the getter and the ComboBox's
                    // visible selection snaps back to what it actually is.
                    this.RaiseNotifyPropertyChangedEvent(nameof(this.Function));
                    return;
                }

                this.function = value;
                this.RaiseNotifyPropertyChangedEvent(nameof(this.Function));
                this.RaiseNotifyPropertyChangedEvent(nameof(this.FunctionLabel));
                if (string.IsNullOrWhiteSpace(this.model) || this.model == this.lastSuggestion)
                {
                    var suggestion = this.suggestModel?.Invoke(value);
                    this.Model = suggestion ?? this.model;
                    this.lastSuggestion = string.IsNullOrEmpty(suggestion) ? null : suggestion;
                }
            }
        }
```

Note this deliberately does NOT use the file's usual `this.SetProperty(ref this.field, value)` helper for the accept path — `SetProperty` has no way to express "check availability first, and on rejection still raise the property's own changed event without touching the field." Handling it explicitly here, matching the existing `SetProperty` equality-then-raise shape by hand, is correct; don't "simplify" this back to `SetProperty` and lose the rejection path.

Everything else in `ModelPreferenceRow` (`FunctionLabel`, `Model`, `Provider`) stays exactly as it is today — this step only touches the fields, constructor, and `Function` property.

- [ ] **Step 2: Add the availability/conflict/next-function helpers to `SuperpowersViewModel`**

Add these three private methods near the existing `SuggestModel`/`LookupProvider` helpers:

```csharp
        private bool IsFunctionAvailable(SuperpowersFunction function, ModelPreferenceRow row) =>
            !this.FunctionRows.Any(other => !ReferenceEquals(other, row) && other.Function == function);

        private void ReportFunctionConflict(SuperpowersFunction function) =>
            this.StatusText = $"{function} already has a model preference row. Remove or change that row first.";

        private SuperpowersFunction? NextAvailableFunction()
        {
            var used = this.FunctionRows.Select(row => row.Function).ToHashSet();
            foreach (var candidate in Enum.GetValues<SuperpowersFunction>())
            {
                if (!used.Contains(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
```

- [ ] **Step 3: Wire the new callbacks into `AddPreferenceRowCommand`**

Replace the constructor's `AddPreferenceRowCommand` registration:

```csharp
            this.AddPreferenceRowCommand = new AsyncCommand((parameter, context, cancellationToken) => { this.FunctionRows.Add(new ModelPreferenceRow(SuperpowersFunction.General, this.SuggestModel, this.LookupProvider)); return Task.CompletedTask; });
```

with:

```csharp
            this.AddPreferenceRowCommand = new AsyncCommand((parameter, context, cancellationToken) =>
            {
                var nextFunction = this.NextAvailableFunction();
                if (nextFunction is null)
                {
                    this.StatusText = "Every function already has a model preference row.";
                    return Task.CompletedTask;
                }

                this.FunctionRows.Add(new ModelPreferenceRow(nextFunction.Value, this.SuggestModel, this.LookupProvider, this.IsFunctionAvailable, this.ReportFunctionConflict));
                return Task.CompletedTask;
            });
```

- [ ] **Step 4: Wire the availability/conflict callbacks into `LoadAsync`'s row reconstruction**

Existing rows loaded from saved preferences are already guaranteed unique (see Global Constraints), so this step does NOT add validation at construction time — it wires the callbacks so that once a loaded row exists, the user can still hit a conflict if they try to CHANGE that row's function to one another loaded row already has. In `LoadAsync`, change:

```csharp
            this.FunctionRows.AddRange(savedPreferences.Preferences.Select(p => new ModelPreferenceRow(p.Function, lookupProvider: this.LookupProvider) { Model = p.Model }));
```

to:

```csharp
            this.FunctionRows.AddRange(savedPreferences.Preferences.Select(p => new ModelPreferenceRow(
                p.Function,
                lookupProvider: this.LookupProvider,
                isFunctionAvailable: this.IsFunctionAvailable,
                reportFunctionConflict: this.ReportFunctionConflict)
            { Model = p.Model }));
```

Note `suggestModel` is still deliberately omitted here (named-argument style, skipping straight to `lookupProvider`) — that constraint is unchanged from the earlier, already-merged plan and this step must not disturb it.

- [ ] **Step 5: Build and run the full unit and integration suites**

Run: `dotnet build TheKameleon.Superpowers.slnx -c Debug`
Expected: 0 errors.

Run: `./TheKameleon.Superpowers.Tests/bin/Debug/net8.0/TheKameleon.Superpowers.Tests.exe`
Expected: PASS, 0 failures (this task adds no new tests but must not break the existing 181).

Run: `./TheKameleon.Superpowers.IntegrationTests/bin/Debug/net8.0-windows8.0/TheKameleon.Superpowers.IntegrationTests.exe`
Expected: PASS, 0 failures.

- [ ] **Step 6: Commit**

```bash
git add TheKameleon.Superpowers.Vsix/SuperpowersViewModel.cs
git commit -m "fix: prevent two model preference rows from targeting the same function"
```

---

### Task 2: Manual verification

**Files:** none (verification only)

- [ ] **Step 1: Manual verification in Visual Studio**

Launch the experimental instance (F5), open the Superpowers tool window, and verify:
1. With no rows yet, click **Add row** repeatedly — each new row gets a different function (General, then Plan, then Execute, ...) rather than every row defaulting to General.
2. With at least two rows present, open the second row's Function `ComboBox` and try to select the function the first row already has — confirm the selection visually snaps back to the row's original function (it does NOT show the conflicting function as selected), and a status message appears explaining the conflict.
3. Add rows until all 9 functions are used, then click **Add row** once more — confirm no new row is added and a status message says every function already has a row.
4. Remove a row (freeing up its function), then click **Add row** — confirm the freed-up function becomes available again as the new row's default.
5. Save preferences with a full, non-conflicting set of rows, reopen the tool window — confirm everything still loads correctly and editing an existing loaded row's function still respects the conflict check (try changing a loaded row's function to one another loaded row already uses).

- [ ] **Step 2: No commit for this task** — verification only.
