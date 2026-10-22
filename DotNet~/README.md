# Generic C# SDK

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

The package targets .NET 8. The Unity UPM package remains the first-class Unity
adapter and includes UI Toolkit, `MonoBehaviour`, ScriptableObject routing, and
sample-scene integration that do not belong in the generic package.

The pure `IndieableSdk.Events` bus sources are compiled into both packages so the
same publish/subscribe model can be used by Unity, Godot C#, server tools, or custom
.NET engines.
