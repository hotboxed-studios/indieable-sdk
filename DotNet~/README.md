# Generic C# SDK

> **SDK in development.** This is experimental or legacy maintainer material,
> not the current product’s supported install path. Use the
> [HTTP API guide](https://github.com/ConnorIllingworth/indieable/blob/codex/unified-player-workspaces/docs/DEVELOPER-WORKFLOW-API.md)
> for current integrations. Unity/v1 setup does not connect to the current v2
> application. Actual engine/export support has not been verified.


`DotNet~/Indieable.Sdk` is the engine-agnostic C# client shipped beside the Unity
Package Manager package.

For the developer-workflow web branch, use the new
`IndieableSdk.DeveloperWorkflow.WorkflowBackendClient` and
`WorkflowSessionClient`. See the [v2 guide](../Documentation~/DEVELOPER-WORKFLOW.md).
The original `IndieableClient` and Unity adapters remain v1 compatibility
surfaces requiring the earlier backend; they do not target the new web branch.

Build and pack locally:

```bash
dotnet build DotNet~/Indieable.Sdk/Indieable.Sdk.csproj --configuration Release
dotnet pack DotNet~/Indieable.Sdk/Indieable.Sdk.csproj \
  --configuration Release \
  --output dist
```

The package targets .NET 8. The retained legacy Unity source is an experimental Unity
adapter and includes UI Toolkit, `MonoBehaviour`, ScriptableObject routing, and
sample-scene integration that do not belong in the generic package.

The pure `IndieableSdk.Events` bus sources are compiled into both development
projects. This does not establish tested Unity, Godot or other engine exports.
