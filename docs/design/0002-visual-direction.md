# Fourthwall — Visual Direction

Status: approved by the maintainer with the M19 plan on 2026-09-13, before any M19 code was written.
Scope: the whole app as one identity — the Phase 3 chrome (header, dock, forms, picker) and the
Phase 4 canvas. Written by applying the `frontend-design` skill's process (brainstorm → plan →
critique → build → critique again); every UI milestone from M19 on re-reads it before building.

## 1. Subject, audience, job

**Subject.** A gamebook workbench. Fourthwall is where a write-your-own-adventure story is born: a
creator writes scenes, wires choices between them, and checks that every path leads somewhere. The
story is a map of scenes; the tool's material is the map, the page, and the pencil.

**Audience.** One writer, on their own machine, for hours at a time. Not a team, not a reader, not a
dashboard viewer. They know their story; they need the tool to stay out of the way and to tell
them, without ceremony, where they are and what is broken.

**Job.** Make the shape of the story legible and editable at a glance. The picker's job is to get
the writer back into a story in one click; the dock's job is the detail view — check, find, edit;
the canvas's job (from M20) is the map itself.

**World to draw from.** The gamebook, not the graph editor: the paperback with the green spine, the
pencil-and-paper map the reader draws so as not to get lost, the ribbon that keeps the place, the
running head at the top of the page, the printed section in a book face. Not: dark panels, neon
ports, blueprint grids, floating cards with shadows, icon-only toolbars.

## 2. Tokens

Every colour, type, spacing, and radius value in the app comes from `wwwroot/app.css` `:root`
under the `--fw-` prefix. The editor's shared vocabulary is applied by role across components from
that same file (a deliberate deviation recorded in `CLAUDE.md`); the layout components' own
`.razor.css` files consume the tokens and never introduce colour, type, spacing, or radius
literals.

### 2.1 Palette

Ink on paper, on a desk, with one ribbon.

| Token | Hex | Named for | Used for |
|---|---|---|---|
| `--fw-ink` | `#1B1F2A` | ink | text, the header block, primary buttons, the start tag |
| `--fw-paper` | `#F5F4EF` | the page | the main ground, input fields, text on ink, the hovered and selected navigator row (rows sit on the desk, so paper is the lift) |
| `--fw-desk` | `#E7E3D8` | the desk under the page | the dock ground and the "open now" line on the picker |
| `--fw-pencil` | `#5F5B4E` | pencil | secondary text: folder paths, hints, eyebrow headings |
| `--fw-rule` | `#CFCABB` | a ruled line | borders and dividers, one pixel |
| `--fw-ribbon` | `#1F6F63` | the ribbon bookmark | selection, focus, the bookmark, primary hover, "no problems" |

Two functional colours, each with a tint for the row it sits on, kept apart from the palette so
severity never competes with identity:

| Token | Hex | Used for |
|---|---|---|
| `--fw-error` / `--fw-error-tint` | `#9E2B25` / `#F6E7E4` | errors: failed operations, validation errors, destructive confirmation |
| `--fw-warning` / `--fw-warning-tint` | `#8A5B00` / `#F5EBD2` | warnings: validation warnings, "no start scene", kind-change prompt |

Contrast target: WCAG AA (4.5:1) for every text and tag against the ground it sits on — ink and
pencil on paper and on desk, ribbon on paper and on desk, paper on ink, each functional colour on
its tint. The ratios are computed from the built CSS and listed in the M19 PR; any pair under 4.5
is darkened before the PR opens.

### 2.2 Type

Three roles, three faces, all self-hosted under the SIL Open Font License from `wwwroot/fonts/`.
The tool must work with the network off, so there is no font CDN and no `<link>` to one.

| Role | Face | Files | Fallback stack | Where |
|---|---|---|---|---|
| Display | **Young Serif** (Regular) | `YoungSerif-Regular.woff2` | `"Iowan Old Style", "Palatino Linotype", Georgia, serif` | the app name, the story title in the toolbar, the picker headline. Nowhere else. |
| Body | **Literata** (variable: `opsz` 7–72, `wght` 400–700, roman and italic) | `Literata[opsz,wght].woff2`, `Literata-Italic[opsz,wght].woff2` | `Georgia, "Times New Roman", serif` | prose: scene text and its textarea, scene snippets in the navigator, hints, empty states, error and validation messages, the picker intro |
| Utility | **iA Writer Quattro S** (Regular, Italic, Bold) | `iAWriterQuattroS-{Regular,Italic,Bold}.woff2` | `"IBM Plex Sans", "Segoe UI", system-ui, sans-serif` | labels, buttons, eyebrow headings, kind tags, chips, folder paths, the header |

Why these. Young Serif is a chunky, low-contrast serif with a hand-cut, paperback-title feel; it
carries the identity in three places and is otherwise absent. Literata is a book face made for
reading long text on screens — the writer's scene text is set the way it will be read. iA Writer
Quattro descends from a typewriter face (IBM Plex Mono, proportionally respaced); it says
"manuscript" for the working parts — labels, paths, buttons — without the Courier cliché, and it
keeps folder paths legible.

