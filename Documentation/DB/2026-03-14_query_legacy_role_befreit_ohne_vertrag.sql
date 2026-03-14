-- KGV / Supabase – Migration Helper
-- Mitglieder, die aktuell (legacy) über Rolle admin/vorstand befreit wären,
-- aber keinen aktiven befreierenden Wartungsvertrag haben.

with role_members as (
    select
        m.id as mitglied_id,
        coalesce(m.hauptmitglied_id, m.id) as hauptmitglied_id,
        m.vorname,
        m.name,
        lower(trim(coalesce(au.role, m.role, ''))) as effective_role
    from mitglied m
    left join app_user au on au.mitglied_id = m.id
    where lower(trim(coalesce(au.role, m.role, ''))) in ('admin', 'vorstand')
),
active_exempt_contract as (
    select distinct z.hauptmitglied_id
    from wartungsvertrag_zuordnungen z
    join wartungsvertraege w on w.id = z.wartungsvertrag_id
    where w.aktiv = true
      and w.befreit_von_pflichtstunden = true
      and z.gueltig_ab <= now()
      and (z.gueltig_bis is null or z.gueltig_bis >= now())
)
select
    rm.hauptmitglied_id,
    rm.mitglied_id,
    rm.vorname,
    rm.name,
    rm.effective_role
from role_members rm
where rm.hauptmitglied_id not in (select hauptmitglied_id from active_exempt_contract)
order by rm.hauptmitglied_id, rm.mitglied_id;
