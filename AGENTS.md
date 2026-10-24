# AI Coding Instructions for SharpKit

These instructions apply to all AI agents working on the SharpKit repository. Follow them when writing, editing, reviewing, or refactoring code.

The goal is to produce code that is **idiomatic, modern, concise, maintainable, performant where necessary, and consistent with the existing architecture**. Do not merely produce code that compiles; make sure it fits the project and solves the intended problem correctly.

## 1. General Principles

- Follow the official [Microsoft C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions) and relevant .NET guidelines, except where these instructions explicitly establish a different preference.
- Follow the conventions already established in the surrounding code when they do not conflict with these instructions.
- Prefer simple, readable solutions over unnecessarily complicated abstractions.
- Avoid overengineering, premature generalization, unnecessary indirection, and speculative features.
- Do not introduce new architectural patterns, frameworks, or dependencies without a clear justification.
- Prefer modern C# language features supported by the project's configured language version.
- Do not sacrifice correctness, clarity, or maintainability merely to reduce line count.
- Make focused changes. Avoid unrelated refactoring or reformatting.
- Do not duplicate existing functionality. Search the codebase for existing implementations and reusable abstractions before creating new ones.
- Never assume an API, method, property, type, or library feature exists. Verify it in the source code or official documentation.
- Do not leave placeholder implementations, fake functionality, unexplained TODOs, or silently swallowed errors unless explicitly requested.

## 2. C# Style and Syntax

### 2.1 Use `var` by default for local variables

Prefer `var` over explicitly specifying a type for local variables when the type can be inferred from the right-hand expression.

Prefer:

```csharp
var document = new PluginDocument();
var count = items.Count;
var name = record.EditorId;
var selectedItems = items.Where(item => item.IsSelected);
```

Instead of:

```csharp
PluginDocument document = new PluginDocument();
int count = items.Count;
string name = record.EditorId;
IEnumerable<Item> selectedItems = items.Where(item => item.IsSelected);
```

Use explicit types when they materially improve readability, clarify a non-obvious conversion, or are required by the language.

For example, explicit types are appropriate when the initialization expression does not communicate the intended type:

```csharp
CancellationToken token = default;
```

This preference applies to **local variables**, not fields, properties, parameters, or return types. Those must have valid, explicit declarations unless the language feature in use dictates otherwise.

### 2.2 Prefer target-typed `new`

When the target type is already apparent, use target-typed construction.

Prefer:

```csharp
PluginDocument document = new();
Dictionary<FormId, FormRecord> records = new();
```

When using `var`, the constructor type must be specified:

```csharp
var document = new PluginDocument();
```

Do not write `var document = new();`, which has no target type from which to infer the type.

### 2.3 Use collection expressions

Prefer modern C# collection expressions (`[]`) over unnecessarily verbose collection initialization.

Prefer:

```csharp
var records = new List<FormRecord>();
FormRecord[] records = [];
List<string> names = ["Iron Sword", "Steel Sword"];
HashSet<FormId> selectedIds = [];
```

Instead of:

```csharp
var records = new List<FormRecord>();
FormRecord[] records = Array.Empty<FormRecord>();
List<string> names = new() { "Iron Sword", "Steel Sword" };
HashSet<FormId> selectedIds = new();
```

Use collection expressions whenever the target type supports them and the result is clear. Do not use them where target typing, collection semantics, or compatibility makes them inappropriate.

Do not replace an existing collection with a different collection type merely to use `[]`. Preserve the intended mutability, ordering, uniqueness, and performance characteristics.

### 2.4 Prefer object initializers

Initialize properties as part of object construction whenever practical.

Prefer:

```csharp
var record = new FormRecord
{
    EditorId = "IronSword",
    Name = "Iron Sword",
    Weight = 9.0f
};
```

Instead of:

```csharp
var record = new FormRecord();
record.EditorId = "IronSword";
record.Name = "Iron Sword";
record.Weight = 9.0f;
```

Use the same principle for collection initializers when collection expressions are not appropriate.

