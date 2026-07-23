-- Initial story schema. A story on disk is one SQLite database holding exactly one
-- story (design doc section 4.3). Tables are created scenes-first so the foreign-key
-- references below always point at a table that already exists.

CREATE TABLE scenes (
    id                 TEXT NOT NULL PRIMARY KEY,
    kind               TEXT NOT NULL CHECK (kind IN ('Choice', 'Linear', 'Ending')),
    text               TEXT NOT NULL,
    image_path         TEXT NULL,
    follow_up_scene_id TEXT NULL REFERENCES scenes(id),
    outcome_kind       TEXT NULL CHECK (outcome_kind IN ('Death', 'Victory', 'Other')),
    outcome_label      TEXT NULL,
    extension_tag      TEXT NULL,   -- D6: reserved slot, written NULL until post-v1 combat authoring
    extension_payload  TEXT NULL    -- D6: opaque JSON payload, never inspected by the tool
);

CREATE TABLE stories (
    id             INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),  -- one story per database
    title          TEXT NOT NULL,
    start_scene_id TEXT NULL REFERENCES scenes(id)
);

CREATE TABLE choices (
    scene_id        TEXT NOT NULL REFERENCES scenes(id),
    order_index     INTEGER NOT NULL,             -- 0-based, reader-facing sibling order
    label           TEXT NOT NULL,
    target_scene_id TEXT NOT NULL REFERENCES scenes(id),
    PRIMARY KEY (scene_id, order_index)
);

CREATE TABLE editor_scene_layout (               -- editor_* : canvas positions the runtime ignores
    scene_id TEXT NOT NULL PRIMARY KEY REFERENCES scenes(id),
    x        REAL NOT NULL,
    y        REAL NOT NULL
);