Licences: `wwwroot/fonts/OFL-YoungSerif.txt`, `wwwroot/fonts/OFL-Literata.txt`,
`wwwroot/fonts/OFL-iAWriter.md` — copied verbatim from the upstream sources (Google Fonts
`ofl/youngserif`, `ofl/literata`; `iaolo/iA-Fonts`). Young Serif and Literata ship upstream as
TTF; they are converted once to WOFF2 with fontTools (`pip install fonttools brotli`, then
`TTFont(src).flavor = "woff2"; save(dst)`) with no subsetting, so any script a story is written in
still renders. iA Writer Quattro ships WOFF2 upstream and is used as is.

Scale (rem; base 16px):

| Token | Size | Role |
|---|---|---|
| `--fw-text-xs` | 0.6875 (11px) | eyebrow headings, kind and start tags, "can't be opened" |
| `--fw-text-s` | 0.8125 (13px) | utility: labels, buttons, chips, paths, navigator rows |
| `--fw-text-m` | 0.9375 (15px) | body prose, inputs, hints, messages |
| `--fw-text-l` | 1.125 (18px) | the app name |
| `--fw-text-xl` | 1.5 (24px) | the story title in the toolbar |
| `--fw-text-2xl` | 2.5 (40px) | the picker headline |

Line height 1.55 for body, 1.4 for utility, 1.15 for display. Eyebrow headings (`h2` in the dock
and picker sections) are utility, uppercase, `letter-spacing: 0.08em`, pencil — the same device
everywhere a section starts, and nowhere else.

### 2.3 Spacing and radius

Four-pixel base: `--fw-space-1` 0.25rem, `-2` 0.5rem, `-3` 0.75rem, `-4` 1rem, `-5` 1.5rem,
`-6` 2rem, `-7` 3rem. Radii stay close to paper: `--fw-radius-s` 2px (inputs, buttons, tags,
chips), `--fw-radius-m` 4px (image preview, prompts). No pills, no shadows, no gradients. Rules are
one pixel of `--fw-rule`; the only heavy block is the ink header.

