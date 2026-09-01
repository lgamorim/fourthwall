# Overlay — Frontend (Blazor)

Add when the project renders UI with Blazor (.NET 8+ Blazor Web App: static
SSR, Interactive Server, Interactive WebAssembly, or Auto).

- Render mode is a per-page/per-component decision, not a global default:
  start from static SSR and add `@rendermode` interactivity only where the UI
  needs it, stating why in the commit — the mode choice carries hosting,
  state, and payload consequences.
- A component targeting WebAssembly or Auto must not assume it runs on the
  server: no `DbContext`, file system, or server-only services in shared
  interactive components — depend on an abstraction with a server
  implementation and an HTTP-client implementation. Introduce that split only
  when a WASM/Auto mode is actually in use, not speculatively.
- DI lifetimes shift per mode: Scoped on Interactive Server means per-circuit
  (long-lived, one per tab), and on WebAssembly is effectively a singleton.
  Never treat a scoped service as per-operation; for EF Core use
  `IDbContextFactory<T>` with short-lived contexts, never an injected scoped
  `DbContext`.
- Interactive Server state lives in the circuit's server memory — a dropped
  connection wipes it. Anything the user must not lose goes to durable
  storage, not component fields.
- Prerendering runs interactive components twice (`OnInitializedAsync` on the
  server, again when interactivity attaches). Carry load-once data across the
  handoff with `PersistentComponentState` instead of fetching twice or
  reflexively disabling prerender.
- JS interop only from `OnAfterRenderAsync(firstRender)` or event handlers —
  never from `OnInitialized{Async}` or during static SSR, where no JS runtime
  exists. In `DisposeAsync`, catch `JSDisconnectedException`.
- Component files: markup in `<Name>.razor`; when the `@code` block outgrows
  roughly a screen, move it to a partial class `<Name>.razor.cs` (same type,
  so core's one-type-per-file rule holds). Component-scoped styles go in
  `<Name>.razor.css`. Never split members between `@code` and a code-behind.
- Parameters are the component's API: `[Parameter]` public properties —
  framework-set, hence settable; that requirement licenses no other mutable
  state — with `[EditorRequired]` on any the component cannot render without.
  Never write to your own parameters; notify the parent via
  `EventCallback<T>` (not `Action`) so re-rendering follows automatically.
- Cascading values are for genuinely ambient state (theme, auth, tenant);
  everything else is an explicit parameter.
- Forms use `EditForm` bound to a dedicated model with DataAnnotations and a
  `DataAnnotationsValidator`; on static SSR bind with
  `[SupplyParameterFromForm]` and rely on the framework's antiforgery.
  Client-side validation is UX only — server enforcement is the authority.
- `StateHasChanged()` has one purpose: state mutated where Blazor can't see
  it (timer, background work) — and then via `InvokeAsync(StateHasChanged)`.
  Event handlers and lifecycle methods already trigger renders; sprinkling it
  after every await is a smell.
- A component owning a timer, `DotNetObjectReference`, JS object reference,
  or event subscription implements `IDisposable`/`IAsyncDisposable`, and
  cancels in-flight async work with a `CancellationTokenSource` disposed with
  the component.
- A WebAssembly bundle is public — its `appsettings.json` included: no
  secrets, and client-side authorization is UX only; the server API enforces.
- Testing: bUnit component tests live in the unit-test project and drive the
  TDD loop for component behavior (render, interact, assert markup); keep
  logic in plain services so most tests need no renderer at all. Prerender
  handoff, JS interop, and render-mode wiring are beyond bUnit — cover them
  with a few E2E (Playwright) tests, or accept them as boundary and say so.