Do not use object initializers when the type's constructor or factory method is specifically designed to enforce invariants, or when initialization order is significant.

### 2.5 Use anonymous types for temporary projections

When creating temporary data projections, especially in LINQ queries, prefer anonymous types over defining a dedicated class or record solely for that one local operation.

Prefer:

```csharp
var entries = records.Select(record => new
{
    record.FormId,
    record.EditorId,
    DisplayName = record.Name ?? record.EditorId
});
```

Use inferred member names when they are descriptive. Specify a name explicitly when it improves clarity or avoids ambiguity.

Do not create a named type solely to carry a handful of values through a local query or intermediate operation.

However, use a named class, record, or tuple when the data must cross an API boundary, be returned from a method, represent a meaningful domain concept, or have behavior or invariants of its own. Anonymous types cannot be used as declared method return types, parameter types, or fields.

### 2.6 Prefer concise expressions

Use expression-bodied members for short, straightforward implementations.

```csharp
public string DisplayName => Name ?? EditorId;

public bool IsValid(FormId id) => id != FormId.None;
```

Use block bodies when they improve readability, require multiple statements, or involve meaningful control flow.

Prefer pattern matching, switch expressions, null-coalescing operators, null-conditional operators, and other modern C# features when they simplify the code without obscuring its behavior.

```csharp
var displayName = record.Name ?? record.EditorId;
var parentName = record.Parent?.Name ?? "None";
```

Avoid deeply nested conditional expressions and overly clever one-liners.

### 2.7 Naming and formatting

- Use `PascalCase` for types, methods, properties, and public members.
- Use `camelCase` for local variables and method parameters.
- Use `_camelCase` for private instance fields, consistent with the existing project style.
- Use `I`-prefixed names for interfaces.
- Use meaningful names that describe intent, not implementation details.
- Use `async` suffixes for asynchronous methods where appropriate.
- Use `const` for compile-time constants and `static readonly` for values initialized at runtime that should not be reassigned.
- Prefer `string` over `String`, `int` over `Int32`, and other C# language aliases over their framework type names.
- Use file-scoped namespaces when consistent with the project.
- Use four spaces for indentation and follow the repository's established brace style.
- Avoid unnecessary comments that merely restate what the code does. Comments should explain intent, constraints, or non-obvious decisions.

Do not rename public APIs, serialized members, or externally significant identifiers just to satisfy naming preferences without checking the compatibility implications.

## 3. Architecture and Project Structure

- Understand the existing project structure before making changes.
- Keep responsibilities separated according to the architecture already in use.
- Avoid putting business logic, data access, or complex application logic directly into WPF views or code-behind when an existing service, model, or view-model layer is appropriate.
- Reuse existing services, models, interfaces, and utilities where possible.
- Avoid creating interfaces for every class by default. Introduce abstractions when they provide a real benefit, such as testability, replaceable implementations, or a meaningful architectural boundary.
- Avoid unnecessary singleton services, static mutable state, service locators, and global registries.
- Avoid circular dependencies between projects and layers.
- Do not move code between projects or introduce new projects without a clear architectural reason.
- Respect the distinction between SharpKit's domain logic, user interface, data handling, and rendering responsibilities.
- Keep changes consistent with the existing implementation, even if a different architecture might be reasonable for a new project.

Before implementing a feature, identify the appropriate location for it and the existing components it should integrate with.

## 4. WPF and UI Development

- Follow the project's existing WPF patterns and conventions.
- Prefer data binding and commands over unnecessarily complex event-handler logic.
- Follow existing MVVM conventions where applicable; do not introduce a new MVVM framework without justification.
- Keep XAML readable, consistently formatted, and free of unnecessary verbosity.
- Use existing resources, styles, templates, converters, and shared controls instead of duplicating them.
- Prefer shared styles and templates over repeating the same presentation properties across multiple controls.
- Use appropriate dependency properties, routed events, and commands when they fit the intended WPF behavior.
- Respect WPF thread affinity. UI elements must be accessed and updated on the UI thread.
- Do not block the UI thread with long-running work, synchronous I/O, or unnecessary waits.
- Use asynchronous operations appropriately and provide cancellation when useful.
- Dispose of timers, event subscriptions, and other resources when their lifetime ends.
- Consider keyboard navigation, focus behavior, accessibility, and responsiveness when implementing UI features.
- Do not make visual or interaction changes unrelated to the requested task.

