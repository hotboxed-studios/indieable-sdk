# UnityExample

This is a minimal Unity 2022.3 project for manually exercising the standalone `com.indieable.sdk` package. It references the repository root as `file:../..`.

Open the `UnityExample` directory in Unity Hub, load `Assets/Scenes/IndieableExample.unity`, and enter Play Mode. The empty scene starts the runtime UI Toolkit dashboard automatically.

Configure `https://preview.indieable.com`, the Preview game's client-safe `ind_pub_...` Public Game Key, and environment `development`. Never use a Server Secret in Unity.

Suggested flow: initialize locally, load notice, connect, continue without optional data, confirm telemetry is blocked, enable Gameplay Telemetry, send a test run, restart and verify continuity, link an account, test forms and Challenges, reset identity, then restart and confirm ephemeral state.
