# Chroma Vale — AI Coding Agent Rules

> [!IMPORTANT]
> Every AI coding agent operating in this repository MUST follow these rules.
> Violations will be rejected in code review. This file is the single source of truth
> for architecture, conventions, and constraints.

## Tech Stack (Non-Negotiable)

| Layer | Technology |
|-------|-----------|
| Engine | Unity 6 LTS (6000.x) |
| Render Pipeline | URP (Universal Render Pipeline) 17.x |
| Language | C# (.NET 8) |
| DI / IoC | VContainer 1.16+ |
| Tweening | DOTween (installed via UnityPackage, OpenUPM scoped registry) |
| Serialization | Newtonsoft.Json (Unity package) |
| Testing | Unity Test Framework + NUnit (hand-written fakes) |

## Architecture: Four-Layer Clean Design

```
Assets/_Project/
├── Core/                  # Engine-agnostic domain logic
├── Domain/                # Unity-specific services
├── Infrastructure/        # Data, analytics, monetization, audio
├── Presentation/          # UI views, components, animations
└── Shared/                # Extensions, constants, utilities
```

### Hard Rules

1. **`Core/` has ZERO Unity dependencies.**
   - No `MonoBehaviour`, no `GameObject`, no `UnityEngine` imports.
   - Pure C# classes and interfaces only.
   - Fully unit-testable without Unity.
   - Assembly: `ChromaVale.Core.asmdef` — references NO Unity assemblies.

2. **`Domain/` depends on `Core/` and Unity APIs only.**
   - May use `MonoBehaviour` for lifecycle, `Vector2`, `Transform`, etc.
   - Implements interfaces defined in Core.
   - Assembly: `ChromaVale.Domain.asmdef` — references Core + Unity assemblies.

3. **`Infrastructure/` implements `Core/` interfaces.**
   - Adapters for external services (PostHog, IronSource, Unity IAP, file I/O).
   - Never accessed directly by Presentation.
   - Assembly: `ChromaVale.Infrastructure.asmdef`.

4. **`Presentation/` never touches data directly.**
   - All data flows through Domain services (injected via VContainer).
   - Views follow MVP pattern: View (MonoBehaviour) → Presenter (pure C#) → UseCase (Domain).
   - Assembly: `ChromaVale.Presentation.asmdef`.

5. **All IAP and Ad calls go through `Monetization/` abstractions.**
   - Never call `IronSource.Agent` or `UnityPurchasing` directly in game code.
   - Use `IMonetizationService` interface defined in Core, implemented in Infrastructure.

6. **All analytics events go through `Analytics/` bridge.**
   - Never call PostHog SDK directly.
   - Use `IAnalyticsService` interface with typed event methods.

## Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Namespaces | `ChromaVale.<Layer>.<Submodule>` | `ChromaVale.Core.GameLogic` |
| Interfaces | `I` prefix | `IPipeRouter`, `ILevelRepository` |
| Classes | PascalCase | `PipeRouter`, `LevelData` |
| MonoBehaviour | PascalCase + no suffix | `PuzzleBoardView`, `WorldMapView` |
| Methods | PascalCase | `RoutePipe()`, `CalculateScore()` |
| Private fields | `_camelCase` | `_pipeGrid`, `_isRouting` |
| Public properties | PascalCase | `CurrentLevel`, `IsComplete` |
| Events (C#) | PascalCase | `OnLevelComplete`, `OnStarEarned` |
| Analytics events | snake_case | `level_complete`, `purchase_start` |

## File Organization

- One class per file (except small related types like enums).
- Filename matches class name exactly: `PipeRouter.cs`, `ILevelRepository.cs`.
- Tests mirror source structure under `Assets/Tests/EditMode/`.
- Prefabs in `Assets/Prefabs/` organized by feature, not type.

## Dependency Injection

- All service registration in `AppInstaller.cs` (MonoBehaviour) in `Assets/_Project/`.
- VContainer `Lifetime` scopes:
  - `Singleton`: Analytics, Monetization, Save/Load, Audio, Config
  - `Scoped` (per-scene): PuzzleBoard, LevelState, UI presenters
  - `Transient`: Factories, utilities
- No `new` for service classes — always resolve through DI.

## Performance Constraints

- Target: **60fps** on iPhone 8 / Galaxy S9 (baseline devices).
- Object pool all frequently spawned objects (orbs, particles, pipe segments).
- No `FindObjectOfType` or `GetComponent` in update loops.
- No LINQ in hot paths (`.Where()`, `.Select()`, etc.) — use `for`/`foreach`.
- UI updates via direct event subscription, not `Update()` polling.
- Sprite Atlas for all 2D assets.

## What NOT to Do

- ❌ Static singletons or service locators (use DI).
- ❌ `PlayerPrefs` for game data (use `IPersistenceService`).
- ❌ Direct scene references across scenes (use DI + addressables if needed).
- ❌ Hardcoded strings for analytics events (use constants/enums).
- ❌ MonoBehaviour inheritance chains deeper than 2 levels.
- ❌ SendMessage or BroadcastMessage — use C# events.

## Animation Guidelines
- **Use DOTween for all visual animation** (position, scale, alpha, color).
- **Coroutines are acceptable for sequencing** (e.g., chaining tweens, flow simulation ticks).

## When Opening This Project in Unity

1. Open Unity Hub → Add project from disk → select this folder.
2. Unity will import packages and generate `Library/`, `Temp/`, etc. (git-ignored).
3. Open `Assets/Scenes/Bootstrap.unity` as the startup scene.
4. Run Tests via Window → General → Test Runner.

## Related

- [[MASTER_MANDATE|Obsidian: MASTER_MANDATE]] — Operational governance
- [[Game Dev Tech Stack|Obsidian: Game Dev Tech Stack]] — Full tech choices
- [[Chroma Vale|Obsidian: Chroma Vale Project]] — Game concept and plan