## 5. Correctness, Error Handling, and Performance

- Understand the relevant code path before changing behavior.
- Preserve existing behavior unless the task explicitly requires changing it.
- Handle nullability and invalid input deliberately. Do not silence nullable warnings with `!` unless the non-null invariant is established and the suppression is justified.
- Catch exceptions only when they can be handled meaningfully, translated into useful feedback, or supplemented with appropriate context.
- Never catch and silently ignore general exceptions.
- Do not use exceptions as routine control flow.
- Avoid unnecessary allocations, repeated expensive computations, and redundant data transformations in frequently executed code.
- Use asynchronous APIs for I/O-bound work when appropriate.
- Avoid introducing concurrency, caching, or complicated optimizations without a demonstrated need.
- Where performance is important, prefer measurement and profiling over assumptions.
- Be particularly careful with large plugin files, record collections, object graphs, and rendering operations.
- Preserve data integrity. Do not silently discard unknown fields, unsupported record types, or data that the application cannot fully interpret.
- Do not claim that a feature is fully supported when the implementation only handles a subset of its cases.

## 6. Dependencies and External APIs

- Prefer existing dependencies over introducing new ones for functionality the project already supports.
- Do not add a dependency for a trivial helper or a small amount of straightforward code.
- Before adding a package, verify that it is compatible with the target frameworks, project license, deployment model, and existing dependency versions.
- Do not upgrade dependencies or change target frameworks as an unrelated side effect.
- Use official documentation and existing project usage to verify API behavior.
- Avoid relying on undocumented implementation details unless necessary and clearly justified.
- Do not copy large amounts of third-party code into the repository without checking its license and attribution requirements.

## 7. Comments and Documentation

- Write comments to explain why something is done, not simply repeat what the code says.
- Document non-obvious algorithms, invariants, workarounds, format constraints, and compatibility considerations.
- Use XML documentation for public APIs when appropriate and consistent with the surrounding code.
- Keep documentation accurate when changing behavior.
- Do not add lengthy documentation to trivial private methods.
- Do not invent guarantees, supported features, or compatibility claims that the implementation does not provide.

## 8. Build and Verification

- The repository currently does not contain a test suite. **AI agents are not expected to create tests or introduce testing infrastructure.**
- Do not add test projects, testing frameworks, or test files unless explicitly requested by the user.
- Verify changes by building the relevant project or solution when feasible.
- Run only the builds and other non-destructive verification steps relevant to the task.
- If a build cannot be run, state that clearly rather than implying it succeeded.
- Do not claim to have verified behavior that was only reviewed by inspection.
- Do not modify unrelated code simply to make an existing build or warning pass. Report unrelated issues separately.
- If a change cannot be verified completely, clearly identify any remaining uncertainty.

## 9. Git Operations: Strictly Read-Only

**AI agents must never perform write operations through Git. Only read-only Git operations are permitted.**

Permitted operations include inspecting repository state, viewing diffs, reading commit history, and examining branches or references. Examples include:

- `git status`
- `git diff`
- `git log`
- `git show`
- `git branch --list`
- `git rev-parse`
- `git ls-files`

Prohibited operations include, but are not limited to:

- Creating commits or amending existing commits.
- Creating, deleting, renaming, or switching branches.
- Merging, rebasing, cherry-picking, or reverting commits.
- Staging or unstaging changes.
- Pushing or pulling changes.
- Fetching or modifying remote references.
- Using Git commands that modify the repository's index, references, configuration, or working-tree state.

Do not use alternative tools or scripts to circumvent this restriction. Do not assume that an operation is permitted merely because it is reversible.

