# AutoChess Demo

This folder owns the headless AutoChess validation demo for EX-GAS 2.0.

The demo is intentionally outside `Assets/GAS/Runtime` so Runtime Core does not depend on real gameplay scenario code. Core GAS creates only the base system groups and core systems; `HeadlessAutoChessRuntimeSystemBootstrap` registers AutoChess-specific systems when the scenario runs.

Current layering:

- `Config`: generated and hand-authored AutoChess definition sources.
- `Config/Generated`: generated-row compatibility layer before the dedicated Luban pipeline lands.
- `Simulation`: headless driver, reactions, execution calculation extensions, and summon lifecycle.
- `Observation`: structured facts, replay log, validation counters, and performance snapshots.
- `Presentation`: marker projection and scene/runtime presentation adapters.
- `Validation`: full-chain scenario assertions and scale profiles.
- `Debugging`: scenario-local diagnostic exports and runtime debugging bridges.

`HeadlessAutoChessScenario` is being split as a partial validation runner while keeping its public API stable:

- `HeadlessAutoChessScenario.cs`: run loop, unit bootstrap, validation report, event counting, and presentation outbox accumulation.
- `HeadlessAutoChessScenarioBootstrap.cs`: GAS runtime initialization and config/provider registration.
- `HeadlessAutoChessScenarioConstants.cs`: public AutoChess ids and tuning constants.
- `HeadlessAutoChessScenarioRuntimeLifecycle.cs`: observation/debugger reset and ASC cleanup after validation runs.
- `HeadlessAutoChessScenarioRuntimeTiming.cs`: ECS group timing capture and Runtime Debugger timing diagnostics.
- `HeadlessAutoChessScenarioState.cs`: private unit definition/runtime state used by the validation runner.
- `HeadlessAutoChessScenarioUnitDefinitions.cs`: scenario unit definitions for default and variant boards.
- `HeadlessAutoChessScenarioUnitResolution.cs`: unit attribute refresh and winner resolution.
- `HeadlessAutoChessScenarioVariants.cs`: scenario variant normalization, deterministic seeds, scale expansion, and unit counts.
- `HeadlessAutoChessScenarioTypes.cs`: public options, results, report DTOs, and timing collectors.

The remaining refactor is to continue extracting unit bootstrap, validation report building, event/outbox counting, and generated rows into smaller contracts without changing validation behavior.
