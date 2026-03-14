-- KGV / Supabase – DB-seitige Absicherung Wartungsvertrag-Zuordnungen
-- Ziel:
-- 1) Duplikatschutz stichtagsbasiert: keine überlappenden Zeiträume pro (hauptmitglied_id, wartungsvertrag_id)
-- 2) Kapazität atomar (race-condition-robust): MaxAktiveZuordnungen pro Wartungsvertrag darf für "aktuell aktiv" nicht überschritten werden
--
-- Hinweis:
-- - Aktivität wird stichtagsbezogen über gueltig_ab/gueltig_bis bewertet.
-- - Kapazität wird für "jetzt" (now()) geprüft und per Row-Lock auf wartungsvertraege serialisiert.
-- - Historie bleibt über gueltig_bis erhalten.

begin;

-- Für Exclusion Constraints (btree-Gleichheit in GiST)
create extension if not exists btree_gist;

-- Konsistenz: gueltig_bis darf nicht vor gueltig_ab liegen
alter table if exists wartungsvertrag_zuordnungen
    drop constraint if exists ck_wartungsvertrag_zuordnungen_gueltig_bis_ge_ab;

alter table if exists wartungsvertrag_zuordnungen
    add constraint ck_wartungsvertrag_zuordnungen_gueltig_bis_ge_ab
    check (gueltig_bis is null or gueltig_bis >= gueltig_ab);

-- Duplikatschutz (stichtagsbasiert): keine überlappenden Zeiträume für denselben Vertrag beim selben Hauptmitglied
alter table if exists wartungsvertrag_zuordnungen
    drop constraint if exists wartungsvertrag_zuordnungen_no_overlap;

alter table if exists wartungsvertrag_zuordnungen
    add constraint wartungsvertrag_zuordnungen_no_overlap
    exclude using gist (
        hauptmitglied_id with =,
        wartungsvertrag_id with =,
        tsrange(gueltig_ab::timestamp, coalesce(gueltig_bis::timestamp, 'infinity'::timestamp), '[]') with &&
    );

-- Kapazitätsprüfung als Trigger (atomar durch FOR UPDATE Lock auf wartungsvertraege-Row)
create or replace function kgv_check_wartungsvertrag_zuordnung_capacity()
returns trigger
language plpgsql
as $$
declare
    v_max integer;
    v_active_count integer;
    v_titel text;
    v_is_active_now boolean;
begin
    -- Nur prüfen, wenn die neue/aktualisierte Zuordnung "jetzt" aktiv wäre
    v_is_active_now := (new.gueltig_ab <= now()) and (new.gueltig_bis is null or new.gueltig_bis >= now());
    if not v_is_active_now then
        return new;
    end if;

    -- Lock: serialisiert parallele Kapazitätsprüfungen pro Vertrag
    select max_aktive_zuordnungen, titel
      into v_max, v_titel
      from wartungsvertraege
     where id = new.wartungsvertrag_id
     for update;

    if v_max is null or v_max <= 0 then
        return new;
    end if;

    select count(*)
      into v_active_count
      from wartungsvertrag_zuordnungen z
     where z.wartungsvertrag_id = new.wartungsvertrag_id
       and z.id <> coalesce(new.id, 0)
       and z.gueltig_ab <= now()
       and (z.gueltig_bis is null or z.gueltig_bis >= now());

    if v_active_count >= v_max then
        raise exception 'Kapazität erreicht: "%" erlaubt max. % aktive Zuordnung(en). Aktuell aktiv: %.', coalesce(v_titel, new.wartungsvertrag_id::text), v_max, v_active_count;
    end if;

    return new;
end;
$$;

drop trigger if exists trg_kgv_check_wartungsvertrag_zuordnung_capacity on wartungsvertrag_zuordnungen;

create trigger trg_kgv_check_wartungsvertrag_zuordnung_capacity
before insert or update on wartungsvertrag_zuordnungen
for each row
execute function kgv_check_wartungsvertrag_zuordnung_capacity();

commit;
