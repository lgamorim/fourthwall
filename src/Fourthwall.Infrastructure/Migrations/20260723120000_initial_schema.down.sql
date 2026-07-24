-- Reverses 20260723120000_initial_schema.up.sql. Tables are dropped in reverse
-- dependency order so no drop removes a table another still references.

DROP TABLE editor_scene_layout;
DROP TABLE choices;
DROP TABLE stories;
DROP TABLE scenes;
