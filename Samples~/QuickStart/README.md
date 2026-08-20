# Quick Start sample

Add `IndieableQuickStart` to any scene object, paste the game's Public Game Key,
and enter Play Mode. The component first reads the side-effect-free public privacy
manifest, then establishes a Connect session and sends the reserved test event.

While connected:

- `F7` opens the optional Player Data privacy preferences.
- `F8` opens the optional playtest feedback form.
- `F9` opens the optional bug-report form.
- `Escape` closes the Indieable forms.

Enabling gameplay telemetry in the privacy UI creates one random game-scoped
Installation credential. Stop and restart Play Mode to verify the same Game Player
reference resumes. Use **Reset this local Indieable identity** to revoke it and test a
fresh ephemeral session.

Removing the component removes all sample behavior.