**AI agents must not commit, push, or otherwise modify Git state.** Leave all Git write operations to the human developer.

This restriction applies even when a Git write operation would appear convenient, customary, or necessary to complete a task.

## 10. Project Boundaries and the Human–AI Development Policy

SharpKit has an intentional boundary between its two main projects. **AI agents must respect this boundary at all times.**

### `SharpKit`

The `SharpKit` project is written by humans and is the primary human-developed part of the codebase.

- Treat the existing code as human-maintained code that should be preserved.
- **Make changes to `SharpKit` only when absolutely necessary to fulfill an explicit user request.**
- Prefer implementing requested changes in `SharpKit.Renderer` or another appropriate location when doing so is architecturally correct and does not require unnecessary modifications to `SharpKit`.
- Do not refactor, rewrite, reformat, reorganize, or modernize `SharpKit` code merely because an alternative implementation seems preferable.
- Do not make opportunistic improvements or unrelated cleanups in this project.
- Do not introduce AI-generated changes into `SharpKit` just to make an implementation more convenient.
- If a requested feature appears to require changes to `SharpKit`, first determine whether those changes are genuinely necessary. Minimize their scope and preserve the existing implementation.
- If a task cannot reasonably be completed without substantial changes to `SharpKit`, explain the situation and seek clarification before proceeding.

### `SharpKit.Renderer`

The `SharpKit.Renderer` project is AI-coded and is the primary area in which AI agents are expected to contribute implementation code.

- AI agents may implement, modify, and refactor `SharpKit.Renderer` as needed to fulfill the user's request.
- Follow all other repository instructions, existing architectural decisions, and applicable coding conventions.
- Review AI-generated code critically. AI authorship does not excuse poor correctness, unnecessary complexity, or inadequate error handling.
- Preserve compatibility with the rest of the application and avoid unnecessary changes to other projects.
- Do not assume that every task belongs in the renderer; respect project responsibilities and keep functionality in the appropriate layer.

### When a task crosses the project boundary

Some functionality may require coordination between `SharpKit` and `SharpKit.Renderer`.

In such cases:

1. Determine the smallest change that can fulfill the request.
2. Implement the necessary functionality in `SharpKit.Renderer` wherever appropriate.
3. Avoid changing `SharpKit` unless the integration genuinely requires it.
4. If a change to `SharpKit` is unavoidable, keep it minimal and directly related to the requested task.
5. If the necessary change would violate the human–AI development boundary or require a substantial modification to `SharpKit`, explain the limitation and ask the user how to proceed.

**The fact that a change is technically convenient does not make it necessary.** Preserving the human-written `SharpKit` project takes priority over convenience when choosing where and how to implement a feature.

## 11. Expectations for AI Agents

- Read the relevant instructions, project files, and nearby implementations before writing code.
- Establish the intended behavior and identify important edge cases before implementing non-trivial changes.
- Prefer a complete, coherent implementation over a superficial patch that only addresses the most obvious case.
- Do not invent requirements or make unrelated product decisions. Ask for clarification when an ambiguity materially affects the design or behavior.
- Do not assume that code generated by another AI agent is correct.
- Review generated code for correctness, consistency, error handling, performance, and integration with the existing architecture.
- Avoid speculative rewrites and unnecessary abstractions.
- Do not leave the repository in a state that is knowingly uncompilable or internally inconsistent.
- At the end of the task, summarize the changes made, verification performed, and any known limitations or unfinished work.
- Respect the project boundaries defined in Section 10 and the read-only Git restriction defined in Section 9.

## 12. Priority of Instructions

When instructions conflict, use this order of precedence:

1. Explicit requirements from the current task, subject to the project's human–AI development boundary and read-only Git restriction.
2. These repository-wide instructions.
3. Existing project architecture and established conventions.
4. Official Microsoft and .NET recommendations.
5. General coding preferences.

**The read-only Git restriction and the human–AI development boundary are standing project policies.** Do not interpret a general request to implement, fix, or refactor something as permission to disregard them.