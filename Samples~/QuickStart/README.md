# Quick Start sample

Create the project-owned settings asset from **Project Settings > Indieable**,
paste the game's Public Game Key, add `IndieableQuickStart` to a scene object,
and enter Play Mode. The SDK initializes before scene `Awake` and opens its
built-in UI Toolkit consent form after the first scene loads. The sample
component does not initialize or configure the SDK.

An explicit decline is stored locally for the current published notice without
creating a persistent Player. Enabling an optional purpose establishes a
Connect session and saves the choice through Indieable. Closing or failing to
load the form does not mark the notice as answered.

While connected:

- `F7` opens the optional Player Data privacy preferences.
- `F8` opens the optional playtest feedback form.
- `F9` opens the optional bug-report form.
- `Escape` closes the Indieable forms.

Enabling gameplay telemetry in the privacy UI creates one random game-scoped
Installation credential. Stop and restart Play Mode to verify the same Game
Player reference resumes. The sample includes a small cursor adapter while an
SDK form is visible; production games should coordinate their own input mode.

Removing the component removes all sample behavior.
