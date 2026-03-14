-- KGV / Supabase – Migration Helper
-- Fix: PK-ID (id) muss in Basistabellen automatisch generiert werden.
-- Hintergrund: Inserts dürfen die Spalte `id` NICHT mitsenden. Ohne Default/Sequence/Identity
-- schlägt ein Insert dann trotzdem fehl (NOT NULL).
--
-- Betroffene Tabellen:
--   - arbeitseinsatz
--   - termin
--   - bekanntmachung
--
-- Dieses Skript:
--   1) stellt sicher, dass `id` ein Default `nextval(...)` hat (falls keine Sequence/Identity verknüpft ist)
--   2) setzt die Sequence auf MAX(id) (damit nextval() sicher > vorhandene IDs ist)
--   3) meldet (NOTICE) ob es Datensätze mit id=0 gibt (keine automatische Massenänderung!)

DO $$
DECLARE
    tbl text;
    seq text;
    max_id bigint;
    zero_count bigint;
BEGIN
    FOREACH tbl IN ARRAY ARRAY['arbeitseinsatz', 'termin', 'bekanntmachung']
    LOOP
        -- Identity/Serial-Sequence ermitteln (funktioniert i.d.R. auch bei identity)
        SELECT pg_get_serial_sequence(tbl, 'id') INTO seq;

        -- Fallback: wenn keine Sequence ermittelbar ist, legen wir eine an und setzen DEFAULT
        IF seq IS NULL THEN
            seq := tbl || '_id_seq';

            EXECUTE format('CREATE SEQUENCE IF NOT EXISTS %I', seq);
            EXECUTE format('ALTER TABLE %I ALTER COLUMN id SET DEFAULT nextval(%L)', tbl, seq);
            EXECUTE format('ALTER SEQUENCE %I OWNED BY %I.id', seq, tbl);
        END IF;

        -- Sequence auf MAX(id) synchronisieren
        EXECUTE format('SELECT COALESCE(MAX(id), 0) FROM %I', tbl) INTO max_id;
        EXECUTE format('SELECT setval(%L, %s, true)', seq, max_id);

        -- Nur prüfen/ausgeben: id=0-Fälle
        EXECUTE format('SELECT COUNT(*) FROM %I WHERE id = 0', tbl) INTO zero_count;

        RAISE NOTICE 'Table=% | Sequence=% | MAX(id)=% | id=0 rows=%', tbl, seq, max_id, zero_count;
    END LOOP;
END $$;