Shell measures: `--fw-header-height` 3rem, `--fw-dock-width` 24rem (up from 22rem: the dock now
holds validation, navigator, and inspector), `--fw-page-width` 46rem (the picker's measure),
`--fw-ribbon-width` 8px and `--fw-ribbon-length` 22px (the bookmark, §4).

Motion: one duration, `--fw-motion` 120ms, on background, colour, border colour, and opacity
changes only; the focus ring appears without transition. Under `prefers-reduced-motion: reduce`
every transition and animation is removed.

## 3. Layout

### 3.1 Editor

One sentence: a slim ink header, a full-bleed main column holding a toolbar over the canvas
region, and a fixed desk-coloured dock on the right stacking validation → navigator → inspector.

```
┌───────────────────────────────────────────────────────────────────────────────────────┐
│ Fourthwall   THE WRECK                                                  [Close story] │  header: ink
├─────────────────────────────────────────────────────────────┬─────────────────────────┤
│ The Wreck                                    ← story title   │ VALIDATION              │
│ ── error line, only when a save fails ──      (display face) │ [Validate story]        │
│                                                             │ Not validated yet. …    │
│                                                             ├─────────────────────────┤
│                                                             │ SCENES                  │
│                                                             │ ▌⑂ A fork      CHOICE   │ ← ribbon: selected
│                 Your scenes live in the navigator.          │  → Below deck  LINEAR   │
│                 Pick one to edit it, or add a new one       │  ■ You drown   ENDING   │
│                 below the list.                             │ ADD A SCENE             │
│                              ↑ canvas placeholder until M20 │ Kind [Linear ▾] Text [] │
│                                                             │ [Add scene]             │
│                                                             ├─────────────────────────┤
│                                                             │ ▌SCENE                  │ ← ribbon again
│                                                             │ Kind [Choice ▾]         │
│                                                             │ Text [ prose, Literata ]│
│                                                             │ IMAGE … CHOICES …       │
└─────────────────────────────────────────────────────────────┴─────────────────────────┘
  main: paper, no padding, overflow hidden                      dock: desk, scrolls on its own
```

- The header names the app (display face) and, when a story is open, the story as a running head
  in utility caps, with "Close story" at the far right. The header is the only ink-filled block.
- The toolbar is the top of the main column: the editable story title set as a title (display
  face, no visible border until hover or focus), and the editor's error line under it. From M21 the
  fit and reset controls sit at its right edge.
- The main region below the toolbar is the canvas region: paper, full bleed, `overflow: hidden`.
  Until M20 it holds the `.canvas-placeholder` empty state, centred, written as an invitation.
- The dock is desk-coloured and scrolls independently. Three sections, each opened by an eyebrow
  heading and separated by a rule: **Validation** (the story-level check, on top because it is
  about the whole story), **Scenes** — the navigator (compact rows: kind mark, snippet, kind tag,
  with "Start here" and "Delete" revealed on the selected row and on hover or focus), then the
  "Add a scene" form, and **Scene** — the inspector for the selected scene (kind, outcome, text,
  image, choices or follow-up), or its empty-state invitation.
- At 1280px wide the main column is 896px; the dock never shrinks below its width.

### 3.2 Picker

One sentence: a single centred page — headline and one-line promise, then the shelf of recent
stories when there are any, then the two forms side by side.

```
┌───────────────────────────────────────────────────────────────┐
│ Fourthwall                                                     │  header: ink
├───────────────────────────────────────────────────────────────┤
│        .page — max 46rem, centred, its own padding             │
│                                                                │
│        Fourthwall                        ← display, 2.5rem     │
│        Write a branching story, see its shape, and check       │
│        that every path leads somewhere.                        │
│                                                                │
│        ▌Open now: The Wreck   C:\stories\wreck   (when open)   │
│        ── error line, only after a failed create or open ──    │
│                                                                │
│        RECENT STORIES                                          │
│        The Wreck            C:\stories\wreck           Forget  │
│        Shadows of Kell      D:\kell   CAN'T BE OPENED  Forget  │
│                                                                │
│        CREATE A STORY                 OPEN A STORY             │
│        Folder [              ]        Folder [              ]  │
│        Title  [              ]        [Open story]             │
│        [Create story]                                          │
└───────────────────────────────────────────────────────────────┘
```

- Recent stories come first when there are any: the picker's job is one click back into the story.
  With no recents the forms are the first thing under the promise.
- The two forms sit side by side from 44rem up and stack below it.
- Recent rows: title (body face, a link-styled button), path (utility, pencil), the "can't be
  opened" tag only after an open has failed, and "Forget" at the right.

## 4. Signature element: the ribbon bookmark

**What.** A ribbon in `--fw-ribbon`, 8px wide and 22px long (`--fw-ribbon-width`,
`--fw-ribbon-length`; a 6px draft read as timid in the first screenshots), hanging from the top
edge of whatever stands for the selected scene, ending in a notched fishtail. In M19 it hangs from the selected navigator row and
from the inspector's heading; from M20 it hangs from the selected node on the canvas.

**Why this, and why only this.** Selection is the one state that must read identically across the
navigator, the inspector, the validation chips, and the canvas — four surfaces that were built
separately and will otherwise each invent their own highlight. A gamebook reader keeps their place
with a ribbon; the writer's place in the book is the scene they are editing. The ribbon is drawn
from the subject, it encodes state rather than decorating, and it is the one element that will be
recognised in a screenshot. Everything around it is quiet: one-pixel rules, small radii, no
shadows, no gradients, no icons on buttons.

**How.** CSS only — a `::before` pseudo-element on `.scene-row-selected` and on `.inspector`,
`clip-path: polygon(...)` for the notch — so no test-targeted markup changes and no SVG sprite to
maintain. M20's node gets the same ribbon as an SVG `<path>` in the same colour.

## 5. Structural devices (quiet, and each encodes something)

- **Kind marks.** A small ink glyph before every kind tag: a fork for Choice, an arrow for Linear,
  a full stop for Ending. Drawn as CSS masks on `.scene-kind::before` from inline SVG, coloured by
  `currentColor`, always beside the word — kind never reads by colour alone or by icon alone. In
  M20 the same three shapes become the node silhouettes, so the navigator and the canvas share one
  vocabulary.
- **Eyebrow headings** open every section of the dock and the picker: utility caps in pencil. One
  device for "a section starts here".
- **Severity rows** (validation, hints, errors) keep the left-rule-plus-tint treatment the app
  already has, now from tokens: error rows in `--fw-error` on its tint, warnings in `--fw-warning`
  on its tint. A row's colour is always paired with its rule name in bold utility, so severity never
  reads by colour alone.
- **Start tag.** The start scene's tag is ink on paper (inverted) — the one row that is the
  beginning is the one row with an ink tag.
- No numbered markers. Scenes carry no number in the domain; gamebook section numbers would be
  decoration here, not addressing. (The picker's forms are not a sequence either.)

## 6. Genericness critique

Checked against the three AI-default looks and against "what would I produce for any graph
editor", before building.

1. **Cream + high-contrast serif + terracotta.** The first draft of this note had a warm paper
   nearer `#F4F1EA`, a gold accent (the yellow-spined paperback), and a serif display. Revised:
   the paper cooled to `#F5F4EF` (less yellow, and paired with a desk tone so it is not one cream
   field); the display serif is chunky and low-contrast (Young Serif), not a Didone; the accent is
   verdigris, not terracotta, and the gold was dropped because it collided with the warning colour
   and failed AA on paper. Paper itself stays: this is a writing tool, and the sheet is its
   material — that is a choice for this subject, stated here, not a default.
2. **Near-black + acid accent.** Not this. The header is ink because a book has a spine, but the
   working surfaces are paper and desk, and the accent is a muted ribbon green.
3. **Hairline broadsheet.** The closest risk: this design does use one-pixel rules and small
   radii. Revised away from it: radii are 2px and 4px, not zero; rows have generous padding and no
   column density; the paper/desk two-tone and the ink header give the page weight; the ribbon
   gives it one saturated, non-typographic element.

**"Any graph editor."** The default answer is dark chrome, a dotted grid ground, floating panels
with shadows, an icon toolbar, pill badges in a rainbow of kinds, and nodes as rounded rectangles
with coloured headers. Revised: no shadows or floating panels — the dock is a fixed desk; the
toolbar is words, not icons; kinds are marks-plus-words in ink, not colours; the canvas ground is
plain paper (M20 decides whether it carries anything at all); nodes will take their silhouette
from the kind marks rather than from a coloured header bar.

**Other things tried and dropped during the pass.** A dark dock (prose on dark reads as developer
tooling, and the dock is where the writing happens). Recent stories rendered as book spines
(decoration; a list with paths is what gets the writer back in one click). Numbered navigator rows
(no number exists in the domain).

## 7. Copy register

Written from the creator's side of the screen. Sentence case. Active voice. Plain verbs. An action
keeps its name through its whole flow. Errors say what happened and how to fix it, never apologise,
never hedge. Empty states invite the next action.

Vocabulary (the creator's words, used consistently): *story, scene, start scene, choice,
follow-up, link* (the creator's word for a transition; "transition" stays in code), *kind, ending,
outcome, image, folder, navigator*. Never: transition, node, edge, asset, workspace.

Action names and their flows:

| Action | Button | While it runs / after |
|---|---|---|
| Create a story | Create story | the editor opens |
| Open a story | Open story | the editor opens |
| Close the story | Close story | the picker |
| Forget a recent story | Forget | the row is gone |
| Add a scene | Add scene | the scene is selected |
| Make a scene the start | Start here | the tag reads Start |
| Delete a scene | Delete → Delete scene / Keep scene | two steps, because links to it go too |
| Change a scene's kind that has links | Change kind and remove links / Keep Choice | |
| Attach an image | Attach an image / Replace the image / Remove image | |
| Add a choice | Add choice | |
| Validate the story | Validate story → Validating… → No problems found. / rows | |

Copy changes M19 makes (current → new):

- Picker intro: "Design, visualize, and validate branching stories." → "Write a branching story,
  see its shape, and check that every path leads somewhere."
- Picker sections: "New story" → "Create a story"; "Open story" → "Open a story"; "Recent" →
  "Recent stories". Recent row: "Remove" → "Forget" (it forgets the entry; "Remove" reads as
  deleting the story); "unavailable" → "can't be opened". "Open:" → "Open now:".
- Editor: the visible "Story title" label becomes the visually hidden label of the title set as a
  title; placeholder "Untitled story".
- Canvas placeholder (new): with scenes, "Your scenes live in the navigator on the right. Pick one
  to edit it, or add a new one below the list."; with none, "This story has no scenes yet. Add the
  one it opens with, in the navigator on the right."
- Inspector empty state: "Select a scene to edit it." → "Pick a scene in the navigator to edit its
  text, kind, image, and links."
- Navigator: "No scene starts this story." → "No scene starts this story yet. Choose one with
  Start here."; "Make start" → "Start here"; tag "start" → "Start"; "Confirm delete" → "Delete
  scene"; "Confirm delete and remove N links to it" → "Delete scene and N links to it".
- Inspector kind prompt: "Changing the kind discards this scene's outgoing transitions." →
  "Changing the kind removes this scene's outgoing links."; "Change kind and discard" → "Change
  kind and remove links".
- Follow-up: "A linear scene flows into exactly one scene." → "A linear scene flows into one
  scene. Choose which."; option "(none)" → "Nowhere yet".
- Image: "That image is larger than 10 MB." → "That image is over 10 MB. Choose a smaller one."
- Validation: idle "Not validated yet." → "Not validated yet. Validate to find unreachable scenes,
  dead ends, and missing images."; running "Checking the story…" → "Validating…" with the button
  keeping its name (disabled) instead of changing to "Validating…"; rule names shown as words
  instead of enum members — Start scene, Unreachable scenes, Links don't match the kind, No ending
  can be reached, Dead ends, Missing image, Unused image.
- Not found page: "Sorry, the content you are looking for does not exist." → "There's nothing at
  this address." with a link back to the picker.
- Blazor error boundary: "An error has occurred." → "Something went wrong here. Reload the page to
  carry on." (what happened and how to fix it, in the interface's voice).
- Left as the framework wrote them: the reconnect dialog and the production error page (their copy
  is the host's, not the editor's; they are restyled from tokens only).

## 8. Quality floor (checked every UI milestone)

- `:focus-visible` on every control: a 2px ribbon outline, 2px offset; never removed. A control may
  add to it (the story title also underlines in ribbon), never replace it.
- `prefers-reduced-motion: reduce` removes every transition and animation.
- Usable at a 1280px-wide window with the dock open; the body never scrolls horizontally.
- Text and tags at WCAG AA against the ground they sit on (§2.1).
- Kind and severity never read by colour alone.
- Fonts load with the network off.

## 9. What later milestones inherit

- **M20 (nodes and edges).** Node silhouettes from the kind marks; the ribbon on the selected node;
  labels in the utility face; edge labels in the utility face with a paper halo; the canvas ground
  is plain paper unless M20's design plan argues otherwise in this note's terms.
- **M21 (toolbar controls).** Words, not icons, at the toolbar's right edge; ground cursor
  grab/grabbing, node cursor move; the canvas error line reuses the editor error treatment.
- **M22 (ports, ghost edge).** The port in ink, the ghost edge in ribbon; default copy written as
  prompts to the creator.
- **M23 (badges).** Error and warning badges use the functional colours with a shape difference,
  consistent with the panel rows.

Each of those milestones adds its design plan as a short section here, so this note stays the one
record of the direction.

## 10. M20 — the canvas: nodes, links, and ground

Approved by the maintainer with the M20 plan on 2026-09-14, before any M20 code was written. §10.2,
§10.3, and §10.5 were revised after the screenshot pass; §10.6 records each change against the
approved text.

**Thesis.** The canvas is the reader's pencil map. Every scene is a page drawn in ink on plain
paper, and *its right edge is how you leave it*: links enter through a straight left edge, and the
kind is the shape of the way out. Each silhouette is the navigator's kind mark (§5) turned to face
the reading direction.

### 10.1 Node

```
   START                                  ← ink tag on the start node's top edge
  ┌▼──────────────────────────┐           ← ▼ the ribbon, on the selected node only
  │  A storm gathers over…     >           Linear: the right edge comes to a point — one way
  │  LINEAR            [img]  /            on, and the link leaves from the tip.
  └──────────────────────────┘

  ┌───────────────────────────┐
  │  A fork in the passage…    <           Choice: a notch cut into the right edge — the fork;
  │  CHOICE                   \            links fan out from its crook.
  └───────────────────────────┘

  ┌──────────────────────────╮
  │  You drown in the da…     )            Ending: a half-circle — the full stop; nothing
  │  ENDING                   ╯            leaves.
  └──────────────────────────╯
```

- **Shape.** A `--fw-paper` fill with a 1px `--fw-ink` outline; the left corners carry
  `--fw-radius-s` (2px), and the left edge is always straight. The point and the notch are 14px deep
  (`CanvasGeometry.ExitDepth`); the Ending's arc has a radius of half the node's height.
- **Kind never by colour alone.** Every node is the same ink and paper. Kind reads by silhouette and
  by the caption word under the label — `CHOICE`, `LINEAR`, `ENDING` in `--fw-text-xs` utility caps,
  pencil — the same mark-plus-word pair the navigator uses. Kind never reads by shape alone either.
- **Label.** One line of the scene's text in the utility face, `--fw-text-s`, ink, from
  `Scenes.Label(scene, maxLength)`: 22 characters, or 15 beside a thumbnail. SVG text does not wrap,
  so the node's `<title>` carries the full text for hover and assistive technology.
- **Thumbnail.** When the scene has an image: a 40×40 plate inside the right edge, before the exit
  shape, `preserveAspectRatio="xMidYMid slice"` through one shared `clipPath` (2px radius) and framed
  in one pixel of `--fw-rule` — an illustration plate in a gamebook. Images keep their colour; they
  are the creator's art.
- **Start marker.** The navigator's ink tag — `START`, paper on ink — sits on the node's top edge at
  the left, like the tab of an index card. It stays the one inverted tag in the app.
- **Hover.** Pointer cursor; the fill becomes `--fw-desk`, the same one-step lift the navigator rows
  use in reverse.
- **Keyboard.** Each node is a focusable `role="button"` in navigator order; Enter or Space selects.
  Focus shows the §8 ring: a 2px ribbon outline.

### 10.2 The selected node carries the ribbon

The §4 ribbon, as an SVG `<path>` in `--fw-ribbon`: 8×22 with the same fishtail notch (at 70% of its
length), hanging from the top edge at x=4 — the margin position it takes in the navigator row,
which is why the label starts at x=20. The outline thickens to 2px ink. No coloured fill, no glow, no
bold label (§10.6): the ribbon alone says "you are here". Because SVG path data cannot read `var()`, the ribbon's size is
mirrored as `CanvasGeometry.RibbonWidth`/`RibbonLength` beside the `--fw-ribbon-*` tokens.

### 10.3 Links

- **Choice.** A 1.5px `--fw-pencil` curve ending in a small solid pencil arrowhead
  (`#canvas-arrow`, sized in user space so its tip lands exactly on the target's left edge). The
  label is utility `--fw-text-xs` in pencil over a paper halo (`paint-order: stroke`, an 8px paper
  stroke with round joins — wide enough to close a word space), cut to 16 characters with the full
  label in its `<title>`. Labels are drawn in their own layer, above every line and below the
  nodes, so no link strikes through another link's label where a fan-out's labels stack in the
  gutter.
- **Self-loop.** A small arch rising from the node's top edge near its right corner — right of the
  start tag and the ribbon, away from the right edge every other link leaves from — with its label
  beside the top of the arch. A second loop on the same scene nests higher and wider.
- **Follow-up.** The same curve drawn as dots — round caps on a `0 6` dash — the leader dots of a
  book's contents page ("… turn to"). There is no decision and no label; the dots are the second
  channel beside the missing label, so a follow-up never reads as an unlabelled choice.
- Links carry no emphasis in M20. Link selection and its marker arrive in M22, severity markers in
  M23.

### 10.4 Ground

Plain `--fw-paper`. A grid would bring back the blueprint look §1 rejects, and the reader's map is
drawn on a blank sheet. An empty story shows a centred invitation inside the canvas, in the body
face and pencil: "This story has no scenes yet. Add the one it opens with, in the navigator on the
right."

### 10.5 Geometry

`CanvasGeometry.NodeWidth` 200 and `NodeHeight` 64 (M18's 180×72 were placeholders), `ExitDepth` 14,
`ThumbnailSize` 40. `AutoLayout` leaves a 120px gutter between columns for link labels
(`ColumnGap` 320) and 40px between rows (`RowGap` 104). At a 1280px window the main column is
896px and three columns span 880px, so a three-column story fits without scrolling. Until M21's pan,
the canvas scrolls: the drawing reaches its furthest node or self-loop label plus a 40px margin
(`ContentMargin`), the same margin the layout leaves at the origin.

### 10.6 Critique

- *Any graph editor* would give nodes coloured header bars, kind pills, or rounded cards with
  shadows. Here kind is a silhouette plus a word, in ink; there is one fill for every node.
- *The notch and the ribbon.* The Choice notch resembles the ribbon's fishtail. Kept: they differ
  in colour, scale (14px deep on a 64px edge against a 22px strip), and axis, and the family
  resemblance is honest — both are cuts in paper.
- *Remove one accessory.* The candidates were the kind caption (it repeats the silhouette) and the
  thumbnail frame. The caption stays under §5's rule that kind never reads by shape alone; the
  frame is the first to go if the screenshots read busy.

**What the screenshot pass changed** (screenshots under `docs/design/screenshots/m20/`):

- The first build set the selected node's label in bold, after the navigator row. With the ribbon
  and the heavier outline that was a third selection cue; the bold went.
- The self-loop first left and re-entered the right edge, where it ran into the scene's other
  links and their labels overprinted. It moved to the top edge (§10.3).
- Link labels drawn with their own line were struck through by later lines, and a 4px halo left
  lines showing between words. Labels moved to their own layer and the halo widened to 8px.
- An 80px drawing margin scrolled a three-column story that fits the main column; the margin is
  now 40px, measured past self-loop labels too.
- A mouse click drew the browser's default focus rectangle around a node; only keyboard focus
  draws the ribbon ring now.

### 10.7 Where the canvas's styles live

In `StoryCanvas.razor.css`, `SceneNode.razor.css`, `SceneEdge.razor.css`, and
`SceneEdgeLabel.razor.css`, each styling only its
own markup (no `::deep`) and consuming the `app.css` tokens. The recorded deviation that keeps
component styles in `app.css` exists for a vocabulary shared by role across the dock's components;
the canvas's rules — SVG strokes, markers, halos — are private to it and grow in M21–M23. So the
canvas follows `overlays/frontend-blazor.md` instead of widening the exception.

## 11. M21 — pan, zoom, and moving scenes

Approved by the maintainer with the M21 plan on 2026-09-14, before any M21 code was written. §11.8
records what the screenshot pass changed against the approved text.

**Thesis.** The map is a sheet larger than the window. The creator slides the sheet under the
window, leans in and out, and moves pages about by hand. The sheet itself never changes: same
paper, same ink, same pages. Nothing new is drawn for M21 except two words in the toolbar.

### 11.1 The toolbar controls

Two words at the toolbar's right edge, on the title's baseline: **Show whole story** and **Actual
size**. Quiet text buttons in pencil, ink with an underline on hover — the treatment the
navigator's row actions already use — not the ink primary: they change the view, not the story.
Disabled at half opacity while the story has no scenes. The copy follows the §7 action table's
form (verb plus object, no article: Close story, Validate story); "Actual size" names the state the
map returns to, in the term every image viewer uses.

```
│ The Wreck                                          Show whole story  Actual size │
│ ── error line, full width, only after a failed rename or save ──                   │
```

In the creator's terms: Show whole story frames every scene, centred, with the map's 40px margin
(`CanvasGeometry.ContentMargin`), and never enlarges past actual size; a story too large for the
wheel's floor (§11.2) is framed below it. Actual size puts the page's
origin at the window's top-left corner at 1:1 — the frame M20 drew. A story opens at actual size
when its whole map fits the window, and showing the whole story otherwise, so the first thing seen
is the whole shape.

### 11.2 Sliding the map and moving a page

The wheel zooms about the cursor, between a quarter and three times actual size, one step per
notch (×1.2 per 100px of wheel travel; the same step for a keyboard press). From a frame below the
quarter the wheel zooms in only: it never jumps the map back to the floor. Pressing on paper and
dragging slides the map. Pressing on a page and dragging past 4px moves it, and its links follow as
it moves. Releasing writes that one position and nothing else, so a validation report survives a
move. A press that never travels 4px stays a click and selects.

A page being moved carries `node-dragging`: it keeps the desk fill the hover already gives it, and
nothing more. No shadow, no ghost, no outline change: the links redrawing under it are the feedback.

### 11.3 Cursor vocabulary

Paper: `grab`, and `grabbing` while sliding. Page: `move`, which supersedes §10.1's pointer
cursor — a page can now be moved as well as picked, and `move` says both. Toolbar words: the
browser's button cursor. No custom cursors.

### 11.4 The error line

A failed save uses the same `.canvas-error` line as a failed read, at the top of the map, in the
shared error treatment, and the page stays where it was dropped for the rest of the session. Both
messages are framed the same way, since they share a line: read, "The scenes' places on the map
couldn't be read, so they're laid out afresh. {message}"; save, "That scene's place on the map
couldn't be saved. {message}". The store's own message follows, because it names the cause (a
read-only folder, a scene that no longer exists).

### 11.5 Keyboard

The map is a focusable region: the §8 ring, drawn 2px inside its edge since the map fills the
region. With the map or a scene focused, the arrow keys slide the map 40px, and `+` or `-` zoom one
step about the window's centre. Tab still moves through the scenes in navigator order; a scene that
receives focus while wholly outside the window is brought to its centre (M23's centre-on-scene,
arriving early for keyboard use). Enter and Space still select. Show whole story and Actual size
are buttons.

There is no keyboard way to move a page in M21. A page's place is a convenience of the map, not
part of the story, so a keyboard-only creator loses nothing of the story. Stated against §8 rather
than hidden.

### 11.6 Motion

None on the viewport: sliding, zooming, framing, and resetting apply instantly, like turning to a
page. §2.3's policy (colour and opacity only) stands, so reduced motion needs nothing new.

### 11.7 Critique

Any graph editor would add a zoom percentage, plus and minus buttons, a minimap, a scaling dotted
grid, a floating control cluster with a shadow, a hand-tool toggle, snapping, and a lifted shadow
on the dragged card. None of that is here: two words in the toolbar, and the pages' size shows the
zoom. Remove-one-accessory candidates for the screenshot pass: the `node-dragging` fill (the first
to go if the moving links suffice) and the disabled state of the two buttons on an empty story.

### 11.8 What the screenshot pass changed

Screenshots under `docs/design/screenshots/m21/`, all at 1280px with the dock open.

- The first "Show whole story" of a seven-column story cut the link that turns back from the
  island to the harbour: a link that doubles back bows past both of its ends, and bounds measured
  from pages and self-loops alone framed the bow out. The drawing's bounds now hold every link's
  curve as well, so the whole story means the links too.
- The two toolbar words sat one step apart (`--fw-space-4`) and read as one phrase, "Show whole
  story Actual size"; they sit at `--fw-space-5` now.
- Forcing a failed save (another process holding the story database) found that the layout store
  let the provider's exception escape from the transaction's start, so the creator saw nothing and
  the browser logged an interop error. Fixed in Infrastructure; the error line now shows. The
  store still waits the provider's default 30 seconds before it gives up on a held database, so
  the line arrives late in that one case; a shorter wait is an Infrastructure decision left to the
  maintainer.
- Both remove-one-accessory candidates stayed. The `node-dragging` fill is the only thing that says
  which page is held while the pointer sits over its text, where the links' movement is out of the
  eye's way; and with no scenes the two words would do nothing, which is better said (half opacity)
  than discovered.
- The zoom step stays at ×1.2: five notches take a page from actual size to two and a half times
  it, and thirteen span the whole range, which felt right under the wheel.

**What the review changed** (PR #27): the error line moved out of the box the shim measures, so a
showing line never shifts the wheel's anchor or the frame; Show whole story goes below the wheel's
quarter for a story that needs it (§11.1, §11.2); and the store's two failure messages were
reworded in the creator's vocabulary, carrying the provider's reason, so §11.4's "names the cause"
holds for every failure.

## 12. M22 — authoring on the map

Approved by the maintainer with the M22 plan on 2026-09-15, before any M22 code was written. §12.3
was revised after the screenshot pass; §12.8 records the change against the approved text.

**Thesis.** The pencil map becomes something the creator draws on. A page's right edge is already
how you leave it (§10); M22 puts a pencil point on that edge, and drawing from it pulls a ribbon
to where the reader goes next. The ribbon already means "you are here" (§4); while a link is
being drawn it means "this is the link you are making", and once dropped the line becomes an
ordinary pencil link.

### 12.1 The port

```
  ┌──────────────────────────┐              ┌───────────────────────────┐
  │  A storm gathers…         >◯            │  A fork in the passage…    ◯   ← in the notch's mouth
  │  LINEAR                  /              │  CHOICE                   \
  └──────────────────────────┘              └───────────────────────────┘
   at rest: ring, paper fill, 1.5px ink       hover on the port: an ink disc, larger
```

- **Where.** At the node's right centre, where every link already leaves (§10.3): the tip of the
  Linear point, the mouth of the Choice notch. An Ending has no port — nothing leaves a full stop.
- **At rest** a ring of radius 4 (`CanvasGeometry.PortRadius`) in 1.5px ink on paper: visible
  without hovering, no bigger than a pencil dot. **Hovered** it fills with ink and grows to radius
  6, with the `crosshair` cursor. An invisible circle of radius 10 takes the press, so the dot is
  easy to catch.
- Its `<title>` reads "Drag to a scene to link it". While a link is drawn from it, the source's
  port stays filled.

### 12.2 The draft link

- A 1.5px line in `--fw-ribbon` ending in a ribbon arrowhead (`#canvas-arrow-draft`). It previews
  what it will become: from a Choice, the solid curve; from a Linear scene, §10.3's leader dots.
- Over paper it follows the pointer along the same curve a link ending there would take. **Over a
  scene it can link to, it snaps** to that scene's left edge at the parallel offset the new link
  will take, so what is shown is exactly the link that will be made.
- It is drawn above every line and below the labels and the pages.

### 12.3 The drop target

The page under the snapped tip carries `node-drop-target`: a 2px ribbon outline, so both ends of
the gesture are in ribbon. The source is never a target — a link
back into its own scene stays an inspector action — so dragging over it neither snaps nor
outlines.

### 12.4 Adding a scene

```
│ The Wreck                              [Add scene]     Show whole story   Actual size │
```

- **Add scene** — the §7 action name, the navigator's button's words — is the small ink primary
  button: unlike the two view words (§11.1) it changes the story. It stands left of them, a
  `--fw-space-6` gap apart, so the story action and the view actions never read as one phrase.
  While a story's places on the map are still being read it is disabled at half opacity, as the
  view words are with nothing to show: there is no map to add to yet, which is better said than
  discovered (decided in the review of the M22 PR).
- **Double-clicking the paper** adds a scene centred on that point; the button centres it in the
  window. Both add a Linear scene with no text. Overlap with a page already there is left for the
  creator to drag apart.
- **First appearance.** No animation (§11.6). The new page arrives selected, carrying the ribbon,
  and the inspector opens on it.
- A page with no text reads **Write this scene**, in pencil italic, until text exists: a prompt,
  not a name. Its accessible name says the same ("Write this scene, Linear"), so what a screen
  reader announces, or a voice command speaks, is what is shown. The inspector's text box asks
  "What happens in this scene?". The navigator's rows and the target dropdowns keep "(no text)" —
  a list needs a name to pick, not an instruction.
- **A new choice** is labelled **Name this choice**: the domain accepts no blank label, the words
  tell the creator what the label wants, and at 16 characters it shows uncut (§10.3). The link's
  scene is selected after the drop, so its row waits in the dock to be renamed.
- The empty story's invitation points at the button: "This story has no scenes yet. Choose Add
  scene above to write the one it opens with."

### 12.5 The selected link

Clicking a link's line or its label selects the link and its scene: the line turns ribbon — 2px on
a choice, heavier dots on a follow-up — and ends in `#canvas-arrow-selected`, and a choice's label
turns ribbon. The scene's page carries its ribbon at the same time. The link's highlight goes as
soon as the selection moves off its scene. Each line takes a transparent 12px stroke for the
press, and links take the `pointer` cursor.

### 12.6 Keyboard

Ports and links are for the pointer. Every link the map can draw, the inspector's transitions
editor makes too (Add choice, Flows into), with the keyboard alone; links are not focusable, and a
keyboard creator reaches a link through its scene. Nothing in the story needs a pointer — stated
against §8 rather than hidden, as §11.5 did for moving a page. **Add scene** is a button, and
Escape abandons a link being drawn.

### 12.7 Critique

Any graph editor would put coloured dots on both sides of every node, a floating "+" button,
snap-to-grid, and a rubber band that ignores what kind of link it is. Here: one port on the exit
edge, one word in the toolbar, and a draft that previews the link's kind and snaps to the exact
link it will make. Remove-one-accessory candidates for the screenshot pass: the source's filled
port while drawing, and the drop target's ribbon outline if the snap alone reads.

### 12.8 What the screenshot pass changed

Screenshots under `docs/design/screenshots/m22/`, all at 1280px with the dock open.

- **Removed: the drop target's desk fill.** The first build lifted the target with the desk fill
  under its ribbon outline. The source page is held under the pointer while a link is drawn, so
  it already carries the hover's desk fill, and the two lifted pages read as one gesture with no
  direction. The target keeps only the ribbon outline; with the snapped ribbon draft ending at it,
  the pair says "from here, to there".
- **Kept: the drop target's outline.** With the fill gone it is the only mark on the receiving
  page; the snapped arrowhead lands on a left edge that other links into that page reach too, so
  the arrowhead alone does not single the page out.
- **Kept: the source's filled port.** In a mouse drag the port stays filled anyway, because the
  captured pointer keeps it hovered; with a pen or a finger there is no hover, and the filled port
  is the only mark at the source.
- **Accepted: a short draft bows.** Near its own port, a draft over paper takes the S-shaped bow
  that any link doubling back takes (§11.8), since it follows the same curve. Special-casing it
  would break "what is shown is the link that will be made" the moment it snaps; it straightens as
  soon as the pointer moves away.
- **Kept: Add scene as the ink button.** Under the ink header it reads as the toolbar's one story
  action, not as a second header block.
- **Noted, not changed:** a scene added or selected on the map opens the inspector below the fold
  of the dock when the navigator is long, so the dock must be scrolled to the text box. This is the
  dock's behaviour from M19 for any selection; bringing the inspector into view is left for a later
  milestone.
