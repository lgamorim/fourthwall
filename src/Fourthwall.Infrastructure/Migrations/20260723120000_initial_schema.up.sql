-- Initial story schema. A story on disk is one SQLite database holding exactly one
-- story (design doc section 4.3). Tables are created scenes-first so the foreign-key
-- references below always point at a table that already exists.
--
-- The self-referencing edges (scenes.follow_up_scene_id, stories.start_scene_id) are
-- DEFERRABLE INITIALLY DEFERRED: stories legitimately contain cycles (design doc 4.2),
-- so no single insert order satisfies immediate foreign-key checks. Deferring to commit
-- lets the repository write a whole story in one pass inside a transaction.

CREATE TABLE scenes (
    id                 TEXT NOT NULL PRIMARY KEY,
    kind               TEXT NOT NULL CHECK (kind IN ('Choice', 'Linear', 'Ending')),
    text               TEXT NOT NULL,
    image_path         TEXT NULL,
    follow_up_scene_id TEXT NULL REFERENCES scenes(id) DEFERRABLE INITIALLY DEFERRED,
    outcome_kind       TEXT NULL CHECK (outcome_kind IN ('Death', 'Victory', 'Other')),
    outcome_label      TEXT NULL,
    extension_tag      TEXT NULL,   -- D6: reserved slot, written NULL until post-v1 combat authoring
    extension_payload  TEXT NULL,   -- D6: opaque JSON payload, never inspected by the tool

    -- The conditional invariants the Domain enforces, encoded so a hand-edited or
    -- runtime-consumed database (design doc D7) cannot hold a malformed scene.
    CHECK ((kind = 'Ending') = (outcome_kind IS NOT NULL)),          -- outcome iff ending
    CHECK (outcome_kind <> 'Other' OR outcome_label IS NOT NULL),    -- an 'Other' ending is labelled
    CHECK (follow_up_scene_id IS NULL OR kind = 'Linear')            -- only a Linear scene flows on
);

CREATE TABLE stories (
    id             INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),  -- one story per database
    title          TEXT NOT NULL,
    start_scene_id TEXT NULL REFERENCES scenes(id) DEFERRABLE INITIALLY DEFERRED
);

CREATE TABLE choices (
    scene_id        TEXT NOT NULL REFERENCES scenes(id),
    order_index     INTEGER NOT NULL,             -- 0-based, reader-facing sibling order
    label           TEXT NOT NULL,
    target_scene_id TEXT NOT NULL REFERENCES scenes(id),
    PRIMARY KEY (scene_id, order_index)
);

CREATE TABLE editor_scene_layout (               -- editor_* : canvas positions the runtime ignores
    scene_id TEXT NOT NULL PRIMARY KEY REFERENCES scenes(id) ON DELETE CASCADE,  -- derived editor state
    x        REAL NOT NULL,
    y        REAL NOT NULL
);
